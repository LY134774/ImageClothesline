using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace ImageClothesline.Tests {
 public static partial class TestRunner {
  static void RunUiTests(){
   if(Application.Current==null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
   Test("晾衣绳横跨主屏幕全宽",UiFullWidth);
   Test("十二颗贴绳星星在窄屏和满屏照片时不遮挡图片",UiStarClearance);
   Test("展开时只显示绳子和图片",UiOnlyLine);
   Test("照片超出一屏时提示滚轮并能横向查看",UiOverflowHint);
   Test("设置页文件收纳开关可关闭并恢复",UiFileSetting);
   Test("屏幕顶部两端都可以触发展开",UiEdgeTrigger);
   Test("收起再展开后图片仍可见",UiReshow);
   Test("收起过程中重新展开不会被旧动画隐藏",UiInterruptTuck);
   Test("悬停时按下轻微移动仍可单击复制",UiHoverClick);
   Test("单击立即复制，双击第二下只预览",UiDoubleClick);
   Test("鼠标悬停 Del 只删除当前一张且不传给前台",()=>UiDeleteKey(46));
   Test("鼠标悬停退格键只删除当前一张且移开后正常退格",()=>UiDeleteKey(8));
   Test("设置打开时悬停删除仍生效且忙碌时受保护",UiSettingsDelete);
   Test("复制提示在图片附近短暂显示后消失",UiCopyNotice);
   Test("界面只展示仍挂着的图片",UiCards);
   Test("已保存图片在卡片上可见",UiThumbnail);
   Test("存储变化后界面更新",UiRefresh);
   Test("多图片界面不一次加载全部缩略图",UiVirtualization);
   Test("监测运行时手动收起能够完成",UiTuck);
   Test("拖出在图片上取消后移开仍自动收起",UiDragLeaveTuck);
   Test("外部文件拖入后取消不会阻止自动收起",UiIncomingCancelTuck);
   if(failed==0)RenderUiEvidence();
  }
  static void Pump(int ms=100){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
  static ImageStore UiStore(){var s=new ImageStore(Path.Combine(Folder(),"store"),new TestBin());s.ImportBitmap(Bitmap());s.ImportBitmap(Bitmap(255,120,60));return s;}
  static void UiFullWidth(){var s=UiStore();var w=new ClotheslineWindow(s,new UiActions(s));w.Show();Pump();try{Native.Rectangle rect;Assert(Native.GetWindowRect(w.Handle,out rect),"无法读取窗口范围");var bounds=System.Windows.Forms.Screen.PrimaryScreen.Bounds;Assert(Math.Abs(rect.Left-bounds.Left)<=2&&Math.Abs(rect.Right-bounds.Right)<=2,"绳子窗口没有覆盖主屏幕全宽");}finally{w.Close();}}
  static void UiStarClearance(){
   var s=UiStore();for(int i=0;i<18;i++)s.ImportBitmap(Bitmap((byte)i,120,80));var w=new ClotheslineWindow(s,new UiActions(s));w.Show();
   try{foreach(double width in new[]{800.0,1280.0,2560.0}){w.Width=width;Pump(150);var rope=FindVisual<Rope>(w);var drawing=VisualTreeHelper.GetDrawing(rope);var stars=drawing.Children.OfType<GeometryDrawing>().Where(x=>x.Brush!=null).ToList();Assert(stars.Count==12,"星星数量不是十二颗");var p=w.HangingItems.TranslatePoint(new Point(0,0),rope);var photos=new Rect(p,new Size(w.HangingItems.ActualWidth,w.HangingItems.ActualHeight));photos.Inflate(8,0);foreach(var star in stars){Assert(star.Bounds.Width>=16,"星星显示尺寸太小");Assert(!star.Bounds.IntersectsWith(photos),"星星进入照片显示区域");Assert(star.Bounds.Top>0&&star.Bounds.Bottom<=rope.ActualHeight,"星星被绘制区域裁切");}}}
   finally{w.Close();}
  }
  const string ScrollHintText="↔ 滚动鼠标滚轮，左右查看更多图片";
  static void UiOverflowHint(){
   var s=UiStore();var w=new ClotheslineWindow(s,new UiActions(s));w.Show();Pump(500);
   try{Assert(!VisibleTexts(w).Any(x=>x.Text==ScrollHintText),"未超出一屏也显示提示");for(int i=0;i<20;i++)s.ImportBitmap(Bitmap((byte)i,90,80));Pump(500);var hint=VisibleTexts(w).SingleOrDefault(x=>x.Text==ScrollHintText);Assert(hint!=null,"超出一屏没有滚轮提示");var viewer=FindVisual<ScrollViewer>(w.HangingItems);double before=viewer.HorizontalOffset;w.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=System.Windows.Input.Mouse.PreviewMouseWheelEvent});Pump(100);Assert(viewer.HorizontalOffset>before,"滚轮没有横向移动照片");
    var visibleRecord=w.HangingItems.Items.Cast<ImageRecord>().First(x=>FindVisual<ImageCard>(w.HangingItems.ItemContainerGenerator.ContainerFromItem(x))!=null);w.ShowCopied(visibleRecord.Id);Pump(100);var copied=VisibleTexts(w).Single(x=>x.Text.Contains("已复制"));var copyRect=copied.TransformToAncestor(w).TransformBounds(new Rect(copied.RenderSize));var hintRect=hint.TransformToAncestor(w).TransformBounds(new Rect(hint.RenderSize));Assert(!copyRect.IntersectsWith(hintRect),"已复制提示与滚轮提示重叠");SaveRender(w,Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","artifacts","overflow-preview.png")),1);
    w.ShowNotice("操作提示测试");Pump(100);Assert(!VisibleTexts(w).Any(x=>x.Text==ScrollHintText),"操作提示没有优先显示");Pump(3200);Assert(VisibleTexts(w).Any(x=>x.Text==ScrollHintText),"操作提示结束后滚轮提示没有恢复");foreach(var item in s.Snapshot().Skip(1))s.CompleteExport(item.Id,true);Pump(300);Assert(!VisibleTexts(w).Any(x=>x.Text==ScrollHintText),"图片减少后提示未隐藏");
   }finally{w.Close();}
  }
  static void UiFileSetting(){
   var s=UiStore();s.SetPreferences(true,true);var w=new SettingsWindow(s,b=>{s.SetPreferences(b,true);return Task.FromResult(0);},b=>{s.SetFileCollection(b);return Task.FromResult(0);},()=>Task.FromResult(0),()=>{});w.Show();Pump(150);
   try{var first=FindVisual<CheckBox>(w);var boxes=((StackPanel)first.Parent).Children.OfType<CheckBox>().ToList();var files=boxes.Single(x=>(string)x.Content=="收纳复制的图片文件");Assert(files.IsChecked==true,"设置页默认未开启");files.IsChecked=false;Pump(100);Assert(!s.CollectFiles&&s.Collecting,"设置页关闭未保存或影响总开关");files.IsChecked=true;Pump(100);Assert(s.CollectFiles,"设置页不能重新开启");}
   finally{w.Close();}
  }
  static void UiOnlyLine(){var s=UiStore();var w=new ClotheslineWindow(s,new UiActions(s));w.Show();Pump(500);try{var texts=VisibleTexts(w);Assert(!texts.Any(x=>x.Text=="图片晾衣绳"||x.Text=="设置"||x.Text=="收起"||x.Text=="单击复制 · 双击预览 · 拖出使用"),"展开时仍显示了工具栏或常驻说明栏");}finally{w.Close();}}
  static System.Collections.Generic.List<TextBlock> VisibleTexts(DependencyObject root){var result=new System.Collections.Generic.List<TextBlock>();if(root==null)return result;var text=root as TextBlock;if(text!=null&&text.IsVisible)result.Add(text);for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)result.AddRange(VisibleTexts(VisualTreeHelper.GetChild(root,i)));return result;}
  [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
  static void UiEdgeTrigger(){Native.Point original;Native.GetCursorPos(out original);var bounds=System.Windows.Forms.Screen.PrimaryScreen.Bounds;var host=new Window{Title="晾衣绳交互检查",Width=320,Height=180,Left=bounds.Left+80,Top=bounds.Top+400,WindowStartupLocation=WindowStartupLocation.Manual};host.Show();host.Activate();Pump(100);var s=UiStore();var w=new ClotheslineWindow(s,new UiActions(s));w.StartWatching();try{Assert(!Native.IsFullscreen(w.Handle),"测试前台仍为全屏窗口，不能验证普通桌面触发");foreach(int x in new[]{bounds.Left+5,bounds.Right-6}){SetCursorPos(bounds.Left+bounds.Width/2,bounds.Top+400);Pump(100);SetCursorPos(x,bounds.Top);Pump(450);Assert(w.IsVisible,"顶部边缘不能展开晾衣绳");w.HideLine();Pump(400);}}finally{w.Close();host.Close();SetCursorPos(original.X,original.Y);}}
  static void UiReshow(){var s=UiStore();var w=new ClotheslineWindow(s,new UiActions(s));w.Dispatcher.Invoke(new Action(w.ShowLine));Pump(700);w.Dispatcher.Invoke(new Action(w.HideLine));Pump(450);w.Dispatcher.Invoke(new Action(w.ShowLine));Pump(700);try{var card=FindVisual<ImageCard>(w.HangingItems.ItemContainerGenerator.ContainerFromIndex(0));var picture=FindVisual<Image>(card);Assert(picture!=null&&picture.Source!=null,"收起再展开后缩略图变空白");}finally{w.Close();}}
  static void UiInterruptTuck(){var s=UiStore();var w=new ClotheslineWindow(s,new UiActions(s));w.Dispatcher.Invoke(new Action(w.ShowLine));Pump(700);w.Dispatcher.Invoke(new Action(w.HideLine));Pump(70);w.Dispatcher.Invoke(new Action(w.ShowLine));Pump(700);try{Assert(w.IsVisible,"旧收起动画隐藏了重新展开的窗口");var card=FindVisual<ImageCard>(w.HangingItems.ItemContainerGenerator.ContainerFromIndex(0));var picture=FindVisual<Image>(card);Assert(picture!=null&&picture.Source!=null,"重新展开后图片不可见");var point=picture.TranslatePoint(new Point(0,0),w);Assert(point.Y>=0&&point.Y<w.ActualHeight,"重新展开后图片仍停在屏幕外");}finally{w.Close();}}
  [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
  static void UiHoverClick(){
   Native.Point original;Native.GetCursorPos(out original);var s=UiStore();var actions=new UiActions(s);var w=new ClotheslineWindow(s,actions);w.Show();Pump(800);
   try{
    var card=FindVisual<ImageCard>(w.HangingItems.ItemContainerGenerator.ContainerFromIndex(0));var target=card.PointToScreen(new Point(card.ActualWidth/2,80));
    foreach(int delay in new[]{40,260}){
     SetCursorPos((int)target.X,400);Pump(250);SetCursorPos((int)target.X,(int)target.Y);Pump(delay);int before=actions.Copies;
     // Release from a worker so even an accidental native drag loop cannot trap the test.
     var release=Task.Run(()=>{System.Threading.Thread.Sleep(80);SetCursorPos((int)target.X+1,(int)target.Y+1);System.Threading.Thread.Sleep(80);mouse_event(4,0,0,0,UIntPtr.Zero);});
     mouse_event(2,0,0,0,UIntPtr.Zero);Pump((int)Native.GetDoubleClickTime()+600);release.GetAwaiter().GetResult();
     Assert(actions.Copies==before+1,"小幅移动被误判为拖出，单击未复制");Assert(s.Snapshot().All(x=>x.Hanging),"小幅移动错误取下了卡片");
    }
   }finally{mouse_event(4,0,0,0,UIntPtr.Zero);w.Close();SetCursorPos(original.X,original.Y);}
  }
  static void UiDoubleClick(){
   Native.Point original;Native.GetCursorPos(out original);var s=UiStore();var actions=new UiActions(s);var w=new ClotheslineWindow(s,actions);w.Show();Pump(800);
   try{var card=FindVisual<ImageCard>(w.HangingItems.ItemContainerGenerator.ContainerFromIndex(0));var p=card.PointToScreen(new Point(card.ActualWidth/2,80));SetCursorPos((int)p.X,(int)p.Y);Pump(200);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(60);Assert(actions.Copies==1,"单击仍在等待双击判定，未立即复制");mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Pump((int)Native.GetDoubleClickTime()+200);Assert(actions.Previews==1&&actions.Copies==1,"双击第二下重复复制或未预览");}finally{w.Close();SetCursorPos(original.X,original.Y);}
  }
  static void UiDeleteKey(byte key){
   Native.Point original;Native.GetCursorPos(out original);var input=new TextBox{Text="abc",Margin=new Thickness(20)};var host=new Window{Title="Del 键检查",Width=360,Height=180,Left=80,Top=400,Content=input};host.Show();host.Activate();input.Focus();input.CaretIndex=1;Pump(100);
   var s=UiStore();var actions=new UiActions(s);var w=new ClotheslineWindow(s,actions);actions.Window=w;w.StartWatching();w.ShowLine();Pump(700);
   try{
    var card=FindVisual<ImageCard>(w.HangingItems.ItemContainerGenerator.ContainerFromIndex(0));var p=card.PointToScreen(new Point(card.ActualWidth/2,80));SetCursorPos((int)p.X,(int)p.Y);Pump(100);
    keybd_event(17,0,0,UIntPtr.Zero);keybd_event(key,0,0,UIntPtr.Zero);keybd_event(key,0,2,UIntPtr.Zero);keybd_event(17,0,2,UIntPtr.Zero);Pump(100);Assert(s.Snapshot().Count==2,"Ctrl 组合键错误删除了图片");input.Text="abc";input.CaretIndex=1;
    keybd_event(key,0,0,UIntPtr.Zero);Pump(150);keybd_event(key,0,0,UIntPtr.Zero);Pump(150);keybd_event(key,0,2,UIntPtr.Zero);Pump(100);
    Assert(s.Snapshot().Count==1&&actions.SilentRemovals==1,"Del 未静默删除当前图片或长按删除了多张");Assert(input.Text=="abc","图片删除键同时影响了前台输入框");
    SetCursorPos(200,480);Pump(80);keybd_event(key,0,0,UIntPtr.Zero);keybd_event(key,0,2,UIntPtr.Zero);Pump(150);Assert(input.Text==(key==8?"bc":"ac")&&s.Snapshot().Count==1,"指针移开后 Del 没有正常交还前台");
   }finally{keybd_event(key,0,2,UIntPtr.Zero);w.Close();host.Close();SetCursorPos(original.X,original.Y);}
  }
  static void UiSettingsDelete(){
   Native.Point original;Native.GetCursorPos(out original);var s=UiStore();s.SetPreferences(true,true);var controller=new AppController(Application.Current,s);var w=controller.Window;Window settings=null;
   try{w.StartWatching();w.ShowLine();controller.OpenSettings();Pump(700);settings=Application.Current.Windows.Cast<Window>().Single(x=>x is SettingsWindow);
    var card=FindVisual<ImageCard>(w.HangingItems.ItemContainerGenerator.ContainerFromIndex(0));var p=card.PointToScreen(new Point(card.ActualWidth/2,80));SetCursorPos((int)p.X,(int)p.Y);Pump(100);
    w.HoldOpen(true);keybd_event(8,0,0,UIntPtr.Zero);keybd_event(8,0,2,UIntPtr.Zero);Pump(100);Assert(s.Snapshot().Count==2,"忙碌操作期间错误删除照片");w.HoldOpen(false);
    keybd_event(8,0,0,UIntPtr.Zero);keybd_event(8,0,2,UIntPtr.Zero);Pump(600);Assert(s.Snapshot().Count==1&&w.HangingItems.Items.Count==1,"设置打开时退格键不能删除悬停照片");
    settings.Close();settings=null;SetCursorPos(200,450);Pump(3300);Assert(!w.IsVisible,"设置关闭后不能正常收起");
   }finally{keybd_event(8,0,2,UIntPtr.Zero);if(settings!=null)settings.Close();controller.Dispose();SetCursorPos(original.X,original.Y);}
  }
  static void UiCopyNotice(){var s=UiStore();var w=new ClotheslineWindow(s,new UiActions(s));w.Show();Pump(700);try{var record=s.Snapshot().First();var card=FindVisual<ImageCard>(w.HangingItems.ItemContainerGenerator.ContainerFromIndex(0));w.ShowCopied(record.Id);Pump(50);Assert(VisibleTexts(card).Any(x=>x.Text.Contains("已复制")),"复制提示没有显示在对应图片上");Pump(1400);Assert(!VisibleTexts(card).Any(x=>x.Text.Contains("已复制")),"复制提示没有自动消失");}finally{w.Close();}}
  static void UiCards(){var s=UiStore();s.CompleteExport(s.Snapshot().First().Id,true);var w=new ClotheslineWindow(s,new UiActions(s));w.Show();Pump();try{Assert(w.HangingItems.Items.Count==1,"界面没有只显示挂起的图片");}finally{w.Close();}}
  static void UiThumbnail(){var s=UiStore();var w=new ClotheslineWindow(s,new UiActions(s));w.Show();Pump(800);try{var container=w.HangingItems.ItemContainerGenerator.ContainerFromIndex(0);var card=FindVisual<ImageCard>(container);Assert(card!=null,"可见卡片没有生成");var picture=FindVisual<Image>(card);Assert(picture!=null&&picture.Source!=null,"卡片未显示已保存的缩略图");}finally{w.Close();}}
  static T FindVisual<T>(DependencyObject root) where T:DependencyObject {if(root==null)return null;if(root is T)return (T)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=FindVisual<T>(VisualTreeHelper.GetChild(root,i));if(child!=null)return child;}return null;}
  static void UiRefresh(){var s=UiStore();var w=new ClotheslineWindow(s,new UiActions(s));w.Show();Pump();try{s.ImportBitmap(Bitmap(70,80,90));Pump();Assert(w.HangingItems.Items.Count==3,"加入后界面没有更新");}finally{w.Close();}}
  static void UiVirtualization(){var s=UiStore();for(int i=0;i<35;i++)s.ImportBitmap(Bitmap((byte)i,100,100));var w=new ClotheslineWindow(s,new UiActions(s));w.Show();Pump(500);try{int loaded=0;for(int i=0;i<w.HangingItems.Items.Count;i++)if(w.HangingItems.ItemContainerGenerator.ContainerFromIndex(i)!=null)loaded++;Assert(w.HangingItems.Items.Count==37&&loaded<20,"缩略图未按可见区域加载");}finally{w.Close();}}
  static void UiTuck(){var s=UiStore();var w=new ClotheslineWindow(s,new UiActions(s));w.StartWatching();w.ShowLine();Pump(300);try{w.HideLine();Pump(700);Assert(w.Visibility!=Visibility.Visible,"持续监测反复重启动画，窗口未收起");}finally{w.Close();}}
  [DllImport("user32.dll")]static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
  static void UiDragLeaveTuck(){
   Native.Point original;Native.GetCursorPos(out original);var host=new Window{Title="拖出检查",Width=360,Height=180,Left=80,Top=400};host.Show();host.Activate();Pump(100);
   var s=UiStore();var actions=new UiActions(s);var w=new ClotheslineWindow(s,actions);actions.Window=w;w.StartWatching();
   try{
    SetCursorPos(10,400);Pump(80);SetCursorPos(10,0);Pump(600);Assert(w.IsVisible,"测试展开失败");
    var card=FindVisual<ImageCard>(w.HangingItems.ItemContainerGenerator.ContainerFromIndex(0));var target=card.PointToScreen(new Point(card.ActualWidth/2,80));SetCursorPos((int)target.X,(int)target.Y);Pump(300);
    var release=Task.Run(()=>{System.Threading.Thread.Sleep(100);SetCursorPos((int)target.X,(int)target.Y+20);System.Threading.Thread.Sleep(200);keybd_event(27,0,0,UIntPtr.Zero);keybd_event(27,0,2,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);System.Threading.Thread.Sleep(150);SetCursorPos(200,450);});
    mouse_event(2,0,0,0,UIntPtr.Zero);Pump(2200);release.GetAwaiter().GetResult();
    Assert(actions.Drags>0,"测试未触发实际拖出");Assert(s.Snapshot().All(x=>x.Hanging),"取消拖出丢失图片");
    Assert(!w.IsVisible,"鼠标已离开且拖动结束，保持展开状态仍未释放");
   }finally{mouse_event(4,0,0,0,UIntPtr.Zero);w.Close();host.Close();SetCursorPos(original.X,original.Y);}
  }
  static void UiIncomingCancelTuck(){
   Native.Point original;Native.GetCursorPos(out original);var dragSource=new Border{Background=Brushes.White};var host=new Window{Title="拖入检查",Width=360,Height=180,Left=80,Top=400,Content=dragSource};host.Show();host.Activate();Pump(100);
   var s=UiStore();var actions=new UiActions(s);var w=new ClotheslineWindow(s,actions);actions.Window=w;w.StartWatching();bool dragged=false;
   dragSource.MouseLeftButtonDown+=(sender,e)=>{var data=new DataObject(DataFormats.FileDrop,new[]{s.ImagePath(s.Snapshot().First().Id)});DragDrop.DoDragDrop(dragSource,data,DragDropEffects.Copy);dragged=true;};
   try{
    SetCursorPos(10,400);Pump(80);SetCursorPos(10,0);Pump(600);var card=FindVisual<ImageCard>(w.HangingItems.ItemContainerGenerator.ContainerFromIndex(0));var target=card.PointToScreen(new Point(card.ActualWidth/2,80));SetCursorPos(180,480);Pump(50);
    var release=Task.Run(()=>{System.Threading.Thread.Sleep(200);SetCursorPos((int)target.X,(int)target.Y);System.Threading.Thread.Sleep(250);keybd_event(27,0,0,UIntPtr.Zero);keybd_event(27,0,2,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);System.Threading.Thread.Sleep(150);SetCursorPos(200,450);});
    mouse_event(2,0,0,0,UIntPtr.Zero);Pump(2300);release.GetAwaiter().GetResult();Assert(dragged,"没有执行外部拖入");Assert(!w.IsVisible,"取消外部拖入后保持展开状态未释放");
   }finally{mouse_event(4,0,0,0,UIntPtr.Zero);w.Close();host.Close();SetCursorPos(original.X,original.Y);}
  }
  static void RenderUiEvidence(){
   string folder=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","artifacts"));Directory.CreateDirectory(folder);
   var store=new ImageStore(Path.Combine(Folder(),"demo-store"),new TestBin());
   store.ImportBitmap(ExampleImage(0));store.ImportBitmap(ExampleImage(1));store.ImportBitmap(ExampleImage(2));store.SetPreferences(true,true);
   var w=new ClotheslineWindow(store,new UiActions(store));w.Show();Pump(1000);SaveRender(w,Path.Combine(folder,"clothesline-preview.png"),1);
   foreach(double scale in new[]{1.25,1.5,2.0})SaveRender(w,Path.Combine(folder,"clothesline-"+(scale*100).ToString("0")+".png"),scale);
   RenderMotion(w,store,Path.Combine(folder,"clothesline-motion.gif"));
   w.Close();
   var welcome=new WelcomeWindow();welcome.Show();Pump();SaveRender(welcome,Path.Combine(folder,"welcome-preview.png"),1);welcome.Close();
   store.CompleteExport(store.Snapshot().Last().Id,true);
   var settings=new SettingsWindow(store,b=>{store.SetPreferences(b,true);return Task.FromResult(0);},b=>{store.SetFileCollection(b);return Task.FromResult(0);},()=>Task.FromResult(0),()=>{});settings.Show();Pump();SaveRender(settings,Path.Combine(folder,"settings-preview.png"),1);settings.Close();
   var record=store.Snapshot().First();var preview=new PreviewWindow(record,ImageCodec.ReadFile(store.ImagePath(record.Id)));preview.Show();Pump();SaveRender(preview,Path.Combine(folder,"image-preview.png"),1);preview.Close();
  }
  static BitmapSource ExampleImage(int kind){
   var visual=new DrawingVisual();using(var d=visual.RenderOpen()){
    d.DrawRectangle(Ui.Brush(kind==0?"#DCE6E0":kind==1?"#F6EDD6":"#E8EDF2"),null,new Rect(0,0,640,420));
    if(kind==0){
     d.DrawEllipse(Ui.Brush("#F0C683"),null,new Point(500,105),50,50);
     var mountain=new StreamGeometry();using(var c=mountain.Open()){c.BeginFigure(new Point(0,310),true,true);c.LineTo(new Point(210,135),true,false);c.LineTo(new Point(350,285),true,false);c.LineTo(new Point(505,180),true,false);c.LineTo(new Point(640,300),true,false);c.LineTo(new Point(640,420),true,false);c.LineTo(new Point(0,420),true,false);}d.DrawGeometry(Ui.Brush("#628676"),null,mountain);
     d.DrawRoundedRectangle(Ui.Brush("#F9F9EE"),null,new Rect(28,24,168,40),8,8);DrawText(d,"周末的小风景",46,33,22,"#365548");
    }else if(kind==1){
     DrawText(d,"今天的小计划",44,40,34,"#735A35");DrawText(d,"做好一件事，也很好。",44,96,20,"#9A886E");
     string[] labels={"整理桌面里的资料","回复需要处理的消息","留一点时间给自己"};for(int i=0;i<3;i++){d.DrawRoundedRectangle(Ui.Brush("#FFFDF7"),null,new Rect(38,150+i*76,562,59),8,8);d.DrawRoundedRectangle(null,new Pen(Ui.Brush("#B49E77"),2),new Rect(58,168+i*76,22,22),4,4);DrawText(d,labels[i],99,163+i*76,24,"#715E42");}
    }else{
     DrawText(d,"项目进度",44,35,32,"#334F64");DrawText(d,"一张随手留下的工作截图",44,86,19,"#7F93A0");
     for(int i=0;i<4;i++){double h=90+i*35;d.DrawRoundedRectangle(Ui.Brush(i==3?"#E5B675":"#89AAA8"),null,new Rect(73+i*130,350-h,75,h),6,6);DrawText(d,"阶段 "+(i+1),72+i*130,370,17,"#687E8C");}
    }
   }
   var bitmap=new RenderTargetBitmap(640,420,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
  }
  static void DrawText(DrawingContext d,string value,double x,double y,double size,string color){d.DrawText(new FormattedText(value,System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,Ui.Brush(color),1.0),new Point(x,y));}
  static BitmapSource CaptureScene(Window window,double scale){
   var element=(FrameworkElement)window.Content;element.UpdateLayout();bool line=window is ClotheslineWindow;double width=line?window.ActualWidth:element.ActualWidth+64,height=line?320:element.ActualHeight+72;
   var visual=new DrawingVisual();using(var d=visual.RenderOpen()){
    var background=new LinearGradientBrush(Color.FromRgb(232,233,254),Color.FromRgb(249,231,234),0);d.DrawRectangle(background,null,new Rect(0,0,width,height));
    if(window.IsVisible){double x=line?0:32,y=line?0:26;var viewport=new Rect(0,0,line?window.ActualWidth:element.ActualWidth,line?window.ActualHeight:element.ActualHeight);var brush=new VisualBrush(line?(Visual)window:element){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=viewport,Stretch=Stretch.Fill};d.PushClip(new RectangleGeometry(new Rect(x,y,viewport.Width,viewport.Height)));d.DrawRectangle(brush,null,new Rect(x,y,viewport.Width,viewport.Height));d.Pop();}
   }
   var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width*scale),(int)Math.Ceiling(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
  }
  static void SaveRender(Window window,string filename,double scale){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(CaptureScene(window,scale)));using(var output=File.Create(filename))encoder.Save(output);}
  static readonly System.Collections.Generic.List<ushort> motionDelays=new System.Collections.Generic.List<ushort>();
  static void AddMotionFrame(GifBitmapEncoder encoder,Window window,ushort delay){motionDelays.Add(delay);var metadata=new BitmapMetadata("gif");metadata.SetQuery("/grctlext/Delay",delay);metadata.SetQuery("/grctlext/Disposal",(byte)2);encoder.Frames.Add(BitmapFrame.Create(CaptureScene(window,.5),null,metadata,null));}
  // WPF's GIF encoder drops frame delays on this runtime; restore them in the GIF control blocks.
  static void SetGifDelays(byte[] gif){
   int p=13+((gif[10]&128)==0?0:3*(1<<((gif[10]&7)+1))),frame=0;
   while(p<gif.Length){byte marker=gif[p++];if(marker==0x3b)break;
    if(marker==0x21){byte label=gif[p++];if(label==0xf9){if(frame>=motionDelays.Count||gif[p]!=4)throw new IOException("GIF 帧结构异常");ushort delay=motionDelays[frame++];gif[p+2]=(byte)(delay&255);gif[p+3]=(byte)(delay>>8);}
     while(gif[p]!=0){int count=gif[p];p+=count+1;}p++;
    }else if(marker==0x2c){byte packed=gif[p+8];p+=9;if((packed&128)!=0)p+=3*(1<<((packed&7)+1));p++;while(gif[p]!=0){int count=gif[p];p+=count+1;}p++;}
    else throw new IOException("GIF 区块异常");
   }
   if(frame!=motionDelays.Count)throw new IOException("GIF 动画帧数量不符");
  }
  static void RenderMotion(ClotheslineWindow window,ImageStore store,string filename){
   motionDelays.Clear();var encoder=new GifBitmapEncoder();window.Dispatcher.Invoke(new Action(window.HideLine));Pump(300);AddMotionFrame(encoder,window,60);
   window.Dispatcher.Invoke(new Action(window.ShowLine));for(int i=0;i<16;i++){Pump(30);AddMotionFrame(encoder,window,4);}Pump(300);AddMotionFrame(encoder,window,70);
   Native.Point original;Native.GetCursorPos(out original);
   try{
    var card=FindVisual<ImageCard>(window.HangingItems.ItemContainerGenerator.ContainerFromIndex(1));var point=card.PointToScreen(new Point(card.ActualWidth/2,80));
    SetCursorPos((int)point.X,(int)point.Y);for(int i=0;i<7;i++){Pump(30);AddMotionFrame(encoder,window,4);}Pump(100);AddMotionFrame(encoder,window,70);
    string hoverFile=Path.Combine(Path.GetDirectoryName(filename),"clothesline-hover.png");SaveRender(window,hoverFile,1);
    var bounds=System.Windows.Forms.Screen.PrimaryScreen.Bounds;SetCursorPos(bounds.Left+20,bounds.Top+400);for(int i=0;i<7;i++){Pump(30);AddMotionFrame(encoder,window,4);}
   }finally{SetCursorPos(original.X,original.Y);}
   window.ShowCopied(store.Snapshot()[1].Id);for(int i=0;i<8;i++){Pump(30);AddMotionFrame(encoder,window,4);}SaveRender(window,Path.Combine(Path.GetDirectoryName(filename),"clothesline-copied.png"),1);AddMotionFrame(encoder,window,70);Pump(1250);AddMotionFrame(encoder,window,40);
   window.Dispatcher.Invoke(new Action(window.HideLine));for(int i=0;i<9;i++){Pump(25);AddMotionFrame(encoder,window,3);}AddMotionFrame(encoder,window,60);
   using(var stream=new MemoryStream()){encoder.Save(stream);byte[] gif=stream.ToArray();SetGifDelays(gif);int table=(gif[10]&128)==0?0:3*(1<<((gif[10]&7)+1));int insert=13+table;byte[] loop={0x21,0xff,0x0b,0x4e,0x45,0x54,0x53,0x43,0x41,0x50,0x45,0x32,0x2e,0x30,0x03,0x01,0,0,0};using(var output=File.Create(filename)){output.Write(gif,0,insert);output.Write(loop,0,loop.Length);output.Write(gif,insert,gif.Length-insert);}}
  }


 }
 public sealed class UiActions:IImageActions {
  public int Copies,Drags,Previews,SilentRemovals;public ClotheslineWindow Window;readonly ImageStore store;public UiActions(ImageStore store){this.store=store;}
  public Task CopyAsync(ImageRecord r){Copies++;return Task.FromResult(0);}public void Preview(ImageRecord r){Previews++;}
  public Task RemoveAsync(ImageRecord r,bool confirm=true){if(!confirm)SilentRemovals++;store.Remove(r.Id);return Task.FromResult(0);}
  public Task FinishDragAsync(ImageRecord r,DragDropEffects effect){Drags++;store.CompleteExport(r.Id,effect==DragDropEffects.Copy);return Task.FromResult(0);}
  public Task ImportFilesAsync(string[] files){foreach(var f in files)store.ImportFile(f);return Task.FromResult(0);}
  public void HoldOpen(bool hold){if(Window!=null)Window.HoldOpen(hold);}public void OpenSettings(){}
 }
}
