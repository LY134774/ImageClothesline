using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ImageClothesline {
 public sealed class WindowsClipboard:IImageClipboard {
  readonly Action<IDataObject> publish;
  public WindowsClipboard():this(data=>Clipboard.SetDataObject(data,true)){}
  internal WindowsClipboard(Action<IDataObject> publish){if(publish==null)throw new ArgumentNullException("publish");this.publish=publish;}
  public uint Sequence {get{return Native.GetClipboardSequenceNumber();}}
  public ClipboardInput ReadInput(bool includeFiles){return ReadInput(Clipboard.GetDataObject(),includeFiles);}
  internal static ClipboardInput ReadInput(IDataObject data,bool includeFiles){
   if(data!=null&&data.GetDataPresent(DataFormats.FileDrop,false)){
    if(!includeFiles)return new ClipboardInput{HasFiles=true};
    var effect=data.GetData("Preferred DropEffect",false);byte[] bytes=effect as byte[];var stream=effect as MemoryStream;if(stream!=null)bytes=stream.ToArray();
    uint flags=bytes!=null&&bytes.Length>=4?BitConverter.ToUInt32(bytes,0):effect is int?unchecked((uint)(int)effect):effect is uint?(uint)effect:0;
    if((flags&2)!=0)return new ClipboardInput{HasFiles=true};
    return new ClipboardInput{HasFiles=true,Files=(data.GetData(DataFormats.FileDrop,false) as string[])??new string[0]};
   }
   return new ClipboardInput{Image=ReadImage(data),HasText=data!=null&&(data.GetDataPresent(DataFormats.UnicodeText,false)||data.GetDataPresent(DataFormats.Text,false))};
  }
  public BitmapSource ReadImage(){return ReadImage(Clipboard.GetDataObject());}
  internal static BitmapSource ReadImage(IDataObject data){
   if(data==null)return null;
   // Screenshot tools may publish PNG under its MIME name instead of "PNG".
   foreach(string format in new[]{"PNG","image/png"}){
    if(!data.GetDataPresent(format,false))continue;
    var payload=data.GetData(format,false);var stream=payload as MemoryStream;byte[] bytes=payload as byte[];
    if(stream!=null){if(stream.Length>128*1024*1024)throw new NotSupportedException("图片太大，暂时无法收纳。");bytes=stream.ToArray();}
    if(bytes==null)continue;if(bytes.Length>128*1024*1024)throw new NotSupportedException("图片太大，暂时无法收纳。");
    using(var copy=new MemoryStream(bytes)){var decoder=new PngBitmapDecoder(copy,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);return ImageCodec.Normalize(decoder.Frames[0]);}
   }
   if(!data.GetDataPresent(DataFormats.Bitmap))return null;
   var bitmap=data.GetData(DataFormats.Bitmap) as BitmapSource;if(bitmap==null)return null;
   var image=ImageCodec.Normalize(bitmap);int stride=checked(image.PixelWidth*4);byte[] pixels=new byte[checked(stride*image.PixelHeight)];image.CopyPixels(pixels,stride,0);
   // Legacy clipboard DIB conversion can leave every alpha byte at zero.
   // Apply this correction only to the bitmap fallback, never to PNG data.
   for(int i=3;i<pixels.Length;i+=4)if(pixels[i]!=0)return image;
   for(int i=3;i<pixels.Length;i+=4)pixels[i]=255;
   var opaque=BitmapSource.Create(image.PixelWidth,image.PixelHeight,image.DpiX,image.DpiY,PixelFormats.Bgra32,null,pixels,stride);opaque.Freeze();return opaque;
  }
  public void WriteImage(ClipboardImage image){var data=new DataObject();data.SetImage(image.Image);data.SetData("PNG",new MemoryStream(image.Png,false),false);publish(data);}
 }
}
