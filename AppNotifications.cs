using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace GameLogDesktop;
public sealed class AppNotification
{
 public string Id{get;set;}=Guid.NewGuid().ToString("N");public string Title{get;set;}="";public string Message{get;set;}="";public string Level{get;set;}="Thông tin";public DateTimeOffset CreatedUtc{get;set;}public bool Read{get;set;}
 [JsonIgnore]public string DateText=>CreatedUtc.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm");
 [JsonIgnore]public string ReadText=>Read?"Đã đọc":"Mới";
}
public sealed class ApiKeyPeriod
{
 public string Fingerprint{get;set;}="";public DateTimeOffset EnteredUtc{get;set;}public DateTimeOffset ExpiresUtc{get;set;}public decimal ChargedOrReservedUsd{get;set;}
}
public sealed class ApiReservation
{
 public string Id{get;set;}=Guid.NewGuid().ToString("N");public string KeyFingerprint{get;set;}="";public decimal ReservedUsd{get;set;}public decimal? ActualUsd{get;set;}public DateTimeOffset CreatedUtc{get;set;}
}
public sealed class NotificationState
{
 public string ActiveKey{get;set;}="";public List<ApiKeyPeriod> Keys{get;set;}=[];public List<ApiReservation> Reservations{get;set;}=[];public List<AppNotification> Messages{get;set;}=[];
}
public sealed class AppNotifications
{
 public const decimal MaxUsd=1m;
 private readonly string file;private readonly Func<DateTimeOffset> now;
 public string DirectoryPath=>Path.GetDirectoryName(file)!;
 public NotificationState State{get;}
 public event Action? Changed;
 public AppNotifications(string directory,Func<DateTimeOffset>? clock=null)
 {
  file=Path.Combine(directory,"notifications.json");now=clock??(()=>DateTimeOffset.UtcNow);
  // Corrupt budget metadata must not silently reset the spending limit.
  State=File.Exists(file)?JsonSerializer.Deserialize<NotificationState>(File.ReadAllText(file),Storage.Json)??throw new InvalidDataException("Không đọc được dữ liệu thông báo/ngân sách."):new();
  if(State.Keys==null||State.Messages==null||State.Reservations==null||State.Keys.Any(x=>x.ChargedOrReservedUsd<0||x.ExpiresUtc!=x.EnteredUtc.AddDays(30)))throw new InvalidDataException("Dữ liệu thời hạn/ngân sách không hợp lệ.");
 }
 public static string Fingerprint(string key)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim())));
 public ApiKeyPeriod? Active=>State.Keys.FirstOrDefault(x=>x.Fingerprint==State.ActiveKey);
 public ApiKeyPeriod Register(string key)
 {
  if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Nhập API key trước.");var hash=Fingerprint(key);var record=State.Keys.FirstOrDefault(x=>x.Fingerprint==hash);
  if(record==null){var entered=now();record=new(){Fingerprint=hash,EnteredUtc=entered,ExpiresUtc=entered.AddDays(30)};State.Keys.Add(record);}
  State.ActiveKey=hash;Save();CheckExpiry();return record;
 }
 public ApiKeyPeriod RequireKey(string key)
 {
  var hash=Fingerprint(key);var record=Active;if(record==null||record.Fingerprint!=hash)throw new InvalidOperationException("Bấm Ghi nhận key để bắt đầu theo dõi thời hạn.");
  if(now()<record.EnteredUtc)throw new InvalidOperationException("Đồng hồ máy đang trước mốc nhập key; kiểm tra lại giờ hệ thống.");
  if(now()>=record.ExpiresUtc){CheckExpiry();throw new InvalidOperationException("Key đã hết thời hạn 30 ngày app ghi nhận. Nhập key mới trước khi dịch.");}return record;
 }
 public string Countdown()
 {
  var key=Active;if(key==null)return "Chưa ghi nhận API key";var left=key.ExpiresUtc-now();if(left<=TimeSpan.Zero)return "Đã hết hạn theo mốc 30 ngày";
  return $"Còn {(int)left.TotalDays} ngày {left.Hours:00}:{left.Minutes:00}:{left.Seconds:00} · hết hạn {key.ExpiresUtc.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy HH:mm} (giờ VN)";
 }
 public void CheckExpiry()
 {
  var key=Active;if(key==null)return;var left=key.ExpiresUtc-now();
  if(left<=TimeSpan.Zero)Add("API key hết hạn","Đã đến mốc 30 ngày tính từ lần ghi nhận đầu tiên. App chặn dịch với key này; hãy nhập key mới.","Cảnh báo","expired/"+key.Fingerprint);
  else if(left<=TimeSpan.FromDays(3))Add("API key sắp hết hạn",$"Key còn dưới hoặc bằng 3 ngày; mốc hết hạn: {key.ExpiresUtc.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy HH:mm} (giờ VN). Chuẩn bị key mới.","Cảnh báo","expiring/"+key.Fingerprint);
 }
 public void Add(string title,string message,string level="Thông tin",string? id=null)
 {
  if(id!=null&&State.Messages.Any(x=>x.Id==id))return;State.Messages.Add(new(){Id=id??Guid.NewGuid().ToString("N"),Title=title,Message=message,Level=level,CreatedUtc=now()});Save();
 }
 public void MarkRead(){foreach(var item in State.Messages)item.Read=true;Save();}
 public void EnsureBudget(decimal ceiling)
 {
  if(ceiling<0||ceiling>MaxUsd)throw new InvalidOperationException($"Chi phí trần ${ceiling:F6} vượt giới hạn 1 USD; chưa gửi yêu cầu dịch.");
 }
 public ApiReservation Reserve(string key,decimal ceiling)
 {
  var record=RequireKey(key);EnsureBudget(ceiling);var reservation=new ApiReservation{KeyFingerprint=record.Fingerprint,ReservedUsd=ceiling,CreatedUtc=now()};record.ChargedOrReservedUsd+=ceiling;State.Reservations.Add(reservation);Save();return reservation;
 }
 public void Settle(ApiReservation reservation,decimal actual)
 {
  var stored=State.Reservations.Single(x=>x.Id==reservation.Id);if(stored.ActualUsd.HasValue)return;if(actual<0)throw new InvalidDataException("Chi phí thực tế không hợp lệ.");
  var record=State.Keys.Single(x=>x.Fingerprint==stored.KeyFingerprint);record.ChargedOrReservedUsd+=actual-stored.ReservedUsd;stored.ActualUsd=actual;Save();
 }
 private void Save(){Directory.CreateDirectory(Path.GetDirectoryName(file)!);var temp=file+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(State,Storage.Json));File.Move(temp,file,true);Changed?.Invoke();}
}
