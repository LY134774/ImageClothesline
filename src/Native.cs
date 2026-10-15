using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using Forms=System.Windows.Forms;
namespace ImageClothesline {
 public static class Native {
  [StructLayout(LayoutKind.Sequential)]public struct Point {public int X,Y;}
  [StructLayout(LayoutKind.Sequential)]public struct Rectangle {public int Left,Top,Right,Bottom;}
  [DllImport("user32.dll")]public static extern IntPtr WindowFromPoint(Point point);
  [DllImport("user32.dll")]public static extern short GetAsyncKeyState(int key);
  internal delegate IntPtr KeyboardProc(int code,IntPtr message,IntPtr data);
  [DllImport("user32.dll",SetLastError=true,CharSet=CharSet.Unicode)]internal static extern IntPtr SetWindowsHookEx(int type,KeyboardProc callback,IntPtr module,uint thread);
  [DllImport("user32.dll")]internal static extern bool UnhookWindowsHookEx(IntPtr hook);
  [DllImport("user32.dll")]internal static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]internal static extern IntPtr GetModuleHandle(string name);
  [DllImport("user32.dll")]public static extern bool GetCursorPos(out Point point);
  [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr window,out Rectangle rectangle);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder name,int count);
  [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowLong(IntPtr window,int index);
  [DllImport("user32.dll")]public static extern uint GetClipboardSequenceNumber();
  [DllImport("user32.dll",SetLastError=true)]public static extern bool AddClipboardFormatListener(IntPtr window);
  [DllImport("user32.dll",SetLastError=true)]public static extern bool RemoveClipboardFormatListener(IntPtr window);
  [DllImport("user32.dll")]public static extern uint GetDoubleClickTime();
  [DllImport("user32.dll")]public static extern bool DestroyIcon(IntPtr icon);
  public static bool IsFullscreen(IntPtr ownWindow){
   var window=GetForegroundWindow();if(window==IntPtr.Zero||window==ownWindow)return false;
   var name=new StringBuilder(256);GetClassName(window,name,name.Capacity);
   if(name.ToString()=="Progman"||name.ToString()=="WorkerW"||name.ToString()=="Shell_TrayWnd")return false;
   if((GetWindowLong(window,-16)&0x00C00000)!=0)return false;
   Rectangle r;if(!GetWindowRect(window,out r))return false;var screen=Forms.Screen.PrimaryScreen.Bounds;
   return r.Left<=screen.Left+2&&r.Top<=screen.Top+2&&r.Right>=screen.Right-2&&r.Bottom>=screen.Bottom-2;
  }
 }
 // Inspect Delete and Backspace only; keep each key press independent.
 internal sealed class HoverDeleteKey:IDisposable {
  readonly Func<bool> tryDelete;readonly Native.KeyboardProc callback;IntPtr hook;readonly bool[] down=new bool[2],consumed=new bool[2];
  public HoverDeleteKey(Func<bool> tryDelete){this.tryDelete=tryDelete;callback=Handle;hook=Native.SetWindowsHookEx(13,callback,Native.GetModuleHandle(null),0);if(hook==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
  IntPtr Handle(int code,IntPtr message,IntPtr data){
   if(code>=0){int key=Marshal.ReadInt32(data),index=key==46?0:key==8?1:-1;
    if(index>=0){int kind=message.ToInt32();
     if(kind==0x100||kind==0x104){if(!down[index]){down[index]=true;consumed[index]=false;bool modified=false;foreach(int modifier in new[]{16,17,18,91,92})if((Native.GetAsyncKeyState(modifier)&0x8000)!=0)modified=true;
       if(!modified){try{consumed[index]=tryDelete();}catch{consumed[index]=false;}}
      }if(consumed[index])return new IntPtr(1);
     }else if(kind==0x101||kind==0x105){bool handled=consumed[index];down[index]=false;consumed[index]=false;if(handled)return new IntPtr(1);}
    }
   }
   return Native.CallNextHookEx(hook,code,message,data);
  }
  public void Dispose(){if(hook!=IntPtr.Zero){Native.UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}GC.KeepAlive(callback);}
 }


}
