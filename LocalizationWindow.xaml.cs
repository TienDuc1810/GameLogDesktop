using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Threading;
namespace GameLogDesktop;
public partial class LocalizationWindow:Window
{
 private readonly Game game; private bool patchBusy;
 private LocalizationInventory? inventory;
 private string scannedRoot="";
 private MetroTextDocument? document;
 private List<GameText> draft=[];
 private CancellationTokenSource? translationCancellation;
 private readonly AppNotifications notices;
 private readonly DispatcherTimer keyTimer=new(){Interval=TimeSpan.FromSeconds(1)};
 private TranslationQuote? quote;
 public LocalizationWindow(Game selected,bool package=false,AppNotifications? notifications=null,bool start=false)
 {
  notices=notifications??new AppNotifications(App.SelfTestDirectory??Path.Combine(AppContext.BaseDirectory,"desktop-data"));
  InitializeComponent();game=selected;GameTitle.Text=game.Name;Tabs.SelectedIndex=package?2:1;
  try{ApiKey.Password=SharedApiKey.Load(notices.DirectoryPath);}catch(Exception ex){Status.Text="Không đọc được key dùng chung: "+ex.Message;}
  if(game.AppId!="286690"){GameTranslateButton.IsEnabled=false;Status.Text="Tự dịch hiện hỗ trợ Metro 2033 Redux. Chọn đúng game trong bảng Việt hóa; thay thư mục không thay game đã chọn.";}
  Closed+=(_,_)=>{keyTimer.Stop();translationCancellation?.Cancel();ApiKey.Clear();};keyTimer.Tick+=(_,_)=>UpdateKeyCountdown();keyTimer.Start();UpdateKeyCountdown();
  try{Folder.Text=SteamService.InstallFolder(game.AppId)??"";}catch{Status.Text="Chọn thư mục game bằng nút Chọn thư mục game.";}
  if(start&&game.AppId=="286690")Loaded+=(_,_)=>{if(!string.IsNullOrWhiteSpace(Folder.Text))TranslateGame(this,new RoutedEventArgs());else Status.Text="Chọn thư mục cài game, rồi bấm Dịch game.";};
 }
 private void CredentialsChanged(object sender,RoutedEventArgs e){if(CostSummary==null)return;InvalidateQuote();}
 private void InvalidateQuote(){quote=null;if(CostSummary!=null)CostSummary.Text="Tính lại token và chi phí cho lô dịch tiếp theo. Giới hạn 1 USD mỗi lần bấm dịch.";if(TranslateButton!=null)TranslateButton.IsEnabled=false;}
 private void RegisterKey(object sender,RoutedEventArgs e)
 {
  try{notices.Register(ApiKey.Password);InvalidateQuote();UpdateKeyCountdown();Status.Text="Đã ghi nhận thời hạn 30 ngày. Nhập lại cùng key giữ nguyên mốc cũ; đây là mốc app theo dõi, không xác minh hạn phía nhà cung cấp.";}
  catch(Exception ex){Status.Text="Không ghi nhận được key: "+ex.Message;}
 }
 private void UpdateKeyCountdown()
 {
  try{notices.CheckExpiry();KeyCountdown.Text=notices.Countdown();if(quote!=null&&(DateTimeOffset.UtcNow-quote.CreatedUtc>TimeSpan.FromMinutes(5)||notices.Active==null||DateTimeOffset.UtcNow>=notices.Active.ExpiresUtc))InvalidateQuote();}
  catch(Exception ex){TranslateButton.IsEnabled=false;Status.Text="Theo dõi thời hạn bị lỗi: "+ex.Message;}
 }
 private List<GameText> NextLines()=>draft.Where(x=>string.IsNullOrWhiteSpace(x.Vietnamese)&&!string.IsNullOrWhiteSpace(x.Source)).Take(20).ToList();
 private void SharedKeySettings(object sender,RoutedEventArgs e){SharedApiKeyDialog.Show(this,notices);ApiKey.Password=SharedApiKey.Load(notices.DirectoryPath);UpdateKeyCountdown();}
 private bool ConfirmJob(TranslationJob job)
 {
  var allowed=job.CeilingUsd<=AppNotifications.MaxUsd;var dialog=new Window{Title="Xác nhận dịch game",Owner=this,Width=560,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};var panel=new System.Windows.Controls.StackPanel{Margin=new Thickness(24)};
  panel.Children.Add(new System.Windows.Controls.TextBlock{Text=$"{game.Name} · {job.Count:N0} câu\n\nChi phí ước tính: ${TranslationQuote.Label(job.CeilingUsd)}. Dùng GPT-4.1 nano; câu lỗi giữ nguyên tiếng Anh. Dừng trước khi yêu cầu tiếp theo có thể làm tổng phí vượt 1 USD.\n"+(allowed?"Bản dịch sẽ tự lưu JSON. Chưa cài vào game; cần xử lý font và đóng gói trước.":"Vượt giới hạn 1 USD mỗi lần bạn đã đặt. App không gửi yêu cầu dịch."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,20)});
  var yes=new System.Windows.Controls.Button{Content="Dịch",IsEnabled=allowed};yes.Click+=(_,_)=>dialog.DialogResult=true;panel.Children.Add(yes);var no=new System.Windows.Controls.Button{Content="Không dịch"};no.Click+=(_,_)=>dialog.DialogResult=false;panel.Children.Add(no);dialog.Content=panel;return dialog.ShowDialog()==true;
 }
 private string SaveJobDraft()
 {
  if(document==null)throw new InvalidOperationException("Chưa đọc văn bản game.");var folder=Path.Combine(notices.DirectoryPath,"translations",game.AppId);Directory.CreateDirectory(folder);var file=Path.Combine(folder,"draft-"+document.TextHash+".json");var temporary=file+".tmp";
  File.WriteAllText(temporary,JsonSerializer.Serialize(new{Product="GameLogTranslationDraft",Version=1,AppId=game.AppId,document.IndexHash,document.TextHash,document.MissingCharacters,Lines=draft},Storage.Json));File.Move(temporary,file,true);return file;
 }
 private async void TranslateGame(object sender,RoutedEventArgs e)
 {
  if(translationCancellation!=null||patchBusy)return;translationCancellation=new();GameTranslateButton.IsEnabled=false;CancelTranslationButton.IsEnabled=true;Folder.IsEnabled=false;ScanButton.IsEnabled=false;SharedKeyButton.IsEnabled=false;string saved="";
  try{
   var key=SharedApiKey.Load(notices.DirectoryPath);if(key==""){SharedApiKeyDialog.Show(this,notices);key=SharedApiKey.Load(notices.DirectoryPath);}notices.RequireKey(key);if(game.AppId!="286690")throw new InvalidOperationException("Bộ xử lý tự dịch hiện chỉ hỗ trợ Metro 2033 Redux.");
   Status.Text="Đang đọc văn bản và chuẩn bị chi phí…";var root=Path.GetFullPath(Folder.Text.Trim());document=await Task.Run(()=>MetroLocalization.ReadEnglish(root),translationCancellation.Token);draft=document.Lines.ToList();
   var previous=Path.Combine(notices.DirectoryPath,"translations",game.AppId,"draft-"+document.TextHash+".json");if(File.Exists(previous)){using var json=JsonDocument.Parse(File.ReadAllText(previous));if(json.RootElement.GetProperty("AppId").GetString()!=game.AppId||json.RootElement.GetProperty("IndexHash").GetString()!=document.IndexHash)throw new InvalidDataException("Bản nháp không khớp tài nguyên game.");var old=json.RootElement.GetProperty("Lines").Deserialize<List<GameText>>(Storage.Json)??[];var originals=document.Lines.ToDictionary(x=>x.Id);if(old.Count!=originals.Count||old.Select(x=>x.Id).Distinct().Count()!=old.Count||old.Any(x=>!originals.TryGetValue(x.Id,out var original)||x.Source!=original.Source))throw new InvalidDataException("Bản nháp không khớp câu gốc.");var done=old.Where(x=>!string.IsNullOrWhiteSpace(x.Vietnamese)).ToList();OpenAiTranslation.Validate(done.Select(x=>originals[x.Id]).ToList(),done);var map=old.ToDictionary(x=>x.Id);draft=draft.Select(x=>map.TryGetValue(x.Id,out var translated)&&translated.Source==x.Source?translated:x).ToList();}
   TextGrid.ItemsSource=draft;using var api=new OpenAiTranslation();var job=TranslationJobPlanner.Plan(draft,await api.CurrentPrice(translationCancellation.Token));if(job.Count==0){Status.Text="Bản nháp đã dịch đủ câu: "+previous;return;}
   if(job.CeilingUsd>1m)Notify("Dịch game vượt giới hạn",$"{game.Name}: trần dự phòng ${TranslationQuote.Label(job.CeilingUsd)} > 1 USD; chưa gửi dịch.","Cảnh báo");
   if(!ConfirmJob(job)){Status.Text="Không gửi dịch. Bạn có thể xem lại giới hạn và bản nháp.";return;}saved=SaveJobDraft();decimal reserved=0;var completed=0;
   foreach(var batch in job.Batches){translationCancellation.Token.ThrowIfCancellationRequested();var q=await api.Quote(batch,key,TranslationCosts.DefaultModel,notices,translationCancellation.Token);if(reserved+q.CeilingUsd>1m)throw new InvalidOperationException("Chi phí mới vượt trần đã xác nhận; dừng trước khi gửi lô tiếp theo.");var result=await api.Translate(batch,key,q,notices,translationCancellation.Token);reserved+=notices.State.Reservations.Last().ActualUsd??q.CeilingUsd;draft=TranslationJobPlanner.Merge(draft,result);saved=SaveJobDraft();completed+=result.Count;TextGrid.ItemsSource=draft;Status.Text=$"Đang dịch {completed:N0}/{job.Count:N0} câu · bản nháp tự lưu.";}
   Status.Text="Đã dịch xong và tự lưu JSON: "+saved+". Chưa cài bản dịch vào game.";Notify("Đã dịch xong",game.Name+" · JSON: "+saved+". Chưa áp dụng vào game.");
  }catch(OperationCanceledException){Status.Text="Đã dừng. Các lô hoàn tất đã tự lưu; yêu cầu đang gửi có thể vẫn bị tính phí.";}
  catch(Exception ex){Status.Text="Chưa hoàn tất dịch game: "+ex.Message;Notify("Dịch game chưa hoàn tất",ex.Message+(saved!=""?" · Bản nháp: "+saved:""),"Cảnh báo");}
  finally{translationCancellation.Dispose();translationCancellation=null;GameTranslateButton.IsEnabled=game.AppId=="286690";CancelTranslationButton.IsEnabled=false;Folder.IsEnabled=true;ScanButton.IsEnabled=true;SharedKeyButton.IsEnabled=true;}
 }
 private async void EstimateCost(object sender,RoutedEventArgs e)
 {
  if(document==null||translationCancellation!=null)return;InvalidateQuote();var lines=NextLines();if(lines.Count==0){Status.Text="Đã hết câu chưa dịch.";return;}
  translationCancellation=new();EstimateButton.IsEnabled=false;ReadTextButton.IsEnabled=false;CancelTranslationButton.IsEnabled=true;
  try{Status.Text="Đang kiểm tra giá chính thức và đếm token đầu vào; chưa gửi yêu cầu dịch…";using var service=new OpenAiTranslation();var key=ApiKey.Password;var model=Model.Text.Trim();var prepared=await service.Quote(lines,key,model,notices,translationCancellation.Token);
   if(key!=ApiKey.Password||model!=Model.Text.Trim())throw new InvalidOperationException("Key hoặc model đã thay đổi; tính lại chi phí.");quote=prepared;
   CostSummary.Text=$"{lines.Count} câu · token vào: {quote.InputTokens:N0} · token ra dự kiến: {quote.EstimatedOutputTokens:N0} (trần {quote.MaxOutputTokens:N0})\nƯớc tính: ${TranslationQuote.Label(quote.EstimatedUsd)} · chi phí trần dự phòng: ${TranslationQuote.Label(quote.CeilingUsd)} / giới hạn $1.00 mỗi lần.\nGiá standard: ${quote.Price.InputPerMillion}/1M token vào, ${quote.Price.OutputPerMillion}/1M token ra; vừa kiểm tra từ OpenAI. Ước tính có hiệu lực 5 phút.";
   TranslateButton.IsEnabled=true;Status.Text="Đã tính xong. Xem chi phí ở trên rồi bấm Dịch để gửi lô này.";
  }catch(OperationCanceledException){Status.Text="Đã huỷ bước tính chi phí; chưa gửi dịch.";}
  catch(Exception ex){Status.Text="Đã chặn dịch: "+ex.Message;Notify("Đã chặn dịch",ex.Message,"Cảnh báo");}
  finally{translationCancellation.Dispose();translationCancellation=null;EstimateButton.IsEnabled=true;ReadTextButton.IsEnabled=true;CancelTranslationButton.IsEnabled=false;}
 }
 private void Notify(string title,string message,string level="Thông tin"){try{notices.Add(title,message,level);}catch{Status.Text+=" Không lưu được thông báo; kiểm tra quyền ghi thư mục dữ liệu.";}}
 private async void ReadText(object sender,RoutedEventArgs e)
 {
  InvalidateQuote();ReadTextButton.IsEnabled=false;EstimateButton.IsEnabled=false;TranslateButton.IsEnabled=false;ExportDraftButton.IsEnabled=false;document=null;draft=[];TextGrid.ItemsSource=null;
  try{if(game.AppId!="286690")throw new InvalidDataException("Bộ đọc hiện chỉ kiểm tra Metro 2033 Redux (Steam AppID 286690).");var root=Path.GetFullPath(Folder.Text.Trim());Status.Text="Đang đọc mục lục và giải nén tài nguyên tiếng Anh…";document=await Task.Run(()=>MetroLocalization.ReadEnglish(root));draft=document.Lines.ToList();TextGrid.ItemsSource=draft;EstimateButton.IsEnabled=true;ExportDraftButton.IsEnabled=true;Status.Text=$"Đã đọc {draft.Count:N0} câu. Bảng ký tự gốc thiếu: {document.MissingCharacters}. Chưa thể áp dụng bản dịch vào game.";}
  catch(Exception ex){Status.Text="Không đọc được văn bản: "+ex.Message;}
  finally{ReadTextButton.IsEnabled=true;}
 }
 private async void TranslateSample(object sender,RoutedEventArgs e)
 {
  if(document==null||translationCancellation!=null)return;
  var lines=NextLines();if(lines.Count==0){Status.Text="Đã hết câu chưa dịch.";return;}
  if(quote==null||quote.Model!=Model.Text.Trim()){Status.Text="Tính token và chi phí trước khi dịch.";return;}
  var prepared=quote;var key=ApiKey.Password;translationCancellation=new();TranslateButton.IsEnabled=false;EstimateButton.IsEnabled=false;ReadTextButton.IsEnabled=false;CancelTranslationButton.IsEnabled=true;
  try{Status.Text=$"Đang gửi {lines.Count} câu đến OpenAI…";using var service=new OpenAiTranslation();var translated=await service.Translate(lines,key,prepared,notices,translationCancellation.Token);var map=translated.ToDictionary(x=>x.Id);draft=draft.Select(x=>map.TryGetValue(x.Id,out var row)?row:x).ToList();TextGrid.ItemsSource=draft;Status.Text=$"Đã nhận {translated.Count} câu dịch, kiểm tra mã câu và biến thành công. Xuất JSON để lưu; chưa ghi vào game.";Notify("Dịch nháp hoàn tất",$"{game.Name}: đã nhận {translated.Count} câu. Xuất JSON trước khi đóng để giữ bản nháp.");}
  catch(OperationCanceledException){Status.Text="Đã dừng chờ dịch. Yêu cầu đã gửi có thể vẫn được OpenAI xử lý và tính phí.";}
  catch(Exception ex){Status.Text="Chưa nhận bản dịch: "+ex.Message;Notify("Dịch bị chặn hoặc chưa hoàn tất",ex.Message,"Cảnh báo");}
  finally{key="";translationCancellation.Dispose();translationCancellation=null;InvalidateQuote();EstimateButton.IsEnabled=true;ReadTextButton.IsEnabled=true;CancelTranslationButton.IsEnabled=false;}
 }
 private async void InstallNoAccents(object sender,RoutedEventArgs e)
 {
  if(translationCancellation!=null||patchBusy){Status.Text="Dừng hoặc chờ tác vụ xong trước khi cài bản thử.";return;}patchBusy=true;
  try{
   if(game.AppId!="286690")throw new InvalidOperationException("Bản thử chỉ dành cho Metro 2033 Redux.");
   var dialog=new OpenFileDialog{Title="Chọn JSON bản dịch Metro đã tự lưu",Filter="Bản dịch JSON|*.json"};if(dialog.ShowDialog(this)!=true)return;
   var root=Path.GetFullPath(Folder.Text.Trim());using var json=JsonDocument.Parse(File.ReadAllText(dialog.FileName));var original=await Task.Run(()=>MetroLocalization.ReadEnglish(root));
   if(json.RootElement.GetProperty("AppId").GetString()!=game.AppId||json.RootElement.GetProperty("IndexHash").GetString()!=original.IndexHash||json.RootElement.GetProperty("TextHash").GetString()!=original.TextHash)throw new InvalidDataException("JSON không khớp game đang cài.");
   var rows=json.RootElement.GetProperty("Lines").Deserialize<List<GameText>>(Storage.Json)??[];var translated=rows.Where(x=>!string.IsNullOrWhiteSpace(x.Vietnamese)).ToList();OpenAiTranslation.Validate(translated,translated);
   Status.Text="Đang tạo và kiểm tra bản thử không dấu…";var destination=Path.Combine(notices.DirectoryPath,"translations",game.AppId,"test-package-"+Guid.NewGuid().ToString("N"));await Task.Run(()=>MetroTestPatch.Build(root,rows,destination));await Task.Run(()=>MetroTestPatch.Install(root,destination));
   Status.Text="Đã cài bản thử tiếng Việt không dấu. Mở Metro, chọn ngôn ngữ English và bật phụ đề để kiểm tra. Có thể bấm Khôi phục tiếng Anh.";Notify("Đã cài bản thử Metro",Status.Text);
  }catch(Exception ex){Status.Text="Không cài được bản thử: "+ex.Message;}finally{patchBusy=false;}
 }
 private async void RestoreEnglish(object sender,RoutedEventArgs e)
 {
  if(patchBusy)return;patchBusy=true;try{if(game.AppId!="286690"||translationCancellation!=null)throw new InvalidOperationException("Chọn Metro và dừng dịch trước khi khôi phục.");await Task.Run(()=>MetroTestPatch.Restore(Folder.Text.Trim()));Status.Text="Đã khôi phục tài nguyên tiếng Anh gốc.";}catch(Exception ex){Status.Text="Không khôi phục được: "+ex.Message;}finally{patchBusy=false;}
 }
 private void CancelTranslation(object sender,RoutedEventArgs e)=>translationCancellation?.Cancel();
 private void ExportDraft(object sender,RoutedEventArgs e)
 {
  if(document==null)return;var dialog=new SaveFileDialog{Filter="Bản nháp JSON|*.json",FileName="metro-vietnamese-draft.json"};if(dialog.ShowDialog(this)!=true)return;
  try{File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(new{Product="GameLogTranslationDraft",Version=1,AppId=game.AppId,document.IndexHash,document.TextHash,document.MissingCharacters,Lines=draft},Storage.Json));Status.Text="Đã lưu bản nháp. Không chứa API key; chưa cài vào game.";}catch(Exception ex){Status.Text="Không lưu được: "+ex.Message;}
 }
 private void ChooseFolder(object sender,RoutedEventArgs e)
 {
  var dialog=new OpenFolderDialog{Title="Chọn thư mục cài đặt "+game.Name,Multiselect=false};
  if(dialog.ShowDialog(this)==true)Folder.Text=dialog.FolderName;
 }
 private async void Scan(object sender,RoutedEventArgs e)
 {
  ScanButton.IsEnabled=false;ExportButton.IsEnabled=false;inventory=null;ResourceGrid.ItemsSource=null;
  try{
   var root=Path.GetFullPath(Folder.Text.Trim());Status.Text="Đang đọc danh sách tài nguyên…";
   var result=await Task.Run(()=>LocalizationInspection.Scan(root));
   inventory=result;scannedRoot=root;ResourceGrid.ItemsSource=result.Resources;Engine.Text=result.Engine;
   Status.Text=$"Đã tìm {result.Resources.Count} tệp ứng viên. Chưa trích xuất, dịch hoặc thay file. Cần xác minh định dạng và font.";
   ExportButton.IsEnabled=true;
  }catch(Exception ex){Status.Text="Không quét được: "+ex.Message;}
  finally{ScanButton.IsEnabled=true;}
 }
 private void Export(object sender,RoutedEventArgs e)
 {
  if(inventory==null)return;
  var dialog=new SaveFileDialog{Title="Lưu báo cáo kiểm tra",Filter="Báo cáo JSON|*.json",FileName="metro-resource-report.json"};
  if(dialog.ShowDialog(this)!=true)return;
  try{File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(new{game.Name,game.AppId,ScannedAt=DateTimeOffset.Now,Folder=scannedRoot,inventory.Engine,inventory.Resources},Storage.Json));Status.Text="Đã xuất báo cáo kiểm tra.";}
  catch(Exception ex){Status.Text="Không xuất được báo cáo: "+ex.Message;}
 }
 private async void InspectPackage(object sender,RoutedEventArgs e)
 {
  var dialog=new OpenFileDialog{Title="Chọn gói tài nguyên Việt hoá",Filter="Gói ZIP|*.zip",Multiselect=false};
  if(dialog.ShowDialog(this)!=true)return;
  PackageFiles.ItemsSource=null;
  try{Status.Text="Đang kiểm tra cấu trúc gói…";var rows=await Task.Run(()=>LocalizationInspection.InspectZip(dialog.FileName));PackageFiles.ItemsSource=rows;Status.Text=$"Đã đọc {rows.Count} tệp. Chưa xác minh tương thích hoặc quét mã độc; chưa cài đặt.";}
  catch(Exception ex){Status.Text="Không chấp nhận gói: "+ex.Message;}
 }
}




