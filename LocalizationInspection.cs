using System.IO;
using System.IO.Compression;
using System.Text.Json;
namespace GameLogDesktop;

// Read-only discovery. Inventory is not permission to overwrite a game resource.
public sealed record LocalizationResource(string RelativePath,string Format,long Bytes,string Status);
public sealed record LocalizationInventory(string Engine,List<LocalizationResource> Resources);
public static class LocalizationInspection
{
 private static readonly HashSet<string> text=new(StringComparer.OrdinalIgnoreCase){".json",".csv",".tsv",".po",".xml",".txt"};
 private static readonly HashSet<string> packed=new(StringComparer.OrdinalIgnoreCase){".pak",".utoc",".ucas",".pck",".rpa",".rpyc",".assets",".bundle",".locres",".strings",".stringtable",".vfx",".vfs0",".upk9",".lng"};
 public static LocalizationInventory Scan(string gameRoot)
 {
  var root=Path.GetFullPath(gameRoot);if(!Directory.Exists(root))throw new DirectoryNotFoundException("Không tìm thấy thư mục game.");
  var resources=new List<LocalizationResource>();var queue=new Queue<string>();queue.Enqueue(root);
  var engine="Chưa xác định";var visited=0;
  while(queue.TryDequeue(out var folder)){
   if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Không quét qua thư mục liên kết.");
   foreach(var file in Directory.EnumerateFiles(folder)){
    if(++visited>100000)throw new InvalidDataException("Quá nhiều tệp; chọn thư mục tài nguyên nhỏ hơn.");
    var name=Path.GetFileName(file);var ext=Path.GetExtension(file);var relative=Path.GetRelativePath(root,file).Replace('\\','/');
    if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;
    if(name.Equals("content.vfx",StringComparison.OrdinalIgnoreCase))engine="4A Engine / Metro — cần đối chiếu phiên bản và font";
    if(name.Equals("globalgamemanagers",StringComparison.OrdinalIgnoreCase)||ext.Equals(".assets",StringComparison.OrdinalIgnoreCase))engine="Unity";
    if(ext.Equals(".utoc",StringComparison.OrdinalIgnoreCase)||ext.Equals(".ucas",StringComparison.OrdinalIgnoreCase))engine="Unreal Engine / IoStore";
    else if(ext.Equals(".pak",StringComparison.OrdinalIgnoreCase)&&engine=="Chưa xác định")engine="Có PAK — cần xác minh engine";
    if(ext.Equals(".pck",StringComparison.OrdinalIgnoreCase))engine="Có PCK — cần xác minh Godot";
    if(ext.Equals(".rpa",StringComparison.OrdinalIgnoreCase)||ext.Equals(".rpyc",StringComparison.OrdinalIgnoreCase))engine="Ren'Py";
    if(!text.Contains(ext)&&!packed.Contains(ext))continue;
    var status=packed.Contains(ext)?"Tài nguyên đóng gói — cần bộ đọc/ghi đúng game":"Ứng viên văn bản — cần xác minh schema, ngôn ngữ và mã hoá";
    resources.Add(new(relative,ext,new FileInfo(file).Length,status));
   }
   foreach(var child in Directory.EnumerateDirectories(folder)){var name=Path.GetFileName(child);if(name is ".git" or "desktop-data" or "backup" or "__pycache__")continue;queue.Enqueue(child);}
  }
  return new(engine,resources);
 }
 public static List<LocalizationResource> InspectZip(string file)
 {
  using var archive=ZipFile.OpenRead(file);var rows=new List<LocalizationResource>();var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long bytes=0;
  foreach(var entry in archive.Entries){
   if(entry.FullName.EndsWith('/'))continue;
   var path=entry.FullName;if(!UpdateService.SafeRelative(path)||!paths.Add(path))throw new InvalidDataException("Gói có đường dẫn không hợp lệ hoặc trùng tên: "+path);
   bytes+=entry.Length;if(bytes>2_000_000_000||rows.Count>=20000)throw new InvalidDataException("Gói vượt giới hạn giải nén.");
   var ext=Path.GetExtension(path);if(!text.Contains(ext)&&!packed.Contains(ext))throw new InvalidDataException("Không tự cài tệp chương trình/script hoặc định dạng chưa hỗ trợ: "+path);
   using var stream=entry.Open();var signature=new byte[4];var read=stream.Read(signature);
   if(read>=2&&signature[0]==0x4D&&signature[1]==0x5A)throw new InvalidDataException("Phát hiện chương trình Windows đổi đuôi: "+path);
   rows.Add(new(path,ext,entry.Length,"Chưa xác minh tương thích, chưa quét mã độc; chưa ghi vào game"));
  }
  if(rows.Count==0)throw new InvalidDataException("Gói không có tài nguyên Việt hoá.");
  return rows;
 }
}
