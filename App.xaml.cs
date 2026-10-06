using System.Windows;
namespace GameLogDesktop;
public partial class App : Application
{
 public static string? SelfTestDirectory {get;private set;}
 protected override void OnStartup(StartupEventArgs e)
 {
  DispatcherUnhandledException += (_, args) => { System.IO.File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"desktop-error.log"),args.Exception+Environment.NewLine); if(SelfTestDirectory!=null){System.IO.Directory.CreateDirectory(SelfTestDirectory);System.IO.File.WriteAllText(System.IO.Path.Combine(SelfTestDirectory,"failure.txt"),args.Exception.ToString());args.Handled=true;Shutdown(1);return;}MessageBox.Show(args.Exception.Message,"GameLog Desktop"); args.Handled=true; };
  if(e.Args.Length>1&&e.Args[0]=="--self-test"){
   ShutdownMode=ShutdownMode.OnExplicitShutdown;
   SelfTestDirectory=System.IO.Path.GetFullPath(e.Args[1]);
   base.OnStartup(e);var window=new MainWindow();MainWindow=window;window.Show();
   window.Dispatcher.InvokeAsync(async ()=>{await System.Threading.Tasks.Task.Delay(300);try{await window.SelfTest(SelfTestDirectory);window.Close();Shutdown(0);}catch(Exception ex){System.IO.File.WriteAllText(System.IO.Path.Combine(SelfTestDirectory,"failure.txt"),ex.ToString());window.Close();Shutdown(1);}});
   return;
  }
  base.OnStartup(e);MainWindow=new MainWindow();MainWindow.Show();
  var ready=Environment.GetEnvironmentVariable("GAMELOG_UPDATE_READY");
  if(ready!=null){var path=System.IO.Path.GetFullPath(ready);var root=System.IO.Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\');if(path.StartsWith(root+"\\.gamelog-update-",StringComparison.OrdinalIgnoreCase)&&System.IO.Path.GetFileName(path)=="ready.json")System.IO.File.WriteAllText(path,UpdateService.CurrentVersion);}
 }
}
