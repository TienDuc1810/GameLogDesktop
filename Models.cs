using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
namespace GameLogDesktop;
public abstract class Observable : INotifyPropertyChanged
{
 public event PropertyChangedEventHandler? PropertyChanged;
 protected void Set<T>(ref T field,T value,[CallerMemberName]string? name=null){field=value;PropertyChanged?.Invoke(this,new(name));}
 public void Notify(){PropertyChanged?.Invoke(this,new(null));}
}
public sealed class Game : Observable
{
 public string Id {get;set;}=Guid.NewGuid().ToString("N");
 private string name="Game mới"; public string Name {get=>name;set=>Set(ref name,value);}
 public string Platform {get;set;}="Steam"; public string Kind {get;set;}="Game"; public string AppId {get;set;}="";
 public string Exe {get;set;}=""; public string Status {get;set;}="Muốn chơi"; public int? Rating {get;set;}
 public bool RatingSet {get;set;} public double Minutes {get;set;} public string LastPlayed {get;set;}="";
 public string Added {get;set;}=DateTime.Now.ToString("yyyy-MM-dd"); public string Notes {get;set;}="";
 public List<Achievement> Achievements {get;set;}=[]; public List<Session> Sessions {get;set;}=[];
 [JsonIgnore] public int Number {get;set;}
 [JsonIgnore] public string Hours => $"{(int)(Minutes/60)}h {(int)(Minutes%60):00}m";
 [JsonIgnore] public string RatingText=>RatingSet && Rating.HasValue ? $"{Rating} / 10" : "Chưa đánh giá";
 [JsonIgnore] public int RatingIndex {get=>RatingSet&&Rating.HasValue?Rating.Value+1:0;set{RatingSet=value>0;Rating=value>0?value-1:null;Notify();}}
 [JsonIgnore] public string AchievementText=>Achievements.Count>0?$"{Achievements.Count(x=>x.Done)}/{Achievements.Count}":"—";
}
public sealed class Achievement { public string Name {get;set;}=""; public bool Done {get;set;} public string Date {get;set;}=""; public string Source {get;set;}=""; public string Desc {get;set;}=""; }
public sealed class Session { public string Start {get;set;}=""; public double Minutes {get;set;} public string Source {get;set;}="Nhập tay"; public string Note {get;set;}=""; }
public sealed class Settings { public string SteamApiKey {get;set;}=""; public string SteamId {get;set;}=""; public bool Tray {get;set;}=true; public bool AutoRefresh {get;set;} public string UpdateFeedUrl {get;set;}=""; public string LocalUpdateFolder{get;set;}=""; }
public sealed class GameFile { public int Version {get;set;}=3; public List<Game> Games {get;set;}=[]; }
public sealed class WatchGame
{
 public string AppId {get;set;}=""; public string Name {get;set;}=""; public double? Price {get;set;} public double? Original {get;set;} public int Discount {get;set;}
 public double? Low {get;set;} public double? SteamDbLow {get;set;} public double? Target {get;set;} public string Release {get;set;}="";
 public bool ComingSoon {get;set;} public bool Wishlist {get;set;} public string Checked {get;set;}=""; public string NotifiedRelease {get;set;}="";
 [JsonIgnore] public int Number {get;set;} [JsonIgnore] public string PriceText=>Money(Price); [JsonIgnore] public string OriginalText=>Money(Original);
 [JsonIgnore] public string LowText=>Money(Low); [JsonIgnore] public string DiscountText=>$"−{Discount}%";
 [JsonIgnore] public string Verdict=>Price is null?"Chưa có giá":SteamDbLow.HasValue?(Price<=SteamDbLow?"≤ mốc SteamDB nhập tay":"Cao hơn mốc SteamDB"):(Price<=Low?"Thấp nhất app ghi nhận":"Chưa xác minh lịch sử");
 public static string Money(double? value)=>value is null?"Chưa có giá":value==0?"Miễn phí":value.Value.ToString("N0",System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))+" ₫";
}
public sealed class NewsItem {public string AppId {get;set;}="";public string Gid {get;set;}="";public string Title {get;set;}="";public string Date {get;set;}="";public string Url {get;set;}="";public string Content {get;set;}="";public bool Patch {get;set;} [JsonIgnore]public int Number{get;set;} [JsonIgnore]public string GameName{get;set;}="";}
public sealed class Discovery {public List<WatchGame> Items{get;set;}=[];public List<NewsItem> News{get;set;}=[];public string LastSync{get;set;}="";public bool AutoRefresh{get;set;}}

