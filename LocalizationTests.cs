using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace GameLogDesktop;
public partial class MainWindow
{
 private sealed class TranslationFixtureHandler:HttpMessageHandler
 {
  public string Body="";
  public string CountBody="";
  public int TranslationRequests;public int CountRequests;public long TokenCount=100;
  public bool BadPrice;public bool FailCount;
  public bool ServerFailure;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
  {
   if(request.RequestUri?.AbsoluteUri==TranslationCosts.PriceUrl||request.RequestUri?.AbsoluteUri==TranslationCosts.ModelPriceUrl(TranslationCosts.DefaultModel)){if(request.Headers.Authorization!=null)throw new InvalidOperationException("Key leaked to public documentation");return new(HttpStatusCode.OK){Content=new StringContent(BadPrice?"pricing unavailable":"Text tokens Per 1M tokens Batch API price Input $0.40 Cached input $0.10 Output $1.60")};}
   if(request.Headers.Authorization?.Scheme!="Bearer")throw new InvalidOperationException("Unexpected API request");
   if(request.RequestUri?.AbsoluteUri=="https://api.openai.com/v1/responses/input_tokens"){CountRequests++;CountBody=await request.Content!.ReadAsStringAsync(cancellationToken);return new(FailCount?HttpStatusCode.Unauthorized:HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{input_tokens=TokenCount}))};}
   if(request.RequestUri?.AbsoluteUri!="https://api.openai.com/v1/responses")throw new InvalidOperationException("Unexpected API request");TranslationRequests++;
   Body=await request.Content!.ReadAsStringAsync(cancellationToken);
   if(ServerFailure)return new((HttpStatusCode)520){Content=new StringContent("Unavailable")};
   var value=JsonSerializer.Serialize(new{lines=new[]{new{id="menu_start",text="Chơi %s"}}});
   return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{status="completed",usage=new{input_tokens=100,output_tokens=20},output=new[]{new{content=new[]{new{type="output_text",text=value}}}}}))};
  }
 }
 private async Task SelfTestLocalization(string directory,Action<bool,string> check)
 {
  var vault=Path.Combine(directory,"shared-key-test");SharedApiKey.Save(vault,"fixture-secret-only");check(SharedApiKey.Load(vault)=="fixture-secret-only"&&!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(vault,"openai-key.dpapi"))).Contains("fixture-secret-only"),"shared key persists through Windows DPAPI without plaintext secret");
  var price=new ModelPrice(0.4m,1.6m,DateTimeOffset.UtcNow,TranslationCosts.PriceUrl);var plan=TranslationJobPlanner.Plan(Enumerable.Range(0,65).Select(i=>new GameText("row"+i,"Play "+i)),price);check(plan.Count==65&&plan.Batches.Count==1&&plan.Batches.All(x=>x.Count<=100),"whole-game plan covers every untranslated row in bounded batches");
  var expensive=TranslationJobPlanner.Plan(Enumerable.Range(0,100000).Select(i=>new GameText("row"+i,"Play "+i)),price);check(expensive.CeilingUsd>AppNotifications.MaxUsd,"whole-game cost aggregates all batches before applying one-dollar limit");
  var longPlan=TranslationJobPlanner.Plan([new("a",new string('a',7000)),new("b",new string('b',7000))],price);check(longPlan.Batches.Count==2&&TranslationJobPlanner.Plan([new("done","Play","Chơi")],price).Count==0,"planner respects text size and skips completed translations");
  var duplicates=TranslationJobPlanner.Plan([new("one","Play"),new("two","Play")],price);check(duplicates.Count==1,"duplicate source strings are translated once");
  var accepted=OpenAiTranslation.AcceptValidLines([new("bad","Buy <menu_buy>"),new("ok","Play")],[new("bad","","Mua"),new("ok","","Chơi")]);check(accepted[0].Vietnamese=="Buy <menu_buy>"&&accepted[1].Vietnamese=="Chơi","invalid line stays English without losing valid neighboring translation");
  var merged=TranslationJobPlanner.Merge([new("one","Play"),new("two","Play")],[new("one","Play","Chơi")]);check(merged.All(x=>x.Vietnamese=="Chơi")&&merged[1].Id=="two","duplicate translations retain each original game identifier");
  var source=new[]{new GameText("menu_start","Play %s")};using var handler=new TranslationFixtureHandler();using var api=new OpenAiTranslation(handler);var notices=new AppNotifications(Path.Combine(directory,"api-notices"));notices.Register("test-placeholder");var quote=await api.Quote(source,"test-placeholder","gpt-4.1-mini",notices,CancellationToken.None);var translated=await api.Translate(source,"test-placeholder",quote,notices,CancellationToken.None);
  using var request=JsonDocument.Parse(handler.Body);check(translated.Single().Vietnamese=="Chơi %s"&&request.RootElement.GetProperty("store").GetBoolean()==false,"translation API uses strict structured output and does not request response storage");
  check(handler.CountRequests==1&&handler.TranslationRequests==1&&quote.InputTokens==100&&quote.MaxOutputTokens==10000,"translation counts complete input before paid inference and quotes bounded output");
  using var countRequest=JsonDocument.Parse(handler.CountBody);check(new[]{"model","input","instructions","text"}.All(name=>countRequest.RootElement.GetProperty(name).ToString()==request.RootElement.GetProperty(name).ToString()),"token count includes identical instructions, source text, and structured output schema");
  check(notices.State.Reservations.Single().ActualUsd==0.000072m,"actual usage settles cost from response tokens");
  handler.ServerFailure=true;var fallback=await api.Translate(source,"test-placeholder",quote,notices,CancellationToken.None);check(fallback.Single().Vietnamese==source.Single().Source&&handler.TranslationRequests==2&&notices.State.Reservations.Last().ActualUsd==null,"HTTP 520 keeps English without retry and retains unknown-cost reservation");handler.ServerFailure=false;
  var rejected=false;try{OpenAiTranslation.Validate(source,[new("menu_start","","Chơi")]);}catch(InvalidDataException){rejected=true;}check(rejected,"translation rejects lost placeholder");
  rejected=false;try{OpenAiTranslation.Validate(source,[new("changed_id","","Chơi %s")]);}catch(InvalidDataException){rejected=true;}check(rejected,"translation rejects changed string identifier");
  rejected=false;try{MetroLocalization.Parse([1,2,3],"test");}catch(InvalidDataException){rejected=true;}check(rejected,"Metro reader rejects truncated language files");
  var folder=Environment.GetEnvironmentVariable("GAMELOG_METRO_PROBE");
  if(!string.IsNullOrWhiteSpace(folder)){
   var original=UpdateService.Hash(Path.Combine(folder,"content.vfx"));var document=await Task.Run(()=>MetroLocalization.ReadEnglish(folder));
   check(document.Lines.Count>1000&&document.MissingCharacters.Contains('đ'),"actual Metro English extraction validates string table and detects missing Vietnamese glyph codes");check(original==UpdateService.Hash(Path.Combine(folder,"content.vfx")),"actual game index remains unchanged after extraction");
   var patchedRows=document.Lines.ToList();patchedRows[0]=patchedRows[0] with{Vietnamese="Chơi game"};var package=Path.Combine(directory,"metro-test-package");var patch=MetroTestPatch.Build(folder,patchedRows,package);var reread=MetroLocalization.ReadEnglish(package);check(reread.Lines.Count==document.Lines.Count&&reread.Lines[0].Source=="Choi game","Metro no-accent patch preserves all keys and decodes translated text through the actual archive format");
   var rawArchive=File.ReadAllBytes(Path.Combine(package,MetroTestPatch.ArchiveName));var encoded=MetroTestPatch.Encode(MetroLocalization.ReadEnglishBytes(folder),patchedRows);check(rawArchive.Length==encoded.Length+24&&rawArchive.AsSpan(0,encoded.Length).SequenceEqual(encoded),"Metro archive stores uncompressed resource bytes directly without linked-block headers");
   var controlPackage=Path.Combine(directory,"metro-control-package");MetroTestPatch.Build(folder,document.Lines,controlPackage,true);check(MetroLocalization.ReadEnglish(controlPackage).TextHash==document.TextHash,"Metro diagnostic control preserves the exact original language resource hash");
   var fixture=Path.Combine(directory,"metro-install-fixture");Directory.CreateDirectory(fixture);File.Copy(Path.Combine(folder,"content.vfx"),Path.Combine(fixture,"content.vfx"));MetroTestPatch.Install(fixture,package);MetroTestPatch.Restore(fixture);check(UpdateService.Hash(Path.Combine(fixture,"content.vfx"))==original&&!File.Exists(Path.Combine(fixture,MetroTestPatch.ArchiveName)),"Metro isolated installation restores the exact original index and removes only its own archive");
   File.WriteAllText(Path.Combine(directory,"metro-diagnostic.json"),JsonSerializer.Serialize(new{document.IndexHash,document.TextHash,document.MissingCharacters,Count=document.Lines.Count},Storage.Json));
  }
  var dialog=new LocalizationWindow(new Game{Name="Metro 2033 Redux",AppId="286690"}){Owner=this};dialog.Show();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
  check(dialog.ActualWidth>=820&&dialog.FindName("ApiKey") is System.Windows.Controls.PasswordBox,"localization workflow opens with masked session API key entry");
  var bitmap=new RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(dialog);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(directory,"localization-dialog.png")))png.Save(file);dialog.Close();
 }
}



