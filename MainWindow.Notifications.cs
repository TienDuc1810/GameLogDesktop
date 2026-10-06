using System.Windows;
namespace GameLogDesktop;
public partial class MainWindow
{
 private AppNotifications? notifications;
 private void InitializeNotifications()
 {
  try{notifications=new AppNotifications(store.DirectoryPath);notifications.Changed+=RefreshNotifications;notifications.CheckExpiry();RefreshNotifications();}
  catch(Exception ex){NotificationSummary.Text="Không đọc được dữ liệu thông báo/ngân sách: "+ex.Message;Say("Phần dịch bị chặn vì không đọc được dữ liệu thời hạn/ngân sách. Giữ nguyên file notifications.json để kiểm tra.");}
 }
 private void RefreshNotifications()
 {
  if(notifications==null)return;var unread=notifications.State.Messages.Count(x=>!x.Read);NotificationsButton.Content=$"Thông báo ({unread})";
  NotificationList.ItemsSource=notifications.State.Messages.OrderByDescending(x=>x.CreatedUtc).ToList();
  NotificationSummary.Text=$"{unread} chưa đọc · {notifications.State.Messages.Count} thông báo · {notifications.Countdown()}";
  NotificationEmpty.Visibility=notifications.State.Messages.Count==0?Visibility.Visible:Visibility.Collapsed;
 }
 private void CheckNotifications(){try{notifications?.CheckExpiry();RefreshNotifications();}catch(Exception ex){Say("Không cập nhật được thông báo: "+ex.Message);}}
 private void OpenNotifications(object sender,RoutedEventArgs e){CheckNotifications();NotificationsTab.IsSelected=true;}
 private void MarkNotificationsRead(object sender,RoutedEventArgs e){try{notifications?.MarkRead();}catch(Exception ex){Say("Không lưu được trạng thái thông báo: "+ex.Message);}}
}
