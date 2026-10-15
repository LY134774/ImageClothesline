using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
namespace ImageClothesline {
 [DataContract] public sealed class ImageRecord {
  [DataMember] public string Id {get;set;}
  [DataMember] public string Hash {get;set;}
  [DataMember] public DateTime AddedUtc {get;set;}
  [DataMember] public bool Hanging {get;set;}
  [DataMember] public int Width {get;set;}
  [DataMember] public int Height {get;set;}
  public ImageRecord Clone(){return (ImageRecord)MemberwiseClone();}
 }
 [DataContract] public sealed class StoreState {
  [DataMember] public int Version {get;set;}
  [DataMember] public bool Collecting {get;set;}
  [DataMember] public bool StartWithWindows {get;set;}
  [DataMember] public bool CollectFiles {get;set;}
  [DataMember] public bool NoticeAcknowledged {get;set;}
  [DataMember] public string SuppressedClipboardHash {get;set;}
  [DataMember] public List<ImageRecord> Images {get;set;}
  public StoreState(){Version=1;StartWithWindows=true;CollectFiles=true;Images=new List<ImageRecord>();}
  [OnDeserializing] void Defaults(StreamingContext context){StartWithWindows=true;CollectFiles=true;}
 }
 public interface IRecycleBin { void Recycle(string fullPath); }
 public sealed class CleanupResult { public int Removed; public readonly List<string> Errors=new List<string>(); }
}
