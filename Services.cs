using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace GameLogDesktop;
public sealed class Storage
{
 public string DirectoryPath {get;}
 public static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true,WriteIndented=true};
 public List<Game> Games {get;set;}=[];public Settings Settings {get;set;}=new();public Discovery Discovery{get;set;}=new();
 public Storage(string? folder=null){DirectoryPath=folder??Path.Combine(AppContext.BaseDirectory,"desktop-data");Directory.CreateDirectory(DirectoryPath);Games=Read<GameFile>("games.json")?.Games??[];Settings=Read<Settings>("settings.json")??new();Discovery=Read<Discovery>("discovery.json")??new();Normalize();}
 private T? Read<T>(string file){var path=Path.Combine(DirectoryPath,file);return File.Exists(path)?JsonSerializer.Deserialize<T>(File.ReadAllText(path),Json):default;}
 private void Write(string file,object data){var path=Path.Combine(DirectoryPath,file);var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(data,Json));File.Move(temp,path,true);}
 public void Save(){Write("games.json",new GameFile{Games=Games});Write("settings.json",Settings);Write("discovery.json",Discovery);}
 public void Import(string path)
 {
  var content=JsonSerializer.Deserialize<GameFile>(File.ReadAllText(path),Json)??throw new InvalidDataException("Tệp games.json không hợp lệ.");
  if(content.Games is null)throw new InvalidDataException("Không có danh sách games.");
  using var raw=JsonDocument.Parse(File.ReadAllText(path));
  var source=raw.RootElement.EnumerateObject().FirstOrDefault(x=>x.Name.Equals("games",StringComparison.OrdinalIgnoreCase)).Value;
  for(var i=0;i<content.Games.Count&&i<source.GetArrayLength();i++){if(!source[i].EnumerateObject().Any(x=>x.Name.Equals("Kind",StringComparison.OrdinalIgnoreCase)))content.Games[i].Kind=Regex.IsMatch(content.Games[i].Name,"^(DSX|Wallpaper|Lossless Scaling|Soundpad)",RegexOptions.IgnoreCase)?"Phần mềm":"Game";}
  Backup();foreach(var game in content.Games){if(!Games.Any(x=>x.Id==game.Id||x.Platform==game.Platform&&x.AppId!=""&&x.AppId==game.AppId))Games.Add(game);}
  var folder=Path.GetDirectoryName(path)!;
  var settings=Path.Combine(folder,"settings.json");if(File.Exists(settings)&&string.IsNullOrWhiteSpace(Settings.SteamId))Settings=JsonSerializer.Deserialize<Settings>(File.ReadAllText(settings),Json)??Settings;
  var discovery=Path.Combine(folder,"discovery.json");if(File.Exists(discovery)&&Discovery.Items.Count==0)Discovery=JsonSerializer.Deserialize<Discovery>(File.ReadAllText(discovery),Json)??Discovery;
  Normalize();Save();
 }
 private void Normalize(){foreach(var g in Games){g.Achievements??=[];g.Sessions??=[];if(!g.RatingSet){if(g.Rating>0)g.RatingSet=true;else g.Rating=null;}if(string.IsNullOrWhiteSpace(g.Kind))g.Kind=Regex.IsMatch(g.Name,"^(DSX|Wallpaper|Lossless Scaling|Soundpad)",RegexOptions.IgnoreCase)?"Phần mềm":"Game";}}
 public string Backup(){var folder=Path.Combine(DirectoryPath,"backup",DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));Directory.CreateDirectory(folder);foreach(var file in Directory.GetFiles(DirectoryPath,"*.json"))File.Copy(file,Path.Combine(folder,Path.GetFileName(file)));return folder;}
}
public sealed class SteamService
{
 private readonly HttpClient client=new(){Timeout=TimeSpan.FromSeconds(30)};
 private async Task<JsonDocument> Fetch(string url,CancellationToken ct)=>JsonDocument.Parse(await client.GetStringAsync(url,ct));
 private static string S(JsonElement x,string key)=>x.TryGetProperty(key,out var v)&&v.ValueKind!=JsonValueKind.Null?v.ToString():"";
 private static int I(JsonElement x,string key)=>int.TryParse(S(x,key),out var n)?n:0;
 public async Task<List<Game>> Owned(Settings settings,CancellationToken ct)
 {
  Validate(settings,true);using var json=await Fetch($"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={Uri.EscapeDataString(settings.SteamApiKey)}&steamid={settings.SteamId}&include_appinfo=1&include_played_free_games=1",ct);
  var result=new List<Game>();if(!json.RootElement.GetProperty("response").TryGetProperty("games",out var games))throw new InvalidOperationException("Steam không trả game. Kiểm tra API key và Chi tiết trò chơi công khai.");
  foreach(var g in games.EnumerateArray())result.Add(new Game{Name=S(g,"name"),AppId=S(g,"appid"),Kind=Regex.IsMatch(S(g,"name"),"^(DSX|Wallpaper|Lossless Scaling|Soundpad)",RegexOptions.IgnoreCase)?"Phần mềm":"Game",Minutes=I(g,"playtime_forever"),LastPlayed=Unix(I(g,"rtime_last_played")),Status=I(g,"playtime_forever")>0?"Đang chơi":"Muốn chơi"});return result;
 }
 public async Task<List<string>> Wishlist(Settings settings,CancellationToken ct)
 {
  Validate(settings,false);var url=$"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?steamid={settings.SteamId}";if(settings.SteamApiKey!="")url+="&key="+Uri.EscapeDataString(settings.SteamApiKey);
  using var j=await Fetch(url,ct);if(!j.RootElement.TryGetProperty("response",out var r)||!r.TryGetProperty("items",out var a))throw new InvalidOperationException("Không đọc được wishlist; kiểm tra SteamID64 và quyền riêng tư.");return a.EnumerateArray().Select(x=>S(x,"appid")).ToList();
 }
 public async Task<WatchGame> Details(string id,CancellationToken ct)
 {
  if(!Regex.IsMatch(id,"^\\d+$"))throw new ArgumentException("AppID phải là số.");
  using var j=await Fetch($"https://store.steampowered.com/api/appdetails?appids={id}&cc=vn&l=english",ct);var entry=j.RootElement.GetProperty(id);
  if(!entry.GetProperty("success").GetBoolean())throw new InvalidOperationException("Không có thông tin tại cửa hàng Việt Nam: "+id);
  var d=entry.GetProperty("data");var w=new WatchGame{AppId=id,Name=S(d,"name"),Checked=DateTime.Now.ToString("yyyy-MM-dd HH:mm")};
  if(d.TryGetProperty("is_free",out var free)&&free.GetBoolean()){w.Price=0;w.Original=0;}
  else if(d.TryGetProperty("price_overview",out var p)&&S(p,"currency")=="VND"){w.Price=I(p,"final")/100d;w.Original=I(p,"initial")/100d;w.Discount=I(p,"discount_percent");}
  if(d.TryGetProperty("release_date",out var release)){w.Release=S(release,"date");w.ComingSoon=release.TryGetProperty("coming_soon",out var c)&&c.GetBoolean();}return w;
 }
 public async Task<List<NewsItem>> News(string id,CancellationToken ct)
 {
  using var j=await Fetch($"https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid={id}&count=8&maxlength=0&feeds=steam_community_announcements",ct);
  return j.RootElement.GetProperty("appnews").GetProperty("newsitems").EnumerateArray().Select(n=>new NewsItem{AppId=id,Gid=S(n,"gid"),Title=S(n,"title"),Date=Unix(I(n,"date")),Url=S(n,"url"),Content=S(n,"contents"),Patch=Regex.IsMatch(S(n,"title"),"patch|hotfix|update|changelog|release notes",RegexOptions.IgnoreCase)}).ToList();
 }
 public async Task<List<Achievement>> Achievements(Game game,Settings settings,CancellationToken ct)
 {
  Validate(settings,true);using var j=await Fetch($"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?appid={game.AppId}&key={Uri.EscapeDataString(settings.SteamApiKey)}&steamid={settings.SteamId}&l=english",ct);
  var p=j.RootElement.GetProperty("playerstats");if(!p.TryGetProperty("achievements",out var a))throw new InvalidOperationException("Không lấy được thành tựu: game không có thành tựu hoặc hồ sơ riêng tư.");
  return a.EnumerateArray().Select(x=>new Achievement{Name=S(x,"name")==""?S(x,"apiname"):S(x,"name"),Desc=S(x,"description"),Done=I(x,"achieved")==1,Date=Unix(I(x,"unlocktime")),Source="steam"}).ToList();
 }
 private static void Validate(Settings s,bool key){if(!Regex.IsMatch(s.SteamId,"^\\d{17}$"))throw new ArgumentException("Nhập SteamID64 gồm 17 chữ số trong Cài đặt.");if(key&&s.SteamApiKey=="")throw new ArgumentException("Chức năng này cần Steam API key.");}
 public static string Unix(long value)=>value>0?DateTimeOffset.FromUnixTimeSeconds(value).LocalDateTime.ToString("yyyy-MM-dd HH:mm"):"";
 public static List<Game> Installed()
 {
  var path=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam","SteamPath",null)?.ToString();if(path is null)throw new InvalidOperationException("Không tìm thấy Steam trên máy.");
  var libraries=new HashSet<string>(StringComparer.OrdinalIgnoreCase){path};var vdf=Path.Combine(path,"steamapps","libraryfolders.vdf");
  if(File.Exists(vdf))foreach(Match m in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))libraries.Add(m.Groups[1].Value.Replace(@"\\",@"\"));
  var result=new List<Game>();foreach(var lib in libraries){var folder=Path.Combine(lib,"steamapps");if(!Directory.Exists(folder))continue;foreach(var file in Directory.GetFiles(folder,"appmanifest_*.acf")){var text=File.ReadAllText(file);var id=Regex.Match(text,"\"appid\"\\s+\"(\\d+)\"").Groups[1].Value;var name=Regex.Match(text,"\"name\"\\s+\"([^\"]+)\"").Groups[1].Value;if(id!=""&&name!=""&&!name.Contains("Redistributable"))result.Add(new Game{Name=name,AppId=id,Kind=Regex.IsMatch(name,"^(DSX|Wallpaper|Soundpad|Lossless Scaling)",RegexOptions.IgnoreCase)?"Phần mềm":"Game"});}}return result;
 }
 public static string? InstallFolder(string appId)
 {
  if(!Regex.IsMatch(appId,"^\\d+$"))return null;
  var path=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam","SteamPath",null)?.ToString();if(path==null)return null;
  var libraries=new HashSet<string>(StringComparer.OrdinalIgnoreCase){path};var vdf=Path.Combine(path,"steamapps","libraryfolders.vdf");
  if(File.Exists(vdf))foreach(Match m in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))libraries.Add(m.Groups[1].Value.Replace(@"\\",@"\"));
  foreach(var lib in libraries){var manifest=Path.Combine(lib,"steamapps","appmanifest_"+appId+".acf");if(!File.Exists(manifest))continue;var name=Regex.Match(File.ReadAllText(manifest),"\"installdir\"\\s+\"([^\"]+)\"").Groups[1].Value;if(name==""||name is "." or ".."||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)continue;var folder=Path.Combine(lib,"steamapps","common",name);if(Directory.Exists(folder))return folder;}
  return null;
 }
}
