using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Threading;
namespace GameLogDesktop;
public partial class LocalizationWindow:Window
{
 private readonly Game game;
 private LocalizationInventory? inventory;
 private string scannedRoot="";
 private MetroTextDocument? document;
 private List<GameText> draft=[];
 private CancellationTokenSource? translationCancellation;
 private readonly AppNotifications notices;
 private readonly DispatcherTimer keyTimer=new(){Interval=TimeSpan.FromSeconds(1)};
 private TranslationQuote? quote;
 public LocalizationWindow(Game selected,bool package=false,AppNotifications? notifications=null)
 {
  notices=notifications??new AppNotifications(App.SelfTestDirectory??Path.Combine(AppContext.BaseDirectory,"desktop-data"));
  InitializeComponent();game=selected;GameTitle.Text=game.Name;Tabs.SelectedIndex=package?2:1;
  Closed+=(_,_)=>{keyTimer.Stop();translationCancellation?.Cancel();ApiKey.Clear();};keyTimer.Tick+=(_,_)=>UpdateKeyCountdown();keyTimer.Start();UpdateKeyCountdown();
  try{Folder.Text=SteamService.InstallFolder(game.AppId)??"";}catch{Status.Text="Chọn thư mục game bằng nút Chọn thư mục game.";}
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

