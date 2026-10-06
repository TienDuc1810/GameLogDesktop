using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace GameLogDesktop;
public partial class MainWindow
{
 private readonly UpdateService updater=new();
 private void ReadUpdateDefaults()
 {
  if(App.SelfTestDirectory!=null||store.Settings.UpdateFeedUrl!=""||store.Settings.LocalUpdateFolder!="")return;
  var file=Path.Combine(AppContext.BaseDirectory,"update-default.json");if(!File.Exists(file))return;
  try{using var json=System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));var source=json.RootElement.GetProperty("FeedUrl").GetString()??"";if(json.RootElement.GetProperty("Product").GetString()=="GameLogDesktop"&&Uri.TryCreate(source,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="github.com"&&uri.AbsolutePath.EndsWith("/releases/latest/download/update-feed.json"))store.Settings.UpdateFeedUrl=source;}
  catch(Exception ex){Say("Chưa đọc được nguồn cập nhật mặc định: "+ex.Message);}
 }
 private async void CheckUpdates(object sender,RoutedEventArgs e)
 {
  if(task!=null){Say("Chờ tác vụ hiện tại hoàn tất trước khi cập nhật.");return;}
  if(string.IsNullOrWhiteSpace(store.Settings.LocalUpdateFolder)&&string.IsNullOrWhiteSpace(store.Settings.UpdateFeedUrl)){UpdateSettings(sender,e);return;}
  UpdatePlan? plan=null;var root=AppContext.BaseDirectory;var stage=Path.Combine(root,".gamelog-update-"+Guid.NewGuid().ToString("N"));
  await Run(async ct=>{
   try{
    string zip;string version;
    if(!string.IsNullOrWhiteSpace(store.Settings.LocalUpdateFolder)){
     Say("Đang tìm bản mới trong thư mục cập nhật…");var package=await Task.Run(()=>UpdateService.LatestLocal(store.Settings.LocalUpdateFolder,UpdateService.CurrentVersion,ct),ct);
     if(package==null){Say("Không có bản mới hơn trong thư mục đã chọn.");return;}zip=package.Value.Path;version=package.Value.Version;
    }else{
     Say("Đang kiểm tra nguồn cập nhật trực tuyến…");var feed=await updater.Check(store.Settings.UpdateFeedUrl,ct);version=feed.Version;
     if(Version.Parse(version)<=Version.Parse(UpdateService.CurrentVersion)){Say("Đang dùng bản mới nhất của nguồn đã chọn.");return;}zip=await updater.Download(feed,stage,Say,ct);
    }
    Say("Đang xác minh bản "+version+" và chuẩn bị tự cập nhật…");plan=await Task.Run(()=>UpdateService.Prepare(zip,root,stage,version),ct);ct.ThrowIfCancellationRequested();Persist();store.Backup();Say("Đã xác minh. Đang cập nhật và mở lại app…");
   }catch{plan=null;UpdateService.CleanupStage(root,stage);throw;}
  });
  if(plan!=null)StartUpdate(plan);
 }
 private void UpdateSettings(object sender,RoutedEventArgs e)
 {
  var dialog=Dialog("Cập nhật GameLog Desktop");var panel=new StackPanel{Margin=new Thickness(24)};
  panel.Children.Add(new TextBlock{Text="Phiên bản đang dùng: "+UpdateService.CurrentVersion,FontSize=20,Margin=new Thickness(0,0,0,16)});
  panel.Children.Add(new TextBlock{Text="Nguồn GitHub: link kho app hoặc link cập nhật HTTPS",Margin=new Thickness(0,0,0,8)});
  var source=new TextBox{Text=store.Settings.UpdateFeedUrl};panel.Children.Add(source);
  panel.Children.Add(new TextBlock{Text="Hoặc thư mục cập nhật trên máy (chọn một lần)",Margin=new Thickness(0,12,0,8)});
  var localFolder=new TextBox{Text=store.Settings.LocalUpdateFolder};panel.Children.Add(localFolder);
  var chooseFolder=new Button{Content="Chọn thư mục chứa gói cập nhật"};chooseFolder.Click+=(_,_)=>{var picker=new OpenFolderDialog{Title="Chọn thư mục chứa GameLogDesktop-Windows-x64-*.zip"};if(picker.ShowDialog(dialog)==true)localFolder.Text=picker.FolderName;};panel.Children.Add(chooseFolder);
  var saveSource=new Button{Content="Lưu nguồn · lần sau bấm Update để tự cài"};saveSource.Click+=(_,_)=>{try{var folder=localFolder.Text.Trim();var url=UpdateService.NormalizeSource(source.Text.Trim());if(folder!=""&&!Directory.Exists(folder))throw new InvalidDataException("Thư mục không tồn tại.");if(folder==""&&(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"))throw new InvalidDataException("Chọn thư mục hoặc nhập nguồn HTTPS trước.");store.Settings.LocalUpdateFolder=folder;store.Settings.UpdateFeedUrl=url;Persist();Say("Đã lưu nguồn. Bấm Update để tự kiểm tra, sao lưu, cài và mở lại app.");dialog.Close();}catch(Exception ex){Say("Không lưu được nguồn: "+ex.Message);}};panel.Children.Add(saveSource);
  var info=new TextBlock{Text="Chưa có nguồn phát hành trực tuyến mặc định. Có thể chọn ZIP để app tự cập nhật. Dữ liệu và các file bạn tự thêm được giữ lại.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,16,0,16)};panel.Children.Add(info);
  var check=new Button{Content="Kiểm tra trực tuyến"};var install=new Button{Content="Cập nhật ngay",Visibility=Visibility.Collapsed};UpdateManifest? feed=null;
  check.Click+=async (_,_)=>{
   if(task!=null){info.Text="Có tác vụ đang chạy; chờ hoàn tất trước khi kiểm tra.";return;}
   await Run(async ct=>{try{feed=null;install.Visibility=Visibility.Collapsed;var url=UpdateService.NormalizeSource(source.Text.Trim());store.Settings.UpdateFeedUrl=url;store.Settings.LocalUpdateFolder="";Persist();info.Text="Đang kiểm tra…";var found=await updater.Check(url,ct);if(Version.Parse(found.Version)<=Version.Parse(UpdateService.CurrentVersion)){info.Text="Bạn đang dùng bản mới nhất của nguồn này.";return;}feed=found;info.Text="Có bản "+found.Version+". Bấm Cập nhật ngay để tải, sao lưu, thay file và mở lại app.";install.Visibility=Visibility.Visible;}catch(Exception ex){info.Text="Chưa kiểm tra được: "+ex.Message;}});
  };
  install.Click+=async (_,_)=>{
   if(feed==null||task!=null)return;var root=AppContext.BaseDirectory;var stage=Path.Combine(root,".gamelog-update-"+Guid.NewGuid().ToString("N"));UpdatePlan? plan=null;
   await Run(async ct=>{try{var zip=await updater.Download(feed,stage,message=>{info.Text=message;Say(message);},ct);plan=await Task.Run(()=>UpdateService.Prepare(zip,root,stage,feed.Version),ct);ct.ThrowIfCancellationRequested();Persist();store.Backup();}catch{plan=null;UpdateService.CleanupStage(root,stage);throw;}});
   if(plan!=null){dialog.Close();StartUpdate(plan);}
  };
  var local=new Button{Content="Cập nhật từ file ZIP"};local.Click+=(_,_)=>{
   if(task!=null){info.Text="Chờ tác vụ hiện tại hoàn tất trước khi cập nhật.";return;}
   var picker=new OpenFileDialog{Filter="Gói cập nhật GameLog|*.zip",Title="Chọn gói cập nhật có update-manifest.json"};if(picker.ShowDialog(dialog)!=true)return;
   var root=AppContext.BaseDirectory;var stage=Path.Combine(root,".gamelog-update-"+Guid.NewGuid().ToString("N"));
   try{var plan=UpdateService.Prepare(picker.FileName,root,stage);Persist();store.Backup();dialog.Close();StartUpdate(plan);}catch(Exception ex){UpdateService.CleanupStage(root,stage);info.Text="Chưa cập nhật: "+ex.Message;Say(info.Text);}
  };
  panel.Children.Add(check);panel.Children.Add(install);panel.Children.Add(local);var cancel=new Button{Content="Đóng"};cancel.Click+=(_,_)=>dialog.Close();panel.Children.Add(cancel);dialog.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};dialog.ShowDialog();
 }
 private void StartUpdate(UpdatePlan plan)
 {
  try{UpdateService.Launch(plan);Close();}catch(Exception ex){Say("Không khởi động cập nhật: "+ex.Message);UpdateService.CleanupStage(plan.Root,plan.Stage);}
 }
 public static string CreateShortcut(string desktop,string executable)
 {
  Directory.CreateDirectory(desktop);var path=Path.Combine(desktop,"GameLog Desktop.lnk");
  // A same-name shortcut owned by another target is preserved.
  var type=Type.GetTypeFromProgID("WScript.Shell")??throw new InvalidOperationException("Không có Windows Script Host.");
  dynamic shell=Activator.CreateInstance(type)!;object? shortcut=null;
  try{
   if(File.Exists(path)){dynamic existing=shell.CreateShortcut(path);try{if(!string.Equals((string)existing.TargetPath,executable,StringComparison.OrdinalIgnoreCase))path=Path.Combine(desktop,"GameLog Desktop - "+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".lnk");}finally{Marshal.FinalReleaseComObject(existing);}}
   shortcut=shell.CreateShortcut(path);dynamic link=shortcut;link.TargetPath=executable;link.WorkingDirectory=Path.GetDirectoryName(executable);link.Description="Nhật ký game và quản lý Steam";link.IconLocation=executable+",0";link.Save();return path;
  }finally{if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);Marshal.FinalReleaseComObject(shell);}
 }
 private void DesktopShortcut(object sender,RoutedEventArgs e)
 {
  try{var exe=Path.Combine(AppContext.BaseDirectory,"GameLogDesktop.exe");var path=CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),exe);Say("Đã tạo shortcut: "+path);}catch(Exception ex){Say("Không tạo được shortcut: "+ex.Message);}
 }
 private void ReadUpdateResult()
 {
  var file=Path.Combine(AppContext.BaseDirectory,"update-result.json");if(!File.Exists(file))return;
  try{using var result=System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));Say(result.RootElement.GetProperty("Success").GetBoolean()?"Đã cập nhật GameLog Desktop "+UpdateService.CurrentVersion+".":"Cập nhật chưa hoàn tất: "+result.RootElement.GetProperty("Message").GetString());File.Delete(file);}catch(Exception ex){Say("Thông tin cập nhật: "+ex.Message);}
 }
}
