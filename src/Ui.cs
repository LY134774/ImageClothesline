using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Automation;
namespace ImageClothesline {
 public static class Ui {
  public static readonly Brush Ink=Brush("#363943"),Muted=Brush("#818590"),Green=Brush("#575D70"),Paper=Brush("#F6F6F8"),Line=Brush("#E5E6EC");
  public static Brush Brush(string value){var result=(SolidColorBrush)new BrushConverter().ConvertFromString(value);result.Freeze();return result;}
  public static TextBlock Text(string value,double size=14,Brush brush=null){return new TextBlock{Text=value,FontSize=size,Foreground=brush??Ink,FontFamily=new FontFamily("Microsoft YaHei UI"),TextWrapping=TextWrapping.Wrap};}
  public static Button Button(string label,bool primary=false){
   var button=new Button{Content=label,Foreground=primary?Brushes.White:Ink,FontSize=13,Cursor=System.Windows.Input.Cursors.Hand,BorderThickness=new Thickness(0),Padding=new Thickness(14,8,14,8),MinHeight=34,FontFamily=new FontFamily("Microsoft YaHei UI")};
   var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(9));border.SetValue(Border.BackgroundProperty,primary?Green:Brush("#EEEFF3"));border.SetValue(Border.PaddingProperty,new Thickness(14,7,14,7));
   var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);
   button.Template=new ControlTemplate(typeof(Button)){VisualTree=border};
   var style=new Style(typeof(Button));var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(UIElement.OpacityProperty,.8));style.Triggers.Add(hover);var disabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.4));style.Triggers.Add(disabled);button.Style=style;
   AutomationProperties.SetName(button,label);return button;
  }
  public static Border Panel(UIElement child){return new Border{Child=child,Background=Brushes.White,CornerRadius=new CornerRadius(12),BorderBrush=Line,BorderThickness=new Thickness(1),Padding=new Thickness(18)};}
  public static DropShadowEffect Shadow(){return new DropShadowEffect{Color=Color.FromRgb(69,67,85),Opacity=.16,BlurRadius=13,ShadowDepth=5};}
  public static void ConfigureDialog(Window window,string title,double width,double height){window.Title=title;window.Width=width;window.Height=height;window.MinWidth=width-40;window.MinHeight=height-40;window.WindowStartupLocation=WindowStartupLocation.CenterScreen;window.Background=Paper;window.FontFamily=new FontFamily("Microsoft YaHei UI");window.Icon=IconFactory.WpfIcon();}
  public static string Size(long bytes){return bytes>=1024*1024?(bytes/1048576.0).ToString("0.0")+" MB":(bytes/1024.0).ToString("0.0")+" KB";}
 }
 public sealed class Rope:FrameworkElement {
  static readonly Brush[] StarColors={Ui.Brush("#F2C867"),Ui.Brush("#EFA6C0"),Ui.Brush("#93C9EC")};
  public static readonly DependencyProperty SagProperty=DependencyProperty.Register("Sag",typeof(double),typeof(Rope),new FrameworkPropertyMetadata(20.0,FrameworkPropertyMetadataOptions.AffectsRender|FrameworkPropertyMetadataOptions.AffectsArrange));
  public double Sag {get{return (double)GetValue(SagProperty);}set{SetValue(SagProperty,value);}}
  public static double SideSpace(double width){return System.Math.Min(250,System.Math.Max(0,width*.25));}
  public static double HeightAt(double x,double width,double sag=20){double t=width<=0?.5:System.Math.Max(0,System.Math.Min(1,x/width));return 11+4*sag*t*(1-t);}
  protected override void OnRender(DrawingContext drawing){
   double w=ActualWidth;if(w<20)return;var geometry=new StreamGeometry();using(var context=geometry.Open()){context.BeginFigure(new Point(0,11),false,false);context.QuadraticBezierTo(new Point(w/2,11+2*Sag),new Point(w,11),true,false);}geometry.Freeze();
   drawing.DrawGeometry(null,new Pen(Brushes.Black,1.5),geometry);
   double sideSpace=SideSpace(w);if(sideSpace<100)return;
   for(int side=0;side<2;side++)for(int i=0;i<6;i++){
    double x=24+i*(sideSpace-48)/5;if(side==1)x=w-x;double radius=9+(i%3)*2,tilt=side==0?-8:8;
    var center=new Point(x,System.Math.Max(radius+1,HeightAt(x,w,Sag)));
    DrawStar(drawing,center,radius,StarColors[(i+side)%3],tilt);
   }
  }
  static void DrawStar(DrawingContext drawing,Point center,double radius,Brush fill,double tilt){
   var star=new StreamGeometry();using(var context=star.Open())for(int i=0;i<10;i++){double angle=(i*36-90+tilt)*System.Math.PI/180,r=i%2==0?radius:radius*.46;var point=new Point(center.X+System.Math.Cos(angle)*r,center.Y+System.Math.Sin(angle)*r);if(i==0)context.BeginFigure(point,true,true);else context.LineTo(point,true,false);}star.Freeze();drawing.DrawGeometry(fill,null,star);
  }

 }
}
