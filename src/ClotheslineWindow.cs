using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace ImageClothesline {
 public sealed class ClotheslineWindow:Window {
  public readonly ListBox HangingItems=new ListBox();
  readonly ImageStore store;readonly IImageActions actions;
  readonly Rope rope=new Rope{Height=84,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false};readonly Grid root;readonly Border empty,noticePanel,scrollHint;readonly TextBlock notice=Ui.Text("",12);
  readonly RevealGate gate=new RevealGate();readonly Stopwatch clock=Stopwatch.StartNew();readonly DispatcherTimer watch,noticeTimer;
  readonly TranslateTransform slide=new TranslateTransform();int holdCount,deleteBlockCount,transition;bool incomingDrag,closed,tucking,deletePending;HoverDeleteKey deleteKey;IntPtr handle;
  public IntPtr Handle {get{return handle;}}
  public ClotheslineWindow(ImageStore store,IImageActions actions){
   this.store=store;this.actions=actions;Title="图片晾衣绳";Width=1120;Height=260;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;Icon=IconFactory.WpfIcon();AllowDrop=true;
   root=new Grid{RenderTransform=slide,ClipToBounds=false};
   root.Children.Add(rope);
   HangingItems.BorderThickness=new Thickness(0);HangingItems.Background=Brushes.Transparent;HangingItems.Padding=new Thickness(0);HangingItems.Focusable=false;HangingItems.HorizontalAlignment=HorizontalAlignment.Center;HangingItems.VerticalAlignment=VerticalAlignment.Top;HangingItems.Height=250;
   ScrollViewer.SetHorizontalScrollBarVisibility(HangingItems,ScrollBarVisibility.Hidden);ScrollViewer.SetVerticalScrollBarVisibility(HangingItems,ScrollBarVisibility.Disabled);ScrollViewer.SetCanContentScroll(HangingItems,true);
   VirtualizingPanel.SetIsVirtualizing(HangingItems,true);VirtualizingPanel.SetVirtualizationMode(HangingItems,VirtualizationMode.Recycling);VirtualizingPanel.SetScrollUnit(HangingItems,ScrollUnit.Pixel);VirtualizingPanel.SetCacheLength(HangingItems,new VirtualizationCacheLength(0));
   var itemsPanel=new FrameworkElementFactory(typeof(VirtualizingStackPanel));itemsPanel.SetValue(VirtualizingStackPanel.OrientationProperty,Orientation.Horizontal);HangingItems.ItemsPanel=new ItemsPanelTemplate(itemsPanel);
   var presenter=new FrameworkElementFactory(typeof(ContentPresenter));var itemStyle=new Style(typeof(ListBoxItem));itemStyle.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(ListBoxItem)){VisualTree=presenter}));itemStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0)));itemStyle.Setters.Add(new Setter(UIElement.FocusableProperty,false));HangingItems.ItemContainerStyle=itemStyle;
   var card=new FrameworkElementFactory(typeof(ImageCard));card.SetValue(ImageCard.ActionsProperty,actions);card.SetValue(ImageCard.StoreProperty,store);HangingItems.ItemTemplate=new DataTemplate(typeof(ImageRecord)){VisualTree=card};root.Children.Add(HangingItems);
   var hint=Ui.Text("拖入图片，或截图后复制",12,Ui.Muted);hint.HorizontalAlignment=HorizontalAlignment.Center;
   empty=new Border{Child=hint,Background=Brushes.White,CornerRadius=new CornerRadius(10),Padding=new Thickness(18,14,18,14),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,46,0,0),Effect=Ui.Shadow()};root.Children.Add(empty);
   noticePanel=new Border{Child=notice,Background=Brushes.White,CornerRadius=new CornerRadius(12),Padding=new Thickness(12,5,12,5),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(10,0,10,4),Effect=Ui.Shadow(),Visibility=Visibility.Collapsed,IsHitTestVisible=false};root.Children.Add(noticePanel);
   scrollHint=new Border{Child=Ui.Text("↔ 滚动鼠标滚轮，左右查看更多图片",12,Ui.Muted),Background=Brushes.White,CornerRadius=new CornerRadius(12),Padding=new Thickness(12,4,12,4),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(10,0,10,4),Visibility=Visibility.Collapsed,IsHitTestVisible=false};root.Children.Add(scrollHint);Content=root;
   HangingItems.AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler((s,e)=>UpdateScrollHint()));Loaded+=(s,e)=>UpdateScrollHint();
   SourceInitialized+=(s,e)=>{handle=new WindowInteropHelper(this).Handle;HwndSource.FromHwnd(handle).AddHook(HandleMessage);PlaceOnPrimary();};
   SizeChanged+=(s,e)=>UpdateViewport();
   store.Changed+=StoreChanged;Closed+=(s,e)=>{closed=true;if(deleteKey!=null)deleteKey.Dispose();watch.Stop();noticeTimer.Stop();store.Changed-=StoreChanged;};
   watch=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};watch.Tick+=Tick;
   noticeTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};noticeTimer.Tick+=(s,e)=>{noticeTimer.Stop();noticePanel.Visibility=Visibility.Collapsed;UpdateScrollHint();};
   PreviewDragOver+=OnDragOver;DragEnter+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop)&&!incomingDrag){incomingDrag=true;HoldOpen(true);}};DragLeave+=(s,e)=>{if(incomingDrag){incomingDrag=false;HoldOpen(false);}};Drop+=DropFiles;
   PreviewMouseWheel+=(s,e)=>{var viewer=FindVisual<ScrollViewer>(HangingItems);if(viewer!=null){viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset-e.Delta);e.Handled=true;}};
   RefreshImages();
  }
  static T FindVisual<T>(DependencyObject item) where T:DependencyObject {if(item==null)return null;if(item is T)return (T)item;for(int i=0;i<VisualTreeHelper.GetChildrenCount(item);i++){var result=FindVisual<T>(VisualTreeHelper.GetChild(item,i));if(result!=null)return result;}return null;}
  IntPtr HandleMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x0021){handled=true;return new IntPtr(3);}if(message==0x02E0||message==0x007E)Dispatcher.BeginInvoke(new Action(PlaceOnPrimary));return IntPtr.Zero;}
  void PlaceOnPrimary(){
   if(handle==IntPtr.Zero)return;var source=HwndSource.FromHwnd(handle);double scale=source.CompositionTarget.TransformToDevice.M11;var screen=Forms.Screen.PrimaryScreen;
   Width=screen.Bounds.Width/scale;Left=screen.Bounds.Left/scale;Top=(Math.Max(screen.Bounds.Top,screen.WorkingArea.Top)+4)/scale;UpdateViewport();
  }
  void UpdateViewport(){double total=HangingItems.Items.Cast<ImageRecord>().Sum(x=>ImageCard.CardWidth(x)+16);HangingItems.Width=Math.Min(Math.Max(1,Width-2*Rope.SideSpace(Width)),Math.Max(1,total+4));}
  void UpdateScrollHint(){if(scrollHint==null)return;var viewer=FindVisual<ScrollViewer>(HangingItems);scrollHint.Visibility=viewer!=null&&viewer.ScrollableWidth>1&&noticePanel.Visibility!=Visibility.Visible?Visibility.Visible:Visibility.Collapsed;}
  void StoreChanged(object sender,EventArgs e){if(!closed)Dispatcher.BeginInvoke(new Action(RefreshImages));}
  void RefreshImages(){if(closed)return;var images=store.Snapshot().Where(x=>x.Hanging).ToList();HangingItems.ItemsSource=images;empty.Visibility=images.Count==0?Visibility.Visible:Visibility.Collapsed;UpdateViewport();}
  public void StartWatching(){new WindowInteropHelper(this).EnsureHandle();if(deleteKey==null){try{deleteKey=new HoverDeleteKey(TryDeleteHovered);}catch(System.ComponentModel.Win32Exception e){ShowNotice("Del / 退格快捷删除暂不可用："+e.Message,true);}}watch.Start();}
  bool TryDeleteHovered(){
   if(closed||!IsVisible||tucking||deleteBlockCount>0||incomingDrag||deletePending)return false;
   Native.Point point;if(!Native.GetCursorPos(out point)||Native.WindowFromPoint(point)!=handle)return false;
   var hit=InputHitTest(PointFromScreen(new Point(point.X,point.Y))) as DependencyObject;ImageCard card=null;
   while(hit!=null){card=hit as ImageCard;if(card!=null)break;hit=VisualTreeHelper.GetParent(hit);}
   if(card==null||!card.CanDeleteHovered)return false;var record=card.DataContext as ImageRecord;if(record==null)return false;
   card.CancelPendingClick();deletePending=true;
   Dispatcher.BeginInvoke(new Action(async()=>{try{if(!closed)await actions.RemoveAsync(record,false);}finally{deletePending=false;}}));return true;
  }
  public void HoldOpen(bool hold,bool blockDelete=true){holdCount=Math.Max(0,holdCount+(hold?1:-1));if(blockDelete)deleteBlockCount=Math.Max(0,deleteBlockCount+(hold?1:-1));}
  public void ShowLine(){gate.ShowManually(clock.ElapsedMilliseconds);Reveal();}
  public void ShowCollected(){if(gate.ShowForImage(clock.ElapsedMilliseconds,Native.IsFullscreen(handle)))Reveal();}
  public void HideLine(){gate.HideManually();Tuck();}
  void Tick(object sender,EventArgs e){
   Native.Point pointer;if(!Native.GetCursorPos(out pointer))return;var bounds=Forms.Screen.PrimaryScreen.Bounds;
   bool trigger=pointer.Y>=bounds.Top&&pointer.Y<=bounds.Top+2&&pointer.X>=bounds.Left&&pointer.X<bounds.Right;
   gate.Tick(clock.ElapsedMilliseconds,trigger,PointerInside(),holdCount>0,Native.IsFullscreen(handle));
   if(gate.Visible&&(Visibility!=Visibility.Visible||tucking))Reveal();else if(!gate.Visible&&Visibility==Visibility.Visible)Tuck();
  }
  bool PointerInside(){Native.Point p;Native.Rectangle r;return Visibility==Visibility.Visible&&Native.GetCursorPos(out p)&&Native.GetWindowRect(handle,out r)&&p.X>=r.Left&&p.X<r.Right&&p.Y>=r.Top&&p.Y<r.Bottom;}
  public double RopeYAt(double x){return Rope.HeightAt(x,ActualWidth,rope.Sag);}
  void Reveal(){
   bool alreadyVisible=IsVisible&&!tucking;tucking=false;transition++;PlaceOnPrimary();Show();UpdateScrollHint();if(alreadyVisible)return;
   var sag=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromMilliseconds(460)};
   sag.KeyFrames.Add(new DiscreteDoubleKeyFrame(8,KeyTime.FromTimeSpan(TimeSpan.Zero)));
   sag.KeyFrames.Add(new EasingDoubleKeyFrame(25,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)),new SineEase{EasingMode=EasingMode.EaseOut}));
   sag.KeyFrames.Add(new EasingDoubleKeyFrame(18,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350)),new SineEase{EasingMode=EasingMode.EaseInOut}));
   sag.KeyFrames.Add(new EasingDoubleKeyFrame(20,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(460)),new SineEase{EasingMode=EasingMode.EaseOut}));rope.BeginAnimation(Rope.SagProperty,sag);
   root.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(130)));
   var fall=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromMilliseconds(380)};
   fall.KeyFrames.Add(new DiscreteDoubleKeyFrame(-Height,KeyTime.FromTimeSpan(TimeSpan.Zero)));
   fall.KeyFrames.Add(new EasingDoubleKeyFrame(7,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230)),new CubicEase{EasingMode=EasingMode.EaseOut}));
   fall.KeyFrames.Add(new EasingDoubleKeyFrame(-2,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(310)),new SineEase{EasingMode=EasingMode.EaseInOut}));
   fall.KeyFrames.Add(new EasingDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)),new SineEase{EasingMode=EasingMode.EaseOut}));slide.BeginAnimation(TranslateTransform.YProperty,fall);
  }
  void Tuck(){
   if(Visibility!=Visibility.Visible||tucking)return;tucking=true;int token=++transition;
   var rise=new DoubleAnimation(-Height,TimeSpan.FromMilliseconds(210)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseIn}};
   rise.Completed+=(s,e)=>{if(token==transition&&!gate.Visible){tucking=false;Hide();rope.BeginAnimation(Rope.SagProperty,null);root.BeginAnimation(UIElement.OpacityProperty,null);slide.BeginAnimation(TranslateTransform.YProperty,null);noticeTimer.Stop();noticePanel.Visibility=Visibility.Collapsed;}};
   slide.BeginAnimation(TranslateTransform.YProperty,rise);root.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(210)));
  }
  public void ShowNotice(string text,bool error=false){notice.Text=text;notice.Foreground=error?Ui.Brush("#A23F42"):Ui.Ink;noticePanel.Visibility=Visibility.Visible;UpdateScrollHint();noticeTimer.Stop();noticeTimer.Start();}
  public void ShowCopied(string id){foreach(var record in HangingItems.Items.Cast<ImageRecord>()){if(record.Id!=id)continue;var card=FindVisual<ImageCard>(HangingItems.ItemContainerGenerator.ContainerFromItem(record));if(card!=null){card.ShowCopied();return;}}ShowNotice("已复制图片");}
  void OnDragOver(object sender,DragEventArgs e){e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)&&!e.Data.GetDataPresent("ImageClothesline.Drag")?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;}
  async void DropFiles(object sender,DragEventArgs e){e.Handled=true;try{if(e.Data.GetDataPresent("ImageClothesline.Drag")||!e.Data.GetDataPresent(DataFormats.FileDrop)){e.Effects=DragDropEffects.None;return;}var files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files==null)return;e.Effects=DragDropEffects.Copy;await actions.ImportFilesAsync(files);}finally{if(incomingDrag){incomingDrag=false;HoldOpen(false);}}}
 }
}
