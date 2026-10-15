using System.Threading.Tasks;
using System.Windows;
namespace ImageClothesline {
 public interface IImageActions {
  Task CopyAsync(ImageRecord image);void Preview(ImageRecord image);Task RemoveAsync(ImageRecord image,bool confirm=true);
  Task FinishDragAsync(ImageRecord image,DragDropEffects effect);Task ImportFilesAsync(string[] files);
  void HoldOpen(bool hold);void OpenSettings();
 }
}
