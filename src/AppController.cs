using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;
namespace ImageClothesline {
 public sealed class AppController:IImageActions,IDisposable {
  readonly Application app;readonly ImageStore store;readonly ClipboardCapture capture;readonly HashSet<Task> jobs=new HashSet<Task>();readonly System.Drawing.Icon icon;
  readonly Forms.NotifyIcon tray;readonly Forms.ToolStripMenuItem toggle;HwndSource source;SettingsWindow settings;bool quitting,disposed;
  public ClotheslineWindow Window {get;private set;}
  public AppController(Application app,ImageStore store,IImageClipboard imageClipboard=null){
   this.app=app;this.store=store;capture=new ClipboardCapture(store,imageClipboard??new WindowsClipboard());Window=new ClotheslineWindow(store,this);app.MainWindow=Window;
   icon=IconFactory.TrayIcon();tray=new Forms.NotifyIcon{Icon=icon,Text="图片晾衣绳"};var menu=new Forms.ContextMenuStrip();menu.Items.Add("显示晾衣绳",null,(s,e)=>Window.ShowLine());menu.Items.Add("隐藏晾衣绳",null,(s,e)=>Window.HideLine());menu.Items.Add(new Forms.ToolStripSeparator());
   toggle=new Forms.ToolStripMenuItem("自动收纳复制的图片");toggle.Click+=async(s,e)=>await ToggleCollection(!store.Collecting);menu.Items.Add(toggle);menu.Items.Add("设置",null,(s,e)=>OpenSettings());menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("退出",null,async(s,e)=>await Quit());
   menu.Opening+=(s,e)=>HoldOpen(true);menu.Closed+=(s,e)=>HoldOpen(false);tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>Window.ShowLine();store.Changed+=Changed;
  }
  public async Task Initialize(){
   Window.StartWatching();source=HwndSource.FromHwnd(Window.Handle);source.AddHook(ClipboardMessage);
   if(!Native.AddClipboardFormatListener(Window.Handle))throw new Win32Exception(Marshal.GetLastWin32Error(),"无法开启图片剪贴板监听。");tray.Visible=true;
   if(!store.NoticeAcknowledged){var welcome=new WelcomeWindow();welcome.ShowDialog();capture.ResetBaseline();await Task.Run(()=>store.SetPreferences(welcome.EnableChosen,true));}
   UpdateTray();Window.ShowLine();if(!string.IsNullOrEmpty(store.RecoveryNotice))Window.ShowNotice(store.RecoveryNotice,true);
  }
  void Changed(object sender,EventArgs e){if(!disposed)Window.Dispatcher.BeginInvoke(new Action(UpdateTray));}
  void UpdateTray(){if(disposed)return;toggle.Checked=store.Collecting;tray.Text=store.Collecting?"图片晾衣绳 · 自动收纳中":"图片晾衣绳 · 自动收纳已暂停";}
  IntPtr ClipboardMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x031D&&!quitting)ReceiveClipboard();return IntPtr.Zero;}
  async void ReceiveClipboard(){
   await Track(async()=>{
    for(int attempt=0;;attempt++){try{var added=await capture.HandleChange();if(added!=null){if(added.Added!=null)Window.ShowCollected();if(added.Errors.Count>0){Window.ShowLine();Window.ShowNotice("有 "+added.Errors.Count+" 张图片未收纳："+added.Errors[0],true);}}break;}catch(ExternalException){if(attempt>=3)throw;}await Task.Delay(80);}
   });
  }
  async Task Track(Func<Task> work){
   if(quitting)return;Task task=Run(work);jobs.Add(task);try{await task;}finally{jobs.Remove(task);}
  }
  async Task Run(Func<Task> work){HoldOpen(true);try{await work();}catch(Exception e){Window.ShowLine();Window.ShowNotice(Friendly(e),true);}finally{HoldOpen(false);}}
  public void HoldOpen(bool hold){Window.HoldOpen(hold);}
  static string Friendly(Exception e){if(e is UnauthorizedAccessException)return "无法读写图片，请检查文件和保存目录的权限。";if(e is OutOfMemoryException)return "图片太大或可用内存不足，这次没有完成。";if(e is NotSupportedException)return e.Message;if(e is FileFormatException)return "图片文件损坏或无法识别，其他图片仍保留。";return "这次操作没有完成："+e.Message;}
  public Task CopyAsync(ImageRecord record){return Track(async()=>{
   var image=await Task.Run(()=>ClipboardImage.FromFile(store.ImagePath(record.Id)));
   for(int attempt=0;;attempt++){try{capture.CopyImage(image);break;}catch(ExternalException){if(attempt>=3)throw;}await Task.Delay(80);}
   Window.ShowCopied(record.Id);
  });}
  public async void Preview(ImageRecord record){await Track(async()=>{
   var image=await Task.Run(()=>ImageCodec.ReadFile(store.ImagePath(record.Id)));var preview=new PreviewWindow(record,image);HoldOpen(true);preview.Closed+=(s,e)=>HoldOpen(false);preview.Show();
  });}
  public Task RemoveAsync(ImageRecord record,bool confirm=true){return Track(async()=>{
   if(confirm&&MessageBox.Show("将这张图片的工具副本放入回收站？\n你导入的原始文件会保留。","移除图片",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
   await Task.Run(()=>store.Remove(record.Id));if(confirm)Window.ShowNotice("工具副本已放入回收站");
  });}
  public Task FinishDragAsync(ImageRecord record,DragDropEffects effect){return Track(async()=>{
   if(effect!=DragDropEffects.Copy){Window.ShowNotice("未完成拖出，图片仍挂在绳子上");return;}
   await Task.Run(()=>store.CompleteExport(record.Id,true));Window.ShowNotice("已取下，本地副本保留到你手动清理");
  });}
  public Task ImportFilesAsync(string[] files){return Track(async()=>{
   var errors=new List<string>();int added=0;
   foreach(string file in files){try{await Task.Run(()=>store.ImportFile(file));added++;}catch(Exception e){errors.Add(Path.GetFileName(file)+"："+Friendly(e));}}
   Window.ShowNotice("已收纳 "+added+" 张"+(errors.Count>0?"，另有 "+errors.Count+" 个文件未加入":""),errors.Count>0);
   if(errors.Count>0)MessageBox.Show(string.Join("\n",errors.Take(5))+(errors.Count>5?"\n其余错误文件也未加入。":""),"部分图片未加入",MessageBoxButton.OK,MessageBoxImage.Information);
  });}
  async Task ToggleCollection(bool enabled){await Track(async()=>{capture.ResetBaseline();await Task.Run(()=>store.SetPreferences(enabled,true));Window.ShowNotice(enabled?"自动收纳已开启":"自动收纳已暂停，仍可以拖入文件");});}
  async Task ToggleFileCollection(bool enabled){await Track(async()=>{capture.ResetBaseline();await Task.Run(()=>store.SetFileCollection(enabled));capture.ResetBaseline();});}
  public void OpenSettings(){
   if(quitting)return;if(settings!=null){settings.Activate();return;}Window.HoldOpen(true,false);
   settings=new SettingsWindow(store,ToggleCollection,ToggleFileCollection,Cleanup,()=>{try{Process.Start(new ProcessStartInfo(store.Root){UseShellExecute=true});}catch(Exception e){Window.ShowNotice(Friendly(e),true);}});
   settings.Closed+=(s,e)=>{settings=null;Window.HoldOpen(false,false);};settings.Show();
  }
  async Task Cleanup(){await Track(async()=>{
   int count=store.Snapshot().Count(x=>!x.Hanging);if(count==0)return;
   if(MessageBox.Show("将 "+count+" 张已取下图片的工具副本放入回收站？\n请确认目标软件已经读取或发送完成。\n导入的原始文件和仍挂着的图片会保留。","清理已取下图片",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
   var result=await Task.Run(()=>store.CleanupExported());Window.ShowNotice("已清理 "+result.Removed+" 张"+(result.Errors.Count>0?"，部分项目需要查看详情":""),result.Errors.Count>0);
   if(result.Errors.Count>0)MessageBox.Show(string.Join("\n",result.Errors.Take(3)),"清理结果说明",MessageBoxButton.OK,MessageBoxImage.Information);
  });}
  async Task Quit(){if(quitting)return;quitting=true;Native.RemoveClipboardFormatListener(Window.Handle);await Task.WhenAll(jobs.ToArray());Dispose();app.Shutdown();}
  public void Dispose(){if(disposed)return;disposed=true;store.Changed-=Changed;Native.RemoveClipboardFormatListener(Window.Handle);if(source!=null)source.RemoveHook(ClipboardMessage);tray.Visible=false;tray.Dispose();icon.Dispose();Window.Close();}
 }
}
