using System;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows;
namespace ImageClothesline {
 public static class Program {
  [STAThread]public static int Main(string[] args){
   bool automatic=Array.Exists(args,x=>string.Equals(x,"--startup",StringComparison.OrdinalIgnoreCase));
   string identity=WindowsIdentity.GetCurrent().User.Value;
   using(var mutex=new Mutex(false,"Local\\ImageClothesline-"+identity)){
    bool acquired=false;try{try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
     if(!acquired){if(!automatic)MessageBox.Show("图片晾衣绳已经运行。请点击右下角托盘里的夹子图标。","图片晾衣绳");return 0;}
     var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};AppController controller=null;
     app.DispatcherUnhandledException+=(s,e)=>{MessageBox.Show("程序遇到问题，已经成功保存的图片仍保存在本机。\n"+e.Exception.Message,"图片晾衣绳",MessageBoxButton.OK,MessageBoxImage.Error);e.Handled=true;app.Shutdown(1);};
     app.Startup+=async(s,e)=>{try{string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ImageClothesline");var store=new ImageStore(root,new WindowsRecycleBin());if(automatic&&!store.StartWithWindows){app.Shutdown();return;}controller=new AppController(app,store,null,StartupManager.Create(store));await controller.Initialize(automatic);}catch(Exception error){MessageBox.Show("程序暂时无法启动。\n"+error.Message,"图片晾衣绳",MessageBoxButton.OK,MessageBoxImage.Error);app.Shutdown(1);}};
     app.Exit+=(s,e)=>{if(controller!=null)controller.Dispose();};return app.Run();
    }finally{if(acquired)mutex.ReleaseMutex();}
   }
  }
 }
}
