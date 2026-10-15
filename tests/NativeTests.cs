using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
namespace ImageClothesline.Tests {
 public static partial class TestRunner {
  static void RunNativeTests(){Test("系统回收站收到测试副本",NativeRecycle);Test("悬停 Del 经真实控制器静默送入回收站",()=>NativeHoverDelete(46));Test("悬停退格键经真实控制器静默送入回收站",()=>NativeHoverDelete(8));Test("恢复缩略图不能覆盖文件链接目标",ThumbnailLinkProtection);}
  [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
  [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.I1)]
  static extern bool CreateSymbolicLink(string link,string target,int flags);
  static void ThumbnailLinkProtection(){
   var f=Folder();var original=Fixture(f,".jpg");var before=File.ReadAllBytes(original);var s=new ImageStore(Path.Combine(f,"store"),new TestBin());var image=s.ImportBitmap(Bitmap());
   File.Delete(Path.Combine(s.Root,"state.json"));string thumbnail=s.ThumbnailPath(image.Id);File.Delete(thumbnail);
   if(!CreateSymbolicLink(thumbnail,original,0))throw new Exception("当前测试环境无法创建隔离文件链接，错误 "+System.Runtime.InteropServices.Marshal.GetLastWin32Error());
   try{var restored=new ImageStore(s.Root,new TestBin());Assert(before.SequenceEqual(File.ReadAllBytes(original)),"启动恢复跟随缩略图链接覆盖了外部原件");}finally{File.Delete(thumbnail);}
  }
  static void NativeHoverDelete(byte key){
   if(System.Windows.Application.Current==null)new System.Windows.Application{ShutdownMode=System.Windows.ShutdownMode.OnExplicitShutdown};
   Native.Point cursor;Native.GetCursorPos(out cursor);string original=Fixture(Folder());byte[] originalBytes=File.ReadAllBytes(original);
   var store=new ImageStore(Path.Combine(Folder(),"store"),new WindowsRecycleBin());var record=store.ImportFile(original);store.SetPreferences(false,true);
   string bin=Path.Combine(Path.GetPathRoot(original),"$Recycle.Bin",WindowsIdentity.GetCurrent().User.Value);var before=Directory.Exists(bin)?Directory.GetFiles(bin,"$I*"):new string[0];
   var controller=new AppController(System.Windows.Application.Current,store);
   try{
    controller.Initialize().GetAwaiter().GetResult();Pump(700);var card=FindVisual<ImageCard>(controller.Window.HangingItems.ItemContainerGenerator.ContainerFromIndex(0));var p=card.PointToScreen(new System.Windows.Point(card.ActualWidth/2,80));SetCursorPos((int)p.X,(int)p.Y);Pump(100);
    keybd_event(key,0,0,UIntPtr.Zero);keybd_event(key,0,2,UIntPtr.Zero);var timeout=System.Diagnostics.Stopwatch.StartNew();while(store.Snapshot().Count>0&&timeout.ElapsedMilliseconds<5000)Pump(50);Pump(100);
    Assert(store.Snapshot().Count==0,"Del 未通过真实控制器完成静默删除");Assert(!VisibleTexts(controller.Window).Any(x=>x.Text=="工具副本已放入回收站"),"Del 成功后仍显示了提示");Assert(File.ReadAllBytes(original).SequenceEqual(originalBytes),"Del 修改了导入原件");
    var paths=new System.Collections.Generic.List<string>();foreach(var metadata in Directory.GetFiles(bin,"$I*").Except(before)){byte[] bytes=File.ReadAllBytes(metadata);if(bytes.Length<28)continue;int offset=BitConverter.ToInt64(bytes,0)==2?28:24;string stored=Encoding.Unicode.GetString(bytes,offset,bytes.Length-offset).TrimEnd('\0');if(stored==store.ImagePath(record.Id)||stored==store.ThumbnailPath(record.Id)){Assert(File.Exists(Path.Combine(bin,"$R"+Path.GetFileName(metadata).Substring(2))),"回收站副本缺失");paths.Add(stored);}}
    Assert(paths.Contains(store.ImagePath(record.Id))&&paths.Contains(store.ThumbnailPath(record.Id)),"图片与缩略图没有都进入回收站");
   }finally{keybd_event(key,0,2,UIntPtr.Zero);controller.Dispose();SetCursorPos(cursor.X,cursor.Y);}
  }
  static void NativeRecycle(){
   string original=Fixture(Folder());string bin=Path.Combine(Path.GetPathRoot(original),"$Recycle.Bin",WindowsIdentity.GetCurrent().User.Value);
   string[] before=Directory.Exists(bin)?Directory.GetFiles(bin,"$I*"):new string[0];
   new WindowsRecycleBin().Recycle(original);Assert(!File.Exists(original),"系统未移走测试副本");
   bool found=false;foreach(var metadata in Directory.GetFiles(bin,"$I*").Except(before)){
    var bytes=File.ReadAllBytes(metadata);if(bytes.Length<28)continue;long version=BitConverter.ToInt64(bytes,0);int offset=version==2?28:24;
    string stored=Encoding.Unicode.GetString(bytes,offset,bytes.Length-offset).TrimEnd('\0');
    if(string.Equals(stored,original,StringComparison.OrdinalIgnoreCase)){string recycled=Path.Combine(bin,"$R"+Path.GetFileName(metadata).Substring(2));Assert(File.Exists(recycled),"回收站元数据存在但副本缺失");found=true;}
   }
   Assert(found,"无法在回收站确认这份测试副本，不能宣称回收成功");
  }
 }
}
