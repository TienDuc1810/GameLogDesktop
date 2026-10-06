using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
namespace GameLogDesktop;

public sealed class UpdateFile {public string Path {get;set;}="";public string Sha256 {get;set;}="";}
public sealed class UpdateManifest
{
 public string Product {get;set;}="GameLogDesktop";
 public string Version {get;set;}="";
 public string PackageUrl {get;set;}="";
 public string PackageSha256 {get;set;}="";
 public List<UpdateFile> Files {get;set;}=[];
}
public sealed class UpdatePlan
{
 public string Root {get;set;}="";public string Stage {get;set;}="";
 public int ProcessId {get;set;}
 public List<string> Files {get;set;}=[];public List<string> Obsolete {get;set;}=[];
}
public sealed class UpdateService
{
 public const string ManifestName="update-manifest.json";
 private readonly HttpClient client=new(){Timeout=TimeSpan.FromMinutes(10)};
 public static string CurrentVersion=>typeof(App).Assembly.GetName().Version?.ToString(3)??"1.2.0";
 public static bool SafeRelative(string path)
 {
  if(string.IsNullOrWhiteSpace(path)||path.Length>240||System.IO.Path.IsPathRooted(path)||path.Contains(':')||path.Contains('\\'))return false;
  var parts=path.Split('/');
  if(parts.Any(p=>p.Length==0||p=="."||p==".."||p.StartsWith('.')||p.EndsWith('.')||p.EndsWith(' ')||p.IndexOfAny(System.IO.Path.GetInvalidFileNameChars())>=0))return false;
  return !new[]{"desktop-data","backup","work","data"}.Contains(parts[0],StringComparer.OrdinalIgnoreCase);
 }
 public static string Resolve(string root,string relative)
 {
  if(!SafeRelative(relative))throw new InvalidDataException("Đường dẫn trong gói cập nhật không hợp lệ: "+relative);
  var full=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative.Replace('/',System.IO.Path.DirectorySeparatorChar)));
  if(!full.StartsWith(System.IO.Path.GetFullPath(root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Tệp nằm ngoài thư mục app.");
  // Never follow directory links into another folder while writing or cleaning up.
  var parent=System.IO.Path.GetDirectoryName(full);
  while(parent!=null&&parent.Length>=root.Length){if(Directory.Exists(parent)&&(File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Không cập nhật qua thư mục liên kết.");parent=System.IO.Path.GetDirectoryName(parent);}
  if(File.Exists(full)&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Không cập nhật tệp liên kết.");
  return full;
 }
 public static void Validate(UpdateManifest manifest)
 {
  if(manifest.Product!="GameLogDesktop"||!System.Version.TryParse(manifest.Version,out _))throw new InvalidDataException("Gói không phải bản cập nhật GameLog Desktop.");
  if(manifest.Files.Count<2||manifest.Files.Count>2000||manifest.Files.Select(x=>x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=manifest.Files.Count)throw new InvalidDataException("Danh sách tệp không hợp lệ hoặc trùng lặp.");
  foreach(var file in manifest.Files)if(!SafeRelative(file.Path)||!System.Text.RegularExpressions.Regex.IsMatch(file.Sha256,"^[a-fA-F0-9]{64}$"))throw new InvalidDataException("Đường dẫn hoặc mã kiểm tra tệp không hợp lệ.");
  if(!manifest.Files.Any(x=>x.Path=="GameLogDesktop.exe")||!manifest.Files.Any(x=>x.Path=="GameLogDesktop.dll"))throw new InvalidDataException("Gói thiếu chương trình chính.");
 }
 public static string Hash(string path){using var s=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(s));}
 public static string NormalizeSource(string source)
 {
  var match=System.Text.RegularExpressions.Regex.Match(source,@"^https://github\.com/([A-Za-z0-9][A-Za-z0-9_.-]*)/([A-Za-z0-9][A-Za-z0-9_.-]*?)(?:\.git)?/?$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
  return match.Success?$"https://github.com/{match.Groups[1].Value}/{match.Groups[2].Value}/releases/latest/download/update-feed.json":source;
 }
 public static (string Path,string Version)? LatestLocal(string folder,string currentVersion,CancellationToken ct)
 {
  if(!Directory.Exists(folder))throw new DirectoryNotFoundException("Không tìm thấy thư mục cập nhật đã chọn.");
  (string Path,string Version)? latest=null;var bad=new List<string>();
  foreach(var file in Directory.EnumerateFiles(folder,"GameLogDesktop-Windows-x64-*.zip",SearchOption.TopDirectoryOnly)){
   ct.ThrowIfCancellationRequested();if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;
   try{using var zip=ZipFile.OpenRead(file);var manifests=zip.Entries.Where(x=>x.Name==ManifestName).ToList();if(manifests.Count!=1||manifests[0].Length>2_000_000)throw new InvalidDataException();using var stream=manifests[0].Open();var manifest=JsonSerializer.Deserialize<UpdateManifest>(stream,Storage.Json)??throw new InvalidDataException();Validate(manifest);var version=System.Version.Parse(manifest.Version);
    if(version>System.Version.Parse(currentVersion)&&(latest==null||version>System.Version.Parse(latest.Value.Version)))latest=(file,manifest.Version);
   }catch(Exception ex)when(ex is InvalidDataException or IOException or JsonException){bad.Add(System.IO.Path.GetFileName(file));}
  }
  if(latest==null&&bad.Count>0)throw new InvalidDataException("Có gói không đọc được trong nguồn cập nhật: "+string.Join(", ",bad)+". Chưa thay đổi app.");return latest;
 }
 public async Task<UpdateManifest> Check(string url,CancellationToken ct)
 {
  if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https")throw new InvalidDataException("Nguồn cập nhật phải là liên kết HTTPS tới update-feed.json.");
  using var response=await client.GetAsync(uri,ct);response.EnsureSuccessStatusCode();
  if(response.RequestMessage?.RequestUri?.Scheme!="https")throw new InvalidDataException("Nguồn chuyển hướng khỏi HTTPS.");
  if(response.Content.Headers.ContentLength>2_000_000)throw new InvalidDataException("Thông tin cập nhật quá lớn.");
  var manifest=JsonSerializer.Deserialize<UpdateManifest>(await response.Content.ReadAsStringAsync(ct),Storage.Json)??throw new InvalidDataException("Nguồn cập nhật không đọc được.");
  if(manifest.Product!="GameLogDesktop"||!System.Version.TryParse(manifest.Version,out _)||!Uri.TryCreate(manifest.PackageUrl,UriKind.Absolute,out var package)||package.Scheme!="https"||!System.Text.RegularExpressions.Regex.IsMatch(manifest.PackageSha256,"^[a-fA-F0-9]{64}$"))throw new InvalidDataException("Nguồn thiếu phiên bản, liên kết ZIP HTTPS hoặc SHA256.");
  return manifest;
 }
 public async Task<string> Download(UpdateManifest feed,string folder,Action<string> progress,CancellationToken ct)
 {
  Directory.CreateDirectory(folder);var zip=System.IO.Path.Combine(folder,"package.zip");
  using var response=await client.GetAsync(feed.PackageUrl,HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
  if(response.RequestMessage?.RequestUri?.Scheme!="https")throw new InvalidDataException("Gói chuyển hướng khỏi HTTPS.");
  var length=response.Content.Headers.ContentLength;if(length>1_000_000_000)throw new InvalidDataException("Gói cập nhật quá lớn.");
  await using(var input=await response.Content.ReadAsStreamAsync(ct))await using(var output=File.Create(zip)){
   var buffer=new byte[81920];long total=0;int read;while((read=await input.ReadAsync(buffer,ct))>0){total+=read;if(total>1_000_000_000)throw new InvalidDataException("Gói cập nhật quá lớn.");await output.WriteAsync(buffer.AsMemory(0,read),ct);progress(length>0?$"Đang tải cập nhật: {total*100/length}%":$"Đang tải: {total/1048576} MB");}
  }
  if(!Hash(zip).Equals(feed.PackageSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SHA256 gói tải không khớp; chưa thay đổi app.");
  return zip;
 }
 public static UpdatePlan Prepare(string zip,string root,string stage,string? expectedVersion=null)
 {
  root=System.IO.Path.GetFullPath(root).TrimEnd('\\');stage=System.IO.Path.GetFullPath(stage);
  if(!stage.StartsWith(root+"\\.gamelog-update-",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Vùng cập nhật phải ở trong thư mục app.");
  Directory.CreateDirectory(stage);var payload=System.IO.Path.Combine(stage,"payload");Directory.CreateDirectory(payload);
  using var archive=ZipFile.OpenRead(zip);
  var manifests=archive.Entries.Where(x=>x.Name==ManifestName).ToList();if(manifests.Count!=1)throw new InvalidDataException("ZIP cần có một update-manifest.json. Gói cũ chưa hỗ trợ cập nhật tự động.");
  if(manifests[0].Length>2_000_000)throw new InvalidDataException("Manifest quá lớn.");
  UpdateManifest manifest;using(var s=manifests[0].Open())manifest=JsonSerializer.Deserialize<UpdateManifest>(s,Storage.Json)??throw new InvalidDataException("Manifest không đọc được.");Validate(manifest);
  if(System.Version.Parse(manifest.Version)<=System.Version.Parse(CurrentVersion))throw new InvalidDataException("Gói không mới hơn bản đang dùng ("+CurrentVersion+").");
  if(expectedVersion!=null&&manifest.Version!=expectedVersion)throw new InvalidDataException("Phiên bản trong ZIP khác nguồn cập nhật.");
  var prefix=manifests[0].FullName[..^ManifestName.Length];var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  var allowed=manifest.Files.Select(x=>x.Path).Append(ManifestName).ToHashSet(StringComparer.OrdinalIgnoreCase);
  long bytes=0;
  foreach(var entry in archive.Entries){if(entry.FullName.EndsWith('/'))continue;if(!entry.FullName.StartsWith(prefix,StringComparison.Ordinal))throw new InvalidDataException("ZIP có tệp ngoài thư mục chương trình.");var relative=entry.FullName[prefix.Length..];if(!SafeRelative(relative)||!allowed.Contains(relative)||!paths.Add(relative))throw new InvalidDataException("ZIP có tệp lạ hoặc trùng lặp.");bytes+=entry.Length;if(bytes>2_000_000_000)throw new InvalidDataException("Dung lượng giải nén vượt giới hạn.");}
  foreach(var file in manifest.Files){var target=Resolve(payload,file.Path);Resolve(root,file.Path);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);var entry=archive.Entries.SingleOrDefault(x=>x.FullName==prefix+file.Path)??throw new InvalidDataException("Thiếu tệp: "+file.Path);entry.ExtractToFile(target,true);if(!Hash(target).Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Tệp hỏng: "+file.Path);}
  File.WriteAllText(System.IO.Path.Combine(payload,ManifestName),JsonSerializer.Serialize(manifest,Storage.Json));
  var obsolete=new List<string>();var oldPath=System.IO.Path.Combine(root,ManifestName);
  if(File.Exists(oldPath)){var old=JsonSerializer.Deserialize<UpdateManifest>(File.ReadAllText(oldPath),Storage.Json);if(old!=null){Validate(old);foreach(var file in old.Files.Where(x=>!allowed.Contains(x.Path))){var oldFile=Resolve(root,file.Path);if(File.Exists(oldFile)){if(!Hash(oldFile).Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))continue;obsolete.Add(file.Path);}}}}
  var plan=new UpdatePlan{Root=root,Stage=stage,ProcessId=Environment.ProcessId,Files=manifest.Files.Select(x=>x.Path).Append(ManifestName).ToList(),Obsolete=obsolete};
  File.WriteAllText(System.IO.Path.Combine(stage,"plan.json"),JsonSerializer.Serialize(plan,Storage.Json));return plan;
 }
 public static void Launch(UpdatePlan plan)
 {
  var script=System.IO.Path.Combine(AppContext.BaseDirectory,"Apply-Update.ps1");var stagedScript=System.IO.Path.Combine(plan.Stage,"Apply-Update.ps1");File.Copy(script,stagedScript,true);
  var start=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
  foreach(var arg in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",stagedScript,"-PlanPath",System.IO.Path.Combine(plan.Stage,"plan.json")})start.ArgumentList.Add(arg);
  _=Process.Start(start)??throw new InvalidOperationException("Không khởi động được trình cập nhật.");
 }
 public static void CleanupStage(string root,string stage)
 {
  var full=System.IO.Path.GetFullPath(stage);var expected=System.IO.Path.GetFullPath(root).TrimEnd('\\')+"\\.gamelog-update-";
  if(!full.StartsWith(expected,StringComparison.OrdinalIgnoreCase)||!System.IO.Path.GetFileName(full).StartsWith(".gamelog-update-"))throw new InvalidDataException("Không dọn thư mục ngoài vùng cập nhật.");
  if(Directory.Exists(full)&&!Directory.EnumerateFileSystemEntries(full,"*",SearchOption.AllDirectories).Append(full).Any(x=>(File.GetAttributes(x)&FileAttributes.ReparsePoint)!=0))Directory.Delete(full,true);
 }
}
