using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace GameLogDesktop;
// Windows DPAPI binds the saved secret to this Windows user and machine.
public static class SharedApiKey
{
 [StructLayout(LayoutKind.Sequential)] private struct Blob {public int Size;public IntPtr Data;}
 [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)] private static extern bool CryptProtectData(ref Blob input,string? description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
 [DllImport("crypt32.dll",SetLastError=true)] private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
 [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr pointer);
 private static byte[] Transform(byte[] bytes,bool protect)
 {
  var input=new Blob{Size=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};Blob output=default;
  try{Marshal.Copy(bytes,0,input.Data,bytes.Length);var ok=protect?CryptProtectData(ref input,"GameLog OpenAI key",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);if(!ok)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());var result=new byte[output.Size];Marshal.Copy(output.Data,result,0,result.Length);return result;}
  finally{for(var i=0;i<bytes.Length;i++)Marshal.WriteByte(input.Data,i,0);Marshal.FreeHGlobal(input.Data);if(output.Data!=IntPtr.Zero)LocalFree(output.Data);Array.Clear(bytes);}
 }
 public static void Save(string directory,string key){Directory.CreateDirectory(directory);var path=Path.Combine(directory,"openai-key.dpapi");var temp=path+".tmp";File.WriteAllBytes(temp,Transform(Encoding.UTF8.GetBytes(key.Trim()),true));File.Move(temp,path,true);}
 public static string Load(string directory){var file=Path.Combine(directory,"openai-key.dpapi");if(!File.Exists(file))return "";var bytes=Transform(File.ReadAllBytes(file),false);try{return Encoding.UTF8.GetString(bytes);}finally{Array.Clear(bytes);}}
}
