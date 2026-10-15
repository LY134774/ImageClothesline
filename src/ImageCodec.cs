using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ImageClothesline {
 public static class ImageCodec {
  static readonly string[] Extensions={".png",".jpg",".jpeg",".bmp",".gif",".tif",".tiff"};
  public static bool IsSupported(string file){return Extensions.Contains(Path.GetExtension(file).ToLowerInvariant());}
  public static BitmapSource ReadFile(string file){
   if(!IsSupported(file))throw new NotSupportedException("暂不支持这个图片格式；请使用 PNG、JPG、BMP、GIF 或 TIFF。");
   using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){
    var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
    var frame=decoder.Frames[0];CheckSize(frame);BitmapSource image=frame;
    var metadata=frame.Metadata as BitmapMetadata;
    if(metadata!=null&&decoder is JpegBitmapDecoder){
     object value=null;try{value=metadata.GetQuery("/app1/ifd/{ushort=274}");}catch(NotSupportedException){}
     int orientation=value==null?1:Convert.ToInt32(value);Transform transform=null;
     switch(orientation){
      case 2:transform=new ScaleTransform(-1,1);break;
      case 3:transform=new RotateTransform(180);break;
      case 4:transform=new ScaleTransform(1,-1);break;
      case 5:transform=new MatrixTransform(0,1,1,0,0,0);break;
      case 6:transform=new RotateTransform(90);break;
      case 7:transform=new MatrixTransform(0,-1,-1,0,0,0);break;
      case 8:transform=new RotateTransform(270);break;
     }
     if(transform!=null)image=new TransformedBitmap(image,transform);
    }
    return Normalize(image);
   }
  }
  public static BitmapSource Normalize(BitmapSource image){
   if(image==null)throw new ArgumentNullException("image");CheckSize(image);
   // Copy pixels to detach from the decoder and its owning dispatcher before background work.
   var converted=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);
   int stride=checked(converted.PixelWidth*4);var pixels=new byte[checked(stride*converted.PixelHeight)];converted.CopyPixels(pixels,stride,0);
   var independent=BitmapSource.Create(converted.PixelWidth,converted.PixelHeight,converted.DpiX,converted.DpiY,PixelFormats.Bgra32,null,pixels,stride);independent.Freeze();return independent;
  }
  static void CheckSize(BitmapSource image){if(image.PixelWidth<=0||image.PixelHeight<=0||(long)image.PixelWidth*image.PixelHeight>64000000)throw new NotSupportedException("图片太大，暂时无法收纳；请先缩小图片。");}
  public static byte[] Encode(BitmapSource image){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}}
  public static string Hash(BitmapSource image){
   int stride=checked(image.PixelWidth*4);var pixels=new byte[checked(stride*image.PixelHeight)];image.CopyPixels(pixels,stride,0);
   using(var sha=SHA256.Create()){var dimensions=BitConverter.GetBytes(image.PixelWidth).Concat(BitConverter.GetBytes(image.PixelHeight)).ToArray();sha.TransformBlock(dimensions,0,dimensions.Length,dimensions,0);sha.TransformFinalBlock(pixels,0,pixels.Length);return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();}
  }
  public static BitmapSource Thumbnail(BitmapSource image){double scale=Math.Min(1,256.0/Math.Max(image.PixelWidth,image.PixelHeight));BitmapSource result=scale<1?new TransformedBitmap(image,new ScaleTransform(scale,scale)):image;result.Freeze();return result;}
  public static BitmapSource ReadThumbnail(string file){using(var stream=File.OpenRead(file)){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=256;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();return bitmap;}}
 }
}
