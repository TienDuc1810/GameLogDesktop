using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
namespace GameLogDesktop;
public sealed record ModelPrice(decimal InputPerMillion,decimal OutputPerMillion,DateTimeOffset CheckedUtc,string Source);
public sealed record TranslationQuote(string Payload,string KeyFingerprint,string Model,long InputTokens,int EstimatedOutputTokens,int MaxOutputTokens,ModelPrice Price,DateTimeOffset CreatedUtc)
{
 public decimal EstimatedUsd=>(InputTokens*Price.InputPerMillion+EstimatedOutputTokens*Price.OutputPerMillion)/1_000_000m;
 // Reserve the output limit and extra input margin, not only the expected translation length.
 public decimal CeilingUsd=>(Math.Ceiling(InputTokens*1.05m)+256)*Price.InputPerMillion/1_000_000m+MaxOutputTokens*Price.OutputPerMillion/1_000_000m;
 public static string Label(decimal usd)=>usd.ToString("0.000000",CultureInfo.InvariantCulture);
}
public static class TranslationCosts
{
 public const string PriceUrl="https://developers.openai.com/api/docs/models/gpt-4.1-mini";
 public static void RequireSupportedModel(string model){if(model!="gpt-4.1-mini")throw new InvalidOperationException("Chưa có bộ đọc giá xác minh cho model này. Hiện chỉ hỗ trợ gpt-4.1-mini; chưa gửi dịch.");}
 internal static ModelPrice ParsePrice(string html,DateTimeOffset checkedUtc)
 {
  var content=Regex.Replace(html,@"<(script|style)\b[^>]*>.*?</\1>","",RegexOptions.Singleline|RegexOptions.IgnoreCase);
  content=WebUtility.HtmlDecode(Regex.Replace(content,"<[^>]+>"," "));content=Regex.Replace(content,@"\s+"," ");
  var section=Regex.Match(content,@"Text tokens\s+Per 1M tokens.*?\bInput\s+\$(?<input>[0-9]+(?:\.[0-9]+)?)\s+Cached input\s+\$[0-9.]+\s+Output\s+\$(?<output>[0-9]+(?:\.[0-9]+)?)",RegexOptions.Singleline);
  if(!section.Success)throw new InvalidDataException("Không xác minh được giá hiện tại từ trang OpenAI. App chặn dịch.");
  var input=decimal.Parse(section.Groups["input"].Value,CultureInfo.InvariantCulture);var output=decimal.Parse(section.Groups["output"].Value,CultureInfo.InvariantCulture);
  if(input<=0||output<=0)throw new InvalidDataException("Bảng giá không hợp lệ.");return new(input,output,checkedUtc,PriceUrl);
 }
}
