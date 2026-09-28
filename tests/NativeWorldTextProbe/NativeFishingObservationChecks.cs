using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Fishing;
using JueMingR.Platform.Information;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFishingObservationChecks
    {
        private static readonly Vector2 Aim=new Vector2(850,718);
        internal static void Run(object context)
        {
            // Additional native simulation must not change the random stream
            // used by later outcome/storage fixtures in this same process.
            var prior=Main.rand;
            try{Main.rand=new Terraria.Utilities.UnifiedRandom(109);Idle(context);Triggers(context);Borrow(context);ReadOnly(context);}
            finally{Main.rand=prior;}
        }
        internal static void Idle(object context)
        {
            object host=Get(context,"Fishing"),tools=Get(context,"Tools"),input=Get(context,"Input"),observation=Get(host,"Observation");
            var failures=new List<string>();
            var modes=new[]{new FishingOptions(auto:true),new FishingOptions(loadout:true),new FishingOptions(equipment:true),new FishingOptions(storeMode:1),new FishingOptions(storeMode:2)};
            string[] names={"Auto","Loadout","Equipment","StoreAll","StoreQuest"};
            for(int mode=0;mode<modes.Length;mode++)foreach(int held in new[]{ItemID.StoneBlock,ItemID.WoodFishingPole})
            {
                NativeFishingChecks.Save(host,new FishingOptions());
                var p=NativeToolExecutionChecks.Reset(context,tools,input,held,0,0);
                p.inventory[54].SetDefaults(ItemID.Worm);p.inventory[54].stack=100;
                Call(host,"OnSessionStarted");NativeFishingChecks.Save(host,modes[mode]);
                long before=(long)Get(observation,"Scans");
                NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
                long initialized=(long)Get(observation,"Scans")-before;
                before=(long)Get(observation,"Scans");uint tick=Main.GameUpdateCount;
                for(int i=0;i<120;i++)NativeFishingChecks.Step(context,input,new Vector2(850,718),false,0);
                long scans=(long)Get(observation,"Scans")-before;
                string label=names[mode]+" held="+held;
                Console.WriteLine("G10 idle observation "+label+" initialization="+initialized+" updates="+(Main.GameUpdateCount-tick)+" scans="+scans);
                Require(Main.GameUpdateCount-tick==120 && (bool)Get(host,"NeedsSession") && !(bool)Get(Get(host,"Session"),"Active"),"enabled idle fixture advances native steps without disabling its consumer: "+label);
                if(scans!=0)failures.Add(label+" scans="+scans);
            }
            NativeFishingChecks.Save(host,new FishingOptions());
            Require(failures.Count==0,"enabled but untriggered fishing must not repeat full projectile scans: "+string.Join("; ",failures));
            Console.WriteLine("PASS G10 enabled idle observation demand for all five session settings, with and without a held rod.");
        }
        private static Player Reset(object context,FishingOptions options)
        {
            object host=Get(context,"Fishing");NativeFishingChecks.Save(host,new FishingOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),ItemID.WoodFishingPole,0,0);
            Call(host,"OnSessionStarted");p.releaseUseItem=true;p.inventory[54].SetDefaults(ItemID.Worm);p.inventory[54].stack=100;
            p.armor[3].SetDefaults(ItemID.HighTestFishingLine);
            for(int x=44;x<74;x++)for(int y=42;y<61;y++){Main.tile[x,y].ClearEverything();if(y>=44 && y<60)Main.tile[x,y].liquid=255;if(y==60)NativeToolsChecks.Tile(x,y,1);}
            NativeFishingChecks.Save(host,options);return p;
        }
        private static Projectile Bobber(Player p){return Main.projectile.FirstOrDefault(b=>b.active && b.bobber && b.owner==p.whoAmI);}
        private static void Step(object context,bool held=false){NativeFishingChecks.Step(context,Get(context,"Input"),Aim,held,0);}
        private static void HostSteps(object host,int count)
        {for(int i=0;i<count;i++){NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",(ulong)Main.GameUpdateCount);}}
        private static void NoScans(object host,int count,string reason)
        {
            object observation=Get(host,"Observation");long scans=(long)Get(observation,"Scans");HostSteps(host,count);
            Require((long)Get(observation,"Scans")==scans,reason);
        }
        private static void UntilWet(object context,Player p,bool active)
        {
            for(int i=0;i<180;i++)
            {
                Step(context);var b=Bobber(p);
                if(b!=null && b.wet && b.ai[0]<1)
                {Require((bool)Get(Get(Get(context,"Fishing"),"Session"),"Active")==active,"first native liquid update has the expected session admission without extra delay");return;}
            }
            throw new InvalidOperationException("native first cast did not reach the fixture pond");
        }
        private static void Triggers(object context)
        {
            object host=Get(context,"Fishing"),session=Get(host,"Session"),observation=Get(host,"Observation");
            var p=Reset(context,new FishingOptions(auto:true));
            // The original start-use gate allows nonzero itemTime. Shooting
            // waits for itemTime to reach zero, independently of the animation.
            p.itemTime=3;Step(context,true);
            Require(Bobber(p)==null && (bool)Get(session,"manualCast"),"real Started can precede projectile creation across updates");
            for(int i=0;i<8 && Bobber(p)==null;i++)Step(context);
            var b=Bobber(p);Require(b!=null && !b.wet && !(bool)Get(session,"Active"),"delayed original shoot wakes dry first-cast observation");
            // Synthetic long flight keeps a real native-created identity; no
            // projectile AI is advanced in this interval, and no timeout may
            // retire it just because it has not reached liquid yet.
            p.itemAnimation=0;HostSteps(host,650);
            Require(!(bool)Get(session,"Active") && (bool)Get(session,"manualCast"),"arbitrarily long live dry cast retains its first-cast eligibility");
            UntilWet(context,p,true);
            long token=(long)Get(session,"Token");
            foreach(var q in Main.projectile)if(q.bobber && q.owner==p.whoAmI)q.active=false;
            HostSteps(host,1);Require(!(bool)Get(session,"Active") && (int)Get(observation,"Count")==0,"last native bobber disappearance retires the session and snapshot");
            NoScans(host,120,"natural stop with Auto still enabled does not poll the projectile table");
            Step(context,true);UntilWet(context,p,true);
            Require((long)Get(session,"Token")>token,"a later legal cast starts without toggling Auto");
            p.selectedItemState.Select(1);p.selectedItemState.Update();
            NoScans(host,120,"manual rod takeover stops G10 observation even before native AI removes the old bobber");

            p=Reset(context,new FishingOptions(auto:true));p.itemTime=30;Step(context,true);
            Require((bool)Get(session,"manualCast") && Bobber(p)==null,"real start-use can have no projectile before its animation expires");
            for(int i=0;i<40;i++)Step(context);
            Require(Bobber(p)==null && !(bool)Get(session,"Active") && !(bool)Get(session,"manualCast"),"completed native use without a bobber retires pending first-cast intent");
            NoScans(host,120,"failed original first cast leaves no permanent observation task");
            Step(context,true);UntilWet(context,p,true);
            NativeFishingChecks.Save(host,new FishingOptions());HostSteps(host,1);
            NoScans(host,120,"disabled session retires observation despite a still-live bobber");
            Console.WriteLine("PASS G10 delayed/failed native first casts, long dry flight, first-wet admission, disappearance, manual takeover and subsequent reactivation.");
        }
        private static void Display(object information,bool enabled)
        {
            NativeQuickItemChecks.Until(()=>{Call(information,"PollPreferences");return (bool)Get(information,"CanConfigure");});
            Require((bool)Call(information,"SetEnabled",InformationKind.FullFish,enabled),"read-only fish display setting admitted");
            NativeQuickItemChecks.Until(()=>{Call(information,"PollPreferences");return Get(Get(information,"Preferences"),"Status").ToString()=="Saved";});
        }
        private static void Borrow(object context)
        {
            object host=Get(context,"Fishing"),tools=Get(context,"Tools"),session=Get(host,"Session"),borrow=Get(tools,"Fishing"),observation=Get(host,"Observation");
            var p=Reset(context,new FishingOptions(auto:true));Step(context,true);UntilWet(context,p,true);
            for(int i=0;i<40;i++)Step(context);
            long sessionToken=(long)Get(session,"Token");NativeToolsChecks.SetMode(tools,0,1);
            long loan=(long)Call(borrow,"Prepare",p);Require(loan>0,"real waiting bobber grants a G09 borrow token");
            HostSteps(host,1);NoScans(host,120,"G09 owns the loan interval without waking G10's full observation");
            Require((bool)Get(session,"Active") && (long)Get(session,"Token")==sessionToken,"observation suspension preserves the G10 participant identity");
            // Exercise the established loan-completion boundary without the
            // unrelated GPU net-hit fixture; the compensation uses real native
            // Player.Update, selection, ItemCheck and projectile creation.
            foreach(var q in Main.projectile)if(q.bobber && q.owner==p.whoAmI)q.active=false;
            Call(borrow,"NetFinished",loan,false,false);
            for(int i=0;i<150 && (bool)Get(borrow,"Active");i++)Step(context);
            Require(Get(borrow,"Phase").ToString()=="Completed" && (bool)Get(borrow,"RecastObserved"),"G09's single original compensation creates a new bobber");
            UntilWet(context,p,true);
            Require((long)Get(session,"Token")==sessionToken && (int)Get(observation,"Count")>0,"G10 resumes observation under the same token after the loan ends");
            NativeToolsChecks.SetMode(tools,0,0);NativeFishingChecks.Save(host,new FishingOptions());HostSteps(host,1);
            Console.WriteLine("PASS G10 borrowed observation suspension and same-session return through G09's real native compensation.");
        }
        private static void ReadOnly(object context)
        {
            object host=Get(context,"Fishing"),observation=Get(host,"Observation"),display=Get(host,"Display"),information=Get(display,"information");
            var p=Reset(context,new FishingOptions());Display(information,true);HostSteps(host,1);
            NoScans(host,120,"enabled read-only display without any bobber performs only its initial discovery");
            Require(GetOptional(display,"Full")==null,"no legal fishing point has no invented preview");
            Step(context,true);UntilWet(context,p,false);
            // First wet contact straddles the surface tile. Let native motion
            // settle the bobber into the pond before asserting its fish list.
            for(int i=0;i<60;i++)Step(context);
            Require(!string.IsNullOrEmpty((string)GetOptional(display,"Full")) && !(bool)Get(host,"NeedsSession"),"real native cast wakes the read-only display with every automation setting off");
            Display(information,false);HostSteps(host,1);NoScans(host,120,"display off with automation off performs no domain reads");
            Display(information,true);HostSteps(host,1);
            Require(!string.IsNullOrEmpty((string)GetOptional(display,"Full")),"enabling a display discovers a pre-existing wet bobber without another cast");
            Display(information,false);HostSteps(host,1);

            // Invoke the same command used by the tested F5 Current button.
            // A one-shot fresh query must not turn into an automatic consumer.
            NativeFishingChecks.Save(host,new FishingOptions(filterMode:1));
            object ui=Get(Get(context,"Shell"),"FishingUi");
            var open=ui.GetType().GetMethod("Open",BindingFlags.Instance|BindingFlags.NonPublic);
            long scans=(long)Get(observation,"Scans");
            open.Invoke(ui,new[]{Enum.Parse(open.GetParameters()[0].ParameterType,"Current")});
            Require((long)Get(observation,"Scans")==scans+1 && ((FishKey[])Get(ui,"candidates")).Contains(new FishKey(FishKind.Item,ItemID.Bass)),"explicit Current query scans once and returns real candidates with Auto and display off: scans="+((long)Get(observation,"Scans")-scans)+" message="+GetOptional(ui,"message")+" position="+Bobber(p)?.Center);
            Call(ui,"CloseOverlay");HostSteps(host,1);NoScans(host,120,"explicit query creates no persistent scan demand");

            Display(information,true);HostSteps(host,1);var old=Bobber(p);int slot=Array.IndexOf(Main.projectile,old);
            Main.projectile[slot]=new Projectile{whoAmI=slot};
            HostSteps(host,1);
            Require(old.active && (int)Get(observation,"Count")==0 && GetOptional(display,"Full")==null,"old active object cannot survive replacement of its native slot");
            NoScans(host,120,"retired slot identity cannot keep empty display scanning");
            old.active=false;Main.projectile[slot]=old;
            Step(context,true);UntilWet(context,p,false);
            Require(!string.IsNullOrEmpty((string)GetOptional(display,"Full")),"creation after slot reuse wakes a fresh read-only display");
            Call(host,"OnSessionEnded");Call(host,"OnSessionStarted");
            Require((int)Get(observation,"Count")==0 && GetOptional(display,"Full")==null,"world/session boundary clears prior observations before any new demand");
            foreach(var q in Main.projectile)q.active=false;
            HostSteps(host,1);NoScans(host,120,"new empty world performs only one display discovery, without old identity reuse");
            Display(information,false);NativeFishingChecks.Save(host,new FishingOptions());HostSteps(host,1);
            Console.WriteLine("PASS G10 independent read-only demand, native creation wakeup, existing-bobber enable, one-shot Current query and slot/session retirement.");
        }
    }
}
