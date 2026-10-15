namespace ImageClothesline {
 public sealed class RevealGate {
  long? entered,left;long manualUntil,imageUntil;bool suppressed;
  public bool Visible {get;private set;}
  public void Tick(long now,bool trigger,bool panel,bool busy,bool fullscreen){
   if(!trigger)suppressed=false;
   if(fullscreen&&!busy&&now>=manualUntil){Visible=false;entered=null;left=null;return;}
   if(!Visible){
    if(trigger&&!suppressed){if(!entered.HasValue)entered=now;if(now-entered.Value>=250){Visible=true;left=null;}}
    else entered=null;return;
   }
   if(trigger||panel||busy||now<manualUntil){left=null;return;}
   if(now<imageUntil){left=imageUntil-500;return;}
   if(!left.HasValue)left=now;
   if(now-left.Value>=500){Visible=false;entered=null;left=null;}
  }
  public void ShowManually(long now){Visible=true;manualUntil=now+3000;left=null;entered=null;suppressed=false;}
  public bool ShowForImage(long now,bool fullscreen){if(fullscreen)return false;Visible=true;imageUntil=now+2000;left=null;entered=null;suppressed=false;return true;}
  public void HideManually(){Visible=false;manualUntil=0;imageUntil=0;left=null;entered=null;suppressed=true;}
 }
}
