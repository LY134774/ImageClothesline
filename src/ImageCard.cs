using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
namespace ImageClothesline {
 public sealed class ImageCard:UserControl {
  public static readonly DependencyProperty ActionsProperty=DependencyProperty.Register("Actions",typeof(IImageActions),typeof(ImageCard));
  public static readonly DependencyProperty StoreProperty=DependencyProperty.Register("Store",typeof(ImageStore),typeof(ImageCard));
  public IImageActions Actions {get{return (IImageActions)GetValue(ActionsProperty);}set{SetValue(ActionsProperty,value);}}
  public ImageStore Store {get{return (ImageStore)GetValue(StoreProperty);}set{SetValue(StoreProperty,value);}}
  readonly Image imageView=new Image{Stretch=Stretch.Uniform};readonly TextBlock errorText=Ui.Text("",11,Ui.Muted);
  readonly TranslateTransform feedback=new TranslateTransform();readonly ScaleTransform hoverScale=new ScaleTransform(1,1);readonly RotateTransform rotation=new RotateTransform();readonly DispatcherTimer copyTimer;
  readonly Border body,copyNotice;Point mouseDown;bool dragging,pressed;int generation;double restingAngle;
  ImageRecord Record {get{return DataContext as ImageRecord;}}
  public static double CardWidth(ImageRecord record){return Math.Max(126,Math.Min(230,record.Width*132.0/Math.Max(1,record.Height)+14));}
  public ImageCard(){
   Width=196;Height=212;Margin=new Thickness(8,20,8,0);Focusable=false;
   var transforms=new TransformGroup();transforms.Children.Add(hoverScale);transforms.Children.Add(rotation);transforms.Children.Add(feedback);
   var root=new Grid();body=new Border{Height=146,VerticalAlignment=VerticalAlignment.Top,Background=Brushes.White,CornerRadius=new CornerRadius(9),BorderBrush=Ui.Brush("#E6E7ED"),BorderThickness=new Thickness(1),Padding=new Thickness(6),Margin=new Thickness(0,13,0,0),Effect=Ui.Shadow(),RenderTransform=transforms,RenderTransformOrigin=new Point(.5,0),Cursor=Cursors.Hand};
   var picture=new Grid();picture.Children.Add(imageView);errorText.HorizontalAlignment=HorizontalAlignment.Center;errorText.VerticalAlignment=VerticalAlignment.Center;errorText.TextAlignment=TextAlignment.Center;errorText.Margin=new Thickness(8);picture.Children.Add(errorText);body.Child=picture;root.Children.Add(body);
   var pegBrush=new LinearGradientBrush();pegBrush.StartPoint=new Point(0,0);pegBrush.EndPoint=new Point(1,0);pegBrush.GradientStops.Add(new GradientStop(Color.FromRgb(245,246,249),0));pegBrush.GradientStops.Add(new GradientStop(Color.FromRgb(198,202,211),.5));pegBrush.GradientStops.Add(new GradientStop(Color.FromRgb(240,242,247),1));pegBrush.Freeze();
   var peg=new Border{Width=7,Height=23,CornerRadius=new CornerRadius(3),Background=pegBrush,BorderBrush=Ui.Brush("#B7BCC7"),BorderThickness=new Thickness(.7),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Child=new Rectangle{Height=1,Fill=Ui.Brush("#9CA3B1"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(1,0,1,0)}};root.Children.Add(peg);
   root.MouseEnter+=(s,e)=>{if(!dragging)AnimateHover(true);};root.MouseLeave+=(s,e)=>AnimateHover(false);
   copyNotice=new Border{Child=Ui.Text("✓ 已复制",11),Background=Brushes.White,CornerRadius=new CornerRadius(11),Padding=new Thickness(9,3,9,3),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,181,0,0),RenderTransform=feedback,Effect=Ui.Shadow(),Visibility=Visibility.Collapsed,IsHitTestVisible=false};root.Children.Add(copyNotice);Content=root;
   copyTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1200)};copyTimer.Tick+=(s,e)=>{copyTimer.Stop();copyNotice.Visibility=Visibility.Collapsed;};
   body.MouseLeftButtonDown+=Down;body.MouseMove+=Move;body.MouseLeftButtonUp+=Up;
   var menu=new ContextMenu();var copy=new MenuItem{Header="复制图片"};var preview=new MenuItem{Header="预览大图"};var delete=new MenuItem{Header="移除到回收站"};
   copy.Click+=async(s,e)=>{if(Record!=null)await Actions.CopyAsync(Record);};preview.Click+=(s,e)=>{if(Record!=null)Actions.Preview(Record);};delete.Click+=async(s,e)=>{if(Record!=null)await Actions.RemoveAsync(Record);};menu.Items.Add(copy);menu.Items.Add(preview);menu.Items.Add(new Separator());menu.Items.Add(delete);body.ContextMenu=menu;
   menu.Opened+=(s,e)=>{if(Actions!=null)Actions.HoldOpen(true);};menu.Closed+=(s,e)=>{if(Actions!=null)Actions.HoldOpen(false);};
   IsVisibleChanged+=(s,e)=>{if(!IsVisible)ReleaseImage();else if(IsLoaded)LoadImage();};
   Loaded+=(s,e)=>{if(IsVisible)LoadImage();};DataContextChanged+=(s,e)=>{UpdateRecordAppearance();if(IsLoaded&&IsVisible)LoadImage();};Unloaded+=(s,e)=>ReleaseImage();LayoutUpdated+=(s,e)=>AlignPeg();
  }
  void UpdateRecordAppearance(){var record=Record;if(record==null)return;Width=CardWidth(record);restingAngle=(Convert.ToInt32(record.Id.Substring(0,2),16)%7-3)*.6;}
  void AlignPeg(){if(!IsLoaded)return;var window=Window.GetWindow(this);if(window==null||window.ActualWidth<=0)return;var point=TransformToAncestor(window).Transform(new Point(ActualWidth/2,0));double top=Math.Max(0,(window is ClotheslineWindow?((ClotheslineWindow)window).RopeYAt(point.X):Rope.HeightAt(point.X,window.ActualWidth))-10);if(Math.Abs(Margin.Top-top)>.2)Margin=new Thickness(8,top,8,0);}
  void ReleaseImage(){generation++;copyTimer.Stop();copyNotice.Visibility=Visibility.Collapsed;imageView.Source=null;feedback.BeginAnimation(TranslateTransform.YProperty,null);copyNotice.BeginAnimation(UIElement.OpacityProperty,null);ResetHover();rotation.BeginAnimation(RotateTransform.AngleProperty,null);}
  async void LoadImage(){
   int token=++generation;imageView.Source=null;errorText.Text="";var record=Record;var imageStore=Store;if(record==null||imageStore==null)return;UpdateRecordAppearance();rotation.Angle=restingAngle;
   try{var thumbnail=await Task.Run(()=>{string thumb=imageStore.ThumbnailPath(record.Id);if(File.Exists(thumb))return ImageCodec.ReadThumbnail(thumb);return ImageCodec.Thumbnail(ImageCodec.ReadFile(imageStore.ImagePath(record.Id)));});
    if(Dispatcher.HasShutdownStarted)return;
    await Dispatcher.InvokeAsync(()=>{if(token!=generation||!IsLoaded||!IsVisible)return;imageView.Source=thumbnail;Sway();});
   }catch(Exception){if(!Dispatcher.HasShutdownStarted)Dispatcher.Invoke(new Action(()=>{if(token==generation&&IsLoaded)errorText.Text="图片暂时无法读取";}));}
  }
  void AnimateHover(bool hovering){
   double target=hovering?1.04:1;var animation=new DoubleAnimation(target,TimeSpan.FromMilliseconds(180)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
   hoverScale.BeginAnimation(ScaleTransform.ScaleXProperty,animation);hoverScale.BeginAnimation(ScaleTransform.ScaleYProperty,animation);
  }
  void ResetHover(){hoverScale.BeginAnimation(ScaleTransform.ScaleXProperty,null);hoverScale.BeginAnimation(ScaleTransform.ScaleYProperty,null);hoverScale.ScaleX=hoverScale.ScaleY=1;}
  void Sway(){
   var swing=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromMilliseconds(650)};
   swing.KeyFrames.Add(new EasingDoubleKeyFrame(restingAngle+6,KeyTime.FromTimeSpan(TimeSpan.Zero)));
   swing.KeyFrames.Add(new EasingDoubleKeyFrame(restingAngle-3,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230)),new SineEase{EasingMode=EasingMode.EaseInOut}));
   swing.KeyFrames.Add(new EasingDoubleKeyFrame(restingAngle+1.2,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(430)),new SineEase{EasingMode=EasingMode.EaseInOut}));
   swing.KeyFrames.Add(new EasingDoubleKeyFrame(restingAngle,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650)),new SineEase{EasingMode=EasingMode.EaseOut}));rotation.BeginAnimation(RotateTransform.AngleProperty,swing);
  }
  public bool CanDeleteHovered {get{return body.IsMouseOver&&!dragging&&!pressed;}}
  public void CancelPendingClick(){pressed=false;}
  void Nudge(){
   var motion=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromMilliseconds(220)};
   motion.KeyFrames.Add(new EasingDoubleKeyFrame(-3,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80)),new SineEase{EasingMode=EasingMode.EaseOut}));
   motion.KeyFrames.Add(new EasingDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)),new SineEase{EasingMode=EasingMode.EaseInOut}));feedback.BeginAnimation(TranslateTransform.YProperty,motion);
  }
  public void ShowCopied(){copyNotice.Visibility=Visibility.Visible;copyNotice.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(80)));Nudge();copyTimer.Stop();copyTimer.Start();}
  void Down(object sender,MouseButtonEventArgs e){if(e.ClickCount==2){pressed=false;if(Record!=null&&Actions!=null)Actions.Preview(Record);e.Handled=true;return;}mouseDown=e.GetPosition(this);pressed=true;dragging=false;Nudge();}
  async void Move(object sender,MouseEventArgs e){
   if(!pressed||dragging||e.LeftButton!=MouseButtonState.Pressed||Record==null||Actions==null)return;var current=e.GetPosition(this);
   if(Math.Abs(current.X-mouseDown.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(current.Y-mouseDown.Y)<SystemParameters.MinimumVerticalDragDistance)return;
   var record=Record;pressed=false;dragging=true;ResetHover();Actions.HoldOpen(true);
   try{string file=Store.ImagePath(record.Id);if(!File.Exists(file))return;var data=new DataObject();data.SetData(DataFormats.FileDrop,new[]{file});data.SetData("ImageClothesline.Drag",record.Id);var effect=DragDrop.DoDragDrop(body,data,DragDropEffects.Copy);await Actions.FinishDragAsync(record,effect);}
   catch(Exception ex){MessageBox.Show("这次拖出没有完成，图片仍保留。\n"+ex.Message,"图片晾衣绳",MessageBoxButton.OK,MessageBoxImage.Information);}
   finally{Actions.HoldOpen(false);dragging=false;if(IsVisible&&IsMouseOver)AnimateHover(true);}
  }
  async void Up(object sender,MouseButtonEventArgs e){bool copy=pressed&&!dragging;pressed=false;e.Handled=true;var record=Record;if(copy&&record!=null&&Actions!=null)await Actions.CopyAsync(record);}
 }
}
