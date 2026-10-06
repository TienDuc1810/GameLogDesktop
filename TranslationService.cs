using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace GameLogDesktop;

public sealed class TranslationService
{
 public const string RedIndex="https://theredteam.vn/viet-hoa";
 public const string PenguinIndex="https://canhcutteam.com/games/";
 private readonly HttpClient client=new(){Timeout=TimeSpan.FromSeconds(30)};
 private readonly string cachePath;
 public TranslationCatalog Catalog {get;private set;}=new();
 public TranslationService(string folder)
 {
  cachePath=Path.Combine(folder,"translations.json");
  var seed=Path.Combine(AppContext.BaseDirectory,"translation-catalog.json");
  foreach(var file in new[]{seed,cachePath})if(File.Exists(file))try{
   var incoming=JsonSerializer.Deserialize<TranslationCatalog>(File.ReadAllText(file),Storage.Json);
   if(incoming==null)continue;
   if(string.CompareOrdinal(incoming.RedChecked,Catalog.RedChecked)>=0){Catalog.Entries.RemoveAll(x=>x.Provider=="red");Catalog.Entries.AddRange(incoming.Entries.Where(x=>x.Provider=="red"));Catalog.RedChecked=incoming.RedChecked;Catalog.RedComplete=incoming.RedComplete;}
   if(string.CompareOrdinal(incoming.PenguinChecked,Catalog.PenguinChecked)>=0){Catalog.Entries.RemoveAll(x=>x.Provider=="penguin");Catalog.Entries.AddRange(incoming.Entries.Where(x=>x.Provider=="penguin"));Catalog.PenguinChecked=incoming.PenguinChecked;Catalog.PenguinComplete=incoming.PenguinComplete;}
  }catch(JsonException){/* A damaged translation cache does not prevent library startup. */}
 }
 public void Save(){var temp=cachePath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(Catalog,Storage.Json));File.Move(temp,cachePath,true);}
 public static string Text(string html)=>WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html,"<(script|style)\\b[^>]*>.*?</\\1>","",RegexOptions.Singleline|RegexOptions.IgnoreCase),"<[^>]+>"," ")).Trim();
 public static string Key(string name)
 {
  name=WebUtility.HtmlDecode(name).Replace("™","").Replace("®","").ToLowerInvariant().Normalize(NormalizationForm.FormD);
  return new string(name.Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark&&char.IsLetterOrDigit(c)).ToArray());
 }
 // Explicit edition mappings, never prefix matching (which confuses sequels and DLC).
 private static readonly Dictionary<string,string[]> aliases=new()
 {
  ["sekiroshadowdietwice"]=["Sekiro: Shadows Die Twice"],
  ["sekiroshadowsdietwice"]=["Sekiro"],
  ["thewitcher3wildhuntremastered"]=["The Witcher 3: Wild Hunt"],
  ["residentevil2"]=["Resident Evil 2 Remake"],
  ["kingdomcomedeliverance"]=["Kingdom Come Deliverance"],
  ["baldursgate3"]=["Baldur's Gate 3"],
  ["hadesii"]=["Hades 2"],
  ["oriandtheblindforestdefinitiveedition"]=["Ori and the Blind Forest"],
  ["metro2033redux"]=["Metro 2033 Redux"],
  ["thebindingofisaacrebirth"]=["The Binding of Isaac: Rebirth"]
 };
 public TranslationEntry? Match(Game game,string provider)
 {
  var list=Catalog.Entries.Where(x=>x.Provider==provider&&x.Pc).ToList();
  if(game.AppId!=""){var id=list.FirstOrDefault(x=>x.AppId==game.AppId);if(id!=null)return id;}
  var key=Key(game.Name);var direct=list.FirstOrDefault(x=>Key(x.Name)==key);if(direct!=null)return direct;
  if(aliases.TryGetValue(key,out var names))return list.FirstOrDefault(x=>names.Any(n=>Key(n)==Key(x.Name)));
  return null;
 }
 public static string Status(TranslationEntry? entry,bool complete)=>entry==null?(complete?"Không có bản dịch":"Chưa kiểm tra"):entry.Access switch{
  "free"=>"Có bản dịch · Free",
  "paid"=>"Có bản dịch · Trả phí",
  "pending"=>"Chưa phát hành bản dịch",
  _=>"Có mục trên nguồn · Chưa rõ phí"
 };
 public List<TranslationRow> Rows(IEnumerable<Game> games,string search="")
 {
  return games.Where(g=>g.Kind!="Phần mềm"&&g.Name.Contains(search,StringComparison.OrdinalIgnoreCase)).OrderByDescending(g=>g.Minutes).ThenBy(g=>g.Name).Select((g,i)=>{
   var red=Match(g,"red");var penguin=Match(g,"penguin");return new TranslationRow{Number=i+1,Game=g,Red=red,Penguin=penguin,RedStatus=Status(red,Catalog.RedComplete),PenguinStatus=Status(penguin,Catalog.PenguinComplete)};
  }).ToList();
 }
 private async Task<string> Fetch(string url,CancellationToken ct)
 {
  if(!IsOfficial(url))throw new InvalidDataException("Liên kết không thuộc nguồn Việt hoá đã chọn.");
  using var response=await client.GetAsync(url,ct);response.EnsureSuccessStatusCode();
  if(!IsOfficial(response.RequestMessage!.RequestUri!.AbsoluteUri))throw new InvalidDataException("Nguồn chuyển hướng sang địa chỉ khác.");
  return await response.Content.ReadAsStringAsync(ct);
 }
 public static bool IsOfficial(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var u)&&u.Scheme=="https"&&u.Host is "theredteam.vn" or "www.theredteam.vn" or "canhcutteam.com" or "www.canhcutteam.com";
 public static List<TranslationEntry> ParseRed(string html)
 {
  var block=Regex.Match(html,@"const\s+games\s*=\s*\[(?<items>.*?)\];",RegexOptions.Singleline).Groups["items"].Value;
  var entries=new List<TranslationEntry>();
  foreach(Match m in Regex.Matches(block,"\\{\\s*name:\\s*\"(?<name>[^\"]+)\"\\s*,\\s*link:\\s*\"(?<url>[^\"]+)\"(?<rest>[^}]+)\\}")){
   var platforms=Regex.Match(m.Groups["rest"].Value,@"platform:\s*\[(.*?)\]",RegexOptions.IgnoreCase).Groups[1].Value;
   var url=new Uri(new Uri("https://theredteam.vn/"),m.Groups["url"].Value).AbsoluteUri;
   if(IsOfficial(url))entries.Add(new(){Provider="red",Name=WebUtility.HtmlDecode(m.Groups["name"].Value),Url=url,Pc=platforms.Contains("pc",StringComparison.OrdinalIgnoreCase),Access="free",Checked=DateTime.Now.ToString("s"),Note="Kho Việt hoá chính thức; The Red Team công bố các bản trên website miễn phí. Kiểm tra trang gốc về phiên bản hỗ trợ."});
  }
  if(entries.Count<10)throw new InvalidDataException("Không đọc được danh mục The Red Team; giữ dữ liệu trước đó.");
  return entries.DistinctBy(x=>x.Url).ToList();
 }
 public static List<TranslationEntry> ParsePenguin(string html)
 {
  var entries=new List<TranslationEntry>();
  foreach(Match m in Regex.Matches(html,"<h2\\b[^>]*class=\"[^\"]*nk-product-title[^\"]*\"[^>]*>\\s*<a\\b[^>]*href=\"(?<url>[^\"]+)\"[^>]*>(?<name>.*?)</a>",RegexOptions.Singleline|RegexOptions.IgnoreCase))
   if(IsOfficial(m.Groups["url"].Value))entries.Add(new(){Provider="penguin",Name=Text(m.Groups["name"].Value),Url=m.Groups["url"].Value});
  if(entries.Count==0)throw new InvalidDataException("Không đọc được danh mục Cánh Cụt Team; giữ dữ liệu trước đó.");
  return entries.DistinctBy(x=>x.Url).ToList();
 }
 public static void InspectPenguin(TranslationEntry entry,string html)
 {
  var status=Regex.Match(html,@"<tr\b[^>]*attribute_pa_trang-thai[^>]*>(?<row>.*?)</tr>",RegexOptions.Singleline|RegexOptions.IgnoreCase).Groups["row"].Value;
  entry.Access=status.Contains("trai-nghiem-som")?"paid":status.Contains("mien-phi")?"free":"unknown";
  var platform=Regex.Match(html,@"<tr\b[^>]*attribute_pa_platform[^>]*>(?<row>.*?)</tr>",RegexOptions.Singleline|RegexOptions.IgnoreCase).Groups["row"].Value;
  if(platform!="")entry.Pc=Regex.IsMatch(platform,@"/platform/pc/",RegexOptions.IgnoreCase);
  entry.AppId=Regex.Match(html,@"store\.steampowered\.com/app/(\d+)").Groups[1].Value;
  entry.Checked=DateTime.Now.ToString("s");entry.Note=entry.Access=="paid"?"Bản trải nghiệm sớm cần ủng hộ/donate. Xem mức phí và điều kiện mới nhất tại trang nguồn.":entry.Access=="free"?"Trang sản phẩm ghi trạng thái Miễn Phí. Sử dụng qua launcher của Cánh Cụt Team.":"Trang nguồn chưa ghi rõ trạng thái; không suy ra miễn phí từ giá sản phẩm 0 ₫.";
 }
 private void Replace(string provider,List<TranslationEntry> entries)
 {
  foreach(var e in entries){var old=Catalog.Entries.FirstOrDefault(x=>x.Provider==provider&&x.Url==e.Url);if(old!=null){e.Access=old.Access;e.Checked=old.Checked;e.Note=old.Note;e.AppId=old.AppId;e.Pc=old.Pc;}}
  Catalog.Entries.RemoveAll(x=>x.Provider==provider);Catalog.Entries.AddRange(entries);
 }
 public async Task<string> Update(IEnumerable<Game> library,Action<string> progress,CancellationToken ct)
 {
  var issues=new List<string>();var redOk=false;var penguinOk=false;
  try{progress("Đang đọc kho Việt hoá The Red Team…");var entries=ParseRed(await Fetch(RedIndex,ct));if(entries.Count<Catalog.Entries.Count(x=>x.Provider=="red")*0.8)throw new InvalidDataException("Danh mục giảm bất thường; giữ kết quả cũ để tránh kết luận thiếu bản dịch.");Replace("red",entries);Catalog.RedComplete=true;Catalog.RedChecked=DateTime.Now.ToString("s");Save();redOk=true;}
  catch(OperationCanceledException){throw;}catch(Exception ex){issues.Add("The Red Team: "+ex.Message);}
  try{
   progress("Đang đọc danh mục Cánh Cụt Team…");var html=await Fetch(PenguinIndex,ct);var entries=ParsePenguin(html);
   var countMatch=Regex.Match(Text(html),@"Hiển thị\s+\d+\s*[–-]\s*\d+\s+của\s+(\d+)\s+kết quả",RegexOptions.IgnoreCase);
   if(!countMatch.Success)throw new InvalidDataException("Không xác định được số trang danh mục.");
   var count=int.Parse(countMatch.Groups[1].Value);var pages=(count+19)/20;if(pages>50)throw new InvalidDataException("Danh mục thay đổi cấu trúc.");
   for(var p=2;p<=pages;p++){progress($"Cánh Cụt Team: trang {p}/{pages}…");await Task.Delay(200,ct);entries.AddRange(ParsePenguin(await Fetch(PenguinIndex+$"page/{p}/",ct)));}
   entries=entries.DistinctBy(x=>x.Url).ToList();if(entries.Count!=count)throw new InvalidDataException("Danh mục tải chưa đầy đủ; không kết luận game thiếu bản dịch.");
   // The general catalog also contains a Switch-only translation. Verify PC membership.
   const string pcIndex="https://canhcutteam.com/platform/pc/";
   progress("Đang đối chiếu danh mục PC của Cánh Cụt Team…");var pcHtml=await Fetch(pcIndex,ct);var pcEntries=ParsePenguin(pcHtml);
   var pcCountMatch=Regex.Match(Text(pcHtml),@"Hiển thị\s+\d+\s*[–-]\s*\d+\s+của\s+(\d+)\s+kết quả",RegexOptions.IgnoreCase);
   if(!pcCountMatch.Success)throw new InvalidDataException("Không xác định được danh mục PC.");
   var pcCount=int.Parse(pcCountMatch.Groups[1].Value);var pcPages=(pcCount+19)/20;if(pcPages>50)throw new InvalidDataException("Danh mục PC thay đổi cấu trúc.");
   for(var p=2;p<=pcPages;p++){progress($"Danh mục PC: trang {p}/{pcPages}…");await Task.Delay(200,ct);pcEntries.AddRange(ParsePenguin(await Fetch(pcIndex+$"page/{p}/",ct)));}
   var pcUrls=pcEntries.Select(x=>x.Url).ToHashSet();if(pcUrls.Count!=pcCount)throw new InvalidDataException("Danh mục PC tải chưa đầy đủ.");
   Replace("penguin",entries);foreach(var entry in entries)entry.Pc=pcUrls.Contains(entry.Url);Catalog.PenguinComplete=true;Catalog.PenguinChecked=DateTime.Now.ToString("s");Save();penguinOk=true;
  }catch(OperationCanceledException){throw;}catch(Exception ex){issues.Add("Cánh Cụt Team: "+ex.Message);}
  var selected=library.Where(g=>g.Kind!="Phần mềm").ToList();
  foreach(var provider in new[]{"red","penguin"}){
   if(provider=="red"&&!redOk||provider=="penguin"&&!penguinOk)continue;
   var matches=selected.Select(g=>Match(g,provider)).Where(x=>x!=null).Cast<TranslationEntry>().DistinctBy(x=>x.Url).ToList();
   for(var i=0;i<matches.Count;i++){var entry=matches[i];ct.ThrowIfCancellationRequested();try{
    progress($"Kiểm tra {(provider=="red"?"The Red Team":"Cánh Cụt Team")} {i+1}/{matches.Count}: {entry.Name}");
    var html=await Fetch(entry.Url,ct);
    if(provider=="penguin")InspectPenguin(entry,html);
    else{entry.AppId=Regex.Match(html,@"store\.steampowered\.com/app/(\d+)").Groups[1].Value;entry.Checked=DateTime.Now.ToString("s");entry.Note="Có trong kho chính thức The Red Team. Bản miễn phí; xem trang gốc để kiểm tra phiên bản game và hướng dẫn cài.";}
    Save();await Task.Delay(200,ct);
   }catch(OperationCanceledException){throw;}catch(Exception ex){issues.Add(entry.Name+": "+ex.Message);}}
  }
  return $"Đã đối chiếu {selected.Count} game với hai nguồn. "+(issues.Count==0?"Dữ liệu đã lưu.":$"{issues.Count} mục chưa cập nhật; giữ kết quả cũ. "+issues[0]);
 }
}
