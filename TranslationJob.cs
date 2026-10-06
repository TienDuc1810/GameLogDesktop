using System.Text;
namespace GameLogDesktop;
public sealed record TranslationJob(List<List<GameText>> Batches,decimal CeilingUsd,int Count);
public static class TranslationJobPlanner
{
 public static TranslationJob Plan(IEnumerable<GameText> rows,ModelPrice price)
 {
  var batches=new List<List<GameText>>();var batch=new List<GameText>();var size=0;
  foreach(var row in rows.Where(x=>string.IsNullOrWhiteSpace(x.Vietnamese)&&!string.IsNullOrWhiteSpace(x.Source))){if(row.Source.Length>12000)throw new InvalidOperationException("Có câu vượt giới hạn dịch; chưa gửi dịch.");if(batch.Count>=20||size+row.Source.Length>12000){batches.Add(batch);batch=[];size=0;}batch.Add(row);size+=row.Source.Length;}
  if(batch.Count>0)batches.Add(batch);
  // UTF-8 byte bound plus protocol overhead avoids understating input before confirmation.
  var ceiling=batches.Sum(b=>new TranslationQuote(OpenAiTranslation.Payload(b,"gpt-4.1-mini"),"","gpt-4.1-mini",Encoding.UTF8.GetByteCount(OpenAiTranslation.Payload(b,"gpt-4.1-mini"))+4096,10000,10000,price,price.CheckedUtc).CeilingUsd);
  return new(batches,ceiling,batches.Sum(b=>b.Count));
 }
}
