using System.Windows;
using System.Windows.Controls;
namespace GameLogDesktop;
public partial class MainWindow
{
 private void PrepareLocalization(object sender,RoutedEventArgs e)=>OpenLocalization(false);
 private void InspectLocalization(object sender,RoutedEventArgs e)=>OpenLocalization(true);
 private void OpenLocalization(bool package)
 {
  if(TranslationGrid.SelectedItem is not TranslationRow row){Say("Chọn game trong bảng Việt hoá trước.");return;}
  if(notifications==null){Say("Chưa đọc được dữ liệu thời hạn/ngân sách, không mở dịch. Xem mục Thông báo.");return;}
  new LocalizationWindow(row.Game,package,notifications){Owner=this}.ShowDialog();
 }
 private TranslationService? translations;
 private TranslationService Translations=>translations??=new(store.DirectoryPath);
 private void RefreshTranslations()
 {
  if(!ready||TranslationGrid==null)return;
  var selected=(TranslationGrid.SelectedItem as TranslationRow)?.Game.Id;
  var rows=Translations.Rows(store.Games,TranslationSearch.Text??"");TranslationGrid.ItemsSource=rows;
  TranslationGrid.SelectedItem=rows.FirstOrDefault(x=>x.Game.Id==selected)??rows.FirstOrDefault();
  TranslationSummary.Text=$"{rows.Count} game · The Red Team: {DateLabel(Translations.Catalog.RedChecked)} · Cánh Cụt Team: {DateLabel(Translations.Catalog.PenguinChecked)}";
 }
 private static string DateLabel(string date)=>DateTime.TryParse(date,out var d)?d.ToString("dd/MM/yyyy HH:mm"):"chưa cập nhật";
 private void TranslationFilterChanged(object sender,TextChangedEventArgs e){if(ready)RefreshTranslations();}
 private async void UpdateTranslations(object sender,RoutedEventArgs e)
 {
  await Run(async ct=>{var games=store.Games.ToList();try{var message=await Translations.Update(games,Say,ct);Say(message);}finally{RefreshTranslations();}});
 }
 private void TranslationSelected(object sender,SelectionChangedEventArgs e)
 {
  if(TranslationGrid.SelectedItem is not TranslationRow row){TranslationDetails.Text="Thư viện chưa có game. Nhập dữ liệu hoặc đồng bộ Steam trước.";return;}
  string Detail(string provider,TranslationEntry? entry,string status,string checkedDate)=>$"{provider}: {status}\n"+(entry==null?$"Không tìm thấy tên khớp trong danh mục PC của nguồn, kiểm tra {DateLabel(checkedDate)}. Bạn có thể mở nguồn để tìm thủ công.":$"Tên trên nguồn: {entry.Name}\n{entry.Note}\nKiểm tra: {DateLabel(entry.Checked)}\n{entry.Url}");
  TranslationDetails.Text=row.Name+"\n\n"+Detail("The Red Team",row.Red,row.RedStatus,Translations.Catalog.RedChecked)+"\n\n"+Detail("Cánh Cụt Team",row.Penguin,row.PenguinStatus,Translations.Catalog.PenguinChecked);
 }
 private void OpenTranslationRed(object sender,RoutedEventArgs e)=>OpenTranslation("red");
 private void OpenTranslationPenguin(object sender,RoutedEventArgs e)=>OpenTranslation("penguin");
 private void OpenTranslation(string provider)
 {
  var row=TranslationGrid.SelectedItem as TranslationRow;var entry=provider=="red"?row?.Red:row?.Penguin;
  var url=entry?.Url??(provider=="red"?TranslationService.RedIndex:TranslationService.PenguinIndex);
  if(TranslationService.IsOfficial(url))Open(url);
 }
}
