using System;
using System.IO;
namespace ImageClothesline.Tests {
 public static partial class TestRunner {
  static void RunAutoStartTests(){
   Test("首次运行与旧版升级默认登记开机启动",AutoStartDefaults);
   Test("关闭开机启动后重开仍关闭且图片设置保留",AutoStartOptOut);
   Test("移动后更新启动路径且不增加重复项",AutoStartMove);
   Test("启动项写入失败不改变用户选择",AutoStartRegistryFailure);
   Test("设置保存失败回滚启动项",AutoStartSaveFailure);
   Test("自动启动不改写启动项且不恢复关闭选择",AutoStartBackground);
   Test("拒绝超长启动命令且允许关闭",AutoStartLongPath);
  }
  sealed class StartupEntry {
   public string Value;public bool Fail;public int Writes;
   public string Read(){return Value;}
   public void Write(string value){if(Fail)throw new UnauthorizedAccessException("测试启动项不可写");Value=value;Writes++;}
  }
  static void AutoStartDefaults(){
   foreach(bool legacy in new[]{false,true}){
    string folder=Folder();if(legacy)File.WriteAllText(Path.Combine(folder,"state.json"),"{\"Version\":1,\"Collecting\":false,\"NoticeAcknowledged\":true,\"Images\":[]}");
    var store=new ImageStore(folder,new TestBin());var entry=new StartupEntry();var manager=new StartupManager(store,entry.Read,entry.Write,@"C:\工具 图片\ImageClothesline.exe");manager.Initialize(false);
    Assert(manager.Enabled&&entry.Value=="\"C:\\工具 图片\\ImageClothesline.exe\" --startup","默认启动项缺失或中文空格路径未加引号");Assert(!store.Collecting,"开机启动擅自开启图片收纳");
   }
  }
  static void AutoStartOptOut(){
   var store=new ImageStore(Folder(),new TestBin());store.SetPreferences(false,true);var photo=store.ImportBitmap(Bitmap());var entry=new StartupEntry();var manager=new StartupManager(store,entry.Read,entry.Write,@"C:\App\ImageClothesline.exe");manager.Initialize(false);manager.SetEnabled(false);
   var restored=new ImageStore(store.Root,new TestBin());var again=new StartupManager(restored,entry.Read,entry.Write,@"C:\App\ImageClothesline.exe");again.Initialize(false);Assert(!again.Enabled&&entry.Value==null&&!restored.StartWithWindows,"关闭选择未保存或重新登记启动项");Assert(!restored.Collecting&&restored.Snapshot()[0].Id==photo.Id,"修改启动设置影响了图片数据");again.SetEnabled(true);Assert(again.Enabled&&new ImageStore(store.Root,new TestBin()).StartWithWindows,"重新开启未保存");
  }
  static void AutoStartMove(){
   var store=new ImageStore(Folder(),new TestBin());var entry=new StartupEntry{Value="\"C:\\Old\\ImageClothesline.exe\" --startup"};var manager=new StartupManager(store,entry.Read,entry.Write,@"D:\新 文件夹\ImageClothesline.exe");manager.Initialize(false);Assert(entry.Value=="\"D:\\新 文件夹\\ImageClothesline.exe\" --startup","移动后未更新路径");int writes=entry.Writes;manager.Initialize(false);Assert(entry.Writes==writes,"启动项相同时仍重复改写");
  }
  static void AutoStartRegistryFailure(){
   var store=new ImageStore(Folder(),new TestBin());store.SetStartWithWindows(false);var entry=new StartupEntry{Fail=true};var manager=new StartupManager(store,entry.Read,entry.Write,@"C:\App\ImageClothesline.exe");manager.Initialize(false);bool caught=false;try{manager.SetEnabled(true);}catch(UnauthorizedAccessException){caught=true;}Assert(caught&&!manager.Enabled&&!store.StartWithWindows&&!new ImageStore(store.Root,new TestBin()).StartWithWindows,"启动项失败却保存或显示成功");
  }
  static void AutoStartSaveFailure(){
   var store=new ImageStore(Folder(),new TestBin());store.SetStartWithWindows(false);var entry=new StartupEntry();var manager=new StartupManager(store,entry.Read,entry.Write,@"C:\App\ImageClothesline.exe");manager.Initialize(false);Directory.CreateDirectory(Path.Combine(store.Root,"state.json.pending"));bool caught=false;try{manager.SetEnabled(true);}catch(Exception){caught=true;}Assert(caught&&!manager.Enabled&&entry.Value==null&&!store.StartWithWindows,"设置失败未恢复启动项和开关");
  }
  static void AutoStartBackground(){
   var store=new ImageStore(Folder(),new TestBin());var entry=new StartupEntry{Value="\"C:\\App\\ImageClothesline.exe\" --startup"};var manager=new StartupManager(store,entry.Read,entry.Write,@"C:\App\ImageClothesline.exe");manager.Initialize(true);Assert(manager.Enabled&&entry.Writes==0,"自动启动时改写了启动项");store.SetStartWithWindows(false);manager.Initialize(true);Assert(!manager.Enabled&&entry.Writes==0,"自动启动覆盖了关闭选择");
  }
  static void AutoStartLongPath(){
   var store=new ImageStore(Folder(),new TestBin());var entry=new StartupEntry();var manager=new StartupManager(store,entry.Read,entry.Write,@"C:\"+new string('a',250)+@"\ImageClothesline.exe");bool caught=false;try{manager.Initialize(false);}catch(NotSupportedException){caught=true;}Assert(caught&&entry.Value==null&&!manager.Enabled,"超长启动命令没有明确拒绝");manager.SetEnabled(false);Assert(!store.StartWithWindows,"超长路径无法关闭开机启动");
  }
  static System.Windows.Controls.CheckBox StartupCheck(System.Windows.DependencyObject parent){
   if(parent==null)return null;var box=parent as System.Windows.Controls.CheckBox;if(box!=null&&(string)box.Content=="开机自动启动")return box;
   for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++){var found=StartupCheck(System.Windows.Media.VisualTreeHelper.GetChild(parent,i));if(found!=null)return found;}return null;
  }
  static void AutoStartUi(){
   if(System.Windows.Application.Current==null)new System.Windows.Application{ShutdownMode=System.Windows.ShutdownMode.OnExplicitShutdown};
   var store=new ImageStore(Folder(),new TestBin());store.SetPreferences(false,true);var entry=new StartupEntry{Value="\"C:\\App\\ImageClothesline.exe\" --startup"};var manager=new StartupManager(store,entry.Read,entry.Write,@"C:\App\ImageClothesline.exe");
   var controller=new AppController(System.Windows.Application.Current,store,new TestClipboard(),manager);SettingsWindow settings=null;
   try{
    controller.Initialize(true).GetAwaiter().GetResult();
    Assert(!controller.Window.IsVisible,"开机启动自动展开了绳子");
    controller.OpenSettings();settings=System.Linq.Enumerable.Single(System.Linq.Enumerable.OfType<SettingsWindow>(System.Windows.Application.Current.Windows));Pump(100);
    var box=StartupCheck(settings);Assert(box!=null&&box.IsChecked==true,"设置中没有默认开启的开机启动开关");
    box.IsChecked=false;var timer=System.Diagnostics.Stopwatch.StartNew();while((!box.IsEnabled||store.StartWithWindows)&&timer.ElapsedMilliseconds<2000)Pump(20);
    Assert(entry.Value==null&&!store.StartWithWindows&&box.IsChecked==false,"设置关闭后启动项未移除");
    box.IsChecked=true;timer.Restart();while((!box.IsEnabled||!store.StartWithWindows)&&timer.ElapsedMilliseconds<2000)Pump(20);
    Assert(entry.Value=="\"C:\\App\\ImageClothesline.exe\" --startup"&&box.IsChecked==true,"设置重新开启没有登记启动项");
    entry.Fail=true;box.IsChecked=false;timer.Restart();while(!box.IsEnabled&&timer.ElapsedMilliseconds<2000)Pump(20);Pump(30);
    Assert(box.IsChecked==true&&store.StartWithWindows,"登记失败后开关没有恢复");Assert(System.Linq.Enumerable.Any(VisibleTexts(controller.Window),x=>x.Text.Contains("开机启动")),"开机启动失败提示没有指出真实操作");
    SaveRender(settings,Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","artifacts","autostart-settings-preview.png")),1);
   }finally{if(settings!=null)settings.Close();controller.Dispose();}
  }

 }
}
