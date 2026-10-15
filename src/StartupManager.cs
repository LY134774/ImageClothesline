using System;
using System.IO;
using Microsoft.Win32;
namespace ImageClothesline {
 public sealed class StartupManager {
  const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
  const string EntryName="ImageClothesline";
  readonly ImageStore store;readonly Func<string> read;readonly Action<string> write;readonly string command;
  public bool Enabled {get;private set;}
  public StartupManager(ImageStore store,Func<string> read,Action<string> write,string executable){
   this.store=store;this.read=read;this.write=write;
   command="\""+Path.GetFullPath(executable)+"\" --startup";
  }
  public static StartupManager Create(ImageStore store){
   return new StartupManager(store,ReadCommand,WriteCommand,System.Reflection.Assembly.GetExecutingAssembly().Location);
  }
  static string ReadCommand(){using(var key=Registry.CurrentUser.OpenSubKey(RunKey,false))return key==null?null:key.GetValue(EntryName,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;}
  static void WriteCommand(string value){
   if(value==null){using(var key=Registry.CurrentUser.OpenSubKey(RunKey,true)){if(key!=null)key.DeleteValue(EntryName,false);}return;}
   using(var key=Registry.CurrentUser.CreateSubKey(RunKey)){key.SetValue(EntryName,value,RegistryValueKind.String);}
  }
  string Desired(bool enabled){
   if(!enabled)return null;
   if(command.Length>260)throw new NotSupportedException("程序所在路径太长，无法登记开机启动。请将程序放到更短的文件夹路径，再重新打开。");
   return command;
  }
  public void Initialize(bool automatic){
   string current=read();Enabled=store.StartWithWindows&&string.Equals(current,command,StringComparison.OrdinalIgnoreCase);
   // Windows is processing Run entries during automatic startup; do not rewrite that key.
   if(automatic)return;
   string desired=Desired(store.StartWithWindows);if(!string.Equals(current,desired,StringComparison.OrdinalIgnoreCase))write(desired);
   Enabled=desired!=null;
  }
  public void SetEnabled(bool enabled){
   string desired=Desired(enabled),previous=read();bool changed=!string.Equals(previous,desired,StringComparison.OrdinalIgnoreCase);
   if(changed)write(desired);
   try{store.SetStartWithWindows(enabled);}
   catch(Exception saveError){
    try{if(changed)write(previous);}
    catch(Exception rollbackError){Enabled=desired!=null;throw new IOException("设置未保存，且启动项未能恢复。请在设置中重新关闭或开启，并检查 Windows 启动应用。",new AggregateException(saveError,rollbackError));}
    throw;
   }
   Enabled=enabled;
  }
 }
}
