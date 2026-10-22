using System;
using System.IO;
using System.Linq;
using Terraria.ID;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Finite original-Main regression. Only the OS input provider is replaced;
    // native input/mapping, UI layers and the production editor all execute.
    // No pointer certificate callback or success flag is invoked or assigned.
    internal static class NativeBlueprintOriginalChecks
    {
        internal static void Run(Main game, object context, Action inputStep, Action draw, Func<RenderTarget2D> canvas, string output, Action<int,int,bool> mouse)
        {
            var host=Get(context,"Blueprint"); var input=Get(context,"Input"); var shell=Get(context,"Shell");
            var state=(BlueprintCaptureState)Get(host,"State");var p=Main.LocalPlayer;
            Require(GetOptional(host,"SetupError")==null,"blueprint hooks installed in initialized original Main");
            Action<int,int,bool> sample=(x,y,down)=>{mouse(x,y,down);inputStep();draw();};
            Action neutral=()=>{sample(1050,550,false);sample(1050,550,false);};
            Action drag=()=>{neutral();sample(1050,550,true);sample(1130,590,true);sample(1130,590,true);};
            Action release=()=>{sample(1130,590,false);sample(1130,590,false);};
            Func<string> selected=()=>String.Join(";",state.Selection.EnumerateVisible(0,0,Main.maxTilesX,Main.maxTilesY).Select(s=>s.Y+":"+s.Left+":"+s.RightExclusive));
            Action<string> image=name=>{using(var stream=File.Create(Path.Combine(output,name+".png")))canvas().SaveAsPng(stream,Main.screenWidth,Main.screenHeight);};
            try
            {
                Call(Get(context,"Shell"),"CloseAndSubmitPosition"); Main.playerInventory=false;
                Main.GameZoomTarget=1f; Main.LocalPlayer.mouseInterface=false;Main.mouseText=false;
                mouse(1050,550,true);inputStep();Call(host,"Toggle");draw();
                sample(1130,590,true);Require(!state.Dragging&&!state.Selection.HasSelection,"startup click cannot become first stroke");
                sample(1050,550,false);
                sample(1050,550,false);
                sample(1050,550,true);
                sample(1130,590,true);
                sample(1130,590,true);
                bool preview=state.Dragging;
                image("blueprint-held-preview");
                sample(1130,590,false);
                sample(1130,590,false);
                Require(preview,"actual original UI certifies held world drag and visible preview before release");
                Require(state.Selection.HasSelection&&!state.Dragging,"actual input and UI release commits one XOR");
                string first=selected();draw();draw();neutral();
                Require(selected()==first,"repeat Draw and ordinary Draw intervals do not repeat XOR or clear committed selection");
                drag();release();Require(!state.Selection.HasSelection,"same rectangle selected twice cancels by XOR");
                sample(1050,550,false);
                sample(1050,550,false);
                mouse(1050,550,true);inputStep();
                long mapping=(long)Get(input,"Mapping");
                Call(input,"BeginUpdate");Call(context,"UpdateRuntime");
                Require((long)Get(input,"Mapping")==mapping&&!(bool)Get(input,"CanStartActions")&&(bool)Get(input,"CanRetainIntent"),"empty outer Update retains focus without new mapping permission");
                draw();
                sample(1130,590,true);
                sample(1130,590,true);
                Require(state.Dragging,"empty outer Update between native input and Draw must retain the held draft");
                mouse(1160,600,true);inputStep();inputStep();
                Require(state.Dragging,"two mapped updates without Draw retain existing held draft");
                draw();sample(1130,590,true);release();
                Require(state.Selection.HasSelection,"certified release after scheduling gaps commits once");first=selected();
                mouse(1130,590,true);inputStep(); // no Draw for the new press
                sample(1160,600,true);sample(1160,600,true);
                Require(!state.Dragging,"uncertified skipped press is not replayed from a later held sample");
                release();Require(selected()==first,"skipped press preserves committed coordinates");
                drag();mouse(1130,590,false);inputStep();inputStep();draw();neutral();
                Require(!state.Dragging&&selected()==first,"unobserved release cancels draft instead of replaying late commit");

                drag();Set(input,"foregroundWindow",(Func<IntPtr>)(()=>IntPtr.Zero));inputStep();draw();
                Require(!state.Dragging&&state.NeedsNeutral&&selected()==first,"focus rejection cancels only draft");
                Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));neutral();neutral();
                drag();Require(state.Dragging,"fresh neutral and press recover after focus loss");
                Main.playerInventory=true;sample(1050,550,true);
                Require(!state.Dragging&&selected()==first,"explicit native inventory cancels draft and preserves committed set");
                // The real DrawInventory -> ItemSlot.Handle consumer must still
                // pick up a slot item while world capture is active.
                Main.mouseItem=new Item();p.inventory[10].SetDefaults(ItemID.DirtBlock);p.inventory[10].stack=3;
                int sx=(int)(40*Main.UIScale),sy=(int)(88*Main.UIScale);
                sample(sx,sy,false);sample(sx,sy,true);
                Require(Main.mouseItem.type==ItemID.DirtBlock&&Main.mouseItem.stack==3&&p.inventory[10].IsAir,"actual native inventory slot click stays usable during capture");
                sample(sx,sy,false);sample(1050,550,true);Main.playerInventory=false;sample(1050,550,true);
                Require(!state.Dragging&&selected()==first,"UI carried-item gesture cannot become world drag outside inventory");
                Main.mouseItem=new Item();neutral();

                p.inventory[0].SetDefaults(ItemID.CopperShortsword);p.selectedItemState.Select(0);p.selectedItemState.Update();
                sample(1050,550,true);PlayerInput.Triggers.Current.CopyInto(p);
                int before=p.itemAnimation;p.ItemCheck();
                Require(!p.controlUseItem&&!p.controlUseTile&&p.itemAnimation==before,"capture blocks held ItemCheck attack initiation");
                Call(host,"Toggle");neutral();sample(1050,550,true);PlayerInput.Triggers.Current.CopyInto(p);p.ItemCheck();
                Require(p.itemAnimation>before,"exit and fresh press restore native attack");
                release();p.itemAnimation=p.itemTime=0;

                p.inventory[0].SetDefaults(ItemID.Gel);Call(host,"SetToolbar",true);neutral();draw();
                var toolbar=Get(host,"Toolbar");var layout=Get(toolbar,"Layout");
                Action<int,bool> button=(index,down)=>
                {
                    var panel=Get(layout,"Panel");var b=((System.Collections.IEnumerable)Get(layout,"Buttons")).Cast<object>().ElementAt(index);var rect=Get(b,"Rect");
                    sample((int)(((float)Get(panel,"X")+(float)Get(rect,"X")+(float)Get(rect,"Width")/2)*Main.UIScale),
                        (int)(((float)Get(panel,"Y")+(float)Get(rect,"Y")+(float)Get(rect,"Height")/2)*Main.UIScale),down);
                };
                button(0,false);button(0,true);button(0,false);
                Require(state.Active&&!state.Dragging&&!state.Selection.HasSelection,"actual toolbar capture button executes without world selection");
                sample(1050,550,true);Require(!state.Dragging,"startup gesture is not reinterpreted outside bar");neutral();
                Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");
                foreach(float scale in new[]{1f,1.5f})
                {
                    Main.UIScale=scale;neutral();draw();button(0,false);draw();
                    Require((bool)Get(Get(toolbar,"hint"),"Visible"),"actual disabled-save hint draws at UI "+scale);
                    image("blueprint-hint-ui-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                Console.WriteLine("PASS BLUEPRINT original input/native UI: startup barrier, held preview, once-only XOR/cancel, empty outer Update, Draw gaps/no replay, focus/UI recovery, actual inventory slot/bar button, attack block/exit restore, native hint screenshots UI100/150. Provider injection is not OS input, ordinary package load, multiplayer, FPS or owner acceptance.");
            }
            finally { Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));state.Exit();mouse(1050,550,false);Main.playerInventory=false;Main.mouseItem=new Item(); }
        }
    }
}
