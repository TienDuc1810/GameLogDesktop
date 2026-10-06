using System.IO;
using System.Text;
using System.Globalization;
using System.Diagnostics;
namespace GameLogDesktop;
public sealed record MetroPatchInfo(string OriginalIndexHash,string PatchedIndexHash,string ArchiveHash,string Folder,int Translated,int English);
public static class MetroTestPatch
{
 public const string ArchiveName="content99.vfs0";
 private const string BackupName="gamelog-metro-backup";
 private static void CheckRoot(string root)
 {
  if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0||(File.GetAttributes(Path.Combine(root,"content.vfx"))&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Không thay file qua thư mục hoặc tệp liên kết.");
  foreach(var process in Process.GetProcesses()){using(process){try{if(process.ProcessName.Equals("metro",StringComparison.OrdinalIgnoreCase)||process.ProcessName.Equals("metro2033",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Đóng Metro trước khi cài hoặc khôi phục bản thử.");}catch(System.ComponentModel.Win32Exception){}}}
 }
 private static void WriteAtomic(string path,byte[] bytes){var temp=path+".gamelog-"+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,bytes);File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public static void Install(string root,string package)
 {
  root=Path.GetFullPath(root);package=Path.GetFullPath(package);CheckRoot(root);
  var info=System.Text.Json.JsonSerializer.Deserialize<MetroPatchInfo>(File.ReadAllText(Path.Combine(package,"patch-manifest.json")),Storage.Json)??throw new InvalidDataException("Thiếu manifest bản thử.");
  var index=Path.Combine(root,"content.vfx");var archive=Path.Combine(root,ArchiveName);var backup=Path.Combine(root,BackupName);
  if(UpdateService.Hash(index)!=info.OriginalIndexHash||UpdateService.Hash(Path.Combine(package,"content.vfx"))!=info.PatchedIndexHash||UpdateService.Hash(Path.Combine(package,ArchiveName))!=info.ArchiveHash)throw new InvalidDataException("Game hoặc gói thử đã thay đổi; không cài.");
  if(File.Exists(archive)||Directory.Exists(backup))throw new InvalidDataException("Có archive hoặc bản sao lưu trước đó. Khôi phục bản cũ trước khi cài lại.");
  var original=File.ReadAllBytes(index);Directory.CreateDirectory(backup);File.WriteAllBytes(Path.Combine(backup,"content.vfx"),original);File.WriteAllText(Path.Combine(backup,"patch-manifest.json"),System.Text.Json.JsonSerializer.Serialize(info,Storage.Json));
  try{File.Copy(Path.Combine(package,ArchiveName),archive,false);WriteAtomic(index,File.ReadAllBytes(Path.Combine(package,"content.vfx")));var verification=MetroLocalization.ReadEnglish(root);if(verification.IndexHash!=info.PatchedIndexHash)throw new InvalidDataException("Không xác minh được bản đã cài.");}
  catch{WriteAtomic(index,original);if(File.Exists(archive)&&UpdateService.Hash(archive)==info.ArchiveHash)File.Delete(archive);throw;}
 }
 public static void Restore(string root)
 {
  root=Path.GetFullPath(root);CheckRoot(root);var backup=Path.Combine(root,BackupName);if((File.GetAttributes(backup)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Bản sao lưu là liên kết.");
  var info=System.Text.Json.JsonSerializer.Deserialize<MetroPatchInfo>(File.ReadAllText(Path.Combine(backup,"patch-manifest.json")),Storage.Json)??throw new InvalidDataException("Thiếu manifest sao lưu.");
  var index=Path.Combine(root,"content.vfx");var archive=Path.Combine(root,ArchiveName);var original=Path.Combine(backup,"content.vfx");
  if(UpdateService.Hash(index)!=info.PatchedIndexHash||UpdateService.Hash(original)!=info.OriginalIndexHash||UpdateService.Hash(archive)!=info.ArchiveHash)throw new InvalidDataException("File game đã thay đổi sau khi cài. Dừng để tránh ghi đè bản khác.");
  WriteAtomic(index,File.ReadAllBytes(original));File.Delete(archive);
  // Keep the backup as recovery evidence; move it aside so a later install starts fresh.
  Directory.Move(backup,Path.Combine(root,BackupName+"-restored-"+Guid.NewGuid().ToString("N")));
 }
 private static string NoAccents(string value)=>new(value.Replace('đ','d').Replace('Đ','D').Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray());
 public static byte[] Encode(byte[] original,IReadOnlyList<GameText> rows)
 {
  var parsed=MetroLocalization.Parse(original,"");if(rows.Count!=parsed.Lines.Count||rows.Where((x,i)=>x.Id!=parsed.Lines[i].Id||x.GameKey!=parsed.Lines[i].GameKey||x.Source!=parsed.Lines[i].Source).Any())throw new InvalidDataException("Bản dịch không khớp câu gốc.");
  var tableBytes=BitConverter.ToUInt32(original,16);var table=new Dictionary<char,int>();for(var i=0;i<tableBytes/2-1;i++)table.TryAdd((char)BitConverter.ToUInt16(original,24+i*2),i);
  var data=new MemoryStream();using(var writer=new BinaryWriter(data,Encoding.UTF8,true))foreach(var line in rows){var text=NoAccents(string.IsNullOrWhiteSpace(line.Vietnamese)?line.Source:line.Vietnamese);text=text.Replace('“','"').Replace('”','"').Replace('’','\'').Replace('–','-').Replace('—','-');if(text.Any(c=>c!='\n'&&!table.ContainsKey(c)))text=line.Source;
   writer.Write(Encoding.ASCII.GetBytes(line.GameKey));writer.Write((byte)0);foreach(var c in text){if(c=='\n'){writer.Write((byte)1);continue;}if(!table.TryGetValue(c,out var index)||index>476)throw new InvalidDataException("Ký tự không có trong font gốc.");if(index<222)writer.Write((byte)(index+2));else{writer.Write((byte)224);writer.Write((byte)(index-221));}}writer.Write((byte)0);
  }
  using var output=new MemoryStream();using var w=new BinaryWriter(output);w.Write(original,0,checked(24+(int)tableBytes));w.Write(checked((uint)data.Length));w.Write(data.ToArray());return output.ToArray();
 }
 public static MetroPatchInfo Build(string root,IReadOnlyList<GameText> rows,string destination,bool retainOriginalText=false)
 {
  root=Path.GetFullPath(root);destination=Path.GetFullPath(destination);if(destination.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||destination.Equals(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Tạo gói ở thư mục riêng, không ghi vào game.");
  var original=File.ReadAllBytes(Path.Combine(root,"content.vfx"));var expected=MetroLocalization.ReadEnglish(root);var originalText=MetroLocalization.ReadEnglishBytes(root);var raw=retainOriginalText?originalText:Encode(originalText,rows);Directory.CreateDirectory(destination);
  using var input=new MemoryStream(original);using var reader=new BinaryReader(input);input.Position=24;var archiveCount=reader.ReadUInt32();var entryCount=reader.ReadUInt32();reader.ReadUInt32();
  string CString(){var bytes=new List<byte>();byte b;while((b=reader.ReadByte())!=0)bytes.Add(b);return Encoding.UTF8.GetString(bytes.ToArray());}
  for(var a=0;a<archiveCount;a++){if(CString().Equals(ArchiveName,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Archive thử đã tồn tại trong mục lục.");var folders=reader.ReadUInt32();for(var f=0;f<folders;f++)CString();reader.ReadUInt32();}
  var archivesEnd=checked((int)input.Position);int target=-1;
  for(var e=0;e<entryCount;e++){var start=checked((int)input.Position);var type=reader.ReadUInt16();if(type==0)input.Position+=14;else if(type==8)input.Position+=6;else throw new InvalidDataException("Mục lục không hỗ trợ.");var length=reader.ReadByte();var key=reader.ReadByte();var name=reader.ReadBytes(length);for(var i=0;i<name.Length-1;i++)name[i]^=key;if(Encoding.UTF8.GetString(name,0,name.Length-1)=="stable_us.lng"){if(target>=0||type!=0)throw new InvalidDataException("Tài nguyên bị trùng.");target=start;}}
  if(target<0)throw new InvalidDataException("Không tìm thấy stable_us.lng.");
  // Use the archive's existing uncompressed resource mode: packed size equals
  // resource size and there are no linked-block headers to interpret.
  using(var archive=File.Create(Path.Combine(destination,ArchiveName)))using(var writer=new BinaryWriter(archive)){writer.Write(raw);writer.Write(0u);writer.Write(16u);writer.Write(original,8,16);}
  var archiveSize=checked((uint)new FileInfo(Path.Combine(destination,ArchiveName)).Length);var packedSize=checked((uint)raw.Length);
  using var extra=new MemoryStream();using(var w=new BinaryWriter(extra,Encoding.UTF8,true)){w.Write(Encoding.ASCII.GetBytes(ArchiveName));w.Write((byte)0);w.Write(0u);w.Write(archiveSize);}
  using var index=new MemoryStream();index.Write(original,0,archivesEnd);index.Write(extra.ToArray());index.Write(original,archivesEnd,original.Length-archivesEnd);var bytes=index.ToArray();BitConverter.GetBytes(archiveCount+1).CopyTo(bytes,24);var pos=target+(int)extra.Length;BitConverter.GetBytes(checked((ushort)archiveCount)).CopyTo(bytes,pos+2);BitConverter.GetBytes(0u).CopyTo(bytes,pos+4);BitConverter.GetBytes(checked((uint)raw.Length)).CopyTo(bytes,pos+8);BitConverter.GetBytes(packedSize).CopyTo(bytes,pos+12);File.WriteAllBytes(Path.Combine(destination,"content.vfx"),bytes);
  var roundtrip=MetroLocalization.ReadEnglish(destination);if(roundtrip.Lines.Count!=rows.Count||roundtrip.Lines.Where((x,i)=>x.GameKey!=rows[i].GameKey).Any())throw new InvalidDataException("Gói thử đọc lại không khớp.");
  var translated=roundtrip.Lines.Where((x,i)=>x.Source!=rows[i].Source).Count();var info=new MetroPatchInfo(expected.IndexHash,UpdateService.Hash(Path.Combine(destination,"content.vfx")),UpdateService.Hash(Path.Combine(destination,ArchiveName)),destination,translated,rows.Count-translated);
  File.WriteAllText(Path.Combine(destination,"patch-manifest.json"),System.Text.Json.JsonSerializer.Serialize(info,Storage.Json));return info;
 }
}
