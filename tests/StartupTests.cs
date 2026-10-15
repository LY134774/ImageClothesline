using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
namespace ImageClothesline.Tests {
 public static partial class TestRunner {
  static void RunStartupTest(){Test("大图复制保持界面响应且保留完整图片",LargeCopyResponsiveness);Test("剪贴板复制重试成功后才提示且数据完整",()=>CopyRetryFeedback(false));Test("剪贴板持续忙碌不显示复制成功",()=>CopyRetryFeedback(true));Test("托盘和剪贴板监听启动关闭",StartupSmoke);Test("真实双击预览关闭后自动收起",PreviewCloseTuck);Test("剪贴板图片保存后展开且文字和自身复制不弹出",CollectionDisplay);Test("照片文件收纳与真实设置开关联动",FileCollectionDisplay);Test("删除后微信短暂文字恢复旧图不重新弹出",DeletedImageAfterTransientText);Test("重开后悬停删除仍阻止微信旧图恢复",()=>DeletedImageAfterTransientText(true,false));Test("暂停重开再开启收纳后删除仍有效",()=>DeletedImageAfterTransientText(true,true));}
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l);
  static void DeletedImageAfterTransientText(){DeletedImageAfterTransientText(false,false);}
  static void DeletedImageAfterTransientText(bool reopen,bool paused){
   if(Application.Current==null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Native.Point original;Native.GetCursorPos(out original);
   var store=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());store.SetPreferences(!paused,true);var clipboard=new TestClipboard();var image=Bitmap(30,160,80);
   if(reopen){store.ImportBitmap(image);clipboard.External(image);store=new ImageStore(store.Root,new TestBin());}
   var controller=new AppController(Application.Current,store,clipboard);SettingsWindow settings=null;
   try{
    controller.Initialize().GetAwaiter().GetResult();
    if(!reopen){clipboard.External(image);SendMessage(controller.Window.Handle,0x031d,IntPtr.Zero,IntPtr.Zero);}
    if(paused){controller.OpenSettings();settings=Application.Current.Windows.OfType<SettingsWindow>().Single();var collect=FindVisual<System.Windows.Controls.CheckBox>(settings);collect.IsChecked=true;var waiting=System.Diagnostics.Stopwatch.StartNew();while((!collect.IsEnabled||!store.Collecting)&&waiting.ElapsedMilliseconds<2000)Pump(30);Assert(store.Collecting&&collect.IsEnabled,"设置未完成开启收纳");settings.Close();settings=null;}
    var timeout=System.Diagnostics.Stopwatch.StartNew();while(store.Snapshot().Count==0&&timeout.ElapsedMilliseconds<2000)Pump(30);Pump(700);Assert(store.Snapshot().Count==1,"样例截图没有挂上");
    var card=FindVisual<ImageCard>(controller.Window.HangingItems.ItemContainerGenerator.ContainerFromIndex(0));var point=card.PointToScreen(new Point(card.ActualWidth/2,80));SetCursorPos((int)point.X,(int)point.Y);Pump(100);
    keybd_event(46,0,0,UIntPtr.Zero);keybd_event(46,0,2,UIntPtr.Zero);timeout.Restart();while(store.Snapshot().Count>0&&timeout.ElapsedMilliseconds<2000)Pump(30);Assert(store.Snapshot().Count==0&&store.HasClipboardSuppression,"悬停删除没有建立删除标记");
    controller.Window.HideLine();Pump(400);
    clipboard.Empty();SendMessage(controller.Window.Handle,0x031d,IntPtr.Zero,IntPtr.Zero);clipboard.External(null);SendMessage(controller.Window.Handle,0x031d,IntPtr.Zero,IntPtr.Zero);Pump(15);
    clipboard.External(image);SendMessage(controller.Window.Handle,0x031d,IntPtr.Zero,IntPtr.Zero);Pump(500);
    Assert(store.Snapshot().Count==0&&store.HasClipboardSuppression&&!controller.Window.IsVisible,"微信短暂文字恢复旧图后，图片或晾衣绳重新出现");
   }finally{keybd_event(46,0,2,UIntPtr.Zero);if(settings!=null)settings.Close();controller.Dispose();SetCursorPos(original.X,original.Y);}
  }
  static void FileCollectionDisplay(){
   if(Application.Current==null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Native.Point original;Native.GetCursorPos(out original);var host=new Window{Title="文件收纳检查",Width=360,Height=180,Left=80,Top=480};host.Show();host.Activate();Pump(100);SetCursorPos(200,550);
   var store=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());store.SetPreferences(true,true);var clipboard=new TestClipboard();var controller=new AppController(Application.Current,store,clipboard);SettingsWindow settings=null;
   try{controller.Initialize().GetAwaiter().GetResult();controller.Window.HideLine();Pump(300);string file=Fixture(Folder());clipboard.ExternalFiles(new[]{file});SendMessage(controller.Window.Handle,0x031d,IntPtr.Zero,IntPtr.Zero);var timeout=System.Diagnostics.Stopwatch.StartNew();while(store.Snapshot().Count==0&&timeout.ElapsedMilliseconds<2000)Pump(30);Pump(150);Assert(store.Snapshot().Count==1&&controller.Window.IsVisible,"文件成功保存后未展开");
    controller.OpenSettings();settings=Application.Current.Windows.OfType<SettingsWindow>().Single();Pump(100);var first=FindVisual<System.Windows.Controls.CheckBox>(settings);var files=((System.Windows.Controls.StackPanel)first.Parent).Children.OfType<System.Windows.Controls.CheckBox>().Single(x=>(string)x.Content=="收纳复制的图片文件");settings.Dispatcher.Invoke(new Action(()=>files.IsChecked=false));timeout.Restart();while((store.CollectFiles||!files.IsEnabled)&&timeout.ElapsedMilliseconds<2000)Pump(30);Assert(!store.CollectFiles,"真实设置回调未关闭文件收纳");string second=Path.Combine(Folder(),"第二张.png");File.WriteAllBytes(second,ImageCodec.Encode(Bitmap(9,8,7)));clipboard.ExternalFiles(new[]{second});SendMessage(controller.Window.Handle,0x031d,IntPtr.Zero,IntPtr.Zero);Pump(100);Assert(store.Snapshot().Count==1,"关闭后仍收纳文件");
    settings.Dispatcher.Invoke(new Action(()=>files.IsChecked=true));timeout.Restart();while((!store.CollectFiles||!files.IsEnabled)&&timeout.ElapsedMilliseconds<2000)Pump(30);SendMessage(controller.Window.Handle,0x031d,IntPtr.Zero,IntPtr.Zero);Pump(100);Assert(store.Snapshot().Count==1,"开启时收纳了原来的文件剪贴板");clipboard.ExternalFiles(new[]{second});SendMessage(controller.Window.Handle,0x031d,IntPtr.Zero,IntPtr.Zero);timeout.Restart();while(store.Snapshot().Count<2&&timeout.ElapsedMilliseconds<2000)Pump(30);Assert(store.Snapshot().Count==2,"重新开启后未收纳新复制文件");
   }finally{if(settings!=null)settings.Close();controller.Dispose();host.Close();SetCursorPos(original.X,original.Y);}
  }
  static void CollectionDisplay(){
   if(Application.Current==null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Native.Point original;Native.GetCursorPos(out original);var host=new Window{Title="收纳展示检查",Width=360,Height=180,Left=80,Top=480};host.Show();host.Activate();Pump(100);SetCursorPos(200,550);
   var store=UiStore();store.SetPreferences(true,true);var clipboard=new TestClipboard();var controller=new AppController(Application.Current,store,clipboard);
   try{
    controller.Initialize().GetAwaiter().GetResult();controller.Window.HideLine();Pump(350);
    clipboard.External(null);SendMessage(controller.Window.Handle,0x031d,IntPtr.Zero,IntPtr.Zero);Pump(150);Assert(!controller.Window.IsVisible,"文字触发了弹出");
    clipboard.External(Bitmap(111,222,33));SendMessage(controller.Window.Handle,0x031d,IntPtr.Zero,IntPtr.Zero);var timeout=System.Diagnostics.Stopwatch.StartNew();while(!controller.Window.IsVisible&&timeout.ElapsedMilliseconds<1500)Pump(30);Assert(controller.Window.IsVisible&&store.Snapshot().Count==3,"图片成功保存后没有展开");Pump(2400);Assert(!controller.Window.IsVisible,"展示结束后未自动收起");
    System.Threading.Tasks.Task copy=null;controller.Window.Dispatcher.Invoke(new Action(()=>copy=controller.CopyAsync(store.Snapshot().First())));timeout.Restart();while(!copy.IsCompleted&&timeout.ElapsedMilliseconds<2000)Pump(30);copy.GetAwaiter().GetResult();SendMessage(controller.Window.Handle,0x031d,IntPtr.Zero,IntPtr.Zero);Pump(100);Assert(!controller.Window.IsVisible&&store.Snapshot().Count==3,"工具自身复制重新弹出了绳子");
   }finally{controller.Dispose();host.Close();SetCursorPos(original.X,original.Y);}
  }
  static void PreviewCloseTuck(){
   if(Application.Current==null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Native.Point original;Native.GetCursorPos(out original);
   var host=new Window{Title="预览收起检查",Width=360,Height=180,Left=80,Top=480};host.Show();host.Activate();Pump(100);
   var store=UiStore();store.SetPreferences(false,true);var controller=new AppController(Application.Current,store,new TestClipboard());
   try{
    controller.Initialize().GetAwaiter().GetResult();controller.Window.HideLine();Pump(400);
    foreach(bool escape in new[]{false,true}){
     SetCursorPos(200,550);Pump(100);SetCursorPos(10,0);Pump(650);
     var card=FindVisual<ImageCard>(controller.Window.HangingItems.ItemContainerGenerator.ContainerFromIndex(0));var p=card.PointToScreen(new Point(card.ActualWidth/2,80));SetCursorPos((int)p.X,(int)p.Y);Pump(150);
     mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(60);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);
     var timeout=System.Diagnostics.Stopwatch.StartNew();while(!Application.Current.Windows.OfType<PreviewWindow>().Any()&&timeout.ElapsedMilliseconds<2000)Pump(50);
     var preview=Application.Current.Windows.OfType<PreviewWindow>().Single();Pump(200);
     if(escape){preview.Activate();keybd_event(27,0,0,UIntPtr.Zero);keybd_event(27,0,2,UIntPtr.Zero);}else{Native.Rectangle rect;Native.GetWindowRect(new WindowInteropHelper(preview).Handle,out rect);SetCursorPos(rect.Right-22,rect.Top+14);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);}
     Pump(200);Assert(!preview.IsVisible,"预览窗口未关闭");SetCursorPos(200,550);Pump(1200);
     Assert(!controller.Window.IsVisible,"关闭预览后没有自动收起");
    }
   }finally{foreach(var preview in Application.Current.Windows.OfType<PreviewWindow>().ToArray())preview.Close();controller.Dispose();host.Close();SetCursorPos(original.X,original.Y);}
  }
  static void CopyRetryFeedback(bool alwaysBusy){
   if(Application.Current==null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var store=UiStore();var record=store.Snapshot().First();byte[] expected=File.ReadAllBytes(store.ImagePath(record.Id));int attempts=0;bool premature=false,valid=true;ClotheslineWindow window=null;
   var clipboard=new WindowsClipboard(data=>{attempts++;premature|=VisibleTexts(window).Any(x=>x.Text.Contains("已复制"));var png=(MemoryStream)data.GetData("PNG",false);valid&=png.Position==0&&png.ToArray().SequenceEqual(expected);png.Position=png.Length;if(alwaysBusy||attempts<3)throw new System.Runtime.InteropServices.ExternalException("测试剪贴板忙碌");});
   var controller=new AppController(Application.Current,store,clipboard);window=controller.Window;window.Show();Pump(700);
   try{System.Threading.Tasks.Task copy=null;window.Dispatcher.Invoke(new Action(()=>copy=controller.CopyAsync(record)));var timeout=System.Diagnostics.Stopwatch.StartNew();while(!copy.IsCompleted&&timeout.ElapsedMilliseconds<3000)Pump(20);Assert(copy.IsCompleted,"剪贴板忙碌重试未结束");copy.GetAwaiter().GetResult();Pump(50);
    Assert(!premature&&valid,"重试提前报告成功或没有提供完整PNG数据");Assert(attempts==(alwaysBusy?4:3),"剪贴板重试次数不正确");Assert(VisibleTexts(window).Any(x=>x.Text.Contains("已复制"))==!alwaysBusy,"复制结果提示与实际结果不符");Assert(store.Snapshot().Count==2,"复制修改了收纳记录");
   }finally{controller.Dispose();}
  }
  static void LargeCopyResponsiveness(){
   if(Application.Current==null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
   int width=3072,height=2048;var pixels=new byte[width*height*4];new Random(27).NextBytes(pixels);
   var image=System.Windows.Media.Imaging.BitmapSource.Create(width,height,96,96,System.Windows.Media.PixelFormats.Bgra32,null,pixels,width*4);image.Freeze();
   var store=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());var record=store.ImportBitmap(image);IDataObject published=null;int publishThread=0,uiThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
   var clipboard=new WindowsClipboard(data=>{published=data;publishThread=System.Threading.Thread.CurrentThread.ManagedThreadId;});var controller=new AppController(Application.Current,store,clipboard);
   var clock=System.Diagnostics.Stopwatch.StartNew();double previous=0,maxGap=0;int ticks=0;var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};
   timer.Tick+=(sender,e)=>{double now=clock.Elapsed.TotalMilliseconds;maxGap=Math.Max(maxGap,now-previous);previous=now;ticks++;};
   try{timer.Start();System.Threading.Tasks.Task copy=null;controller.Window.Dispatcher.Invoke(new Action(()=>copy=controller.CopyAsync(record)));
    while(!copy.IsCompleted&&clock.ElapsedMilliseconds<10000)Pump(10);Assert(copy.IsCompleted,"大图复制未在时限内完成");copy.GetAwaiter().GetResult();Pump(30);timer.Stop();
    Console.WriteLine("MEASURE large copy UI max gap ms: "+maxGap.ToString("F1")+", total ms: "+clock.ElapsedMilliseconds+", ticks: "+ticks);
    Assert(published!=null&&publishThread==uiThread,"剪贴板未在界面线程提交");
    var dataPng=(MemoryStream)published.GetData("PNG",false);Assert(dataPng.ToArray().SequenceEqual(File.ReadAllBytes(store.ImagePath(record.Id))),"复制没有保持保存的PNG数据");
    var bitmap=(System.Windows.Media.Imaging.BitmapSource)published.GetData(DataFormats.Bitmap);Assert(bitmap.PixelWidth==width&&bitmap.PixelHeight==height&&ImageCodec.Hash(bitmap)==ImageCodec.Hash(image),"复制改变了尺寸、颜色或透明度");
    Assert(maxGap<250,"复制准备阻塞界面超过250毫秒："+maxGap.ToString("F1"));
   }finally{timer.Stop();controller.Dispose();}
  }
  static void StartupSmoke(){
   if(Application.Current==null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
   var store=UiStore();store.SetPreferences(false,true);var controller=new AppController(Application.Current,store);
   try{
    controller.Initialize().GetAwaiter().GetResult();Pump(300);Assert(controller.Window.Handle!=IntPtr.Zero&&controller.Window.IsVisible,"程序窗口没有正常启动");
    Assert(controller.Window.HangingItems.Items.Count==2&&!store.Collecting,"启动时丢失图片或未经同意开启收纳");
    Native.Rectangle rectangle;Assert(Native.GetWindowRect(controller.Window.Handle,out rectangle),"无法确认窗口位置");var bounds=System.Windows.Forms.Screen.PrimaryScreen.Bounds;
    Assert(rectangle.Left>=bounds.Left&&rectangle.Right<=bounds.Right&&rectangle.Top>=bounds.Top,"窗口超出主屏幕");
    Console.WriteLine("DISPLAY_SCALE: "+HwndSource.FromHwnd(controller.Window.Handle).CompositionTarget.TransformToDevice.M11.ToString("0.00"));
   }finally{controller.Dispose();}
   Assert(!controller.Window.IsVisible,"关闭后窗口仍显示");
  }
 }
}
