using System.Windows;
using System.Windows.Controls;
namespace GameLogDesktop;
public static class SharedApiKeyDialog
{
 public static void Show(Window owner,AppNotifications notices)
 {
  var window=new Window{Owner=owner,Title="API key dịch dùng chung",Width=540,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};
  var panel=new StackPanel{Margin=new Thickness(24)};panel.Children.Add(new TextBlock{Text="Nhập một lần, dùng chung cho các game. Key được Windows mã hóa trên máy này.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16)});var key=new PasswordBox{Padding=new Thickness(12)};panel.Children.Add(key);var info=new TextBlock{Text=notices.Countdown(),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,16,0,16)};panel.Children.Add(info);
  var save=new Button{Content="Lưu API key"};save.Click+=(_,_)=>{try{if(string.IsNullOrWhiteSpace(key.Password))throw new ArgumentException("Nhập key mới để lưu; key hiện có được giữ nguyên khi đóng.");SharedApiKey.Save(notices.DirectoryPath,key.Password);notices.Register(key.Password);key.Clear();window.Close();}catch(Exception ex){info.Text=ex.Message;}};panel.Children.Add(save);var close=new Button{Content="Đóng"};close.Click+=(_,_)=>window.Close();panel.Children.Add(close);window.Content=panel;window.ShowDialog();key.Clear();
 }
}
