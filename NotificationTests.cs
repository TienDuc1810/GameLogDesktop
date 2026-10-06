using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
namespace GameLogDesktop;
public partial class MainWindow
{
 private async Task SelfTestNotifications(string directory,Action<bool,string> check)
 {
  var clock=new DateTimeOffset(2026,10,6,5,0,0,TimeSpan.Zero);var entered=clock;var folder=Path.Combine(directory,"expiry-tests");var alerts=new AppNotifications(folder,()=>clock);var record=alerts.Register("dummy-expiry-key");
  check(record.EnteredUtc==entered&&record.ExpiresUtc==entered.AddDays(30),"API key period starts at enrollment and ends exactly 30 days later");
  clock=entered.AddDays(27).AddSeconds(-1);alerts.CheckExpiry();check(alerts.State.Messages.Count==0,"expiry warning does not appear before final 3 days");
  clock=entered.AddDays(27);alerts.CheckExpiry();alerts.CheckExpiry();check(alerts.State.Messages.Count==1&&alerts.State.Messages.Single().Title.Contains("sắp"),"final 3 day notification appears once at the boundary");
  clock=entered.AddDays(28);alerts=new AppNotifications(folder,()=>clock);var same=alerts.Register("dummy-expiry-key");check(same.EnteredUtc==entered&&alerts.Countdown().Contains("2 ngày"),"reopening app and reentering same key preserves countdown");
  clock=entered.AddDays(30);var refused=false;try{alerts.RequireKey("dummy-expiry-key");}catch(InvalidOperationException){refused=true;}check(refused&&alerts.State.Messages.Count==2,"expired key is blocked and expiry notification is persistent");
  alerts.MarkRead();check(new AppNotifications(folder,()=>clock).State.Messages.All(x=>x.Read),"notification read state survives restart");
  clock=entered.AddDays(31);var fresh=alerts.Register("dummy-new-key");check(fresh.EnteredUtc==clock&&fresh.ExpiresUtc==clock.AddDays(30),"different key gets its own 30 day period");
  check(!File.ReadAllText(Path.Combine(folder,"notifications.json")).Contains("dummy-"),"notification storage does not contain plaintext API keys");
  alerts.EnsureBudget(1m);refused=false;try{alerts.EnsureBudget(1.000001m);}catch(InvalidOperationException){refused=true;}check(refused,"per-click USD budget permits exactly one dollar and blocks above one dollar without rounding");
  fresh.ChargedOrReservedUsd=9m;alerts.EnsureBudget(0.02m);check(fresh.ChargedOrReservedUsd==9m,"one dollar limit applies per click rather than cumulative key spending");
  var source=new[]{new GameText("menu_start","Play %s")};using var handler=new TranslationFixtureHandler();using var api=new OpenAiTranslation(handler);var apiNotices=new AppNotifications(Path.Combine(directory,"quote-guard-tests"));apiNotices.Register("dummy-quote-key");var quote=await api.Quote(source,"dummy-quote-key","gpt-4.1-mini",apiNotices,CancellationToken.None);
  handler.TokenCount=3_000_000;refused=false;try{await api.Quote(source,"dummy-quote-key","gpt-4.1-mini",apiNotices,CancellationToken.None);}catch(InvalidOperationException){refused=true;}check(refused&&handler.TranslationRequests==0,"over-budget preflight never sends a translation request");
  handler.TokenCount=100;refused=false;try{await api.Translate(source,"dummy-quote-key",quote with{CreatedUtc=DateTimeOffset.UtcNow.AddMinutes(-6)},apiNotices,CancellationToken.None);}catch(InvalidOperationException){refused=true;}check(refused&&handler.TranslationRequests==0,"expired quote blocks inference");
  refused=false;try{await api.Translate(source,"different-key",quote,apiNotices,CancellationToken.None);}catch(InvalidOperationException){refused=true;}check(refused&&handler.TranslationRequests==0,"changed API key invalidates cost quote before inference");
  refused=false;try{await api.Translate([new("menu_start","Changed %s")],"dummy-quote-key",quote,apiNotices,CancellationToken.None);}catch(InvalidOperationException){refused=true;}check(refused&&handler.TranslationRequests==0,"changed source content invalidates cost quote before inference");
  refused=false;try{await api.Quote(source,"dummy-quote-key","unknown-model",apiNotices,CancellationToken.None);}catch(InvalidOperationException){refused=true;}check(refused&&handler.TranslationRequests==0,"unpriced model is blocked before inference");
  handler.BadPrice=true;refused=false;try{await api.Quote(source,"dummy-quote-key","gpt-4.1-mini",apiNotices,CancellationToken.None);}catch(InvalidDataException){refused=true;}check(refused&&handler.TranslationRequests==0,"unrecognized official pricing response blocks inference");
  handler.BadPrice=false;handler.FailCount=true;refused=false;try{await api.Quote(source,"dummy-quote-key","gpt-4.1-mini",apiNotices,CancellationToken.None);}catch(InvalidOperationException){refused=true;}check(refused&&handler.TranslationRequests==0,"failed token count blocks inference");
  notifications!.Add("Thông báo kiểm tra","API key còn 3 ngày. Lô dịch được kiểm tra token và chi phí trước khi gửi.","Cảnh báo");NotificationsTab.IsSelected=true;await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
  check(NotificationsButton.Content.ToString()!.Contains("1")&&NotificationList.Items.Count==1,"notification center and unread badge update immediately");Capture(Path.Combine(directory,"notification-center.png"));
  if(Environment.GetEnvironmentVariable("GAMELOG_PRICE_PROBE")=="1"){
   using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(30)};var price=TranslationCosts.ParsePrice(await client.GetStringAsync(TranslationCosts.PriceUrl),DateTimeOffset.UtcNow);check(price.InputPerMillion>0&&price.OutputPerMillion>0,"live official model page is parsed and provides current rates");File.WriteAllText(Path.Combine(directory,"live-price.txt"),$"{price.InputPerMillion} / {price.OutputPerMillion} per million tokens; {price.Source}");
  }
 }
}
