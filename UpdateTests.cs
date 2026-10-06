using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
namespace GameLogDesktop;
public partial class MainWindow
{
 private async Task SelfTestUpdates(string directory,Action<bool,string> check)
 {
  check(!UpdateService.SafeRelative("../outside.dll")&&!UpdateService.SafeRelative("desktop-data/games.json")&&!UpdateService.SafeRelative("E:/game.dll")&&!UpdateService.SafeRelative("foo\\bar.dll"),"updater rejects traversal and data paths");
  check(UpdateService.SafeRelative("vi/System.resources.dll"),"updater allows managed runtime subfolders");
  var probe=Path.Combine(directory,"localization-inspection");Directory.CreateDirectory(Path.Combine(probe,"Content"));File.WriteAllText(Path.Combine(probe,"Content","game.utoc"),"fixture");File.WriteAllText(Path.Combine(probe,"Content","en.json"),"{\"line\":\"Hello\"}");
  var original=UpdateService.Hash(Path.Combine(probe,"Content","en.json"));var inventory=LocalizationInspection.Scan(probe);check(inventory.Engine=="Unreal Engine / IoStore"&&inventory.Resources.Count==2&&UpdateService.Hash(Path.Combine(probe,"Content","en.json"))==original,"localization inventory recognizes packed assets without modifying files");
  var resourceZip=Path.Combine(probe,"resource.zip");using(var archive=ZipFile.Open(resourceZip,ZipArchiveMode.Create)){using var writer=new StreamWriter(archive.CreateEntry("Content/en.json").Open());writer.Write("{\"line\":\"Xin chào\"}");}check(LocalizationInspection.InspectZip(resourceZip).Single().Status.Contains("chưa ghi"),"import inspection lists target-relative resources without installing");
  var unsafeZip=Path.Combine(probe,"unsafe.zip");using(var archive=ZipFile.Open(unsafeZip,ZipArchiveMode.Create)){using var writer=new StreamWriter(archive.CreateEntry("Content/dialogue.json").Open());writer.Write("MZ executable renamed");}var unsafeRejected=false;try{LocalizationInspection.InspectZip(unsafeZip);}catch(InvalidDataException){unsafeRejected=true;}check(unsafeRejected,"renamed executable blocked during resource inspection");
  var fixture=Path.Combine(directory,"update-tests");Directory.CreateDirectory(fixture);
  string Zip(string name,string version,bool corrupt=false){
   var file=Path.Combine(fixture,name);using var archive=ZipFile.Open(file,ZipArchiveMode.Create);
   var manifest=new UpdateManifest{Version=version};
   foreach(var item in new[]{("GameLogDesktop.exe","new-exe"),("GameLogDesktop.dll","new-dll")}){
    var bytes=Encoding.UTF8.GetBytes(item.Item2);manifest.Files.Add(new(){Path=item.Item1,Sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))});
    using var stream=archive.CreateEntry("Update/"+item.Item1).Open();stream.Write(corrupt&&item.Item1.EndsWith("dll")?Encoding.UTF8.GetBytes("corrupt"):bytes);
   }
   using(var writer=new StreamWriter(archive.CreateEntry("Update/"+UpdateService.ManifestName).Open()))writer.Write(JsonSerializer.Serialize(manifest,Storage.Json));return file;
  }
  string Root(string name){var root=Path.Combine(fixture,name);Directory.CreateDirectory(Path.Combine(root,"desktop-data"));
   File.WriteAllText(Path.Combine(root,"desktop-data","games.json"),"user-data");File.WriteAllText(Path.Combine(root,"custom.txt"),"keep");
   foreach(var file in new[]{"GameLogDesktop.exe","GameLogDesktop.dll","obsolete.dll","modified.dll"})File.WriteAllText(Path.Combine(root,file),"old");
   var manifest=new UpdateManifest{Version="1.1.0",Files=new[]{"GameLogDesktop.exe","GameLogDesktop.dll","obsolete.dll","modified.dll"}.Select(p=>new UpdateFile{Path=p,Sha256=UpdateService.Hash(Path.Combine(root,p))}).ToList()};
   File.WriteAllText(Path.Combine(root,UpdateService.ManifestName),JsonSerializer.Serialize(manifest,Storage.Json));File.WriteAllText(Path.Combine(root,"modified.dll"),"user-changed");return root;
  }
  async Task<int> Apply(UpdatePlan plan){plan.ProcessId=0;var path=Path.Combine(plan.Stage,"plan.json");File.WriteAllText(path,JsonSerializer.Serialize(plan,Storage.Json));
   var start=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true};foreach(var arg in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",Path.Combine(AppContext.BaseDirectory,"Apply-Update.ps1"),"-PlanPath",path,"-SkipRestart"})start.ArgumentList.Add(arg);
   using var process=Process.Start(start)!;await process.WaitForExitAsync();return process.ExitCode;
  }
  var goodZip=Zip("good.zip","9.0.0");var root=Root("install");var stage=Path.Combine(root,".gamelog-update-test");var plan=UpdateService.Prepare(goodZip,root,stage);
  check(UpdateService.NormalizeSource("https://github.com/example/game-log.git")=="https://github.com/example/game-log/releases/latest/download/update-feed.json","GitHub repository URL maps to stable latest-release feed");
  var local=Path.Combine(fixture,"local-feed");Directory.CreateDirectory(local);File.Copy(goodZip,Path.Combine(local,"GameLogDesktop-Windows-x64-v9.zip"));File.Copy(Zip("newer.zip","10.0.0"),Path.Combine(local,"GameLogDesktop-Windows-x64-v10.zip"));
  check(UpdateService.LatestLocal(local,"8.0.0",CancellationToken.None)?.Version=="10.0.0","local update discovery selects greatest manifest version rather than filename or modification time");
  check(UpdateService.LatestLocal(local,"10.0.0",CancellationToken.None)==null,"update discovery does not offer same version or downgrade");
  check(plan.Obsolete.SequenceEqual(new[]{"obsolete.dll"}),"only unchanged obsolete managed files are scheduled for removal");
  check(await Apply(plan)==0,"external updater applies verified package");
  check(File.ReadAllText(Path.Combine(root,"GameLogDesktop.exe"))=="new-exe"&&!File.Exists(Path.Combine(root,"obsolete.dll")),"managed files replaced and obsolete file removed");
  check(File.ReadAllText(Path.Combine(root,"desktop-data","games.json"))=="user-data"&&File.ReadAllText(Path.Combine(root,"custom.txt"))=="keep"&&File.ReadAllText(Path.Combine(root,"modified.dll"))=="user-changed","user data and unowned or modified files preserved");
  check(!Directory.Exists(stage),"temporary package and rollback workspace cleaned after success");
  var badRoot=Root("corrupt");var badStage=Path.Combine(badRoot,".gamelog-update-test");var rejected=false;
  try{UpdateService.Prepare(Zip("corrupt.zip","9.0.0",true),badRoot,badStage);}catch(InvalidDataException){rejected=true;}finally{UpdateService.CleanupStage(badRoot,badStage);}
  check(rejected&&File.ReadAllText(Path.Combine(badRoot,"GameLogDesktop.exe"))=="old","corrupt package rejected before installed files change");
  var lowerStage=Path.Combine(badRoot,".gamelog-update-version");rejected=false;try{UpdateService.Prepare(Zip("older.zip","1.1.0"),badRoot,lowerStage);}catch(InvalidDataException){rejected=true;}finally{UpdateService.CleanupStage(badRoot,lowerStage);}
  check(rejected,"downgrade package rejected");
  var failRoot=Root("rollback");File.Delete(Path.Combine(failRoot,"GameLogDesktop.dll"));Directory.CreateDirectory(Path.Combine(failRoot,"GameLogDesktop.dll"));var failStage=Path.Combine(failRoot,".gamelog-update-test");
  check(await Apply(UpdateService.Prepare(goodZip,failRoot,failStage))!=0,"simulated copy failure detected");
  check(File.ReadAllText(Path.Combine(failRoot,"GameLogDesktop.exe"))=="old"&&File.ReadAllText(Path.Combine(failRoot,"desktop-data","games.json"))=="user-data","partial update rolls back and preserves journal");
  check(!Directory.Exists(failStage),"failed update workspace cleaned after rollback");
  var shortcut=CreateShortcut(Path.Combine(fixture,"desktop"),Path.Combine(AppContext.BaseDirectory,"GameLogDesktop.exe"));check(File.Exists(shortcut),"Desktop shortcut file created");
  dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;dynamic link=shell.CreateShortcut(shortcut);try{check((string)link.TargetPath==Path.Combine(AppContext.BaseDirectory,"GameLogDesktop.exe")&&(string)link.WorkingDirectory==AppContext.BaseDirectory.TrimEnd('\\'),"shortcut targets installed app and working folder");}finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
  check(Find<System.Windows.Controls.Button>(this).Any(b=>b.Content?.ToString()=="Kiểm tra cập nhật"),"update button present on main screen");
  var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};var inspected=false;
  timer.Tick+=(_,_)=>{timer.Stop();var dialog=OwnedWindows.Cast<System.Windows.Window>().Single();inspected=Find<System.Windows.Controls.TextBlock>(dialog).Any(t=>t.Text.Contains("Chưa có nguồn phát hành"));var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(dialog);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(directory,"update-dialog.png")))png.Save(stream);dialog.Close();};timer.Start();CheckUpdates(this,new System.Windows.RoutedEventArgs());check(inspected,"update dialog explains missing online source and supports ZIP fallback");
 }
}
