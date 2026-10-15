using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
namespace ImageClothesline {
 public interface IImageClipboard {uint Sequence {get;} ClipboardInput ReadInput(bool includeFiles);void WriteImage(ClipboardImage image);}
 public sealed class ClipboardImage {
  public readonly BitmapSource Image;public readonly byte[] Png;
  public ClipboardImage(BitmapSource image,byte[] png){Image=image;Png=png;}
  public static ClipboardImage FromFile(string file){
   // The owned file is already PNG. Reuse its bytes instead of encoding again on the UI thread.
   byte[] png=File.ReadAllBytes(file);
   using(var stream=new MemoryStream(png,false)){var decoder=new PngBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);return new ClipboardImage(ImageCodec.Normalize(decoder.Frames[0]),png);}
  }
 }
 public sealed class ClipboardInput {public BitmapSource Image;public string[] Files;}
 public sealed class ClipboardResult {public ImageRecord Added;public readonly System.Collections.Generic.List<string> Errors=new System.Collections.Generic.List<string>();}
 public sealed class ClipboardCapture {
  readonly ImageStore store;readonly IImageClipboard clipboard;
  uint lastSequence,ownSequence;
  public ClipboardCapture(ImageStore store,IImageClipboard clipboard){this.store=store;this.clipboard=clipboard;ResetBaseline();}
  public void ResetBaseline(){lastSequence=clipboard.Sequence;}
  public async Task<ClipboardResult> HandleChange(){
   if(!store.Collecting)return null;uint sequence=clipboard.Sequence;
   if(sequence==lastSequence||sequence==ownSequence)return null;
   // Do not advance the baseline until reading succeeds: a busy clipboard can be retried.
   var input=clipboard.ReadInput(store.CollectFiles);lastSequence=sequence;
   if(input.Files!=null){return await Task.Run(()=>{
    var result=new ClipboardResult();foreach(string file in input.Files){
     if(!store.Collecting||!store.CollectFiles)break;
     try{if(string.IsNullOrEmpty(file)||!ImageCodec.IsSupported(file)||Directory.Exists(file))continue;result.Added=store.ImportFile(file);}
     catch(Exception e){if(e is OutOfMemoryException)throw;result.Errors.Add(Path.GetFileName(file)+"："+e.Message);}
    }return result.Added==null&&result.Errors.Count==0?null:result;
   }).ConfigureAwait(false);}
   var source=input.Image;if(source==null)return null;
   var bitmap=ImageCodec.Normalize(source);
   return new ClipboardResult{Added=await Task.Run(()=>store.ImportBitmap(bitmap)).ConfigureAwait(false)};
  }
  public void CopyImage(ClipboardImage image){clipboard.WriteImage(image);ownSequence=clipboard.Sequence;lastSequence=ownSequence;}
 }
}
