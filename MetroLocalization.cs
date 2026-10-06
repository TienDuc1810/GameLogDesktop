using System.IO;
using System.Text;
namespace GameLogDesktop;
public sealed record GameText(string Id,string Source,string Vietnamese="",string GameKey="");
public sealed record MetroTextDocument(string IndexHash,string TextHash,string MissingCharacters,List<GameText> Lines);
// Read only. No archive writer is enabled until the font profile is verified.
public static class MetroLocalization
{
 private sealed record Entry(ushort Type,string Name,ushort Archive,uint Offset,uint Size,uint Packed,ushort Children,uint First);
 public static MetroTextDocument ReadEnglish(string root)=>Parse(ReadEnglishBytes(root),UpdateService.Hash(Path.Combine(root,"content.vfx")));
 public static byte[] ReadEnglishBytes(string root)
 {
  var index=Path.Combine(root,"content.vfx");if(new FileInfo(index).Length>64_000_000)throw new InvalidDataException("Mục lục quá lớn.");
  using var stream=File.OpenRead(index);using var reader=new BinaryReader(stream,Encoding.UTF8);
  if(reader.ReadUInt32()!=1||reader.ReadUInt32()!=1)throw new InvalidDataException("Phiên bản VFX chưa hỗ trợ.");
  if(reader.ReadBytes(16).Length!=16)throw new EndOfStreamException();
  var archiveCount=reader.ReadUInt32();var entryCount=reader.ReadUInt32();var secondaryCount=reader.ReadUInt32();
  if(archiveCount>1000||entryCount>1_000_000||secondaryCount>1_000_000)throw new InvalidDataException("Mục lục không hợp lệ.");
  string CString(){var bytes=new List<byte>();byte b;while((b=reader.ReadByte())!=0){if(bytes.Count>4096)throw new InvalidDataException("Tên quá dài.");bytes.Add(b);}return new UTF8Encoding(false,true).GetString(bytes.ToArray());}
  var archives=new List<string>();for(var a=0;a<archiveCount;a++){var name=CString();if(!UpdateService.SafeRelative(name)||name.Contains('/')||!name.StartsWith("content",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Tên archive không hợp lệ.");archives.Add(name);var count=reader.ReadUInt32();if(count>100000)throw new InvalidDataException("Quá nhiều thư mục.");for(var f=0;f<count;f++)CString();reader.ReadUInt32();}
  var entries=new List<Entry>();for(var i=0;i<entryCount;i++){
   var type=reader.ReadUInt16();ushort archive=0,children=0;uint offset=0,size=0,packed=0,first=0;
   if(type==0){archive=reader.ReadUInt16();offset=reader.ReadUInt32();size=reader.ReadUInt32();packed=reader.ReadUInt32();if(archive>=archives.Count)throw new InvalidDataException("Archive không tồn tại.");}
   else if(type==8){children=reader.ReadUInt16();first=reader.ReadUInt32();}else throw new InvalidDataException("Loại mục lục chưa hỗ trợ.");
   var length=reader.ReadByte();var key=reader.ReadByte();var bytes=reader.ReadBytes(length);if(length==0||bytes.Length!=length||bytes[^1]!=0)throw new InvalidDataException("Tên tài nguyên lỗi.");
   for(var b=0;b<length-1;b++)bytes[b]^=key;
   var name=new UTF8Encoding(false,true).GetString(bytes,0,length-1);entries.Add(new(type,name,archive,offset,size,packed,children,first));
  }
  if(stream.Length-stream.Position<(long)secondaryCount*16)throw new InvalidDataException("Mục lục phụ bị thiếu.");
  var seen=new HashSet<int>();Entry? english=null;
  void Walk(int i,string parent,int depth){if(depth>100||i<0||i>=entries.Count||!seen.Add(i))throw new InvalidDataException("Cây tài nguyên lỗi.");var e=entries[i];var path=(parent+"/"+e.Name).TrimStart('/').Replace('\\','/');if(e.Type==8){for(var j=0;j<e.Children;j++)Walk(checked((int)e.First+j),path,depth+1);}else if(path.Equals("content/localization/stable_us.lng",StringComparison.OrdinalIgnoreCase)){if(english!=null)throw new InvalidDataException("Trùng tài nguyên tiếng Anh.");english=e;}}
  Walk(0,"",0);if(seen.Count!=entries.Count||english==null)throw new InvalidDataException("Chưa nhận diện được tài nguyên tiếng Anh.");
  var resource=Extract(Path.Combine(root,archives[english.Archive]),english.Offset,english.Packed,english.Size);
  return resource;
 }
 internal static byte[] Extract(string path,uint offset,uint packed,uint size)
 {
  if(size>16_000_000||packed>16_000_000)throw new InvalidDataException("Tài nguyên ngôn ngữ quá lớn.");
  if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Không đọc archive liên kết.");
  using var file=File.OpenRead(path);if((long)offset+packed>file.Length)throw new InvalidDataException("Tài nguyên nằm ngoài archive.");file.Position=offset;var input=new byte[packed];file.ReadExactly(input);if(packed==size)return input;
  var result=new byte[size];int p=0,o=0;
  while(p<input.Length){if(input.Length-p<8)throw new InvalidDataException("Header khối bị thiếu.");var c=BitConverter.ToUInt32(input,p);var u=BitConverter.ToUInt32(input,p+4);p+=8;if(c<8||c-8>input.Length-p||(c==u?u-8:u)>size-o)throw new InvalidDataException("Kích thước khối lỗi.");var end=checked(p+(int)c-8);var start=o;
   if(c==u){var count=end-p;if(count>result.Length-o)throw new InvalidDataException("Khối thô quá lớn.");input.AsSpan(p,count).CopyTo(result.AsSpan(o));o+=count;p=end;continue;}
   int Extended(int length){if(length==15){int b;do{if(p>=end)throw new InvalidDataException("Khối LZ4 bị thiếu.");b=input[p++];length=checked(length+b);}while(b==255);}return length;}
   while(p<end){var token=input[p++];var literal=Extended(token>>4);if(literal>end-p||literal>result.Length-o)throw new InvalidDataException("LZ4 literal lỗi.");input.AsSpan(p,literal).CopyTo(result.AsSpan(o));p+=literal;o+=literal;if(p==end)break;if(end-p<2)throw new InvalidDataException("LZ4 offset lỗi.");var distance=input[p]|input[p+1]<<8;p+=2;if(distance==0||distance>o)throw new InvalidDataException("LZ4 tham chiếu lỗi.");var count=checked(Extended(token&15)+4);if(count>result.Length-o)throw new InvalidDataException("LZ4 output lỗi.");for(var j=0;j<count;j++){result[o]=result[o-distance];o++;}}
   if(o-start!=u)throw new InvalidDataException("Độ dài giải nén không khớp.");
  }
  if(o!=size)throw new InvalidDataException("Tài nguyên giải nén không đủ.");return result;
 }
 internal static MetroTextDocument Parse(byte[] bytes,string indexHash)
 {
  using var stream=new MemoryStream(bytes);using var r=new BinaryReader(stream);
  if(bytes.Length<32)throw new InvalidDataException("LNG quá ngắn.");var header=new uint[6];for(var i=0;i<6;i++)header[i]=r.ReadUInt32();if(header[0]!=0||header[1]!=4||header[2]!=0||header[3]!=1||header[4]>20000||header[4]%2!=0)throw new InvalidDataException("Biến thể LNG chưa hỗ trợ.");
  var chars=new List<char>();for(var i=0;i<header[4]/2;i++){var c=r.ReadUInt16();if(c==0){if(i!=header[4]/2-1)throw new InvalidDataException("Bảng ký tự lỗi.");}else chars.Add((char)c);}
  var size=r.ReadUInt32();if(size!=stream.Length-stream.Position)throw new InvalidDataException("Độ dài phần câu không khớp.");
  byte[] ZeroTerminated(){var list=new List<byte>();byte b;while((b=r.ReadByte())!=0)list.Add(b);return list.ToArray();}
  var lines=new List<GameText>();
  while(stream.Position<stream.Length){var idBytes=ZeroTerminated();if(idBytes.Length==0||idBytes.Any(b=>b<32||b>126))throw new InvalidDataException("ID câu không hợp lệ.");var id=Encoding.ASCII.GetString(idBytes);var raw=ZeroTerminated();var text=new StringBuilder();for(var i=0;i<raw.Length;i++){var b=raw[i];if(b==1){text.Append('\n');continue;}int index=b-2;if(b>=224){if(++i>=raw.Length)throw new InvalidDataException("Mã ký tự thiếu byte.");index=221+raw[i];}if(index<0||index>=chars.Count)throw new InvalidDataException("Mã ký tự chưa hỗ trợ.");text.Append(chars[index]);}lines.Add(new($"line_{lines.Count:D6}",text.ToString(),"",id));}
  var required="ĂăÂâĐđÊêÔôƠơƯưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ";
  return new(indexHash,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),new string(required.Where(c=>!chars.Contains(c)).Distinct().ToArray()),lines);
 }
}


