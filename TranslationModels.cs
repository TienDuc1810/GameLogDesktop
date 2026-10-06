using System.Text.Json.Serialization;
namespace GameLogDesktop;

public sealed class TranslationCatalog
{
 public List<TranslationEntry> Entries {get;set;}=[];
 public string RedChecked {get;set;}="";
 public string PenguinChecked {get;set;}="";
 public bool RedComplete {get;set;}
 public bool PenguinComplete {get;set;}
}
public sealed class TranslationEntry
{
 public string Provider {get;set;}="";
 public string Name {get;set;}="";
 public string Url {get;set;}="";
 public string AppId {get;set;}="";
 public bool Pc {get;set;}=true;
 // Unknown is deliberately distinct from free and from not listed.
 public string Access {get;set;}="unknown";
 public string Checked {get;set;}="";
 public string Note {get;set;}="";
}
public sealed class TranslationRow
{
 public int Number {get;set;}
 public Game Game {get;set;}=new();
 public string Name=>Game.Name;
 public TranslationEntry? Red {get;set;}
 public TranslationEntry? Penguin {get;set;}
 public string RedStatus {get;set;}="Chưa kiểm tra";
 public string PenguinStatus {get;set;}="Chưa kiểm tra";
 public string Later=>"Làm sau";
 public string DraftStatus=>Game.AppId=="286690"?"Dịch nháp · cần xử lý font":"Chưa hỗ trợ định dạng";
 public string ImportStatus=>"Kiểm tra ZIP · chưa cài";
}
