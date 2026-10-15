using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Media.Imaging;
namespace ImageClothesline {
 public sealed class ImageStore {
  readonly object gate=new object();
  readonly IRecycleBin bin;
  StoreState state;
  string currentClipboardHash;
  public string Root {get;private set;}
  string IndexPath {get{return Path.Combine(Root,"state.json");}}
  string BackupPath {get{return Path.Combine(Root,"state.backup.json");}}
  public string RecoveryNotice {get;private set;}
  public event EventHandler Changed;
  public ImageStore(string root,IRecycleBin bin){
   if(bin==null)throw new ArgumentNullException("bin");this.bin=bin;Root=Path.GetFullPath(root);
   EnsureDirectory(Root);EnsureDirectory(Path.Combine(Root,"images"));EnsureDirectory(Path.Combine(Root,"thumbnails"));
   state=Load();RecoverUntrackedImages();
  }
  static void EnsureDirectory(string p){Directory.CreateDirectory(p);if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("图片保存目录不能是符号链接或目录联接。");}
  public bool Collecting {get{lock(gate)return state.Collecting;}}
  public bool StartWithWindows {get{lock(gate)return state.StartWithWindows;}}
  public void SetStartWithWindows(bool enabled){lock(gate){bool previous=state.StartWithWindows;state.StartWithWindows=enabled;try{Save();}catch{state.StartWithWindows=previous;throw;}}Notify();}
  public bool CollectFiles {get{lock(gate)return state.CollectFiles;}}
  public bool HasClipboardSuppression {get{lock(gate)return state.SuppressedClipboardHash!=null;}}
  public bool NoticeAcknowledged {get{lock(gate)return state.NoticeAcknowledged;}}
  public IList<ImageRecord> Snapshot(){lock(gate)return state.Images.OrderByDescending(x=>x.AddedUtc).Select(x=>x.Clone()).ToList();}
  public string ImagePath(string id){ValidateId(id);return Path.Combine(Root,"images",id+".png");}
  public string ThumbnailPath(string id){ValidateId(id);return Path.Combine(Root,"thumbnails",id+".png");}
  static void ValidateId(string id){Guid value;if(id==null||!Guid.TryParseExact(id,"N",out value))throw new IOException("图片记录包含无效编号，已停止操作以保护原始文件。");}
  void CheckOwnedFile(string file){EnsureDirectory(Root);EnsureDirectory(Path.GetDirectoryName(file));if(File.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("不能处理符号链接图片。");}
  StoreState ReadState(string p){
   using(var stream=File.OpenRead(p)){
    var result=(StoreState)new DataContractJsonSerializer(typeof(StoreState)).ReadObject(stream);
    if(result==null||result.Version!=1||result.Images==null)throw new IOException("图片记录格式无效。");
    var ids=new HashSet<string>();
    foreach(var image in result.Images){if(image==null)throw new IOException("图片记录包含空项。");ValidateId(image.Id);if(!ids.Add(image.Id)||string.IsNullOrEmpty(image.Hash)||image.Width<=0||image.Height<=0)throw new IOException("图片记录内容无效。");}
    result.Images=result.Images.Where(x=>File.Exists(ImagePath(x.Id))).ToList();return result;
   }
  }
  StoreState Load(){
   if(!File.Exists(IndexPath)&&!File.Exists(BackupPath))return new StoreState();
   try{return ReadState(IndexPath);}catch(Exception e){if(!(e is IOException||e is SerializationException||e is UnauthorizedAccessException))throw;}
   try{var recovered=ReadState(BackupPath);RecoveryNotice="图片记录已从备份恢复，请检查绳子上的图片。";return recovered;}
   catch(Exception e){throw new IOException("无法读取图片记录，已保留全部图片文件。请先备份保存目录，再处理损坏的记录。",e);}
  }
  void RecoverUntrackedImages(){
   var known=new HashSet<string>(state.Images.Select(x=>x.Id));
   foreach(var file in Directory.GetFiles(Path.Combine(Root,"images"),"*.png")){
    string id=Path.GetFileNameWithoutExtension(file);Guid value;
    if(!Guid.TryParseExact(id,"N",out value)||known.Contains(id))continue;
    try{CheckOwnedFile(file);var bitmap=ImageCodec.ReadFile(file);state.Images.Add(new ImageRecord{Id=id,Hash=ImageCodec.Hash(bitmap),Width=bitmap.PixelWidth,Height=bitmap.PixelHeight,Hanging=true,AddedUtc=File.GetLastWriteTimeUtc(file)});string thumbnail=ThumbnailPath(id);CheckOwnedFile(thumbnail);File.WriteAllBytes(thumbnail,ImageCodec.Encode(ImageCodec.Thumbnail(bitmap)));RecoveryNotice="已找回意外中断前保存的图片。";}
    catch(Exception e){if(e is OutOfMemoryException)throw;RecoveryNotice="有图片暂时无法读取，文件已保留。";}
   }
  }
  void Save(){
   string pending=IndexPath+".pending";CheckOwnedFile(pending);CheckOwnedFile(IndexPath);CheckOwnedFile(BackupPath);
   using(var stream=new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None)){new DataContractJsonSerializer(typeof(StoreState)).WriteObject(stream,state);stream.Flush(true);}
   // Replacement is atomic on the local filesystem. Keep the preceding complete index.
   if(File.Exists(IndexPath))File.Replace(pending,IndexPath,BackupPath,true);else File.Move(pending,IndexPath);
  }
  void Notify(){var handler=Changed;if(handler!=null)handler(this,EventArgs.Empty);}
  public ImageRecord ImportFile(string file){return ImportBitmap(ImageCodec.ReadFile(file));}
  public ImageRecord ImportBitmap(BitmapSource source){
   var bitmap=ImageCodec.Normalize(source);return ImportNormalized(bitmap,ImageCodec.Hash(bitmap));
  }
  public ImageRecord ImportClipboardBitmap(BitmapSource source){
   var bitmap=ImageCodec.Normalize(source);string hash=ImageCodec.Hash(bitmap);
   lock(gate){
    currentClipboardHash=hash;
    if(state.SuppressedClipboardHash==hash)return null;
    ClearClipboardSuppression();
   }
   return ImportNormalized(bitmap,hash,true);
  }
  public void NoteClipboardWithoutImage(){lock(gate){currentClipboardHash=null;ClearClipboardSuppression();}}
  public void NoteClipboardBaseline(string hash){lock(gate){currentClipboardHash=hash;if(state.SuppressedClipboardHash!=hash)ClearClipboardSuppression();}}
  public void NoteCopiedImage(string hash){lock(gate){currentClipboardHash=hash;if(state.SuppressedClipboardHash!=hash)ClearClipboardSuppression();}}
  void ClearClipboardSuppression(){
   if(state.SuppressedClipboardHash==null)return;
   string previous=state.SuppressedClipboardHash;state.SuppressedClipboardHash=null;
   try{Save();}catch{state.SuppressedClipboardHash=previous;throw;}
  }
  ImageRecord ImportNormalized(BitmapSource bitmap,string hash,bool skipHanging=false){
   ImageRecord result=null;
   // Clipboard updates may restore an image already on the line; do not reorder or announce it.
   lock(gate){var existing=state.Images.FirstOrDefault(x=>x.Hash==hash&&File.Exists(ImagePath(x.Id)));if(existing!=null){if(skipHanging&&existing.Hanging)return null;result=Rehang(existing);}}
   if(result!=null){Notify();return result;}
   // Encoding is expensive; keep it outside the lock used by the UI. Recheck duplicates before committing.
   byte[] imageBytes=ImageCodec.Encode(bitmap),thumbnailBytes=ImageCodec.Encode(ImageCodec.Thumbnail(bitmap));
   lock(gate){
    var existing=state.Images.FirstOrDefault(x=>x.Hash==hash&&File.Exists(ImagePath(x.Id)));
    if(existing!=null){if(skipHanging&&existing.Hanging)return null;result=Rehang(existing);}
    else{
     var record=new ImageRecord{Id=Guid.NewGuid().ToString("N"),Hash=hash,AddedUtc=DateTime.UtcNow,Hanging=true,Width=bitmap.PixelWidth,Height=bitmap.PixelHeight};
     string imagePath=ImagePath(record.Id),thumbPath=ThumbnailPath(record.Id);
     try{
      using(var stream=new FileStream(imagePath,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(imageBytes,0,imageBytes.Length);stream.Flush(true);}
      File.WriteAllBytes(thumbPath,thumbnailBytes);state.Images.Add(record);Save();result=record.Clone();
     }catch{state.Images.Remove(record);DeleteIncomplete(imagePath);DeleteIncomplete(thumbPath);throw;}
    }
   }
   Notify();return result;
  }
  ImageRecord Rehang(ImageRecord existing){bool oldHanging=existing.Hanging;DateTime oldDate=existing.AddedUtc;existing.Hanging=true;existing.AddedUtc=DateTime.UtcNow;try{Save();}catch{existing.Hanging=oldHanging;existing.AddedUtc=oldDate;throw;}return existing.Clone();}
  static void DeleteIncomplete(string p){try{if(File.Exists(p))File.Delete(p);}catch(IOException){}catch(UnauthorizedAccessException){}}
  public void SetPreferences(bool collecting,bool acknowledged){lock(gate){bool oldCollect=state.Collecting,oldAck=state.NoticeAcknowledged;state.Collecting=collecting;state.NoticeAcknowledged=acknowledged;try{Save();}catch{state.Collecting=oldCollect;state.NoticeAcknowledged=oldAck;throw;}}Notify();}
  public void SetFileCollection(bool enabled){lock(gate){bool previous=state.CollectFiles;state.CollectFiles=enabled;try{Save();}catch{state.CollectFiles=previous;throw;}}Notify();}
  public void CompleteExport(string id,bool accepted){
   if(!accepted)return;lock(gate){var image=state.Images.Single(x=>x.Id==id);bool previous=image.Hanging;image.Hanging=false;try{Save();}catch{image.Hanging=previous;throw;}}Notify();
  }
  public void Remove(string id){
   bool removed=false;
   try{lock(gate){var image=state.Images.Single(x=>x.Id==id);string file=ImagePath(id);CheckOwnedFile(file);string thumbnail=ThumbnailPath(id);CheckOwnedFile(thumbnail);if(File.Exists(thumbnail))bin.Recycle(thumbnail);bin.Recycle(file);if(File.Exists(file))throw new IOException("图片尚未进入回收站，已保留记录。");state.Images.Remove(image);bool currentMatch=image.Hash==currentClipboardHash;if(currentMatch)state.SuppressedClipboardHash=image.Hash;removed=true;
     try{Save();}catch(Exception e){throw new IOException("图片副本已放入回收站，但记录保存失败。卡片已取下；需要找回时请打开系统回收站。",e);}
   }}finally{if(removed)Notify();}
  }
  public CleanupResult CleanupExported(){
   var result=new CleanupResult();
   foreach(var candidate in Snapshot().Where(x=>!x.Hanging)){
    try{
     lock(gate){
      // Recheck under the same lock as removal: clipboard imports can rehang an archived image.
      var current=state.Images.FirstOrDefault(x=>x.Id==candidate.Id);
      if(current==null||current.Hanging)continue;
      Remove(current.Id);result.Removed++;
     }
    }catch(Exception e){if(!Snapshot().Any(x=>x.Id==candidate.Id))result.Removed++;result.Errors.Add(e.Message);}
   }
   return result;
  }
  public long ExportedBytes {get{lock(gate)return state.Images.Where(x=>!x.Hanging).Sum(x=>File.Exists(ImagePath(x.Id))?new FileInfo(ImagePath(x.Id)).Length:0L);}}
 }
}
