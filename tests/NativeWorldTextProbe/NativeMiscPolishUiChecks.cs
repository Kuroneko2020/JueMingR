using System;
using System.Collections;
using System.Linq;
using JueMingR.Features.Processing;
using JueMingR.Features.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // User requirements supply the oracle: an addable complete name exposes
    // one left-side command; preview cannot publish a settings command.
    internal static class NativeMiscPolishUiChecks
    {
        internal static void Processing(object context)
        {
            var host=Get(context,"Processing");var shell=Get(context,"Shell");var ui=Get(shell,"ReforgeUi");var state=Get(shell,"State");
            var settings=((ProcessingSettings[])Get(host,"Settings"))[2];var oldItem=Main.reforgeItem;
            string prefix=Lang.prefix[5].Value;
            try
            {
                Main.reforgeItem=new Item();NativeProcessingUiChecks.Save(settings,new ProcessingOptions());
                foreach(var size in new[]{new[]{960,760,100},new[]{1280,720,150},new[]{640,440,100}})
                {
                    Call(state,"ScrollTo",0f);
                    Action prepare=()=>NativeProcessingUiChecks.Prepare(context,size[0],size[1],size[2]/100f);
                    SetDraft(ui,"");prepare();Require(!Parts(ui).Any(p=>Command(p)==2),"empty reforge draft has no Add hit region");
                    SetDraft(ui,"not-a-prefix");prepare();Require(!Parts(ui).Any(p=>Command(p)==2),"invalid reforge draft has no Add hit region");
                    SetDraft(ui,prefix);long accepted=settings.AcceptedCommandId,revision=settings.Revision;prepare();
                    var add=Parts(ui).Single(p=>Command(p)==2);var field=Parts(ui).Single(p=>Command(p)==1);
                    var a=Rect(add);var f=Rect(field);
                    Require((float)Get(a,"Y")== (float)Get(f,"Y") && (float)Get(a,"Right")<=(float)Get(f,"X"),"valid Add stays left and on the field row at each viewport");
                    long scans=(long)Get(ui,"AdditionValidations"),layouts=(long)Get(ui,"LayoutBuilds");
                    for(int i=0;i<120;i++)prepare();
                    Require(scans==(long)Get(ui,"AdditionValidations") && layouts==(long)Get(ui,"LayoutBuilds"),"stable valid draft reuses validation and geometry");
                    Require(settings.AcceptedCommandId==accepted && settings.Revision==revision && !settings.Busy && settings.Value.Names.Count==0,"preview cannot save or mutate list revision");
                    var point=Point(add);Step(context,ui,false,point);Step(context,ui,true,point);SetDraft(ui,"invalid");Step(context,ui,false,point);prepare();
                    Require(settings.AcceptedCommandId==accepted && !Parts(ui).Any(p=>Command(p)==2),"draft invalidation revokes pressed Add before release");
                }
                SetDraft(ui,prefix);NativeProcessingUiChecks.Prepare(context,960,760,1);
                Main.reforgeItem=new Item();Main.reforgeItem.SetDefaults(1);Main.reforgeItem.damage=0;
                NativeProcessingUiChecks.Prepare(context,960,760,1);Require(!Parts(ui).Any(p=>Command(p)==2),"native rounded stat rejects an inapplicable damage prefix");
                Main.reforgeItem.damage=10;NativeProcessingUiChecks.Prepare(context,960,760,1);
                Require(Parts(ui).Any(p=>Command(p)==2),"same Item/type stat change invalidates the preview key");
                var current=Parts(ui).Single(p=>Command(p)==2);var oldPoint=Point(current);long before=settings.AcceptedCommandId;
                Step(context,ui,false,oldPoint);Step(context,ui,true,oldPoint);Main.reforgeItem.damage=0;Step(context,ui,false,oldPoint);
                Require(settings.AcceptedCommandId==before,"slot applicability change between press/release cannot save old Add");
                Main.reforgeItem=new Item();SetDraft(ui,prefix);NativeProcessingUiChecks.Prepare(context,960,760,1);
                current=Parts(ui).Single(p=>Command(p)==2);Step(context,ui,false,Point(current));Step(context,ui,true,Point(current));Step(context,ui,false,Point(current));
                NativeQuickItemChecks.Until(()=>{settings.Poll();return !settings.Busy;});
                Require(settings.Value.Names.SequenceEqual(new[]{prefix}),"actual Add click persists exactly one complete name through shared submit");
                SetDraft(ui,prefix);NativeProcessingUiChecks.Prepare(context,960,760,1);Require(!Parts(ui).Any(p=>Command(p)==2),"duplicate complete draft has no Add control");
                var name=Parts(ui).First(p=>GetOptional(Get(p,"Element"),"Description")!=null);
                NativeQuickUiChecks.FinalHint(shell,Get(name,"Element"),"按住重铸键直到名单内前缀停下(要按两下)");
                var body=Parts(ui).Single(p=>Command(p)==7);var bodyPoint=Point(body);object[] bodyArgs={bodyPoint.X,bodyPoint.Y,null,null};
                Require(ui.GetType().GetMethod("Hint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(ui,bodyArgs)==null,"prefix card body has no extra hover text");
                var cross=Parts(ui).Single(p=>Command(p)==3);var crossPoint=Point(cross);object[] crossArgs={crossPoint.X,crossPoint.Y,null,null};
                Require((string)ui.GetType().GetMethod("Hint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(ui,crossArgs)=="从名单移除「"+prefix+"」","overlapping delete corner retains full-name hint priority");
                Console.WriteLine("PASS Misc polish reforge preview, paired geometry, exact hints, real click, no-save/stable cache, duplicate and same-object native applicability invalidation.");
            }
            finally{Main.reforgeItem=oldItem;NativeProcessingUiChecks.Save(settings,new ProcessingOptions());SetDraft(ui,"");}
        }
        internal static void CaptureGeometry(object popup)
        {
            var buttons=((IEnumerable)Get(popup,"buttons")).Cast<object>().ToArray();var cells=((IEnumerable)Get(popup,"cells")).Cast<object>().ToArray();var panel=Get(popup,"Panel");
            Require(buttons.Length==8 && cells.Length==8,"capture preserves two columns and four rows");
            for(int i=0;i<8;i++)
            {
                var text=Get(buttons[i],"TextSize");var rect=cells[i];
                Require((float)Get(rect,"Width")>=(float)Get(text,"Width")+36,"capture longest text has symmetric checkmark clearance");
                if(i>=2)Require((float)Get(rect,"Y")>(float)Get(cells[i-2],"Y"),"capture category order retains four rows");
            }
            Require((float)Get(panel,"Bottom")-(float)Get(cells[7],"Bottom")<=12,"deleted capture notes leave no extra line height");
        }
        private static object[] Parts(object ui){return NativeProcessingUiChecks.Parts(ui);}
        private static int Command(object part){return (int)Get(part,"Command");}
        private static object Rect(object part){return Get(Get(part,"Element"),"Rect");}
        private static Vector2 Point(object part){var r=Rect(part);return new Vector2((float)Get(r,"X")+3,(float)Get(r,"Y")+3);}
        private static void SetDraft(object ui,string text)
        {ui.GetType().GetField("draft",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ui,new TextEditBuffer(text,true,128,512,"词缀名称最多 128 个字"));}
        private static void Step(object context,object ui,bool left,Vector2 point)
        {
            Main.keyState=new KeyboardState();var input=Get(context,"Input");NativeToolsUiChecks.Mouse(point,left);
            Call(input,"BeginUpdate");Call(ui,"BeforeInput",true);Call(input,"AfterMapping");PlayerInput.WritingText=false;Call(input,"AfterKeyboardRefresh");Main.keyCount=0;
            Call(ui,"ProcessInput",true,new KeyboardState(),point,true,true,false);
        }
    }
}
