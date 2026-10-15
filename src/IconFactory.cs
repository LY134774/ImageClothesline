using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ImageSource=System.Windows.Media.ImageSource;
using System.Windows.Media.Imaging;
namespace ImageClothesline {
 public static class IconFactory {
  public static Bitmap DrawBitmap(int size){
   var bitmap=new Bitmap(size,size,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
   using(var g=Graphics.FromImage(bitmap)){
    g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);g.ScaleTransform(size/32f,size/32f);
    using(var pen=new Pen(Color.FromArgb(35,35,40),1.7f)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawBezier(pen,2,5,11,10,22,10,30,5);}
    var saved=g.Save();g.TranslateTransform(14,10);g.RotateTransform(-7);
    using(var shadow=new SolidBrush(Color.FromArgb(38,25,30,40)))g.FillRectangle(shadow,-10.2f,1.2f,21,19.5f);
    using(var paper=new SolidBrush(Color.White))g.FillRectangle(paper,-11,0,21,19.5f);
    using(var border=new Pen(Color.FromArgb(75,79,88),.7f))g.DrawRectangle(border,-11,0,21,19.5f);
    using(var sky=new SolidBrush(Color.FromArgb(217,235,245)))g.FillRectangle(sky,-8.5f,2.5f,16,12);
    using(var hill=new SolidBrush(Color.FromArgb(113,162,148)))g.FillPolygon(hill,new[]{new PointF(-8.5f,14.5f),new PointF(-8.5f,10),new PointF(-3,5.5f),new PointF(2,10.5f),new PointF(5,8),new PointF(7.5f,10.5f),new PointF(7.5f,14.5f)});
    using(var sun=new SolidBrush(Color.FromArgb(248,201,101)))g.FillEllipse(sun,2,4,3,3);
    g.Restore(saved);
    using(var peg=new Pen(Color.FromArgb(143,149,161),2.1f)){peg.StartCap=peg.EndCap=LineCap.Round;g.DrawLine(peg,13,7,13,12);}
    var star=new PointF[10];for(int i=0;i<10;i++){double a=-Math.PI/2+i*Math.PI/5;float radius=i%2==0?4.5f:2.1f;star[i]=new PointF(26+(float)Math.Cos(a)*radius,7+(float)Math.Sin(a)*radius);}
    using(var fill=new LinearGradientBrush(new PointF(23,3),new PointF(29,11),Color.FromArgb(255,213,103),Color.FromArgb(245,155,146)))g.FillPolygon(fill,star);
    using(var edge=new Pen(Color.FromArgb(184,123,66),.55f))g.DrawPolygon(edge,star);
   }
   return bitmap;
  }
  static Icon CreateIcon(int size){using(var bitmap=DrawBitmap(size)){IntPtr handle=bitmap.GetHicon();try{using(var borrowed=Icon.FromHandle(handle))return (Icon)borrowed.Clone();}finally{Native.DestroyIcon(handle);}}}
  public static Icon TrayIcon(){return CreateIcon(32);}
  public static ImageSource WpfIcon(){using(var icon=CreateIcon(128)){var source=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle,System.Windows.Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());source.Freeze();return source;}}
 }
}
