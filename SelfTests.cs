using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace GameLogDesktop;
public partial class MainWindow
{
 public async Task SelfTest(string directory)
 {
  var checks=new List<string>();void Check(bool condition,string name){if(!condition)throw new InvalidOperationException("Self-test failed: "+name);checks.Add("PASS: "+name);}
  var legacy=Path.Combine(directory,"legacy");Directory.CreateDirectory(legacy);var old=Path.Combine(legacy,"games.json");
  File.WriteAllText(old,"""
  {"version":1,"games":[{"Id":"old-one","Name":"Legacy game","Platform":"Steam","AppId":"10","Minutes":75.5,"Rating":0,"Notes":"Ghi chú tiếng Việt","Added":"2025-03-01","Sessions":[{"Start":"2025-03-01 10:00","Minutes":75.5,"Source":"nhập tay"}],"Achievements":[{"Name":"First","Done":true}]},{"Id":"old-two","Name":"DSX","Platform":"Steam","AppId":"1812620","Minutes":9000,"Rating":9}]}
  """);
  var hash=SHA256.HashData(File.ReadAllBytes(old));store.Games.Clear();store.Import(old);store.Import(old);
  Check(store.Games.Count==2,"migration deduplicates repeated import");Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(old))),"legacy file unchanged");
  var g=store.Games.First(x=>x.Id=="old-one");var dsx=store.Games.First(x=>x.Id=="old-two");
  Check(g.Minutes==75.5&&g.Notes=="Ghi chú tiếng Việt"&&g.Sessions.Count==1&&g.Achievements.Count==1,"migration retains time, Unicode notes, sessions and achievements");
  Check(dsx.Kind=="Phần mềm"&&dsx.Rating==9&&dsx.RatingSet,"legacy software and positive rating migration");Check(g.Rating is null&&!g.RatingSet,"legacy default zero becomes unrated");
  g.RatingIndex=1;store.Save();var reload=new Storage(directory);Check(reload.Games.First(x=>x.Id==g.Id).Rating==0&&reload.Games.First(x=>x.Id==g.Id).RatingSet,"intentional zero survives restart");
  g.RatingIndex=0;store.Save();reload=new Storage(directory);Check(reload.Games.First(x=>x.Id==g.Id).Rating is null,"cleared rating survives restart");
  for(var i=0;i<45;i++)store.Games.Add(new Game{Name="Game "+i,Minutes=i*71,Status=i%2==0?"Muốn chơi":"Đang chơi"});
  Refresh();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
  Check(((IEnumerable<Game>)Library.ItemsSource).First().Minutes==44*71,"descending playtime sort");Check(!((IEnumerable<Game>)Library.ItemsSource).Any(x=>x.Kind=="Phần mềm"),"default excludes software");
  KindFilter.SelectedIndex=1;Check(((IEnumerable<Game>)Library.ItemsSource).Single().Name=="DSX","software filter");KindFilter.SelectedIndex=0;
  Library.SelectedItem=g;await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);RatingCombo.SelectedIndex=10;Persist();Check(g.Rating==9,"dropdown stores selected score");
  RatingCombo.SelectedIndex=0;Persist();Check(g.Rating is null&&g.Minutes==75.5,"saving unrated preserves precise minutes");
  AddMinutes(g,DateTime.Now.AddMinutes(-10),10,"QA");Check(g.Minutes==85.5&&g.Sessions.Count==2,"manual session adds time once");
  store.Discovery.Items=[new(){AppId="10",Name="Watch fixture",Price=123456,Original=234567,Low=123456,Discount=47,ComingSoon=true,Release="TBA"}];
  store.Discovery.News=[new(){AppId="10",Gid="qa",Title="Patch fixture",Content="[b]Unicode bản vá[/b]",Date="2026-10-06"}];RefreshDiscovery();
  Check(store.Discovery.Items[0].PriceText=="123.456 ₫","VND formatting");Check(new WatchGame().PriceText=="Chưa có giá","unavailable price is not free");
  var tabs=Find<TabControl>(this).First();for(var i=0;i<tabs.Items.Count;i++){tabs.SelectedIndex=i;await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Check(((TabItem)tabs.Items[i]).IsSelected,"navigation tab "+i);}
  tabs.SelectedIndex=0;await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Capture(Path.Combine(directory,"desktop-maximized.png"));
  tabs.SelectedIndex=1;await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Capture(Path.Combine(directory,"desktop-wishlist.png"));
  tabs.SelectedIndex=2;NewsGrid.SelectedIndex=0;await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Check(NewsBody.Text.Contains("Unicode bản vá"),"news body selection");Capture(Path.Combine(directory,"desktop-news.png"));
  tabs.SelectedIndex=0;WindowState=WindowState.Normal;Width=1150;Height=800;await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Capture(Path.Combine(directory,"desktop-compact.png"));
  var originalCatalog=Translations.Catalog.Entries.ToList();
  Check(originalCatalog.Count(x=>x.Provider=="red")>200&&originalCatalog.Count(x=>x.Provider=="penguin")>100,"bundled official catalogs load");
  var lies=new Game{Name="Lies of P",AppId="1627720",Minutes=2000};var onimusha=new Game{Name="Onimusha: Way of the Sword",Minutes=1000};
  var baldurs=new Game{Name="Baldur's Gate 3",Minutes=900};
  store.Games.AddRange([lies,onimusha,baldurs]);RefreshTranslations();
  var translationRows=((IEnumerable<TranslationRow>)TranslationGrid.ItemsSource).ToList();
  Check(translationRows.Count==store.Games.Count(x=>x.Kind!="Phần mềm")&&!translationRows.Any(x=>x.Name=="DSX"),"translation list follows library and excludes software");
  Check(translationRows.Select(x=>x.Number).SequenceEqual(Enumerable.Range(1,translationRows.Count)),"translation serial numbers are consecutive");
  Check(Translations.Match(lies,"red")!=null&&Translations.Match(lies,"penguin")?.Access=="free","Lies of P has both verified sources");
  Check(Translations.Match(onimusha,"penguin")?.Access=="paid","early access classified as paid despite storefront zero");
  Check(Translations.Match(new Game{Name="The Witcher 3: Wild Hunt — Remastered"},"penguin")?.Name=="The Witcher 3: Wild Hunt","explicit remastered edition mapping");
  Check(Translations.Match(new Game{Name="Dead Space"},"penguin")==null,"sequel does not match base game");
  Check(Translations.Match(new Game{Name="Subnautica"},"red")==null,"Subnautica does not match Subnautica 2");
  var fixture="<h2 class=\"nk-product-title h5\"><a href=\"https://canhcutteam.com/games/test/\">Baldur&#8217;s Gate 3</a></h2>";
  Check(TranslationService.ParsePenguin(fixture)[0].Name=="Baldur’s Gate 3","HTML entities decode in official listing");
  var entry=new TranslationEntry();TranslationService.InspectPenguin(entry,"<div>0 ₫</div><tr class=\"attribute_pa_trang-thai\"><td><a href=\"/trang-thai/trai-nghiem-som/\">Trải Nghiệm Sớm</a></td></tr>");
  Check(entry.Access=="paid","product status overrides zero-price display");TranslationService.InspectPenguin(entry,"<div>0 ₫</div>");Check(entry.Access=="unknown","missing status never inferred as free");
  Check(TranslationService.Status(null,false)=="Chưa kiểm tra"&&TranslationService.Status(null,true)=="Không có bản dịch","incomplete catalog not treated as no translation");
  Check(!TranslationService.IsOfficial("https://canhcutteam.com.evil.test/game")&&!TranslationService.IsOfficial("http://theredteam.vn/game"),"provider link validation");
  Translations.Save();var translationReload=new TranslationService(directory);Check(translationReload.Catalog.Entries.Count==originalCatalog.Count,"translation cache survives restart");
  TranslationSearch.Text="Lies of P";Check(((IEnumerable<TranslationRow>)TranslationGrid.ItemsSource).Single().Name=="Lies of P","translation search");TranslationSearch.Text="";
  tabs.SelectedIndex=4;TranslationGrid.SelectedItem=((IEnumerable<TranslationRow>)TranslationGrid.ItemsSource).First(x=>x.Game==onimusha);WindowState=WindowState.Maximized;await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Capture(Path.Combine(directory,"desktop-translations.png"));
  Check(TranslationDetails.Text.Contains("Trả phí")&&TranslationDetails.Text.Contains("https://canhcutteam.com/"),"selection displays source evidence and paid status");
  var a=store.Backup();Check(Directory.GetFiles(a,"*.json").Length==4,"backup includes translation cache with library settings discovery");
  if(Environment.GetEnvironmentVariable("GAMELOG_TRANSLATION_LIVE_TEST")=="1"){
   var result=await Translations.Update([lies,onimusha,baldurs],Say,CancellationToken.None);
   File.WriteAllText(Path.Combine(directory,"live-result.txt"),result);
   Check(result.Contains("Dữ liệu đã lưu."),"live provider catalogs and detail requests complete");
   Check(Translations.Match(lies,"penguin")?.Access=="free"&&Translations.Match(onimusha,"penguin")?.Access=="paid"&&Translations.Match(baldurs,"penguin")?.Access=="paid","live official free and paid classifications");
  }
  await SelfTestUpdates(directory,Check);
  await SelfTestLocalization(directory,Check);
  await SelfTestNotifications(directory,Check);
  File.WriteAllLines(Path.Combine(directory,"checks.txt"),checks);
 }
 private static IEnumerable<T> Find<T>(DependencyObject parent)where T:DependencyObject{for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T found)yield return found;foreach(var nested in Find<T>(child))yield return nested;}}
 private void Capture(string file){var bitmap=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(file);encoder.Save(stream);}
}
