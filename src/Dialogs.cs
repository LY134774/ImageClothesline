using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ImageClothesline {
 public sealed class WelcomeWindow:Window {
  public bool EnableChosen {get;private set;}
  public WelcomeWindow(){
   Ui.ConfigureDialog(this,"欢迎使用图片晾衣绳",480,330);ResizeMode=ResizeMode.NoResize;
   var stack=new StackPanel{Margin=new Thickness(30)};var title=Ui.Text("给图片一个随手取用的地方",21);title.FontWeight=FontWeights.SemiBold;stack.Children.Add(title);
   var text=Ui.Text("开启自动收纳后，你随后复制的图片和照片文件会保存在这台电脑上，并挂到屏幕顶部。文字不会收纳，图片不会上传。",14,Ui.Muted);text.Margin=new Thickness(0,18,0,14);stack.Children.Add(text);
   var help=Ui.Text("鼠标在屏幕顶部中央停留，绳子就会展开。\n也可以从右下角托盘显示绳子，再拖入图片文件。",13);help.Margin=new Thickness(0,0,0,22);stack.Children.Add(help);
   var buttons=new StackPanel{Orientation=Orientation.Horizontal};var yes=Ui.Button("开启自动收纳",true);var no=Ui.Button("暂不开启");no.Margin=new Thickness(10,0,0,0);yes.Click+=(s,e)=>{EnableChosen=true;DialogResult=true;};no.Click+=(s,e)=>{EnableChosen=false;DialogResult=false;};buttons.Children.Add(yes);buttons.Children.Add(no);stack.Children.Add(buttons);Content=stack;
  }
 }
 public sealed class PreviewWindow:Window {
  public PreviewWindow(ImageRecord record,BitmapSource image){
   double width=Math.Max(360,Math.Min(960,SystemParameters.WorkArea.Width-48)),height=Math.Max(280,Math.Min(720,SystemParameters.WorkArea.Height-48));Ui.ConfigureDialog(this,"图片预览",width,height);
   var root=new Grid{Margin=new Thickness(20)};root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(38)});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(32)});
   var heading=Ui.Text("图片预览",18);heading.FontWeight=FontWeights.SemiBold;root.Children.Add(heading);
   var frame=new Border{Background=Brushes.White,CornerRadius=new CornerRadius(10),Padding=new Thickness(12),BorderBrush=Ui.Line,BorderThickness=new Thickness(1),Child=new Image{Source=image,Stretch=Stretch.Uniform}};Grid.SetRow(frame,1);root.Children.Add(frame);
   var footer=Ui.Text(record.Width+" × "+record.Height+" 像素 · "+record.AddedUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm"),12,Ui.Muted);footer.VerticalAlignment=VerticalAlignment.Bottom;Grid.SetRow(footer,2);root.Children.Add(footer);Content=root;PreviewKeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Escape)Close();};
  }
 }
 public sealed class SettingsWindow:Window {
  readonly ImageStore store;readonly Func<bool,Task> save,saveFiles;readonly Func<Task> cleanup;readonly CheckBox collect,collectFiles;readonly TextBlock statistics=Ui.Text("",14);readonly Button clear;bool syncing,closed;
  public SettingsWindow(ImageStore store,Func<bool,Task> save,Func<bool,Task> saveFiles,Func<Task> cleanup,Action openFolder){
   this.store=store;this.save=save;this.saveFiles=saveFiles;this.cleanup=cleanup;Ui.ConfigureDialog(this,"图片晾衣绳 · 设置",540,580);ResizeMode=ResizeMode.NoResize;
   var stack=new StackPanel{Margin=new Thickness(26)};var title=Ui.Text("设置",23);title.FontWeight=FontWeights.SemiBold;stack.Children.Add(title);
   var sub=Ui.Text("图片只保存在本机，清理时送入系统回收站。",12,Ui.Muted);sub.Margin=new Thickness(0,6,0,18);stack.Children.Add(sub);
   var collecting=new StackPanel();collect=new CheckBox{Content="自动收纳复制的图片",FontSize=14,Foreground=Ui.Ink,IsChecked=store.Collecting};collecting.Children.Add(collect);var description=Ui.Text("总开关：收纳新复制的图片；暂停后仍可拖入文件。",12,Ui.Muted);description.Margin=new Thickness(0,8,0,0);collecting.Children.Add(description);
   collectFiles=new CheckBox{Content="收纳复制的图片文件",FontSize=14,Foreground=Ui.Ink,IsChecked=store.CollectFiles,Margin=new Thickness(0,18,0,0)};collecting.Children.Add(collectFiles);var filesDescription=Ui.Text("默认开启。总开关开启时，复制文件夹里的照片也会收纳；不处理剪切，原文件保留。",12,Ui.Muted);filesDescription.Margin=new Thickness(0,8,0,0);collecting.Children.Add(filesDescription);collectFiles.Checked+=ToggleFiles;collectFiles.Unchecked+=ToggleFiles;stack.Children.Add(Ui.Panel(collecting));collect.Checked+=Toggle;collect.Unchecked+=Toggle;
   var exported=new StackPanel();var label=Ui.Text("已取下的图片",14);label.FontWeight=FontWeights.SemiBold;exported.Children.Add(label);statistics.Margin=new Thickness(0,8,0,10);exported.Children.Add(statistics);var why=Ui.Text("拖出后保留副本，供目标软件稍后读取。发送完成后再清理。",12,Ui.Muted);why.Margin=new Thickness(0,0,0,12);exported.Children.Add(why);clear=Ui.Button("清理已取下图片");clear.HorizontalAlignment=HorizontalAlignment.Left;clear.Click+=Clear;exported.Children.Add(clear);var exportPanel=Ui.Panel(exported);exportPanel.Margin=new Thickness(0,12,0,12);stack.Children.Add(exportPanel);
   var folder=Ui.Button("打开保存目录");folder.HorizontalAlignment=HorizontalAlignment.Left;folder.Click+=(s,e)=>openFolder();stack.Children.Add(folder);var location=Ui.Text(store.Root,10,Ui.Muted);location.Margin=new Thickness(0,8,0,0);location.TextWrapping=TextWrapping.NoWrap;location.TextTrimming=TextTrimming.CharacterEllipsis;location.ToolTip=store.Root;stack.Children.Add(location);Height=Math.Min(Height,Math.Max(300,SystemParameters.WorkArea.Height-40));MinHeight=Math.Min(MinHeight,Height);Content=new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
   store.Changed+=Changed;Closed+=(s,e)=>{closed=true;store.Changed-=Changed;};Refresh();
  }
  void Changed(object sender,EventArgs e){if(!closed)Dispatcher.BeginInvoke(new Action(Refresh));}
  void Refresh(){if(closed)return;syncing=true;collect.IsChecked=store.Collecting;collectFiles.IsChecked=store.CollectFiles;syncing=false;int count=store.Snapshot().Count(x=>!x.Hanging);statistics.Text=count+" 张 · "+Ui.Size(store.ExportedBytes);clear.IsEnabled=count>0;}
  async void Toggle(object sender,RoutedEventArgs e){if(syncing)return;collect.IsEnabled=false;try{await save(collect.IsChecked==true);}finally{if(!closed){collect.IsEnabled=true;Refresh();}}}
  async void ToggleFiles(object sender,RoutedEventArgs e){if(syncing)return;collectFiles.IsEnabled=false;try{await saveFiles(collectFiles.IsChecked==true);}finally{if(!closed){collectFiles.IsEnabled=true;Refresh();}}}
  async void Clear(object sender,RoutedEventArgs e){clear.IsEnabled=false;try{await cleanup();}finally{Refresh();}}
 }
}
