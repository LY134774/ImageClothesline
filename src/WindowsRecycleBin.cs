using System;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
namespace ImageClothesline {
 public sealed class WindowsRecycleBin:IRecycleBin {
  public void Recycle(string fullPath){
   if(!Path.IsPathRooted(fullPath)||!File.Exists(fullPath))throw new IOException("图片文件不存在，未执行清理。");
   Exception error=null;
   var thread=new Thread(()=>{try{RecycleOnSta(fullPath);}catch(Exception e){error=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
   if(error!=null)throw new IOException("无法将图片放入回收站，未尝试永久删除。"+error.Message,error);
  }
  static void RecycleOnSta(string path){
   IFileOperation operation=null;IShellItem item=null;
   try{
    operation=(IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09")));
    // RECYCLEONDELETE + undo + early failure. Never use SHFileOperation's permanent-delete fallback.
    operation.SetOperationFlags(0x00080000|0x20000000|0x00100000|0x00000400|0x00000004|0x00000010);
    Guid iid=new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out item));
    var guard=new RecycleGuard();operation.DeleteItem(item,guard);operation.PerformOperations();bool aborted;operation.GetAnyOperationsAborted(out aborted);
    if(aborted||!guard.Recycled||File.Exists(path))throw new IOException("系统没有确认回收完成。");
   }finally{if(item!=null)Marshal.FinalReleaseComObject(item);if(operation!=null)Marshal.FinalReleaseComObject(operation);}
  }
  [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)]static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,out IShellItem item);
  [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IShellItem {
   void BindToHandler(IntPtr context,ref Guid handler,ref Guid iid,out IntPtr result);
   void GetParent(out IShellItem parent);void GetDisplayName(uint kind,out IntPtr name);void GetAttributes(uint mask,out uint attributes);void Compare(IShellItem other,uint hint,out int order);
  }
  [ComImport,Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IFileOperation {
   void Advise(IFileOperationProgressSink sink,out uint cookie);void Unadvise(uint cookie);void SetOperationFlags(uint flags);
   void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)]string message);void SetProgressDialog([MarshalAs(UnmanagedType.Interface)]object dialog);void SetProperties([MarshalAs(UnmanagedType.Interface)]object properties);void SetOwnerWindow(IntPtr owner);
   void ApplyPropertiesToItem(IShellItem item);void ApplyPropertiesToItems([MarshalAs(UnmanagedType.IUnknown)]object items);
   void RenameItem(IShellItem item,[MarshalAs(UnmanagedType.LPWStr)]string name,IFileOperationProgressSink sink);void RenameItems([MarshalAs(UnmanagedType.IUnknown)]object items,[MarshalAs(UnmanagedType.LPWStr)]string name);
   void MoveItem(IShellItem item,IShellItem destination,[MarshalAs(UnmanagedType.LPWStr)]string name,IFileOperationProgressSink sink);void MoveItems([MarshalAs(UnmanagedType.IUnknown)]object items,IShellItem destination);
   void CopyItem(IShellItem item,IShellItem destination,[MarshalAs(UnmanagedType.LPWStr)]string name,IFileOperationProgressSink sink);void CopyItems([MarshalAs(UnmanagedType.IUnknown)]object items,IShellItem destination);
   void DeleteItem(IShellItem item,IFileOperationProgressSink sink);void DeleteItems([MarshalAs(UnmanagedType.IUnknown)]object items);
   void NewItem(IShellItem destination,uint attributes,[MarshalAs(UnmanagedType.LPWStr)]string name,[MarshalAs(UnmanagedType.LPWStr)]string template,IFileOperationProgressSink sink);
   void PerformOperations();void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)]out bool aborted);
  }
  [Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IFileOperationProgressSink {
   [PreserveSig]int StartOperations();[PreserveSig]int FinishOperations(int result);
   [PreserveSig]int PreRenameItem(uint flags,IShellItem item,[MarshalAs(UnmanagedType.LPWStr)]string name);
   [PreserveSig]int PostRenameItem(uint flags,IShellItem item,[MarshalAs(UnmanagedType.LPWStr)]string name,int result,IShellItem created);
   [PreserveSig]int PreMoveItem(uint flags,IShellItem item,IShellItem destination,[MarshalAs(UnmanagedType.LPWStr)]string name);
   [PreserveSig]int PostMoveItem(uint flags,IShellItem item,IShellItem destination,[MarshalAs(UnmanagedType.LPWStr)]string name,int result,IShellItem created);
   [PreserveSig]int PreCopyItem(uint flags,IShellItem item,IShellItem destination,[MarshalAs(UnmanagedType.LPWStr)]string name);
   [PreserveSig]int PostCopyItem(uint flags,IShellItem item,IShellItem destination,[MarshalAs(UnmanagedType.LPWStr)]string name,int result,IShellItem created);
   [PreserveSig]int PreDeleteItem(uint flags,IShellItem item);[PreserveSig]int PostDeleteItem(uint flags,IShellItem item,int result,IShellItem created);
   [PreserveSig]int PreNewItem(uint flags,IShellItem destination,[MarshalAs(UnmanagedType.LPWStr)]string name);
   [PreserveSig]int PostNewItem(uint flags,IShellItem destination,[MarshalAs(UnmanagedType.LPWStr)]string name,[MarshalAs(UnmanagedType.LPWStr)]string template,uint attributes,int result,IShellItem created);
   [PreserveSig]int UpdateProgress(uint total,uint completed);[PreserveSig]int ResetTimer();[PreserveSig]int PauseTimer();[PreserveSig]int ResumeTimer();
  }
  [ComVisible(true),ClassInterface(ClassInterfaceType.None)]sealed class RecycleGuard:IFileOperationProgressSink {
   public bool Recycled;
   public int StartOperations(){return 0;}public int FinishOperations(int result){return 0;}
   // If the shell switches to permanent deletion, abort before it touches the file.
   public int PreDeleteItem(uint flags,IShellItem item){return (flags&0x80)!=0?0:unchecked((int)0x80004004);}
   public int PostDeleteItem(uint flags,IShellItem item,int result,IShellItem created){Recycled=result>=0;return 0;}
   public int PreRenameItem(uint f,IShellItem i,string n){return 0;}public int PostRenameItem(uint f,IShellItem i,string n,int r,IShellItem c){return 0;}
   public int PreMoveItem(uint f,IShellItem i,IShellItem d,string n){return 0;}public int PostMoveItem(uint f,IShellItem i,IShellItem d,string n,int r,IShellItem c){return 0;}
   public int PreCopyItem(uint f,IShellItem i,IShellItem d,string n){return 0;}public int PostCopyItem(uint f,IShellItem i,IShellItem d,string n,int r,IShellItem c){return 0;}
   public int PreNewItem(uint f,IShellItem d,string n){return 0;}public int PostNewItem(uint f,IShellItem d,string n,string t,uint a,int r,IShellItem c){return 0;}
   public int UpdateProgress(uint t,uint c){return 0;}public int ResetTimer(){return 0;}public int PauseTimer(){return 0;}public int ResumeTimer(){return 0;}
  }
 }
}
