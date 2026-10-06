using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
namespace GameLogDesktop;
public partial class MainWindow : Window
{
 private readonly Storage store=new(App.SelfTestDirectory);private readonly SteamService steam=new();private CancellationTokenSource? task;
 private Game? current;private Game? manual;private DateTime manualStart;
 private readonly Dictionary<string,DateTime> autoSessions=[];private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(15)};
 private DateTime lastAuto=DateTime.Now;private bool ready;
 private static readonly string[] statuses=["Muốn chơi","Đang chơi","Đã xong","Bỏ dở"];
 public MainWindow()
 {
  InitializeComponent();StatusFilter.ItemsSource=new[]{"Lọc trạng thái"}.Concat(statuses);StatusFilter.SelectedIndex=0;
  KindFilter.ItemsSource=new[]{"Chỉ game","Chỉ phần mềm","Game / phần mềm"};KindFilter.SelectedIndex=0;
  StatusCombo.ItemsSource=statuses;KindCombo.ItemsSource=new[]{"Game","Phần mềm"};PlatformCombo.ItemsSource=new[]{"Steam","Epic","GOG","Battle.net","EA","Ubisoft","Microsoft Store","Console","Mobile","Khác"};
  RatingCombo.ItemsSource=new[]{"Chưa đánh giá"}.Concat(Enumerable.Range(0,11).Select(x=>$"{x} — {(x<=2?"Cực tệ":x<=5?"Tệ":x<=8?"Cũng được":x==9?"Hay":"GOTY")}"));
  ready=true;Refresh();RefreshDiscovery();RefreshTranslations();ReadUpdateResult();ReadUpdateDefaults();InitializeNotifications();timer.Tick+=TrackTick;timer.Start();
 }
 private void Say(string message)=>Notice.Text=message;
 private void Persist(){CommitDetails();store.Save();current?.Notify();Stats.Text=$"{store.Games.Count(x=>x.Kind!="Phần mềm")} game · {store.Games.Count(x=>x.Kind=="Phần mềm")} phần mềm";}
 private void CommitDetails(){if(Details is null)return;void Visit(DependencyObject element){if(element is TextBox text)text.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(element);i++)Visit(System.Windows.Media.VisualTreeHelper.GetChild(element,i));}Visit(Details);}
 private void Refresh()
 {
  if(!ready)return;var selected=current?.Id;var visible=store.Games.Where(g=>g.Name.Contains(Search.Text??"",StringComparison.OrdinalIgnoreCase)&& (StatusFilter.SelectedIndex<=0||g.Status==(string)StatusFilter.SelectedItem)&& (KindFilter.SelectedIndex==2||g.Kind==(KindFilter.SelectedIndex==1?"Phần mềm":"Game"))).OrderBy(g=>g.Kind=="Phần mềm").ThenByDescending(g=>g.Minutes).ThenBy(g=>g.Name).ToList();
  for(var i=0;i<visible.Count;i++)visible[i].Number=i+1;Library.ItemsSource=visible;Library.SelectedItem=visible.FirstOrDefault(x=>x.Id==selected)??visible.FirstOrDefault();RefreshTranslations();Stats.Text=$"{store.Games.Count(x=>x.Kind!="Phần mềm")} game · {store.Games.Count(x=>x.Kind=="Phần mềm")} phần mềm";
 }
 private void RefreshDiscovery(){var watches=store.Discovery.Items.OrderByDescending(x=>x.Discount).ThenBy(x=>x.Name).ToList();for(var i=0;i<watches.Count;i++)watches[i].Number=i+1;WatchGrid.ItemsSource=watches;var news=store.Discovery.News.OrderByDescending(x=>x.Date).ToList();for(var i=0;i<news.Count;i++){news[i].Number=i+1;news[i].GameName=store.Games.FirstOrDefault(x=>x.AppId==news[i].AppId)?.Name??store.Discovery.Items.FirstOrDefault(x=>x.AppId==news[i].AppId)?.Name??news[i].AppId;}NewsGrid.ItemsSource=news;}
 private void FilterChanged(object sender,RoutedEventArgs e){if(ready)Refresh();}
 private void GameSelected(object sender,SelectionChangedEventArgs e)
 {
  current=Library.SelectedItem as Game;Details.DataContext=current;Details.IsEnabled=current!=null;AchievementsGrid.ItemsSource=current?.Achievements;SessionsGrid.ItemsSource=current?.Sessions;
 }
 private async Task Run(Func<CancellationToken,Task> work)
 {
  if(task!=null){Say("Có tác vụ đang chạy; chờ hoàn tất hoặc bấm Huỷ.");return;}
  task=new();CancelButton.Visibility=Visibility.Visible;
  try{await work(task.Token);}catch(OperationCanceledException){Say("Đã huỷ. Dữ liệu tải thành công trước đó được giữ lại.");}catch(Exception ex){Say("Không hoàn tất: "+ex.Message);}finally{task.Dispose();task=null;CancelButton.Visibility=Visibility.Collapsed;}
 }
 private void CancelTask(object sender,RoutedEventArgs e)=>task?.Cancel();
 private void ImportOld(object sender,RoutedEventArgs e){var d=new OpenFileDialog{Title="Chọn data/games.json của app PowerShell",Filter="Nhật ký game|games.json|JSON|*.json"};if(d.ShowDialog()==true)try{store.Import(d.FileName);Refresh();RefreshDiscovery();Say("Đã nhập dữ liệu. Bản PowerShell được giữ nguyên.");}catch(Exception ex){Say("Nhập thất bại: "+ex.Message);}}
 private void AddGame(object sender,RoutedEventArgs e){var g=new Game{Platform="Khác"};store.Games.Add(g);current=g;KindFilter.SelectedIndex=2;Search.Text="";StatusFilter.SelectedIndex=0;Refresh();Persist();Say("Đã thêm game. Điền thông tin rồi lưu.");}
 private void SaveGame(object sender,RoutedEventArgs e){try{Persist();Refresh();Say("Đã lưu thay đổi.");}catch(Exception ex){Say("Lưu thất bại: "+ex.Message);}}
 private void DeleteGame(object sender,RoutedEventArgs e){if(current==null)return;if(manual==current||autoSessions.ContainsKey(current.Id)){Say("Kết thúc phiên chơi trước khi xoá.");return;}Confirm("Xoá game?","Thao tác xoá game và các phiên chơi trong bản desktop.",()=>{store.Games.Remove(current);current=null;Persist();Refresh();Say("Đã xoá game.");});}
 private void LaunchGame(object sender,RoutedEventArgs e){if(current?.AppId is not {Length:>0}){Say("Game chưa có Steam AppID.");return;}Open("steam://rungameid/"+current.AppId);}
 private void LaunchStore(object sender,RoutedEventArgs e){if(current?.AppId is {Length:>0})Open("https://store.steampowered.com/app/"+current.AppId);}
 private static void Open(string url){if(Uri.TryCreate(url,UriKind.Absolute,out var u)&&u.Scheme is "https" or "http" or "steam")Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
 private void OpenData(object sender,RoutedEventArgs e)=>Process.Start(new ProcessStartInfo("explorer.exe",store.DirectoryPath){UseShellExecute=true});
 private void BackupClick(object sender,RoutedEventArgs e){try{Persist();Say("Đã sao lưu: "+store.Backup());}catch(Exception ex){Say(ex.Message);}}
 private void Merge(IEnumerable<Game> incoming){foreach(var g in incoming){var old=store.Games.FirstOrDefault(x=>x.Platform=="Steam"&&x.AppId==g.AppId);if(old==null)store.Games.Add(g);else{old.Minutes=Math.Max(old.Minutes,g.Minutes);if(string.CompareOrdinal(g.LastPlayed,old.LastPlayed)>0)old.LastPlayed=g.LastPlayed;old.Notify();}}Persist();Refresh();}
 private async void SyncOwned(object sender,RoutedEventArgs e)=>await Run(async ct=>{Say("Đang đồng bộ thư viện Steam…");var games=await steam.Owned(store.Settings,ct);Merge(games);Say($"Đã đồng bộ {games.Count} game Steam.");});
 private void ImportInstalled(object sender,RoutedEventArgs e){try{var games=SteamService.Installed();Merge(games);Say($"Đã nhập {games.Count} ứng dụng Steam đã cài. Giờ chơi đầy đủ cần đồng bộ API.");}catch(Exception ex){Say(ex.Message);}}
 private async void SyncAchievements(object sender,RoutedEventArgs e){var g=current;if(g?.AppId is not {Length:>0}){Say("Chọn game có Steam AppID.");return;}await Run(async ct=>{var a=await steam.Achievements(g,store.Settings,ct);g.Achievements=g.Achievements.Where(x=>x.Source!="steam").Concat(a).ToList();Persist();if(g==current)AchievementsGrid.ItemsSource=g.Achievements;Say($"Đã đồng bộ {a.Count} thành tựu.");});}
 private void AddAchievement(object sender,RoutedEventArgs e){if(current==null||string.IsNullOrWhiteSpace(AchievementInput.Text))return;current.Achievements.Add(new(){Name=AchievementInput.Text.Trim(),Source="Nhập tay"});AchievementInput.Clear();Persist();AchievementsGrid.ItemsSource=null;AchievementsGrid.ItemsSource=current.Achievements;Say("Đã thêm thành tựu.");}
 private void DeleteAchievement(object sender,RoutedEventArgs e){if(current==null||AchievementsGrid.SelectedItem is not Achievement a)return;current.Achievements.Remove(a);Persist();AchievementsGrid.ItemsSource=null;AchievementsGrid.ItemsSource=current.Achievements;}
 private void AchievementEdited(object sender,DataGridCellEditEndingEventArgs e){if(e.Row.Item is Achievement a&&a.Source=="steam"){e.Cancel=true;Say("Thành tựu Steam được cập nhật bằng Đồng bộ Steam.");return;}Dispatcher.InvokeAsync(()=>{if(e.Row.Item is Achievement a){a.Date=a.Done?DateTime.Now.ToString("yyyy-MM-dd HH:mm"):"";Persist();}},DispatcherPriority.Background);}
 private void DeleteSession(object sender,RoutedEventArgs e){if(current==null||SessionsGrid.SelectedItem is not Session record)return;current.Minutes=Math.Max(0,current.Minutes-record.Minutes);current.Sessions.Remove(record);Persist();SessionsGrid.ItemsSource=null;SessionsGrid.ItemsSource=current.Sessions;Refresh();Say("Đã xoá phiên và trừ thời gian tương ứng.");}
 private async Task Prices(IEnumerable<string> ids,bool wishlist,CancellationToken ct)
 {
  var list=ids.Distinct().ToList();var errors=new List<string>();var alerts=new List<string>();var completed=0;
  foreach(var id in list){ct.ThrowIfCancellationRequested();try{Say($"Đang kiểm tra giá {completed+1}/{list.Count}…");var fresh=await steam.Details(id,ct);var old=store.Discovery.Items.FirstOrDefault(x=>x.AppId==id);fresh.Wishlist=wishlist||old?.Wishlist==true;fresh.Low=fresh.Price.HasValue?Math.Min(old?.Low??fresh.Price.Value,fresh.Price.Value):old?.Low;fresh.SteamDbLow=old?.SteamDbLow;fresh.Target=old?.Target;fresh.NotifiedRelease=old?.NotifiedRelease??"";
   if(old?.Price!=null&&fresh.Price<old.Price)alerts.Add(fresh.Name+": giảm còn "+fresh.PriceText);
   if(fresh.Target>0&&fresh.Price<=fresh.Target&&(old?.Price==null||old.Price>fresh.Target))alerts.Add(fresh.Name+": đạt giá mục tiêu");
   if(DateTime.TryParseExact(fresh.Release,["d MMM, yyyy","MMM d, yyyy","d MMM yyyy","MMMM d, yyyy"],CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)&&fresh.ComingSoon&&date.Date>=DateTime.Today&&date.Date<=DateTime.Today.AddDays(7)&&fresh.NotifiedRelease!=fresh.Release){alerts.Add(fresh.Name+": sắp ra mắt "+fresh.Release);fresh.NotifiedRelease=fresh.Release;}
   if(old?.ComingSoon==true&&!fresh.ComingSoon)alerts.Add(fresh.Name+": đã phát hành");
   store.Discovery.Items.RemoveAll(x=>x.AppId==id);store.Discovery.Items.Add(fresh);Persist();RefreshDiscovery();completed++;
  }catch(OperationCanceledException){throw;}catch(Exception ex){errors.Add(id+": "+ex.Message);}await Task.Delay(350,ct);}
  if(wishlist){foreach(var w in store.Discovery.Items)w.Wishlist=list.Contains(w.AppId);}
  store.Discovery.LastSync=DateTime.Now.ToString("s");Persist();Say($"Đã cập nhật {completed}/{list.Count}; {errors.Count} lỗi. "+string.Join(" · ",alerts.Take(3))+ (errors.Count>0?" "+errors[0]:""));
 }
 private async void SyncWishlist(object sender,RoutedEventArgs e)=>await Run(async ct=>{Say("Đang lấy wishlist…");var ids=await steam.Wishlist(store.Settings,ct);await Prices(ids,true,ct);});
 private async void RefreshPrices(object sender,RoutedEventArgs e)=>await Run(ct=>Prices(store.Discovery.Items.Select(x=>x.AppId).ToList(),false,ct));
 private async void AddWatch(object sender,RoutedEventArgs e){var id=WatchId.Text.Trim();await Run(ct=>Prices([id],false,ct));}
 private WatchGame? Watch=>WatchGrid.SelectedItem as WatchGame;
 private void WatchSteam(object sender,RoutedEventArgs e){if(Watch!=null)Open("https://store.steampowered.com/app/"+Watch.AppId);}
 private void WatchDb(object sender,RoutedEventArgs e){if(Watch!=null)Open("https://steamdb.info/app/"+Watch.AppId+"/?__currency=VND");}
 private void PriceInput(bool db){if(Watch==null){Say("Chọn game trong bảng theo dõi.");return;}if(!double.TryParse(TargetPrice.Text.Trim().Replace(".","").Replace(",",""),NumberStyles.None,CultureInfo.InvariantCulture,out var amount)||amount<0){Say("Nhập số tiền VND nguyên, không âm.");return;}if(db)Watch.SteamDbLow=amount;else Watch.Target=amount;Persist();RefreshDiscovery();Say(db?"Đã lưu mốc SteamDB nhập tay; đây không phải dữ liệu tự xác minh.":"Đã lưu giá mục tiêu.");}
 private void SaveTarget(object sender,RoutedEventArgs e)=>PriceInput(false);private void SaveDbLow(object sender,RoutedEventArgs e)=>PriceInput(true);
 private async Task LoadNews(IEnumerable<string> ids,CancellationToken ct){var count=0;var errors=0;foreach(var id in ids.Distinct()){ct.ThrowIfCancellationRequested();try{var news=await steam.News(id,ct);foreach(var n in news)if(!store.Discovery.News.Any(x=>x.Gid==n.Gid)){store.Discovery.News.Add(n);count++;}Say("Đang tải tin: "+id);}catch(OperationCanceledException){throw;}catch{errors++;}await Task.Delay(350,ct);}store.Discovery.News=store.Discovery.News.OrderByDescending(x=>x.Date).Take(1000).ToList();Persist();RefreshDiscovery();Say($"Đã thêm {count} bài; {errors} game không đọc được tin.");}
 private async void SelectedNews(object sender,RoutedEventArgs e){if(current?.AppId is not {Length:>0}){Say("Chọn game trong thư viện trước.");return;}var id=current.AppId;await Run(ct=>LoadNews([id],ct));}
 private async void OwnedNews(object sender,RoutedEventArgs e)=>await Run(ct=>LoadNews(store.Games.Where(x=>x.Platform=="Steam"&&x.AppId!="").Select(x=>x.AppId).ToList(),ct));
 private void NewsSelected(object sender,SelectionChangedEventArgs e){if(NewsGrid.SelectedItem is NewsItem n)NewsBody.Text=n.Title+"\n"+n.Date+"\n\n"+System.Net.WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(n.Content,"<[^>]+>",""),"\\[/?[^\\]]+\\]",""));}
 private void OpenNews(object sender,RoutedEventArgs e){if(NewsGrid.SelectedItem is NewsItem n)Open(n.Url);}
 private void AddMinutes(Game g,DateTime start,double minutes,string source){if(minutes<=0)return;g.Minutes+=minutes;g.LastPlayed=DateTime.Now.ToString("yyyy-MM-dd HH:mm");g.Sessions.Add(new(){Start=start.ToString("yyyy-MM-dd HH:mm"),Minutes=Math.Round(minutes,1),Source=source});if(g.Status=="Muốn chơi")g.Status="Đang chơi";g.Notify();Persist();if(g==current){SessionsGrid.ItemsSource=null;SessionsGrid.ItemsSource=g.Sessions;}Refresh();}
 private void ToggleSession(object sender,RoutedEventArgs e){if(manual!=null){var g=manual;manual=null;AddMinutes(g,manualStart,(DateTime.Now-manualStart).TotalMinutes,"Hẹn giờ");TimerButton.Content="Bắt đầu phiên";SessionClock.Text="";return;}if(current==null)return;if(autoSessions.ContainsKey(current.Id)){Say("Game đang được tự theo dõi; không bắt đầu thêm phiên để tránh tính hai lần.");return;}manual=current;manualStart=DateTime.Now;TimerButton.Content="Kết thúc phiên";Say("Đã bắt đầu: "+manual.Name);}
 private void ManualSession(object sender,RoutedEventArgs e){if(current==null)return;if(!double.TryParse(MinutesInput.Text,out var m)||m<=0||m>100000){Say("Nhập số phút từ 1 đến 100000.");return;}AddMinutes(current,DateTime.Now.AddMinutes(-m),m,"Nhập tay");MinutesInput.Clear();Say("Đã thêm phiên chơi.");}
 private async void TrackTick(object? sender,EventArgs e)
 {
  CheckNotifications();
  if(manual!=null)SessionClock.Text=manual.Name+" · "+(DateTime.Now-manualStart).ToString(@"hh\:mm\:ss");
  try{var processes=Process.GetProcesses();var names=processes.Select(x=>x.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);foreach(var p in processes)p.Dispose();foreach(var g in store.Games.Where(x=>x.Exe!=""&&x!=manual).ToList()){var running=names.Contains(Path.GetFileNameWithoutExtension(g.Exe));if(running&&!autoSessions.ContainsKey(g.Id))autoSessions[g.Id]=DateTime.Now;else if(!running&&autoSessions.Remove(g.Id,out var start))AddMinutes(g,start,(DateTime.Now-start).TotalMinutes,"Tự động");}}catch(Exception ex){Say("Theo dõi tiến trình: "+ex.Message);}
  if(store.Settings.AutoRefresh&&task==null&&(DateTime.Now-lastAuto).TotalHours>=6){lastAuto=DateTime.Now;await Run(ct=>Prices(store.Discovery.Items.Select(x=>x.AppId).ToList(),false,ct));}
 }
 private void WindowClosing(object? sender,System.ComponentModel.CancelEventArgs e){if(task!=null){task.Cancel();Say("Đang huỷ tác vụ. Đóng cửa sổ lần nữa sau khi tác vụ dừng.");e.Cancel=true;return;}try{timer.Stop();if(manual!=null){var g=manual;manual=null;AddMinutes(g,manualStart,(DateTime.Now-manualStart).TotalMinutes,"Hẹn giờ");}foreach(var pair in autoSessions.ToList()){autoSessions.Remove(pair.Key);var g=store.Games.FirstOrDefault(x=>x.Id==pair.Key);if(g!=null)AddMinutes(g,pair.Value,(DateTime.Now-pair.Value).TotalMinutes,"Tự động");}Persist();}catch(Exception ex){e.Cancel=true;Say("Chưa đóng được vì lưu dữ liệu thất bại: "+ex.Message);timer.Start();}}
 private void SteamEvents(object sender,RoutedEventArgs e)=>Open("https://partner.steamgames.com/doc/marketing/upcoming_events");private void SteamSales(object sender,RoutedEventArgs e)=>Open("https://store.steampowered.com/specials/");private void EpicEvents(object sender,RoutedEventArgs e)=>Open("https://store.epicgames.com/");private void GogEvents(object sender,RoutedEventArgs e)=>Open("https://www.gog.com/");
 private void Confirm(string title,string text,Action action){var d=Dialog(title);var p=new StackPanel{Margin=new Thickness(24)};p.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,20)});var b=new Button{Content="Xác nhận"};b.Click+=(_,_)=>{action();d.Close();};p.Children.Add(b);var cancel=new Button{Content="Huỷ"};cancel.Click+=(_,_)=>d.Close();p.Children.Add(cancel);d.Content=p;d.ShowDialog();}
 private Window Dialog(string title)=>new(){Title=title,Owner=this,Width=570,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,MaxHeight=800};
 private void SettingsClick(object sender,RoutedEventArgs e)
 {
  var d=Dialog("Cài đặt Steam");var panel=new StackPanel{Margin=new Thickness(24)};var key=new TextBox{Text=store.Settings.SteamApiKey};var sid=new TextBox{Text=store.Settings.SteamId};var auto=new CheckBox{Content="Cập nhật giá mỗi 6 giờ khi app mở",IsChecked=store.Settings.AutoRefresh,Foreground=System.Windows.Media.Brushes.White,Margin=new Thickness(0,16,0,16)};
  foreach(var pair in new[]{("Steam API key",key),("SteamID64 — 17 chữ số",sid)}){panel.Children.Add(new TextBlock{Text=pair.Item1,Margin=new Thickness(0,0,0,8)});pair.Item2.Margin=new Thickness(0,0,0,16);panel.Children.Add(pair.Item2);}
  var link=new Button{Content="Lấy API key trên Steam"};link.Click+=(_,_)=>Open("https://steamcommunity.com/dev/apikey");panel.Children.Add(link);panel.Children.Add(auto);panel.Children.Add(new TextBlock{Text="Hồ sơ và wishlist cần công khai. Cài đặt lưu trong desktop-data/settings.json.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16)});
  var error=new TextBlock{Foreground=System.Windows.Media.Brushes.LightSalmon};panel.Children.Add(error);var save=new Button{Content="Lưu"};save.Click+=(_,_)=>{if(sid.Text!=""&&!Regex.IsMatch(sid.Text.Trim(),"^\\d{17}$")){error.Text="SteamID64 phải gồm 17 chữ số.";return;}store.Settings.SteamId=sid.Text.Trim();store.Settings.SteamApiKey=key.Text.Trim();store.Settings.AutoRefresh=auto.IsChecked==true;try{Persist();Say("Đã lưu cài đặt.");d.Close();}catch(Exception ex){error.Text=ex.Message;}};panel.Children.Add(save);var cancel=new Button{Content="Huỷ"};cancel.Click+=(_,_)=>d.Close();panel.Children.Add(cancel);d.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};d.ShowDialog();
 }
 private void FunctionsClick(object sender,RoutedEventArgs e)
 {
  var menu=new ContextMenu{Background=System.Windows.Media.Brushes.MidnightBlue,Foreground=System.Windows.Media.Brushes.White};
  void Item(string text,RoutedEventHandler handler){var i=new MenuItem{Header=text};i.Click+=handler;menu.Items.Add(i);}
  Item("Nhập game Steam trên máy",ImportInstalled);Item("Đồng bộ thư viện Steam API",SyncOwned);Item("Xuất CSV",ExportCsv);Item("Tạo shortcut ngoài Desktop",DesktopShortcut);Item("Kiểm tra cập nhật",CheckUpdates);Item("Thiết lập nguồn cập nhật",UpdateSettings);menu.PlacementTarget=sender as Button;menu.IsOpen=true;
 }
 private void ExportCsv(object sender,RoutedEventArgs e){var d=new SaveFileDialog{Filter="CSV|*.csv",FileName="games.csv"};if(d.ShowDialog()!=true)return;static string Q(object? v)=>"\""+(v?.ToString()??"").Replace("\"","\"\"")+"\"";File.WriteAllLines(d.FileName,new[]{"Tên game,Nền tảng,Phân loại,Phút,Điểm,Ghi chú"}.Concat(store.Games.Select(g=>string.Join(",",new[]{Q(g.Name),Q(g.Platform),Q(g.Kind),Q(g.Minutes),Q(g.RatingSet?g.Rating:null),Q(g.Notes)}))),new System.Text.UTF8Encoding(true));Say("Đã xuất CSV.");}
}


