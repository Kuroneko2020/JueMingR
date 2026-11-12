using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.AfkMode;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Settings;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Real Player.Update/ItemCheck, factory and Projectile.Update supply all
    // starts, timers, releases and retirements. Only cosmetic/achievement
    // outlets inherited from the existing headless fixture are intercepted.
    internal static class NativeAfkModeChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static object owner;
        private static readonly List<uint> starts=new List<uint>();
        private static readonly List<uint> completions=new List<uint>();
        private static int births,previousCompletions;
        private static Vector2 lastBulletVelocity;
        private static bool lateVeto;
        private static bool startupTailAudit;
        internal static void Run(object context,string section="all")
        {
            owner=Get(context,"Afk");var tools=Get(context,"Tools");var input=Get(context,"Input");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(owner,"ControlsEnabled") && ((JueMingR.Features.Tools.ToolSettings[])Get(tools,"Settings")).All(x=>x.Loaded);});
            NativeCombatAimChecks.Initialize();
            var audit=new Harmony("JueMingR.Tests.AfkMode");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(AccessTools.Method(typeof(Terraria.GameContent.Achievements.AchievementsHelper),name),prefix:new HarmonyMethod(typeof(NativeAfkModeChecks),nameof(Skip)));
            audit.Patch(AccessTools.Method(typeof(Player),"ItemCheck_StartActualUse"),postfix:new HarmonyMethod(typeof(NativeAfkModeChecks),nameof(Started)));
            audit.Patch(AccessTools.Method(typeof(Player),"TryUpdateChannel"),postfix:new HarmonyMethod(typeof(NativeAfkModeChecks),nameof(Created)));
            audit.Patch(AccessTools.Method(typeof(Player),"ItemCheck",Type.EmptyTypes),prefix:new HarmonyMethod(typeof(NativeAfkModeChecks),nameof(LateVeto)){priority=Priority.First});
            audit.Patch(AccessTools.Method(typeof(Projectile),"AI"),transpiler:new HarmonyMethod(typeof(NativeCombatBeamChecks).GetMethod("RippleOutlet",BindingFlags.NonPublic|BindingFlags.Static)));
            try
            {
                Preferences();
                Player p;
                if(section!="borrow")
                {
                p=Reset(context,98,300);
                for(int i=0;i<4;i++)Step(context,false);
                Require(starts.Count==0 && !(bool)Get(owner,"Running"),"enabling arms only; no accepted use");
                Step(context,true);for(int i=0;i<230;i++)Step(context,false);
                Require(starts.Count>=5 && completions.Count>=4,"real automatic minishark cycles continue after physical release");
                Require(p.inventory[54].stack<9999,"AFK consumes original ammunition");
                Console.WriteLine("AFK timeline gun starts="+string.Join(",",starts)+" completed="+string.Join(",",completions));

                Reset(context,98,0);for(int i=0;i<180;i++)Step(context,true);int automated=starts.Count;
                Reset(context,98,0);Call(owner,"SetModeEnabled",false);for(int i=0;i<180;i++)Step(context,true);int native=starts.Count;
                Require(automated==native,"zero gap preserves exact real native held cadence: auto="+automated+" native="+native);

                Reset(context,434,200);Step(context,true);for(int i=0;i<240;i++)Step(context,false);
                Require(starts.Count>=3 && births>=completions.Count*3 && births<=starts.Count*3,"same-animation completed three-shot volleys remain intact: starts="+starts.Count+" births="+births);
                Reset(context,55,200);Step(context,true);for(int i=0;i<260;i++)Step(context,false);
                Require(starts.Count>=2 && completions.Count>=1,"returning boomerang actually retires before gap and next use");
                Reset(context,198,200);Step(context,true);for(int i=0;i<260;i++)Step(context,false);
                Require(starts.Count>=2 && completions.Count>=1 && births>=2,"native release-shot completes its real release and return before the gap");

                Reset(context,3278,200);Step(context,true);for(int i=0;i<340;i++)Step(context,false);
                Require(starts.Count>=2 && completions.Count>=1,"yoyo throws, reaches effective target, naturally returns and repeats");
                Console.WriteLine("AFK timeline yoyo starts="+string.Join(",",starts)+" completed="+string.Join(",",completions));

                Reset(context,1309,200);Step(context,true);for(int i=0;i<180;i++)Step(context,false);
                Require(starts.Count>=2 && Main.projectile.Any(q=>q.active && q.minion),"summoning action completes independently of persistent minion");
                if(section=="completion"){Console.WriteLine("PASS AFK completion: real normal/burst/release/return/yoyo/summon timelines and exact zero-gap native cadence.");return;}
                }

                p=Reset(context,3541,300);Step(context,true);for(int i=0;i<240;i++){p.statMana=200;Step(context,true);}
                Require(starts.Count==1 && completions.Count==0 && p.channel,"infinite prism remains one real continuous operation, no periodic gap release");
                QuickBorrow(context,p,true);
                p=Reset(context,3541,300);Step(context,true);for(int i=0;i<45;i++){p.statMana=200;Step(context,true);}
                QuickBorrow(context,p,true,98);
                OverrideSelection(context);
                AutomaticTool(context);
                AimConsumption(context);
                StartupTail(context);
                RejectedQuickRequests(context);
                Boundaries(context);
                Costs(context);
                Console.WriteLine("PASS AFK borrow/boundaries: persistent channel, explicit quick return, override-slot selection, manual completion timing, UI/selection veto and bounded work.");
            }
            finally{Call(owner,"SetModeEnabled",false);foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);owner=null;}
        }
        private static void Preferences()
        {
            var codec=new AfkPreferenceCodec();var value=new AfkPreferences(true,int.MaxValue);
            Require(codec.Decode(codec.Encode(value)).Equals(value),"nonnegative int range codec roundtrip");
            foreach(string number in new[]{"-1","2147483648","1.5"})
            {
                bool rejected=false;try{codec.Decode(System.Text.Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.AfkMode\",\"version\":1,\"enabled\":true,\"gapMs\":"+number+"}"));}catch(PreferenceFormatException){rejected=true;}
                Require(rejected,"invalid persisted gap rejected: "+number);
            }
        }
        private static Player Reset(object context,int item,int gap)
        {
            Call(owner,"SetModeEnabled",false);var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);
            Call(Get(context,"Shell"),"CloseAndSubmitPosition");
            p.inventory[54].SetDefaults(97);p.inventory[54].stack=9999;p.reuseDelay=0;p.statMana=p.statManaMax=p.statManaMax2=200;p.releaseUseItem=true;
            Main.blockMouse=false;p.delayUseItem=false;Main.drawingPlayerChat=false;Main.mapFullscreen=false;
            NativeCombatCadenceChecks.Step(context,false,false,0);
            Call(owner,"SetGapMilliseconds",gap);Call(owner,"SetModeEnabled",true);
            starts.Clear();completions.Clear();births=0;previousCompletions=(int)Get(owner,"Completions");return p;
        }
        private static void Step(object context,bool left,Vector2? point=null)
        {
            NativeCombatCadenceChecks.Step(context,left,false,0,point:point??new Vector2(825,660));
            int completed=(int)Get(owner,"Completions");if(completed!=previousCompletions){previousCompletions=completed;completions.Add((uint)Get(owner,"LastCompletion"));}
        }
        private static void Started(Player __instance,Item __0)
        {
            if(__instance.whoAmI!=Main.myPlayer || owner==null)return;
            bool owned=(int)Get(owner,"inCheck")>0;
            if(startupTailAudit)Require(owned && (long)Get(owner,"token")!=0,"ALL native starts during the startup hold belong to an admitted AFK operation");
            if(starts.Count<3)Console.WriteLine("AFK START tick="+Main.GameUpdateCount+" owned="+owned+" operating="+Get(owner,"operating")+" token="+Get(owner,"token")+" native animation="+__instance.itemAnimation+" time="+__instance.itemTime+" completions="+Get(owner,"Completions")+" manual="+Get(owner,"manual"));
            if(owned && starts.Count>0)
            {
                if(__0.type==434)Require(births==starts.Count*3,"the next actual use cannot truncate the preceding three-shot animation");
                uint last=(uint)Get(owner,"LastCompletion");int gap=(int)Get(owner,"GapMilliseconds");
                Require(unchecked(Main.GameUpdateCount-last)>=(uint)(((long)gap*60+999)/1000),"next actual AFK start must follow actual completion + gap");
                if(gap>0)Require((int)Get(owner,"Completions")>0,"no second accepted AFK start before a completion receipt");
            }
            starts.Add(Main.GameUpdateCount);
        }
        private static void Created(Player __instance,Projectile __0){if(__instance.whoAmI==Main.myPlayer){births++;if(__0.type==14)lastBulletVelocity=__0.velocity;}}
        private static bool Skip(){return false;}
        private static void LateVeto(Player __instance){if(lateVeto && ReferenceEquals(__instance,Main.LocalPlayer))__instance.delayUseItem=true;}
        private static void QuickBorrow(object context,Player p,bool hold=false,int borrowedType=50)
        {
            var quick=Get(context,"QuickItems");var settings=(QuickItemSettings)Get(quick,"Settings");
            p.inventory[5].SetDefaults(borrowedType);
            var entry=new QuickItemEntry("0123456789abcdef0123456789abcdef",borrowedType,QuickItemMode.Use,true,true);string reason;
            Require(settings.TryChange(new QuickItemDocument(false,true,new[]{entry}),entry.Id,out reason),"explicit quick borrow fixture accepted: "+reason);
            NativeQuickItemChecks.Until(()=>{Call(quick,"Poll");return !settings.Busy;});
            JueMingR.Platform.Hotkeys.HotkeyChord chord;string chordReason;
            Require(JueMingR.Platform.Hotkeys.HotkeyChord.TryCreate(65,JueMingR.Platform.Hotkeys.HotkeyModifiers.None,out chord,out chordReason),"quick fixture chord");
            Call(quick,"Request",entry.Id,chord);
            Call(quick,"AfterDispatch",true);
            Require((bool)Get(owner,"Running") && (bool)Get(owner,"quickRequested"),"manual shortcut retains AFK run while releasing real prism");
            int startsBeforeBorrow=starts.Count;int afkStartsBeforeBorrow=(int)Get(owner,"Starts");
            bool borrowed=false;
            for(int i=0;i<260;i++)
            {
                Step(context,hold);
                borrowed|=(bool)Get(Get(quick,"Use"),"Active");
                p.statMana=200;
            }
            Require(borrowed && (bool)Get(owner,"Running") && p.selectedItem==0 && !(bool)Get(Get(quick,"Use"),"Active"),"explicit temporary item obtains token, completes original use and returns without killing the AFK run: borrowed="+borrowed+" running="+Get(owner,"Running")+" slot="+p.selectedItem+" active="+Get(Get(quick,"Use"),"Active")+" message="+GetOptional(quick,"Message"));
            if(borrowedType==98)Require(starts.Count==startsBeforeBorrow+2 && (int)Get(owner,"Starts")==afkStartsBeforeBorrow+1 && p.channel,"held startup tail permits exactly one native autoReuse borrow and then resumes the original prism");
        }
        private static void Boundaries(object context)
        {
            var vetoPlayer=Reset(context,98,300);lateVeto=true;
            try{Step(context,true);Require(starts.Count==0 && !(bool)Get(owner,"Running"),"native veto arising AFTER Sync revokes an unaccepted startup instead of firing later");}
            finally{lateVeto=false;vetoPlayer.delayUseItem=false;Main.blockMouse=false;}
            Step(context,false);
            Reset(context,98,400);Step(context,true);Step(context,false);Step(context,true);
            for(int i=0;i<25;i++)Step(context,true,new Vector2(710,640));
            Require((bool)Get(owner,"Running") && (bool)Get(owner,"manual") && completions.Count==1,"old AFK completes on its native cooldown during ongoing manual use, exactly once");
            Step(context,false);
            var p=Reset(context,98,400);Step(context,true);Step(context,false);
            Vector2 remembered=(Vector2)Get(owner,"point");p.position.X+=80;Main.screenPosition.X+=40;
            for(int i=0;i<30;i++)Step(context,false);
            Require((bool)Get(owner,"Running") && ((Vector2)Get(owner,"point"))==remembered,"movement and camera preserve world intention and operation");
            Step(context,true,new Vector2(710,640));Step(context,false);
            Require((bool)Get(owner,"Running") && ((Vector2)Get(owner,"point"))==remembered,"manual click yields its own aim without relocking");
            p.inventory[1].SetDefaults(98);p.selectedItemState.Select(1);
            Require(!(bool)Get(owner,"Running") && (bool)Get(owner,"ModeEnabled"),"actual native selection cancels only run with tools and combat disabled");
            Step(context,false);Step(context,true);Main.mapFullscreen=true;Step(context,false);Main.mapFullscreen=false;Step(context,false);
            Require(!(bool)Get(owner,"Running"),"map close cannot resurrect run");
            Step(context,true);Main.drawingPlayerChat=true;Step(context,false);Main.drawingPlayerChat=false;Step(context,false);
            Require(!(bool)Get(owner,"Running"),"chat close requires a fresh start click");
            Step(context,true);Call(Get(Get(context,"Shell"),"State"),"RestoreVisible");Step(context,false);Call(Get(context,"Shell"),"CloseAndSubmitPosition");Step(context,false);
            Require(!(bool)Get(owner,"Running"),"visible F5 cancels and close stays armed");
            Main.blockMouse=true;Step(context,true);Require(!(bool)Get(owner,"Running"),"native mouse veto rejects startup");Main.blockMouse=false;Step(context,false);
        }
        private static void OverrideSelection(object context)
        {
            var p=Reset(context,98,200);p.inventory[2].SetDefaults(50);Step(context,true);Step(context,false);
            var quick=Get(context,"QuickItems");var settings=(QuickItemSettings)Get(quick,"Settings");
            var entry=new QuickItemEntry("0123456789abcdef0123456789abcdef",50,QuickItemMode.Use,true,true);string entryReason;
            Require(settings.TryChange(new QuickItemDocument(false,true,new[]{entry}),entry.Id,out entryReason),"override fixture publishes its own mirror entry: "+entryReason);
            NativeQuickItemChecks.Until(()=>{Call(quick,"Poll");return !settings.Busy;});
            JueMingR.Platform.Hotkeys.HotkeyChord chord;string reason;
            Require(JueMingR.Platform.Hotkeys.HotkeyChord.TryCreate(65,JueMingR.Platform.Hotkeys.HotkeyModifiers.None,out chord,out reason),"override fixture chord");
            Call(quick,"Request",entry.Id,chord);
            bool overridden=false;
            for(int i=0;i<150;i++)
            {
                Step(context,false);
                if(p.selectedItemState.HasActiveOverride && p.selectedItem==2){overridden=true;break;}
            }
            Require(overridden && p.selectedItemState.Hotbar==0 && (bool)Get(owner,"Running"),"real quick-item override selects hotbar2 while retaining original hotbar0 and AFK run");
            p.selectedItemState.Select(2);
            Require(p.selectedItemState.Hotbar==2 && !(bool)Get(owner,"Running"),"manual Select of CURRENT override slot still changes hotbar and cancels AFK");
            for(int i=0;i<150;i++)Step(context,false);
            Require(p.selectedItemState.Hotbar==2 && !(bool)Get(owner,"Running"),"temporary return cannot resurrect AFK or undo the new hotbar intent");
        }
        private static void AutomaticTool(object context)
        {
            var p=Reset(context,98,0);var tools=Get(context,"Tools");p.inventory[12].SetDefaults(ItemID.StaffofRegrowth);
            Step(context,true);for(int i=0;i<15;i++)Step(context,false);
            Require(WorldGen.PlaceTile(41,42,78,mute:true,forced:true,plr:0),"AFK borrow real herb pot");NativeToolsChecks.Tile(41,41,84);
            NativeToolsChecks.SetMode(tools,1,1);bool borrowed=false;
            for(int i=0;i<230;i++){Step(context,false);borrowed|=(bool)Get(Get(tools,"Use"),"Active");}
            Require(borrowed && Main.tile[41,41].type!=84 && (bool)Get(owner,"Running") && p.selectedItem==0,
                "zero-gap latch yields actual shared use token to native herb harvest/return then retains AFK run");
            NativeToolsChecks.SetMode(tools,1,0);
        }
        private static void AimConsumption(object context)
        {
            var observation=Get(context,"CombatObservation");
            foreach(int weapon in new[]{98,3541})
            {
                var p=Reset(context,weapon,200);
                var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;
                target.Center=p.Center+new Vector2(160,-100);target.velocity=target.netOffset=Vector2.Zero;target.aiStyle=-1;target.noGravity=true;
                target.life=target.lifeMax=100000;target.knockBackResist=0;Array.Clear(target.immune,0,target.immune.Length);
                NativeCombatObservationChecks.Save(observation,new JueMingR.Features.Combat.ObservationOptions(false,false,false,false,false,25,false,true));
                var basePoint=p.Center+new Vector2(-180,0);
                for(int i=0;i<12;i++)NativeCombatSelectionPreviewChecks.Step(context,basePoint,false);
                NativeCombatSelectionPreviewChecks.Step(context,basePoint,true);
                var saved=(Vector2)Get(owner,"point");
                for(int i=0;i<80;i++){p.statMana=200;NativeCombatSelectionPreviewChecks.Step(context,p.Center+new Vector2(-200,160),false);}
                var wanted=(target.Center-p.MountedCenter).SafeNormalize(Vector2.UnitX);
                Vector2 actual=lastBulletVelocity;
                if(weapon==3541)
                {
                    var parent=Main.projectile.FirstOrDefault(q=>q.active && q.owner==p.whoAmI && q.type==633);
                    Require(parent!=null && starts.Count==1,"AFK prism uses one actual continuous controller while self aim changes its consumers");actual=parent.velocity;
                }
                float toward=Vector2.Dot(actual.SafeNormalize(Vector2.UnitY),wanted);
                Require(toward>.94f && ((Vector2)Get(owner,"point"))==saved,"existing ordinary/continuous aim wins after AFK base projection and fixed run point remains intact: weapon="+weapon+" dot="+toward);
                Console.WriteLine("AFK actual aim consumer weapon="+weapon+" targetDot="+toward+" preservedWorld="+saved);
                Call(owner,"SetModeEnabled",false);NativeCombatObservationChecks.Save(observation,new JueMingR.Features.Combat.ObservationOptions());
            }
        }
        private static void Costs(object context)
        {
            Reset(context,98,0);Call(owner,"SetModeEnabled",false);int sources=(int)Get(owner,"SourceReads"),bodies=(int)Get(owner,"BodyReads");
            for(int i=0;i<120;i++)Step(context,false);
            Require((int)Get(owner,"SourceReads")==sources && (int)Get(owner,"BodyReads")==bodies,"disabled has no AFK source/body observation");
            Reset(context,98,0);Step(context,true);for(int i=0;i<120;i++)Step(context,false);
            Require((int)Get(owner,"BodyReads")==bodies,"ordinary flying bullets incur no completion-body reads");
            sources=(int)Get(owner,"SourceReads");var unrelated=Main.projectile[900];unrelated.SetDefaults(14);unrelated.owner=Main.myPlayer;unrelated.active=true;
            for(int i=0;i<1000;i++)Call(owner,"AfterProjectile",unrelated);
            Require((int)Get(owner,"SourceReads")==sources && (int)Get(owner,"BodyReads")==bodies,"unrelated real world-body callback count does not scale AFK completion observation");
            Require(!(bool)Get(Get(Get(context,"Tools"),"Use"),"Active"),"AFK does not enable tools");
        }
        private static void StartupTail(object context)
        {
            Reset(context,98,300);var combat=Get(context,"Combat");NativeCombatCadenceChecks.Save(combat,new JueMingR.Features.Combat.CombatOptions(1));
            int before=(int)Get(owner,"Starts");startupTailAudit=true;
            try{for(int i=0;i<190;i++)Step(context,true);}
            finally{startupTailAudit=false;NativeCombatCadenceChecks.Save(combat,new JueMingR.Features.Combat.CombatOptions());}
            Require(starts.Count>=5 && births==starts.Count && (int)Get(owner,"Starts")-before==starts.Count,
                "startup held tail has no uncounted native start/shot during positive gaps");
            // Flintlock has no native Minishark ammo-conservation roll; keep
            // this short real-debit/manual sample deterministic.
            var p=Reset(context,95,300);Step(context,true);
            for(int i=0;i<60 && completions.Count==0;i++)Step(context,false);
            Require(completions.Count==1,"manual-gap fixture has an actually completed first use");
            int nativeBefore=starts.Count,afkBefore=(int)Get(owner,"Starts");Step(context,true);
            Require(starts.Count==nativeBefore+1 && (int)Get(owner,"Starts")==afkBefore && (bool)Get(owner,"manual"),
                "a NEW real click may use the same gun inside the AFK gap, with its own native start");
            Require(p.inventory[54].stack<9999,"startup/manual representative uses original real ammunition");Step(context,false);
        }
        private static void RejectedQuickRequests(object context)
        {
            Call(owner,"SetModeEnabled",false);var quick=Get(context,"QuickItems");var settings=(QuickItemSettings)Get(quick,"Settings");var entry=settings.Current.Entries[0];
            JueMingR.Platform.Hotkeys.HotkeyChord chord;string reason;
            Require(JueMingR.Platform.Hotkeys.HotkeyChord.TryCreate(65,JueMingR.Platform.Hotkeys.HotkeyModifiers.None,out chord,out reason),"rejected quick fixture chord");
            Step(context,false);Call(quick,"Request",entry.Id,chord);Call(quick,"AfterDispatch",false);
            Require(GetOptional(quick,"requested")==null && !(bool)Get(quick,"afkDeferred"),"denied fresh non-AFK request is retired without leaving a queue");
            Call(quick,"Request",entry.Id,chord);
            Require((string)GetOptional(quick,"requested")==entry.Id,"next explicit shortcut remains available after a rejected dispatch");Call(quick,"AfterDispatch",false);
        }
    }
}
