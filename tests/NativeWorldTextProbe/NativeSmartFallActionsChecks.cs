using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;
using HarmonyLib;
using JueMingR.Features.Movement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeSmartFallActionsChecks
    {
        private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static int stage;
        private const int Floor=125;
        private static object movement,input,host,items;
        private static long frame;
        internal static void Run(object context)
        {
            movement=Get(context,"Movement");input=Get(context,"Input");host=Get(movement,"FallHost");items=Get(context,"items");
            Console.WriteLine("SMART-FALL REPRESENTATIVE SELECTION canonical-all");
            var before=(SmartFallProtectionOptions)Get(movement,"SmartFallProtection");bool jumping=(bool)Call(movement,"IsEnabled",0);
            Player original=Main.LocalPlayer;var isolation=new Harmony("JueMingR.Tests.SmartFall.NativeSegments");
            var oldWingStats=ArmorIDs.Wing.Sets.Stats;var oldGore=(Gore[])Main.gore.Clone();
            Terraria.Initializers.WingStatsInitializer.Load();
            for(int i=0;i<Main.gore.Length;i++)if(Main.gore[i]==null)Main.gore[i]=new Gore();
            var update=typeof(Player).GetMethod("Update",new[]{typeof(int)});
            isolation.Patch(update,transpiler:new HarmonyMethod(typeof(NativeSmartFallActionsChecks),nameof(Segments)){priority=Priority.Last});
            foreach(string cosmetic in new[]{"WingAirVisuals","RocketBootVisuals"})isolation.Patch(AccessTools.Method(typeof(Player),cosmetic),prefix:new HarmonyMethod(typeof(NativeSmartFallActionsChecks),nameof(NoCosmetic)));
            foreach(string cosmetic in new[]{"TeleportEffect","StartCameraTransitionForPlayerTeleport"})isolation.Patch(typeof(Main).GetMethod(cosmetic,F),prefix:new HarmonyMethod(typeof(NativeSmartFallActionsChecks),nameof(NoCosmetic)));
            try
            {
                Call(movement,"SetEnabled",0,false);Call(movement,"SetEnabled",3,true);
                // The independent native negative control must actually hurt.
                var baseline=PlayerAt(1);baseline.position.Y=Floor*16-baseline.height;baseline.velocity.Y=0;
                int health=baseline.statLife;Damage(baseline);
                Require(baseline.statLife<health,"Unprotected native landing really consumes life in the independent original Hurt segment.");
                Exception first=null;
                foreach(var family in new[]{SmartFallFamily.DoubleJump,SmartFallFamily.RocketBoots,SmartFallFamily.Carpet,SmartFallFamily.Horseshoe,SmartFallFamily.Wings,SmartFallFamily.DjinnLegs,SmartFallFamily.Umbrella,SmartFallFamily.TeleportRod,SmartFallFamily.GravityGlobe})
                {var current=family;Independent(family.ToString(),()=>Consumer(current),ref first);}
                Independent("HoverPhase",HoverPhase,ref first);
                Independent("OriginalMountTakeover",OriginalMountTakeover,ref first);
                Independent("LateRocketDjinn",LateRocketDjinn,ref first);
                Independent("GravityReturn",GravityReturn,ref first);
                Independent("CacheFacts",CacheFacts,ref first);
                Independent("SafeMount",()=>MountRescue(SmartFallFamily.SafeMount,2771,false),ref first);
                Independent("SafeMountDisabled",()=>MountRescue(SmartFallFamily.SafeMount,2771,false,true),ref first);
                Independent("FlyingMount",()=>MountRescue(SmartFallFamily.FlyingMount,3367,true),ref first);
                Independent("Grapple84",()=>GrappleRescue(84,480,false),ref first);
                Independent("Grapple1800Borrowed",()=>GrappleRescue(1800,240,true),ref first);
                Independent("Grapple4980Teleport",()=>GrappleRescue(4980,240,false),ref first);
                Independent("GrappleFailedReturnCache",GrappleFailedReturnCache,ref first);
                Independent("GrappleLateContact",GrappleLateContact,ref first);
                Independent("DoubleJumpMismatch",DoubleJumpMismatch,ref first);
                Independent("PairedRocketCosts",PairedRocketCosts,ref first);
                if(first!=null)throw new InvalidOperationException("One or more independent native smart-fall representatives failed; individual raw failures retained.",first);
                Console.WriteLine("PASS SMART-FALL native representatives selection=canonical-all; independent earlier evidence retains its original identity.");
            }
            finally
            {
                Call(host,"ClearSession");Call(movement,"EndUpdate",Main.LocalPlayer);
                foreach(var method in isolation.GetPatchedMethods().ToArray())isolation.Unpatch(method,HarmonyPatchType.All,isolation.Id);
                Main.player[Main.myPlayer]=original;Call(movement,"SetEnabled",0,jumping);
                ArmorIDs.Wing.Sets.Stats=oldWingStats;for(int i=0;i<Main.gore.Length;i++)Main.gore[i]=oldGore[i];
                for(int i=0;i<12;i++)Call(movement,"SetSmartFallPermission",i,before.Allows(i));Call(movement,"SetEnabled",3,before.Enabled);
            }
        }
        private static void Independent(string name,Action check,ref Exception first)
        {
            try{check();Console.WriteLine("SMART-FALL REPRESENTATIVE PASS "+name);}
            catch(Exception error){Console.Error.WriteLine("SMART-FALL REPRESENTATIVE FAIL "+name+": "+error);if(first==null)first=error;}
            finally{Call(movement,"EndUpdate",Main.LocalPlayer);Call(host,"ClearSession");Call(movement,"SetEnabled",3,true);}
        }
        private static Player PlayerAt(float gap)
        {
            var p=new Player{whoAmI=Main.myPlayer,active=true,position=new Vector2(700,Floor*16-42-gap),velocity=new Vector2(0,10),gravDir=1,gravity=.4f,maxFallSpeed=10,fallStart=0,statLife=5000,statLifeMax=5000,statLifeMax2=5000};
            Main.player[Main.myPlayer]=p;Main.mouseItem.TurnToAir();Main.mouseLeft=Main.mouseRight=false;
            for(int x=35;x<65;x++) { var t=Main.tile[x,Floor];t.active(true);t.type=1;t.slope(0);t.halfBrick(false); }
            Call(Get(items,"World"),"InvalidateObservation");return p;
        }
        private static void Select(SmartFallFamily family)
        { for(int i=0;i<12;i++)Call(movement,"SetSmartFallPermission",i,i==(int)family);Call(host,"ClearSession"); }
        private static void Begin(Player p)
        {
            NativeQuickItemChecks.BeginWorldStep();NativeQuickItemChecks.Sample(input,new Keys[0]);
            Require((bool)Call(movement,"BeginUpdate",p),"Exact isolated local scope begins.");Call(movement,"CaptureInput",p);frame++;
        }
        private static void End(Player p){Call(movement,"EndUpdate",p);}
        private static void Damage(Player p){stage=1;p.Update(Main.myPlayer);}
        private static void Motion(Player p){stage=2;p.Update(Main.myPlayer);}
        private static void Eligibility(Player p){stage=4;p.Update(Main.myPlayer);}
        private static void DoubleJumpMismatch()
        {
            Select(SmartFallFamily.DoubleJump);
            foreach(bool matching in new[]{false,true})
            {
                var p=PlayerAt(5);p.armor[3].SetDefaults(1724);p.UpdateEquips(p.whoAmI);p.canJumpAgain_Fart=true;
                Item prior=p.armor[3];p.armor[3]=new Item();p.inventory[17].SetDefaults(53);if(matching)p.inventory[18]=prior;
                long actions=(long)Get(host,"NativeActions");Begin(p);
                try
                {
                    p.ResetEffects();p.UpdateEquips(p.whoAmI);Eligibility(p);Motion(p);Call(host,"BeforeGrapple",p);
                    if(matching)Require(p.jump>0 && p.velocity.Y<0 && !p.canJumpAgain_Fart && p.inventory[17].type==53 && ReferenceEquals(p.inventory[18],prior),"Actual matching Fart source behind Cloud preserves and consumes only its own original unused charge, then returns the exact item.");
                    else Require((long)Get(host,"NativeActions")==actions && p.jump==0 && p.velocity.Y>0 && !p.canJumpAgain_Fart,"Wrong Cloud source cannot retain a Fart charge through the original pre-Jump eligibility clear or submit an unusable rescue pulse.");
                }
                finally{End(p);Call(host,"ClearSession");}
            }
        }
        private static void Consumer(SmartFallFamily family)
        {
            Select(family);var p=PlayerAt(family==SmartFallFamily.Umbrella || family==SmartFallFamily.TeleportRod || family==SmartFallFamily.RocketBoots || family==SmartFallFamily.DjinnLegs?15:5);
            int type=family==SmartFallFamily.DoubleJump?53:family==SmartFallFamily.RocketBoots?128:family==SmartFallFamily.Carpet?934:family==SmartFallFamily.Horseshoe?158:family==SmartFallFamily.Wings?493:family==SmartFallFamily.DjinnLegs?3770:family==SmartFallFamily.Umbrella?946:family==SmartFallFamily.TeleportRod?5335:1131;
            var item=new Item();item.SetDefaults(type);
            bool active=family==SmartFallFamily.DoubleJump || family==SmartFallFamily.RocketBoots || family==SmartFallFamily.Carpet;
            if(active)p.armor[3]=item;else p.inventory[17]=item;
            if(active)
            {
                p.UpdateEquips(p.whoAmI);
                // Reachable start state: worn resource abilities replenished
                // on an earlier ordinary ground update, never refilled after
                // this action starts. The native consumers own all later use.
                if(family==SmartFallFamily.DoubleJump)p.canJumpAgain_Cloud=true;
                if(family==SmartFallFamily.RocketBoots)p.rocketTime=7;
                if(family==SmartFallFamily.Carpet)p.canCarpet=true;
            }
            int originalStart=p.fallStart,health=p.statLife;float rocket=p.rocketTime;
            Begin(p);
            try
            {
                p.selectedItemState.Update();typeof(Player).GetMethod("TrySyncingInput",F).Invoke(p,null);
                // This is the actual post-Hurt equip consumer. Tests do not
                // assign any immunity/wing/carpet/gravControl effect field.
                p.UpdateEquips(p.whoAmI);
                Motion(p);Call(host,"BeforeGrapple",p);
                if(family==SmartFallFamily.DoubleJump)
                    Require(p.jump>0 && p.velocity.Y<0 && !p.canJumpAgain_Cloud,"Production pulse is consumed by original Cloud Jump; no counter refill.");
                if(family==SmartFallFamily.RocketBoots)
                    Require(p.rocketTime<rocket && p.rocketDelay>0 && p.fallStart==originalStart,"Original first ignition spends fuel but does not invent an immediate baseline reset.");
                if(family==SmartFallFamily.Carpet)
                    Require(p.carpetTime==299 && p.carpetFrame>=0 && p.fallStart!=originalStart,"Original carpet starts and spends its own first tick.");
                if(family==SmartFallFamily.Horseshoe || family==SmartFallFamily.Wings || family==SmartFallFamily.DjinnLegs)
                {
                    Require(family==SmartFallFamily.Horseshoe?p.noFallDmg:family==SmartFallFamily.Wings?p.hasWings:p.slowFall,"Actual equipped item contributes native protection: "+family);
                    p.DryCollision(false,false);p.SlopingCollision(false,false);
                }
                if(family==SmartFallFamily.Umbrella || family==SmartFallFamily.TeleportRod)
                {
                    // ItemCheck is later than collision in Update. Do the
                    // actual native held/use stage before the next landing.
                    p.ItemCheck();
                    Console.WriteLine("SMART-FALL HELD family="+family+" result="+Get(host,"Result")+" selected="+p.selectedItem+" item="+p.HeldItem.type+" animation="+p.itemAnimation+" time="+p.itemTime+" use="+p.controlUseItem+" pending="+Get(host,"HasPending")+" pos="+p.position);
                    Require(p.fallStart!=originalStart,"Original held/use stage changes its own baseline family="+family);
                    if(family==SmartFallFamily.Umbrella)Require(p.velocity.Y<=2.01f,"Native held umbrella actually slows the fall.");
                    else Require(Vector2.DistanceSquared(p.position,new Vector2(700,Floor*16-42-15))>16,"Original rod actually teleports rather than only submitting input.");
                }
                if(family==SmartFallFamily.GravityGlobe)Require(p.gravDir==-1 && p.fallStart!=originalStart,"Original gravity branch consumes Up and reverses gravity.");
                if(family==SmartFallFamily.RocketBoots || family==SmartFallFamily.DjinnLegs)
                {p.DryCollision(false,false);p.SlopingCollision(false,false);Require(p.velocity.Y!=0,"Delayed natural reset is armed before, rather than after, first contact.");}
            }
            finally{End(p);}
            if(family==SmartFallFamily.RocketBoots || family==SmartFallFamily.DjinnLegs)
            {
                Damage(p);Require(p.statLife==health && p.fallStart!=originalStart,"Next real early native reset establishes protection before collision family="+family);
                Begin(p);try{p.ResetEffects();p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);}finally{End(p);}
                Require((SmartFallResult)Get(host,"Result")==SmartFallResult.Protected,"Natural baseline consumer, rather than preparing an item, confirms protection family="+family);
            }
            if(family==SmartFallFamily.Horseshoe || family==SmartFallFamily.Wings)
            {
                // DryCollision initially clips velocity to the remaining
                // positive gap. Hurt needs a later native collision's zero,
                // not just a first edge contact followed by a skipped gate.
                for(int tick=0;tick<3 && p.velocity.Y!=0;tick++)
                {Begin(p);try{Damage(p);p.ResetEffects();p.UpdateEquips(p.whoAmI);Motion(p);p.DryCollision(false,false);p.SlopingCollision(false,false);}finally{End(p);}}
                Require(p.velocity.Y==0,"Native landing has reached the real Hurt prerequisite family="+family);
                Begin(p);try{Damage(p);p.ResetEffects();p.UpdateEquips(p.whoAmI);}finally{End(p);}
                Require(p.statLife==health && p.fallStart!=originalStart && !(bool)Get(host,"HasPending"),"Actual early landing gate consumes immunity then finishes the real equipment return family="+family);
            }
            if(family==SmartFallFamily.TeleportRod)
            {
                for(int tick=0;tick<25 && (bool)Get(host,"HasPending");tick++)
                {Begin(p);try{p.selectedItemState.Update();p.ItemCheck();}finally{End(p);}}
                Require(!(bool)Get(host,"HasPending") && !p.controlUseItem && !Main.mouseLeft,"Native rod animation and selected-item owner finish the finite return.");
            }
            Require(!p.controlUseItem && !Main.mouseLeft,"Action finalizer returns only supplemental item input family="+family);
            Console.WriteLine("SMART-FALL ACTION family="+family+" result="+Get(host,"Result")+" life="+p.statLife+" fallStart="+p.fallStart+" rocket="+p.rocketTime+" carpet="+p.carpetTime+" position="+p.position+" phase=native-components frame="+frame);
            Call(host,"ClearSession");
        }
        private static void HoverPhase()
        {
            var p=PlayerAt(20);int old=p.fallStart;
            p.mount.SetMount(12,p);p.mount.Hover(p);
            Require(p.fallStart==old,"SetMount plus first frameState=0 Hover cannot certify protection.");
            p.PlayerFrame();p.mount.Hover(p);
            Require(p.fallStart==(int)(p.position.Y/16),"Native PlayerFrame then next Hover supplies the real phase-specific protection.");
            p.mount.TryDismount(p);Require(!p.mount.Active,"Native return can occur before original platform collision.");
        }
        private static void LateRocketDjinn()
        {
            Call(movement,"SetEnabled",3,false);
            foreach(bool rocket in new[]{true,false})
            {
                var p=PlayerAt(0);int health=p.statLife;
                p.armor[rocket?3:2].SetDefaults(rocket?128:3770);p.UpdateEquips(p.whoAmI);
                if(rocket){p.rocketTime=7;p.controlJump=p.releaseJump=p.rocketRelease=true;}
                Motion(p);p.DryCollision(false,false);p.SlopingCollision(false,false);
                Require(p.velocity.Y==0,"Late native action reaches contact before its next early reset rocket="+rocket);
                Damage(p);Require(p.statLife<health,"Hurt precedes the natural rocket/slowFall reset; a one-phase-late manual action cannot rescue rocket="+rocket);
            }
            Call(movement,"SetEnabled",3,true);
        }
        private static void GravityReturn()
        {
            Select(SmartFallFamily.GravityGlobe);var p=PlayerAt(5);p.inventory[17].SetDefaults(1131);
            for(int tick=0;tick<2;tick++){Begin(p);try{p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);}finally{End(p);}}
            Require(p.gravDir==1 && !(bool)Get(host,"HasPending") && !p.controlUp,"Second legal native Up restores original gravity and retires borrowing/input.");
        }
        private static void CacheFacts()
        {
            Select(SmartFallFamily.Horseshoe);Call(movement,"SetSmartFallPermission",(int)SmartFallFamily.Horseshoe,false);
            var p=PlayerAt(200);p.inventory[17].SetDefaults(158);
            Begin(p);End(p);long scans=(long)Get(host,"SourceScans");
            Call(movement,"SetSmartFallPermission",(int)SmartFallFamily.Horseshoe,true);Begin(p);End(p);
            Console.WriteLine("SMART-FALL CACHE scans-before="+scans+" after="+Get(host,"SourceScans")+" permission="+((SmartFallProtectionOptions)Get(movement,"SmartFallProtection")).Horseshoe+" source="+(((Item[])Get(host,"sources"))[(int)SmartFallFamily.Horseshoe]?.type??0)+" count="+Get(host,"count")+" active="+Get(host,"active")+" takenOver="+Get(host,"takenOver")+" noFall="+p.noFallDmg+" life="+p.statLife);
            Require((long)Get(host,"SourceScans")==scans+1 && ReferenceEquals(((Item[])Get(host,"sources"))[(int)SmartFallFamily.Horseshoe],p.inventory[17]),"Changing a permission invalidates an empty dangerous capability cache without an inventory mutation.");
            var before=(SmartFallContact)Get(host,"contact");long plans=(long)Get(host,"Plans");
            Require(before.Found,"Contact mutation representative has a real initial floor.");
            for(int x=(int)(p.position.X/16);x<=(int)((p.position.X+p.width-1)/16);x++)Main.tile[x,before.TileY].halfBrick(true);
            Begin(p);End(p);var half=(SmartFallContact)Get(host,"contact");
            Require((long)Get(host,"Plans")==plans+1 && half.HalfBrick && half.Y==before.Y+8,"Same active/slope tile changing to half brick refreshes the actual contact and timing.");
            for(int x=(int)(p.position.X/16);x<=(int)((p.position.X+p.width-1)/16);x++){var changed=Main.tile[x,before.TileY];changed.type=19;changed.frameY=18*49;changed.halfBrick(false);}plans=(long)Get(host,"Plans");Begin(p);End(p);
            var platform=(SmartFallContact)Get(host,"contact");
            Require((long)Get(host,"Plans")==plans+1 && platform.Platform && platform.SafeSurface,"Type/frame changes refresh platform and safe-surface meaning with only the retained tile read.");
            Call(movement,"SetSmartFallPermission",(int)SmartFallFamily.Horseshoe,false);scans=(long)Get(host,"SourceScans");Begin(p);End(p);
            Require((long)Get(host,"SourceScans")==scans+1 && ((Item[])Get(host,"sources"))[(int)SmartFallFamily.Horseshoe]==null,"Disabling the capability also invalidates only the source cache.");
        }
        private static void OriginalMountTakeover()
        {
            var p=PlayerAt(20);
            // Reach the exact pending-return ownership state without
            // fabricating any native protection or mount outcome.
            host.GetType().GetField("originalMount",F).SetValue(host,12);
            p.mount.SetMount(0,p);
            Call(host,"ReturnOriginalMount",p);
            Require(p.mount.Active && p.mount.Type==0 && !(bool)Get(host,"HasPending"),"New native mount retires the previous mount return permission.");
            p.mount.Dismount(p);Call(host,"ReturnOriginalMount",p);
            Require(!p.mount.Active,"Later manual dismount cannot resurrect the retired original mount.");
        }
        private static void MountRescue(SmartFallFamily family,int itemType,bool platform,bool disable=false)
        {
            Select(family);var p=PlayerAt(15);p.inventory[17].SetDefaults(itemType);int health=p.statLife;
            if(platform)for(int x=35;x<65;x++){Main.tile[x,Floor].type=19;Main.tile[x,Floor].frameY=0;}
            for(int tick=0;tick<4;tick++)
            {
                if(disable && tick==1)Call(movement,"SetEnabled",3,false);
                Begin(p);
                try
                {
                    Damage(p);p.selectedItemState.Update();p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);
                    if(disable && tick==1)Require(p.mount.Active,"Closing master cannot unload the safe mount's sole protection before real landing Hurt.");
                    if(platform && tick==0)Require(p.mount.Active,"New flying mount waits for its first real airborne frame, before contact.");
                    if(platform && tick==1)Require(!p.mount.Active,"Actual Hover rescue returns before this Update computes platform ignore flags.");
                    p.DryCollision(false,false);p.SlopingCollision(false,false);p.PlayerFrame();
                    Console.WriteLine("SMART-FALL MOUNT-STATE family="+family+" tick="+tick+" result="+Get(host,"Result")+" active="+p.mount.Active+" type="+p.mount.Type+" vel="+p.velocity+" pos="+p.position+" start="+p.fallStart);
                }
                finally{End(p);}
                if(disable && tick>0 && !(bool)Get(host,"HasPending"))break;
            }
            Require(p.velocity.Y==0,"Complete native mount action preserves its original contact platform="+platform);
            Damage(p);Require(p.statLife==health,"Original landing damage sees real mount/previous Hover protection family="+family);
            if(disable)Call(movement,"SetEnabled",3,true);
            Begin(p);try{p.UpdateEquips(p.whoAmI);}finally{End(p);}
            Require(!p.mount.Active && !(bool)Get(host,"HasPending"),"Real native mount and exact borrowed item finish their short return family="+family);
            Console.WriteLine("SMART-FALL MOUNT family="+family+" platform="+platform+" original-contact-preserved=true life="+p.statLife+" pending="+Get(host,"HasPending"));
        }
        private static void PairedRocketCosts()
        {
            Select(SmartFallFamily.RocketBoots);
            var begin=(Func<Player,bool>)Delegate.CreateDelegate(typeof(Func<Player,bool>),movement,movement.GetType().GetMethod("BeginUpdate",F));
            var capture=(Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>),movement,movement.GetType().GetMethod("CaptureInput",F));
            var end=(Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>),movement,movement.GetType().GetMethod("EndUpdate",F));
            var beforeGrapple=(Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>),host,host.GetType().GetMethod("BeforeGrapple",F));
            var allocated=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",F));
            var manualTimes=new double[24];var assistedTimes=new double[24];long manualBytes=0,assistedBytes=0,reads=0,plans=0;
            for(int sample=-4;sample<24;sample++)
            {
                Player manual=null,assisted=null;
                for(int side=0;side<2;side++)
                {
                    bool help=side==1;Call(movement,"SetEnabled",3,help);Call(host,"ClearSession");
                    var p=PlayerAt(15);p.armor[3].SetDefaults(128);p.UpdateEquips(p.whoAmI);p.rocketTime=7;
                    p.controlJump=!help;p.releaseJump=!help;p.rocketRelease=!help;
                    NativeQuickItemChecks.BeginWorldStep();NativeQuickItemChecks.Sample(input,new Keys[0]);stage=2;
                    long tiles=(long)Get(host,"TileReads"),built=(long)Get(host,"Plans"),bytes=allocated(),start=Stopwatch.GetTimestamp();
                    bool entered=begin(p);if(entered)capture(p);
                    try{p.Update(Main.myPlayer);if(entered)beforeGrapple(p);}finally{if(entered)end(p);}
                    long elapsed=Stopwatch.GetTimestamp()-start,delta=allocated()-bytes;
                    if(sample>=0)
                    {
                        if(help){assistedTimes[sample]=elapsed*1000.0/Stopwatch.Frequency;assistedBytes+=delta;reads+=(long)Get(host,"TileReads")-tiles;plans+=(long)Get(host,"Plans")-built;}
                        else {manualTimes[sample]=elapsed*1000.0/Stopwatch.Frequency;manualBytes+=delta;}
                    }
                    if(help)assisted=p;else manual=p;
                }
                Require(assisted.velocity==manual.velocity && assisted.rocketTime==manual.rocketTime && assisted.rocketDelay==manual.rocketDelay && assisted.jump==manual.jump,
                    "Paired exact native rocket action preserves manual motion and resource consumption sample="+sample);
            }
            Array.Sort(manualTimes);Array.Sort(assistedTimes);
            Console.WriteLine("SMART-FALL PAIRED-COST native=rocket first-ignition samples=24 warmups=4 helper-near-probe-plans="+plans+" helper-tile-reads="+reads+" manual-ms-p50="+manualTimes[12]+" manual-ms-p95="+manualTimes[22]+" assisted-ms-p50="+assistedTimes[12]+" assisted-ms-p95="+assistedTimes[22]+" manual-thread-bytes-total="+manualBytes+" assisted-thread-bytes-total="+assistedBytes+" scope=original-motion+production-scope/plan/first-action-finalizer no-render/no-FPS");
            Call(host,"ClearSession");Call(movement,"SetEnabled",3,true);
        }
        private static void GrappleRescue(int itemType,float gap,bool borrowed)
        {
            Select(SmartFallFamily.Grapple);var p=PlayerAt(gap);int health=p.statLife;
            for(int x=35;x<65;x++)for(int y=40;y<Floor;y++)Main.tile[x,y]=new Tile();
            for(int x=35;x<65;x++){var t=Main.tile[x,Floor];t.active(true);t.type=19;t.frameY=0;t.slope(0);t.halfBrick(false);}
            for(int i=0;i<Main.projectile.Length;i++){if(Main.projectile[i]==null)Main.projectile[i]=new Projectile();Main.projectile[i].active=false;}
            p.miscEquips[4].SetDefaults(borrowed?84:itemType);Item originalHook=p.miscEquips[4],selected=p.miscEquips[4];
            if(borrowed){p.inventory[17].SetDefaults(itemType);selected=p.inventory[17];}
            int birth=-1,attached=-1,protectedAt=-1,plannedDelay=-1;Projectile owned=null;long plansAtProtection=0,scansAtProtection=0;bool through=false;
            for(int tick=0;tick<64;tick++)
            {
                Begin(p);
                try
                {
                    if(itemType==84 && (tick<2 || tick>=12 && tick<=14))
                    {var c=(SmartFallContact)Get(host,"contact");var plan=Get(host,"grapplePlan");Console.WriteLine("SMART-FALL ORDINARY-PREP tick="+tick+" source="+(((Item[])Get(host,"sources"))[(int)SmartFallFamily.Grapple]?.type??0)+" count="+Get(host,"count")+" contact="+c.Found+" ticks="+c.Ticks+" plan="+Get(plan,"Found")+" delay="+Get(plan,"Delay")+" rejected="+Get(host,"rejected")+" pos="+p.position);}
                    Damage(p);p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);p.GrappleMovement();
                    if(birth>=0 && (SmartFallResult)Get(host,"Result")==SmartFallResult.Protected && protectedAt<0)
                    {
                        protectedAt=tick;plansAtProtection=(long)Get(host,"Plans");scansAtProtection=(long)Get(host,"SourceScans");
                        Require(attached>=0 && protectedAt>attached,"Attached projectile alone cannot be called Player protection item="+itemType);
                        Require(protectedAt==attached+1 && protectedAt==birth+plannedDelay,"Immediate original attachment registration makes the next Player the predicted first protection, including a final sub-update item="+itemType);
                        if(itemType!=4980)Require(p.position.Y+p.height<Floor*16,"Real ordinary hook protection is established before the first platform contact, not merely before the later Hurt gate item="+itemType);
                        Require(p.statLife==health && !(bool)Get(host,"HasPending"),"Real GrappleMovement protects before damage and returns finite borrowing item="+itemType);
                    }
                    bool ignore=p.GoingDownWithGrapple || p.gravDir<0;
                    p.DryCollision(p.controlDown || ignore,ignore);p.SlopingCollision(p.controlDown || ignore,ignore);
                    if(p.position.Y+p.height>Floor*16+.1f && owned!=null && owned.active)through=true;
                    Housekeeping(p);
                }
                finally{End(p);}
                for(int slot=0;slot<Main.projectile.Length;slot++)
                {
                    var q=Main.projectile[slot];if(q==null || !q.active || q.owner!=p.whoAmI || q.aiStyle!=7)continue;
                    if(birth<0){birth=tick;owned=q;plannedDelay=(int)Get(Get(host,"grapplePlan"),"Delay");Require(q.type==selected.shoot,"Actual native QuickGrapple uses the selected real hook, including temporary faster source item="+itemType);}
                    q.Update(slot);if(q.active && q.ai[0]==2 && attached<0){attached=tick;Require(p.grapCount>0 && Array.IndexOf(p.grappling,slot)>=0,"Original ai0 attachment immediately registers the hook before ai2's stable branch item="+itemType);}
                }
                if(attached>=0 && protectedAt<0)Require((SmartFallResult)Get(host,"Result")!=SmartFallResult.Protected,"Projectile attachment phase is not early success.");
                if(protectedAt>=0 && tick>=protectedAt+3)break;
            }
            Console.WriteLine("SMART-FALL GRAPPLE-STATE item="+itemType+" birth="+birth+" attached="+attached+" protected="+protectedAt+" result="+Get(host,"Result")+" active="+(owned!=null && owned.active)+" ai="+(owned==null?-1:owned.ai[0])+" life="+p.statLife+" position="+p.position+" velocity="+p.velocity);
            Require(protectedAt>=0 && owned!=null && owned.active,"Native hook really reaches protection and remains attached item="+itemType);
            Require((long)Get(host,"Plans")==plansAtProtection && (long)Get(host,"SourceScans")==scansAtProtection,"A handed-off live hook creates no further rescue planning/debt item="+itemType);
            if(itemType!=4980)Require(through,"Timely native immunity permits crossing the original platform while hook remains active item="+itemType);
            if(borrowed)Require(ReferenceEquals(p.miscEquips[4],originalHook) && ReferenceEquals(p.inventory[17],selected),"Temporary exact hook returns without making the attached projectile fall off.");
            p.controlJump=true;p.releaseJump=true;p.GrappleMovement();Require(!owned.active,"Real manual Jump immediately releases the handed-off native hook.");
            Console.WriteLine("SMART-FALL GRAPPLE item="+itemType+" born="+birth+" attached-projectile-phase="+attached+" protected-player-phase="+protectedAt+" platform-through="+through+" manual-release=true life="+p.statLife+" new-plans-after-success=0");
            Call(host,"ClearSession");
        }
        private static void GrappleFailedReturnCache()
        {
            Select(SmartFallFamily.Grapple);var p=PlayerAt(240);p.inventory[17].SetDefaults(1800);
            for(int x=35;x<65;x++)for(int y=40;y<Floor;y++)Main.tile[x,y]=new Tile();
            for(int i=0;i<Main.projectile.Length;i++){if(Main.projectile[i]==null)Main.projectile[i]=new Projectile();Main.projectile[i].active=false;}
            var q=Main.projectile[0];q.SetDefaults(13);q.active=true;q.owner=p.whoAmI;q.ai[0]=0;q.position=p.Center;q.velocity=Vector2.UnitY;
            Item anchor=p.miscEquips[4],source=p.inventory[17];long actions=(long)Get(host,"NativeActions");bool failed=false;
            try
            {
            for(int tick=0;tick<20;tick++)
            {
                Begin(p);try{p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);p.DryCollision(false,false);p.SlopingCollision(false,false);}
                finally{End(p);}
                if((long)Get(host,"NativeActions")>actions && (SmartFallResult)Get(host,"Result")==SmartFallResult.Failed){failed=true;break;}
            }
            Require(failed && (long)Get(host,"NativeActions")==actions+1 && !(bool)Get(host,"HasPending"),"Real native QuickGrapple rejects a second flying single hook, then returns the temporary exact item.");
            Require(ReferenceEquals(p.miscEquips[4],anchor) && ReferenceEquals(p.inventory[17],source),"Failed native action restores the real misc/source references.");
            Begin(p);End(p);long scans=(long)Get(host,"SourceScans"),plans=(long)Get(host,"Plans");actions=(long)Get(host,"NativeActions");
            for(int i=0;i<64;i++){Begin(p);End(p);}
            Require((long)Get(host,"SourceScans")==scans && (long)Get(host,"Plans")==plans && (long)Get(host,"NativeActions")==actions,"Own return revision cannot reopen identical failed borrowing/planning/actions in 64 unchanged dangerous updates.");
            }
            finally{q.active=false;}
        }
        private static void GrappleLateContact()
        {
            Call(movement,"SetEnabled",3,false);int mx=Main.mouseX,my=Main.mouseY;
            try
            {
                foreach(int type in new[]{84,1800})
                {
                    var p=PlayerAt(0);int health=p.statLife;p.miscEquips[4].SetDefaults(type);
                    for(int i=0;i<Main.projectile.Length;i++)Main.projectile[i].active=false;
                    Main.mouseX=(int)(p.Center.X-Main.screenPosition.X);Main.mouseY=(int)(Floor*16+8-Main.screenPosition.Y);
                    Motion(p);p.QuickGrapple();
                    var q=Main.projectile.FirstOrDefault(x=>x.active && x.owner==p.whoAmI && x.aiStyle==7);
                    Require(q!=null,"Late manual control really launches its original selected hook item="+type);
                    p.DryCollision(false,false);p.SlopingCollision(false,false);
                    Require(p.velocity.Y==0,"Already flush late native landing reaches the actual early-Hurt prerequisite item="+type);
                    q.Update(q.whoAmI);Damage(p);
                    Require(p.statLife<health,"A hook fired too late at contact cannot undo the next original early Hurt item="+type);
                    q.active=false;
                }
            }
            finally{Main.mouseX=mx;Main.mouseY=my;foreach(var q in Main.projectile)if(q!=null && q.aiStyle==7 && q.owner==Main.myPlayer)q.active=false;}
        }
        private static void Housekeeping(Player p){stage=3;p.Update(Main.myPlayer);}
        private static int Stage(){return stage;}
        private static bool NoCosmetic(){return false;}
        private static IEnumerable<CodeInstruction> Segments(IEnumerable<CodeInstruction> source,ILGenerator generator)
        {
            var code=source.ToList();
            int damageStop=Unique(code,c=>(c.operand as MethodInfo)?.Name=="ChestChangeEvents")-1;
            int fall2=Enumerable.Range(0,damageStop).Last(i=>code[i].StoresField(AccessTools.Field(typeof(Player),"fallStart2")));
            int damageStart=-1;
            for(int i=fall2-1;i>=0;i--)if(code[i].opcode==OpCodes.Ldarg_1 && i+1<code.Count && code[i+1].LoadsField(AccessTools.Field(typeof(Main),"myPlayer"))){damageStart=i;break;}
            int gravityRead=-1;
            for(int i=fall2;i<damageStop;i++)if(code[i].opcode==OpCodes.Ldc_R8 && (double)code[i].operand==.8){gravityRead=i-2;break;}
            int motionStart=Unique(code,c=>(c.operand as MethodInfo)?.Name=="HorizontalMovement")-1;
            int paste=Unique(code,c=>(c.operand as MethodInfo)?.Name=="PasteInto");
            int eligibilityStart=-1;
            for(int i=paste+1;i<motionStart;i++)if(code[i].LoadsField(AccessTools.Field(typeof(Player),"mount"))){eligibilityStart=i-1;break;}
            int eligibilityStop=Enumerable.Range(0,motionStart).Last(i=>code[i].StoresField(AccessTools.Field(typeof(Player),"canJumpAgain_Basilisk")))+1;
            int grab=Unique(code,c=>(c.operand as MethodInfo)?.Name=="GrabItems"),motionStop=-1;
            for(int i=grab-1;i>=grab-10;i--)if(code[i].LoadsField(AccessTools.Field(typeof(Player),"dead"))){motionStop=i-1;break;}
            Require(damageStart>=0 && gravityRead>=0 && motionStart>damageStop && motionStop>motionStart,"Unique original damage/equip-before-motion phase anchors.");
            int local=LocalIndex(code[gravityRead]);Require(local>=0,"Original gravity ratio has a typed local.");
            int clearCount=Unique(code,c=>c.StoresField(AccessTools.Field(typeof(Player),"grapCount"))),clearStart=-1;
            for(int i=clearCount-1;i>=clearCount-12;i--)if(code[i].LoadsField(AccessTools.Field(typeof(Player),"grappling"))){clearStart=i-1;break;}
            Require(clearStart>motionStop,"Original post-player grappling-list handoff is retained before projectile rebuilding.");
            Require(eligibilityStart>damageStop && eligibilityStop>eligibilityStart && eligibilityStop<motionStart,"Original item-specific pre-Jump unused-charge clearing is retained.");
            Label damage=generator.DefineLabel(),motion=generator.DefineLabel(),prologue=generator.DefineLabel(),housekeeping=generator.DefineLabel(),eligibility=generator.DefineLabel();
            code[damageStart].labels.Add(damage);code[motionStart].labels.Add(motion);
            code[clearStart].labels.Add(housekeeping);
            code[eligibilityStart].labels.Add(eligibility);
            foreach(int stop in new[]{clearCount+1,motionStop,eligibilityStop,damageStop}) {var ret=new CodeInstruction(OpCodes.Ret);ret.labels.AddRange(code[stop].labels);code[stop].labels.Clear();code.Insert(stop,ret);}
            var load=new CodeInstruction(OpCodes.Ldc_R4,1f);load.labels.Add(prologue);
            code.InsertRange(0,new[]{new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(NativeSmartFallActionsChecks),nameof(Stage))),new CodeInstruction(OpCodes.Ldc_I4_1),new CodeInstruction(OpCodes.Beq,prologue),new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(NativeSmartFallActionsChecks),nameof(Stage))),new CodeInstruction(OpCodes.Ldc_I4_3),new CodeInstruction(OpCodes.Beq,housekeeping),new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(NativeSmartFallActionsChecks),nameof(Stage))),new CodeInstruction(OpCodes.Ldc_I4_4),new CodeInstruction(OpCodes.Beq,eligibility),new CodeInstruction(OpCodes.Br,motion),load,new CodeInstruction(OpCodes.Stloc,local),new CodeInstruction(OpCodes.Br,damage)});
            Console.WriteLine("SMART-FALL ORIGINAL SEGMENTS Hurt-before-ResetEffects=true native-Hurt-and-motion-retained=true");return code;
        }
        private static int LocalIndex(CodeInstruction c)
        {if(c.opcode==OpCodes.Ldloc_0)return 0;if(c.opcode==OpCodes.Ldloc_1)return 1;if(c.opcode==OpCodes.Ldloc_2)return 2;if(c.opcode==OpCodes.Ldloc_3)return 3;if(c.opcode==OpCodes.Ldloc || c.opcode==OpCodes.Ldloc_S)return c.operand is LocalBuilder?((LocalBuilder)c.operand).LocalIndex:Convert.ToInt32(c.operand);return -1;}
        private static int Unique(List<CodeInstruction> code,Func<CodeInstruction,bool> predicate)
        {int[] at=Enumerable.Range(0,code.Count).Where(i=>predicate(code[i])).ToArray();Require(at.Length==1,"Unique native segment consumer count="+at.Length);return at[0];}
        private static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    }
}
