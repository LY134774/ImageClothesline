using System.IO;
using System.Drawing.Imaging;
namespace ImageClothesline.Tests {
 public static class IconWriter {
  public static int Main(string[] args){
   int[] sizes={16,24,32,48,64,128,256};var frames=new byte[sizes.Length][];
   for(int i=0;i<sizes.Length;i++)using(var bitmap=IconFactory.DrawBitmap(sizes[i]))using(var data=new MemoryStream()){bitmap.Save(data,ImageFormat.Png);frames[i]=data.ToArray();}
   using(var stream=File.Create(args[0]))using(var writer=new BinaryWriter(stream)){
    writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);uint offset=(uint)(6+16*sizes.Length);
    for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write((uint)frames[i].Length);writer.Write(offset);offset+=(uint)frames[i].Length;}
    foreach(var frame in frames)writer.Write(frame);
   }
   return 0;
  }
 }
}
