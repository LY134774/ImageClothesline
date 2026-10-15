using System;
using System.IO;
using System.Windows;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
namespace ImageClothesline.Tests {
 public static partial class TestRunner {
  static void RunBehaviorTests(){
   Test("微信 image/png 图片不被错误读取为空白位图",ClipboardPngMime);
   Test("旧式位图的零透明度按普通截图处理",ClipboardOpaqueBitmap);
   Test("剪贴板 PNG 的真实透明度保留",ClipboardPngAlpha);
   Test("位图中的有效混合透明度不会被覆盖",ClipboardMixedAlpha);
   Test("PNG 截图经过后台保存后仍可读取",ClipboardPngBackground);
   Test("复制多张照片文件后保存并保留原件",ClipboardFiles);
   Test("文件收纳开关默认开启并保存关闭选择",FileCollectionPreference);
   Test("关闭文件收纳不影响截图且总开关暂停全部",ClipboardFilesSwitch);
   Test("剪切与混合文件跳过，损坏图片不影响其他项",ClipboardFilesPartial);
   Test("暂停时不读取剪贴板图片",ClipboardPaused);
   Test("收纳新图片而不收纳文字",ClipboardImage);
   Test("收纳仅在保存成功后返回待展示图片",ClipboardCollectionResult);
   Test("自身复制不重复收纳",OwnCopy);
   Test("剪贴板忙碌后可以再次读取",ClipboardBusy);
   Test("开启后只收纳随后复制的图片",ClipboardBaseline);
   Test("顶部停留和离开延迟",RevealDelay);
   Test("新图片展示两秒并避开全屏",CollectedTimer);
   Test("忙碌时不收起并避开全屏",RevealBusy);
   Test("手动隐藏后不会立即重新弹出",ManualHide);
   Test("删除时也移走派生缩略图",RemoveThumbnail);
   Test("清理期间重新挂起的图片不能删除",RehangDuringCleanup);
   Test("含空记录的损坏索引使用备份",NullIndexBackup);
  }
  static void FileCollectionPreference(){
   var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());Assert(s.CollectFiles,"新设置默认没有开启");s.ImportBitmap(Bitmap());s.SetPreferences(true,true);string index=Path.Combine(s.Root,"state.json");string json=File.ReadAllText(index);string oldJson=json.Replace("\"CollectFiles\":true,","");Assert(!oldJson.Contains("CollectFiles"),"未生成缺少新开关的旧版记录");File.WriteAllText(index,oldJson);var legacy=new ImageStore(s.Root,new TestBin());Assert(legacy.CollectFiles&&legacy.Snapshot().Count==1,"旧版本缺少开关时没有默认开启或图片记录丢失");legacy.SetFileCollection(false);var restored=new ImageStore(s.Root,new TestBin());Assert(!restored.CollectFiles&&restored.Collecting,"重启丢失关闭选择或误改总开关");Directory.CreateDirectory(Path.Combine(s.Root,"state.json.pending"));bool failed=false;try{restored.SetFileCollection(true);}catch(IOException){failed=true;}catch(UnauthorizedAccessException){failed=true;}Assert(failed&&!restored.CollectFiles,"保存失败后开关没有回退");
  }
  static void ClipboardFiles(){
   var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());s.SetPreferences(true,true);var c=new TestClipboard();var capture=new ClipboardCapture(s,c);string a=Fixture(Folder()),b=Path.Combine(Folder(),"另一张.JPG");File.WriteAllBytes(b,ImageCodec.Encode(Bitmap(60,70,80)));byte[] before=File.ReadAllBytes(a);c.ExternalFiles(new[]{a,b});var result=capture.HandleChange().GetAwaiter().GetResult();Assert(result!=null&&result.Added!=null&&s.Snapshot().Count==2,"复制的多张照片未加入");Assert(before.SequenceEqual(File.ReadAllBytes(a))&&File.Exists(b),"复制收纳修改了原件");var record=s.Snapshot().First();s.CompleteExport(record.Id,true);c.ExternalFiles(new[]{b});capture.HandleChange().GetAwaiter().GetResult();Assert(s.Snapshot().Count==2&&s.Snapshot().All(x=>x.Hanging),"再次复制没有复用并重新挂起");
  }
  static void ClipboardFilesSwitch(){
   var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());s.SetPreferences(true,true);s.SetFileCollection(false);var c=new TestClipboard();var capture=new ClipboardCapture(s,c);string file=Fixture(Folder());c.ExternalFiles(new[]{file});Assert(capture.HandleChange().GetAwaiter().GetResult()==null&&s.Snapshot().Count==0,"关闭开关仍收纳文件");c.External(Bitmap());capture.HandleChange().GetAwaiter().GetResult();Assert(s.Snapshot().Count==1,"关闭文件开关影响了截图");c.ExternalFiles(new[]{file});s.SetFileCollection(true);capture.ResetBaseline();Assert(capture.HandleChange().GetAwaiter().GetResult()==null,"打开开关收纳了旧剪贴板");s.SetPreferences(false,true);int reads=c.Reads;c.ExternalFiles(new[]{file});capture.HandleChange().GetAwaiter().GetResult();Assert(c.Reads==reads,"总开关关闭仍读取文件");
  }
  static void ClipboardFilesPartial(){
   var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());s.SetPreferences(true,true);var c=new TestClipboard();var capture=new ClipboardCapture(s,c);string good=Fixture(Folder()),broken=Path.Combine(Folder(),"损坏.png"),text=Path.Combine(Folder(),"说明.txt"),folder=Path.Combine(Folder(),"目录.png");File.WriteAllText(broken,"bad image");File.WriteAllText(text,"not an image");Directory.CreateDirectory(folder);c.ExternalFiles(new[]{good},true);Assert(capture.HandleChange().GetAwaiter().GetResult()==null&&s.Snapshot().Count==0,"剪切操作被收纳");c.ExternalFiles(new[]{broken,text,folder,good});var result=capture.HandleChange().GetAwaiter().GetResult();Assert(result!=null&&result.Errors.Count==1&&result.Added!=null&&s.Snapshot().Count==1,"混合列表没有独立处理有效图片和失败项");
  }
  static void ClipboardPngMime(){var data=new DataObject();data.SetData("image/png",new MemoryStream(ImageCodec.Encode(Bitmap(210,80,30))),false);data.SetImage(Bitmap(210,80,30,0));var result=WindowsClipboard.ReadImage(data);Assert(result!=null,"没有读取 PNG 图片");byte[] px=new byte[4];result.CopyPixels(new Int32Rect(0,0,1,1),px,4,0);Assert(px[2]==210&&px[1]==80&&px[0]==30&&px[3]==255,"微信正常 PNG 被透明位图替代");}
  static void ClipboardOpaqueBitmap(){var data=new DataObject();data.SetImage(Bitmap(40,120,200,0));var result=WindowsClipboard.ReadImage(data);byte[] px=new byte[4];result.CopyPixels(new Int32Rect(0,0,1,1),px,4,0);Assert(px[2]==40&&px[1]==120&&px[0]==200&&px[3]==255,"旧式截图透明度全零，导致卡片空白");}
  static void ClipboardMixedAlpha(){byte[] pixels={30,20,10,0,60,50,40,128};var source=BitmapSource.Create(2,1,96,96,System.Windows.Media.PixelFormats.Bgra32,null,pixels,8);source.Freeze();var data=new DataObject();data.SetImage(source);var result=WindowsClipboard.ReadImage(data);byte[] actual=new byte[8];result.CopyPixels(actual,8,0);Assert(actual.SequenceEqual(pixels),"混合透明度或原始颜色被修改");}
  static void ClipboardPngAlpha(){foreach(byte alpha in new byte[]{0,100}){var data=new DataObject();data.SetData("PNG",new MemoryStream(ImageCodec.Encode(Bitmap(50,60,70,alpha))),false);var result=WindowsClipboard.ReadImage(data);byte[] px=new byte[4];result.CopyPixels(new Int32Rect(0,0,1,1),px,4,0);Assert(px[3]==alpha,"PNG 的真实透明度被改为不透明");}}
  static void ClipboardPngBackground(){
   var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());s.SetPreferences(true,true);
   var c=new TestClipboard();var capture=new ClipboardCapture(s,c);
   foreach(byte alpha in new byte[]{255,100,0}){
    var data=new DataObject();data.SetData("image/png",new MemoryStream(ImageCodec.Encode(Bitmap(210,80,30,alpha))),false);
    c.External(WindowsClipboard.ReadImage(data));capture.HandleChange().GetAwaiter().GetResult();
    var record=s.Snapshot().First();var restored=ImageCodec.ReadFile(s.ImagePath(record.Id));byte[] px=new byte[4];restored.CopyPixels(new Int32Rect(0,0,1,1),px,4,0);
    Assert(px.SequenceEqual(new byte[]{30,80,210,alpha}),"截图保存后颜色或透明度错误");Assert(File.Exists(s.ThumbnailPath(record.Id)),"未生成缩略图");
    var thumbnail=Task.Run(()=>ImageCodec.ReadThumbnail(s.ThumbnailPath(record.Id))).GetAwaiter().GetResult();Assert(thumbnail.PixelWidth>0,"后台缩略图不可读取");
    var loaded=Task.Run(()=>ImageCodec.ReadFile(s.ImagePath(record.Id))).GetAwaiter().GetResult();Assert(ImageCodec.Encode(loaded).Length>0,"后台读入的图片无法在主线程复制");
   }
   Assert(new ImageStore(s.Root,new TestBin()).Snapshot().Count==3,"连续截图未全部保存并恢复");
  }
  static void ClipboardPaused(){var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());var c=new TestClipboard();var capture=new ClipboardCapture(s,c);c.External(Bitmap());capture.HandleChange().GetAwaiter().GetResult();Assert(c.Reads==0&&s.Snapshot().Count==0,"未开启时读取了剪贴板");}
  static void ClipboardImage(){var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());s.SetPreferences(true,true);var c=new TestClipboard();var capture=new ClipboardCapture(s,c);c.External(Bitmap());capture.HandleChange().GetAwaiter().GetResult();Assert(s.Snapshot().Count==1,"剪贴板图片没有收纳");c.External(null);capture.HandleChange().GetAwaiter().GetResult();Assert(s.Snapshot().Count==1,"文字产生了卡片");}
  static void ClipboardCollectionResult(){
   var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());s.SetPreferences(true,true);var c=new TestClipboard();var capture=new ClipboardCapture(s,c);c.External(Bitmap());
   Task pending=capture.HandleChange();pending.GetAwaiter().GetResult();var result=pending as Task<ClipboardResult>;Assert(result!=null&&result.Result!=null&&File.Exists(s.ImagePath(result.Result.Added.Id)),"保存成功没有返回可展示图片");
   c.External(null);var textTask=capture.HandleChange() as Task<ClipboardResult>;textTask.GetAwaiter().GetResult();Assert(textTask.Result==null,"文字错误触发图片展示");capture.CopyImage(ImageClothesline.ClipboardImage.FromFile(s.ImagePath(result.Result.Added.Id)));var own=capture.HandleChange() as Task<ClipboardResult>;own.GetAwaiter().GetResult();Assert(own.Result==null,"自身复制错误触发展示");
  }
  static void OwnCopy(){var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());s.SetPreferences(true,true);var c=new TestClipboard();var capture=new ClipboardCapture(s,c);var image=Bitmap();var r=s.ImportBitmap(image);s.CompleteExport(r.Id,true);capture.CopyImage(ImageClothesline.ClipboardImage.FromFile(s.ImagePath(r.Id)));capture.HandleChange().GetAwaiter().GetResult();Assert(c.Writes==1&&!s.Snapshot().Single().Hanging,"自身复制重新挂起了图片");c.External(image);capture.HandleChange().GetAwaiter().GetResult();Assert(s.Snapshot().Single().Hanging,"外部再次复制被误判为自身复制");}
  static void ClipboardBusy(){var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());s.SetPreferences(true,true);var c=new TestClipboard();var capture=new ClipboardCapture(s,c);c.External(Bitmap());c.Busy=true;try{capture.HandleChange().GetAwaiter().GetResult();}catch(ExternalException){}c.Busy=false;capture.HandleChange().GetAwaiter().GetResult();Assert(s.Snapshot().Count==1,"忙碌后重试没有收纳");}
  static void ClipboardBaseline(){var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());var c=new TestClipboard();c.External(Bitmap());var capture=new ClipboardCapture(s,c);s.SetPreferences(true,true);capture.ResetBaseline();capture.HandleChange().GetAwaiter().GetResult();Assert(s.Snapshot().Count==0,"开启时收纳了旧剪贴板");c.External(Bitmap(1,2,3));capture.HandleChange().GetAwaiter().GetResult();Assert(s.Snapshot().Count==1,"新复制没有收纳");}
  static void CollectedTimer(){
   var g=new RevealGate();Assert(!g.ShowForImage(0,true)&&!g.Visible,"全屏时错误弹出新图片");Assert(g.ShowForImage(0,false),"普通桌面未展示新图片");g.Tick(0,false,false,false,false);g.Tick(1999,false,false,false,false);Assert(g.Visible,"新图片展示不足两秒");g.Tick(2000,false,false,false,false);Assert(!g.Visible,"新图片两秒后未开始收起");
   g.ShowForImage(3000,false);g.Tick(5500,false,true,false,false);Assert(g.Visible,"鼠标在图片上仍被自动收起");g.Tick(5600,false,false,false,false);g.Tick(6100,false,false,false,false);Assert(!g.Visible,"查看新图片后离开未收起");
  }
  static void RevealDelay(){var g=new RevealGate();g.Tick(0,true,false,false,false);g.Tick(249,true,false,false,false);Assert(!g.Visible,"过早展开");g.Tick(250,true,false,false,false);Assert(g.Visible,"停留后没有展开");g.Tick(500,false,false,false,false);g.Tick(999,false,false,false,false);Assert(g.Visible,"过早收起");g.Tick(1000,false,false,false,false);Assert(!g.Visible,"离开后没有收起");}
  static void RevealBusy(){var g=new RevealGate();g.ShowManually(0);g.Tick(4000,false,false,true,false);g.Tick(5000,false,false,true,false);Assert(g.Visible,"操作中被收起");g.Tick(6000,true,false,false,true);g.Tick(6500,true,false,false,true);Assert(!g.Visible,"全屏时展开");}
  static void ManualHide(){var g=new RevealGate();g.ShowManually(0);g.HideManually();g.Tick(100,true,false,false,false);g.Tick(500,true,false,false,false);Assert(!g.Visible,"隐藏后立刻弹出");g.Tick(600,false,false,false,false);g.Tick(700,true,false,false,false);g.Tick(950,true,false,false,false);Assert(g.Visible,"重新到顶部后无法展开");}
  static void NullIndexBackup(){var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());var image=s.ImportBitmap(Bitmap());s.SetPreferences(false,true);File.WriteAllText(Path.Combine(s.Root,"state.json"),"{\"Version\":1,\"Images\":[null]}");var restored=new ImageStore(s.Root,new TestBin());Assert(restored.Snapshot().Single().Id==image.Id&&File.Exists(restored.ImagePath(image.Id)),"没有从完好备份恢复图片");}
  static void RehangDuringCleanup(){var bin=new TestBin();var s=new ImageStore(Path.Combine(Folder(),"store"),bin);var first=Bitmap();var a=s.ImportBitmap(first);System.Threading.Thread.Sleep(10);var b=s.ImportBitmap(Bitmap(1,2,3));s.CompleteExport(a.Id,true);s.CompleteExport(b.Id,true);bin.BeforeRecycle=()=>s.ImportBitmap(first);var result=s.CleanupExported();Assert(result.Removed==1&&s.Snapshot().Count==1&&s.Snapshot().Single().Id==a.Id&&s.Snapshot().Single().Hanging,"清理删掉了刚重新挂起的图片");}
  static void RemoveThumbnail(){var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());var r=s.ImportBitmap(Bitmap());s.Remove(r.Id);Assert(!File.Exists(s.ImagePath(r.Id))&&!File.Exists(s.ThumbnailPath(r.Id)),"副本或缩略图仍留在保存目录");}
 }
 public sealed class TestClipboard:IImageClipboard {
  public uint Sequence {get;private set;}
  BitmapSource image;DataObject files;public int Reads,Writes;public bool Busy;
  public void External(BitmapSource source){files=null;image=source;Sequence++;}
  public void ExternalFiles(string[] paths,bool cut=false){image=null;files=new DataObject();files.SetData(DataFormats.FileDrop,paths);files.SetData("Preferred DropEffect",new MemoryStream(BitConverter.GetBytes(cut?2:1)),false);Sequence++;}
  public ClipboardInput ReadInput(bool includeFiles){Reads++;if(Busy)throw new ExternalException("忙碌");return files==null?new ClipboardInput{Image=image}:WindowsClipboard.ReadInput(files,includeFiles);}
  public void WriteImage(ClipboardImage source){Writes++;External(source.Image);}
 }
}
