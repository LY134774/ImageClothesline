using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ImageClothesline.Tests {
 public static partial class TestRunner {
  static int passed,failed;
  static string runRoot;
  [STAThread] public static int Main(string[] args) {
   Console.OutputEncoding=new System.Text.UTF8Encoding(false);
   runRoot=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fixtures-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(runRoot);
   Test("保存静态图片并保留导入原件",SaveAndKeepOriginal);
   Test("退出后恢复图片和收纳设置",Restart);
   Test("同一图片复用副本并重新挂起",Duplicate);
   Test("接受拖出后取下但保留副本",Export);
   Test("取消拖出时卡片不消失",CancelExport);
   Test("清理只处理已取下副本",Cleanup);
   Test("回收失败保留图片和记录",RecycleFailure);
   Test("保存记录失败时不显示新图片",WriteFailure);
   Test("删除后记录写入失败仍通知界面并可重启",RemoveWriteFailure);
   Test("大图收纳期间读取状态不会长时间阻塞",LargeImportResponsiveness);
   Test("拒绝损坏图片且不影响已保存图片",InvalidImage);
   Test("拒绝未支持格式",UnsupportedImage);
   Test("索引损坏时从备份恢复",BackupRecovery);
   Test("索引路径不能指向原始文件",UnsafeIndex);
   Test("批量清理部分失败时保留失败项",PartialCleanup);
   Test("新图片按加入时间在前",Ordering);
   Test("首次使用不自动开启收纳",FirstRun);
   Test("PNG 透明度在保存后保留",Transparency);
   Test("GIF 和 TIFF 只保存第一帧",FirstFrame);
   RunBehaviorTests();
   if(args.Contains("--ui"))RunUiTests();
   if(args.Contains("--settings-delete")){if(Application.Current==null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Test("设置打开时悬停删除仍生效且忙碌时受保护",UiSettingsDelete);}
   if(args.Contains("--native-recycle"))RunNativeTests();
   if(args.Contains("--startup"))RunStartupTest();
   if(args.Contains("--copy-performance"))Test("大图复制保持界面响应且保留完整图片",LargeCopyResponsiveness);
   Console.WriteLine("RESULT: "+passed+" passed, "+failed+" failed");
   string expectedPrefix=Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)+"fixtures-";
   if(!Path.GetFullPath(runRoot).StartsWith(expectedPrefix,StringComparison.OrdinalIgnoreCase))throw new IOException("测试清理目录不在预期位置。");
   try{Directory.Delete(runRoot,true);}catch(IOException){Console.WriteLine("NOTE: 测试样例暂未完全清除。");}catch(UnauthorizedAccessException){Console.WriteLine("NOTE: 测试样例暂未完全清除。");}
   return failed==0?0:1;
  }
  static void Test(string name,Action body){try{body();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.GetType().Name+" "+e.Message);}}
  public static void Assert(bool value,string message){if(!value)throw new Exception(message);}
  static string Folder(){string p=Path.Combine(runRoot,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}
  static BitmapSource Bitmap(byte r=20,byte g=160,byte b=130,byte a=255){byte[] px=new byte[24*16*4];for(int i=0;i<px.Length;i+=4){px[i]=b;px[i+1]=g;px[i+2]=r;px[i+3]=a;}var v=BitmapSource.Create(24,16,96,96,PixelFormats.Bgra32,null,px,24*4);v.Freeze();return v;}
  static string Fixture(string folder,string extension=".png",bool second=false){BitmapEncoder encoder;
   switch(extension){case ".jpg":encoder=new JpegBitmapEncoder();break;case ".bmp":encoder=new BmpBitmapEncoder();break;case ".gif":encoder=new GifBitmapEncoder();break;case ".tiff":encoder=new TiffBitmapEncoder();break;default:encoder=new PngBitmapEncoder();break;}
   encoder.Frames.Add(BitmapFrame.Create(Bitmap()));if(second)encoder.Frames.Add(BitmapFrame.Create(Bitmap(255,10,10)));
   string p=Path.Combine(folder,"中文图片"+extension);using(var s=File.Create(p))encoder.Save(s);return p;
  }
  static ImageRecord Added(ImageStore store,string p){var r=store.ImportFile(p);Assert(r!=null,"导入后应该产生可恢复的图片记录");return r;}
  static void SaveAndKeepOriginal(){foreach(string ext in new[]{".png",".jpg",".bmp",".gif",".tiff"}){var f=Folder();var p=Fixture(f,ext);var before=File.ReadAllBytes(p);var s=new ImageStore(Path.Combine(f,"store"),new TestBin());var r=Added(s,p);Assert(File.Exists(s.ImagePath(r.Id)),"图片副本没有保存");Assert(s.Snapshot().Count==1,"图片没有挂上");Assert(before.SequenceEqual(File.ReadAllBytes(p)),"原件被修改");Assert(File.Exists(s.ThumbnailPath(r.Id)),"缩略图没有保存");}}
  static void Restart(){var f=Folder();var root=Path.Combine(f,"store");var s=new ImageStore(root,new TestBin());var r=Added(s,Fixture(f));s.SetPreferences(true,true);var again=new ImageStore(root,new TestBin());Assert(again.Collecting&&again.NoticeAcknowledged,"设置未恢复");Assert(again.Snapshot().Single().Id==r.Id,"图片未恢复");}
  static void Duplicate(){var f=Folder();var s=new ImageStore(Path.Combine(f,"store"),new TestBin());var p=Fixture(f);var r=Added(s,p);s.CompleteExport(r.Id,true);var next=Added(s,p);Assert(next.Id==r.Id&&s.Snapshot().Count==1,"重复图片未复用");Assert(s.Snapshot().Single().Hanging,"重复加入未重新挂起");}
  static void Export(){var f=Folder();var s=new ImageStore(Path.Combine(f,"store"),new TestBin());var r=Added(s,Fixture(f));s.CompleteExport(r.Id,true);Assert(!s.Snapshot().Single().Hanging,"接受拖出后未取下");Assert(File.Exists(s.ImagePath(r.Id)),"拖出后副本被删除");Assert(s.ExportedBytes>0,"已取下占用没有统计");var again=new ImageStore(s.Root,new TestBin());Assert(!again.Snapshot().Single().Hanging,"取下状态未恢复");}
  static void CancelExport(){var f=Folder();var s=new ImageStore(Path.Combine(f,"store"),new TestBin());var r=Added(s,Fixture(f));s.CompleteExport(r.Id,false);Assert(s.Snapshot().Single().Hanging,"取消拖出丢了卡片");}
  static void Cleanup(){var f=Folder();var bin=new TestBin();var s=new ImageStore(Path.Combine(f,"store"),bin);var original=Fixture(f);var r=Added(s,original);var r2=s.ImportBitmap(Bitmap(10,10,255));s.CompleteExport(r.Id,true);var result=s.CleanupExported();Assert(result.Removed==1&&result.Errors.Count==0,"未完成清理");Assert(s.Snapshot().Single().Id==r2.Id,"仍挂着的图片被清理");Assert(File.Exists(original),"导入的原件被清理");Assert(!File.Exists(s.ImagePath(r.Id)),"副本没有离开保存目录");}
  static void RecycleFailure(){var f=Folder();var s=new ImageStore(Path.Combine(f,"store"),new TestBin{Fail=true});var r=Added(s,Fixture(f));bool caught=false;try{s.Remove(r.Id);}catch(IOException){caught=true;}Assert(caught,"回收失败未报告");Assert(s.Snapshot().Count==1&&File.Exists(s.ImagePath(r.Id)),"失败时丢了图片");}
  static void WriteFailure(){var f=Folder();var s=new ImageStore(Path.Combine(f,"store"),new TestBin());var p=Fixture(f);Directory.CreateDirectory(Path.Combine(s.Root,"state.json.pending"));bool caught=false;try{s.ImportFile(p);}catch(IOException){caught=true;}catch(UnauthorizedAccessException){caught=true;}Assert(caught,"写入失败没有报告");Assert(s.Snapshot().Count==0,"保存失败的图片仍显示");Assert(File.Exists(p),"保存失败修改了原件");}
  static void RemoveWriteFailure(){
   var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());var r=s.ImportBitmap(Bitmap());int notices=0;s.Changed+=(sender,e)=>notices++;
   Directory.CreateDirectory(Path.Combine(s.Root,"state.json.pending"));string error=null;try{s.Remove(r.Id);}catch(Exception e){error=e.Message;}
   Assert(error!=null,"索引保存失败没有报告");Assert(!File.Exists(s.ImagePath(r.Id))&&s.Snapshot().Count==0,"已回收图片仍在内存记录中");
   Assert(notices==1,"已回收后索引保存失败，没有通知界面移除旧卡片");Assert(error.Contains("已放入回收站"),"错误提示未区分回收成功与记录保存失败");
   Assert(new ImageStore(s.Root,new TestBin()).Snapshot().Count==0,"重启后出现已回收图片");
  }
  static void LargeImportResponsiveness(){
   var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());int width=3072,height=2048;var pixels=new byte[width*height*4];new Random(17).NextBytes(pixels);for(int i=3;i<pixels.Length;i+=4)pixels[i]=255;
   var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);bitmap.Freeze();
   var task=System.Threading.Tasks.Task.Run(()=>s.ImportBitmap(bitmap));double longest=0;int reads=0;
   while(!task.IsCompleted){var clock=System.Diagnostics.Stopwatch.StartNew();s.Snapshot();bool collecting=s.Collecting;clock.Stop();longest=Math.Max(longest,clock.Elapsed.TotalMilliseconds);reads++;System.Threading.Thread.Sleep(5);}
   task.GetAwaiter().GetResult();Console.WriteLine("MEASURE large import state-read max ms: "+longest.ToString("F1")+", reads: "+reads);Assert(s.Snapshot().Count==1,"大图未完成保存");Assert(longest<400,"大图处理阻塞状态读取超过400毫秒："+longest.ToString("F1"));
  }
  static void InvalidImage(){var f=Folder();var s=new ImageStore(Path.Combine(f,"store"),new TestBin());Added(s,Fixture(f));var p=Path.Combine(f,"损坏.png");File.WriteAllText(p,"broken");bool caught=false;try{s.ImportFile(p);}catch(Exception){caught=true;}Assert(caught&&s.Snapshot().Count==1,"损坏图片没有被隔离");}
  static void UnsupportedImage(){var f=Folder();var p=Fixture(f);var webp=Path.Combine(f,"图.webp");File.Copy(p,webp);var s=new ImageStore(Path.Combine(f,"store"),new TestBin());bool caught=false;try{s.ImportFile(webp);}catch(NotSupportedException){caught=true;}Assert(caught&&s.Snapshot().Count==0,"不支持的格式被导入");}
  static void BackupRecovery(){var f=Folder();var s=new ImageStore(Path.Combine(f,"store"),new TestBin());var r=Added(s,Fixture(f));s.SetPreferences(true,true);File.WriteAllText(Path.Combine(s.Root,"state.json"),"broken");var again=new ImageStore(s.Root,new TestBin());Assert(again.Snapshot().Any(x=>x.Id==r.Id),"损坏索引没有从备份恢复");Assert(File.Exists(again.ImagePath(r.Id)),"恢复时图片丢失");}
  static void UnsafeIndex(){var f=Folder();var root=Path.Combine(f,"store");Directory.CreateDirectory(root);var original=Fixture(f);File.WriteAllText(Path.Combine(root,"state.json"),"{\"Version\":1,\"Images\":[{\"Id\":\"../中文图片\",\"Hanging\":true}]}");bool caught=false;try{new ImageStore(root,new TestBin());}catch(IOException){caught=true;}Assert(caught&&File.Exists(original),"非法索引没有拒绝");}
  static void PartialCleanup(){var f=Folder();var bin=new TestBin();var s=new ImageStore(Path.Combine(f,"store"),bin);var a=Added(s,Fixture(f));var b=s.ImportBitmap(Bitmap(50,60,70));s.CompleteExport(a.Id,true);s.CompleteExport(b.Id,true);bin.Blocked=s.ImagePath(b.Id);var result=s.CleanupExported();Assert(result.Removed==1&&result.Errors.Count==1,"部分失败未正确报告");Assert(s.Snapshot().Single().Id==b.Id&&File.Exists(s.ImagePath(b.Id)),"失败项被删除");}
  static void Ordering(){var f=Folder();var s=new ImageStore(Path.Combine(f,"store"),new TestBin());var a=Added(s,Fixture(f));System.Threading.Thread.Sleep(20);var b=s.ImportBitmap(Bitmap(60,80,100));Assert(s.Snapshot().First().Id==b.Id,"新图片未排在前面");}
  static void FirstRun(){var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());Assert(!s.Collecting&&!s.NoticeAcknowledged,"首次启动提前监听");s.SetPreferences(false,true);Assert(new ImageStore(s.Root,new TestBin()).NoticeAcknowledged,"不开启收纳时提示状态没有保存");}
  static void Transparency(){var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());var r=s.ImportBitmap(Bitmap(40,60,80,100));Assert(r!=null,"图片未保存");using(var stream=File.OpenRead(s.ImagePath(r.Id))){var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];var normalized=new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0);byte[] px=new byte[4];normalized.CopyPixels(new Int32Rect(0,0,1,1),px,4,0);Assert(px[3]==100,"透明度丢失");}}
  static void FirstFrame(){foreach(string ext in new[]{".gif",".tiff"}){var f=Folder();var s=new ImageStore(Path.Combine(f,"store"),new TestBin());var r=Added(s,Fixture(f,ext,true));using(var stream=File.OpenRead(s.ImagePath(r.Id))){var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);Assert(decoder.Frames.Count==1,"没有转为单帧静态图片");var frame=new FormatConvertedBitmap(decoder.Frames[0],PixelFormats.Bgra32,null,0);byte[] px=new byte[4];frame.CopyPixels(new Int32Rect(0,0,1,1),px,4,0);Assert(px[1]>100&&px[2]<100,"保存了错误的帧");}}}
 }
 public sealed class TestBin:IRecycleBin {public bool Fail;public string Blocked;public Action BeforeRecycle;public void Recycle(string p){var before=BeforeRecycle;BeforeRecycle=null;if(before!=null)before();if(Fail||p==Blocked)throw new IOException("测试回收失败");Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(p),"test-recycle"));File.Move(p,Path.Combine(Path.GetDirectoryName(p),"test-recycle",Path.GetFileName(p)));}}
}
