using System.Text;
namespace GameLogDesktop;
public sealed record TranslationJob(List<List<GameText>> Batches,decimal CeilingUsd,int Count);
public static class TranslationJobPlanner
{
 public static TranslationJob Plan(IEnumerable<GameText> rows,ModelPrice price)
 {
  var batches=new List<List<GameText>>();var batch=new List<GameText>();var size=0;
  foreach(var row in rows.Where(x=>string.IsNullOrWhiteSpace(x.Vietnamese)&&!string.IsNullOrWhiteSpace(x.Source)).DistinctBy(x=>x.Source,StringComparer.Ordinal)){
   if(row.Source.Length>12000)continue;
   if(batch.Count>=100||size+row.Source.Length>12000){batches.Add(batch);batch=[];size=0;}batch.Add(row);size+=row.Source.Length;
  }
  if(batch.Count>0)batches.Add(batch);
  var estimate=batches.Sum(b=>(Encoding.UTF8.GetByteCount(OpenAiTranslation.Payload(b,TranslationCosts.DefaultModel))*price.InputPerMillion+b.Sum(x=>x.Source.Length)*price.OutputPerMillion)/1_000_000m);
  return new(batches,estimate,batches.Sum(b=>b.Count));
 }
 public static List<GameText> Merge(IEnumerable<GameText> draft,IReadOnlyList<GameText> translated)
 {
  var byText=translated.GroupBy(x=>x.Source,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.First().Vietnamese,StringComparer.Ordinal);
  return draft.Select(x=>string.IsNullOrWhiteSpace(x.Vietnamese)&&byText.TryGetValue(x.Source,out var text)?x with{Vietnamese=text}:x).ToList();
 }
}

