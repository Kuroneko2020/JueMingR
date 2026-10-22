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
using Terraria.DataStructures;
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
                Independent("GravityPreparationTakeover",GravityPreparationTakeover,ref first);
                Independent("CacheFacts",CacheFacts,ref first);
                Independent("SafeMount",()=>MountRescue(SmartFallFamily.SafeMount,2771,false),ref first);
                Independent("SafeMountDisabled",()=>MountRescue(SmartFallFamily.SafeMount,2771,false,true),ref first);
                Independent("FlyingMount",()=>MountRescue(SmartFallFamily.FlyingMount,3367,true),ref first);
                Independent("FlyingMountSlopeLeft",()=>MountRescue(SmartFallFamily.FlyingMount,3367,true,false,1,25,2),ref first);
                Independent("FlyingMountSlopeRight",()=>MountRescue(SmartFallFamily.FlyingMount,3367,true,false,2,25,-2),ref first);
                Independent("FlyingMountLateSlopeControl",FlyingMountLateSlopeControl,ref first);
                Independent("CriticalLanding",CriticalLanding,ref first);
                Independent("SafeShortFallBraking",SafeShortFallBraking,ref first);
                Independent("Grapple84",()=>GrappleRescue(84,480,false),ref first);
                Independent("GrappleCriticalPreparation",()=>GrappleRescue(84,480,false,false,false,true),ref first);
                Independent("Grapple1800Borrowed",()=>GrappleRescue(1800,240,true),ref first);
                Independent("Grapple4980Teleport",()=>GrappleRescue(4980,240,false),ref first);
                Independent("GrappleJointQueen",()=>GrappleRescue(4980,100,true),ref first);
                Independent("GrappleDuplicateGeometry",GrappleDuplicateGeometry,ref first);
                Independent("GrappleDuplicateNative",()=>GrappleRescue(1800,240,true,false,true),ref first);
                Independent("GrappleOrdinaryShort",GrappleOrdinaryShort,ref first);
                Independent("GrappleBusyFallback",()=>GrappleRescue(1800,240,false,true),ref first);
                Independent("GrappleFailedReturnCache",GrappleFailedReturnCache,ref first);
                Independent("GrappleNewUpperAnchor",GrappleNewUpperAnchor,ref first);
                Independent("GrappleOwnFailure",GrappleOwnFailure,ref first);
                Independent("BusyPreparation",BusyPreparation,ref first);
                Independent("DynamicInterference",DynamicInterference,ref first);
                Independent("CursedJump",CursedJump,ref first);
                Independent("GrappleLateContact",GrappleLateContact,ref first);
                Independent("DoubleJumpMismatch",DoubleJumpMismatch,ref first);
                Independent("SocialEquipmentConsumers",SocialEquipmentConsumers,ref first);
                Independent("VanillaResourceBoundaries",VanillaResourceBoundaries,ref first);
                Independent("LegalInputStages",LegalInputStages,ref first);
                Independent("MountReturnGates",MountReturnGates,ref first);
                Independent("PendingMountRecovery",PendingMountRecovery,ref first);
                Independent("OppositeCaptureWork",OppositeCaptureWork,ref first);
                Independent("NativeConsumerFailure",NativeConsumerFailure,ref first);
                Independent("PairedRocketCosts",PairedRocketCosts,ref first);
                if(first!=null)throw new InvalidOperationException("One or more independent native smart-fall representatives failed; individual raw failures retained.",first);
                Console.WriteLine("PASS SMART-FALL native representatives selection=canonical-all; independent earlier evidence retains its original identity.");
            }
            finally
            {
                Call(host,"ClearSession");Call(movement,"EndUpdate",Main.LocalPlayer);
                foreach(var method in isolation.GetPatchedMethods().ToArray())isolation.Unpatch(method,HarmonyPatchType.All,isolation.Id);
                Main.player[Main.myPlayer]=original;Call(Get(items,"World"),"BeginSession");Call(movement,"SetEnabled",0,jumping);
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
            var p=new Player{whoAmI=Main.myPlayer,active=true,position=new Vector2(700,Floor*16-42-gap),velocity=new Vector2(0,10),releaseJump=true,rocketRelease=true,releaseUp=true,gravDir=1,gravity=.4f,maxFallSpeed=10,fallStart=0,statLife=5000,statLifeMax=5000,statLifeMax2=5000};
            Main.player[Main.myPlayer]=p;Main.mouseItem.TurnToAir();Main.mouseLeft=Main.mouseRight=false;
            for(int x=35;x<65;x++) { var t=Main.tile[x,Floor];t.active(true);t.type=1;t.slope(0);t.halfBrick(false); }
            Call(Get(items,"World"),"BeginSession");Call(Get(items,"World"),"InvalidateObservation");return p;
        }
        private static void Select(SmartFallFamily family)
        { for(int i=0;i<12;i++)Call(movement,"SetSmartFallPermission",i,i==(int)family);Call(host,"ClearSession"); }
        private static void Begin(Player p)
        {
            NativeQuickItemChecks.BeginWorldStep();NativeQuickItemChecks.Sample(input,new Keys[0]);
            if(!ReferenceEquals(Get(Get(items,"World"),"SessionPlayer"),p))Call(Get(items,"World"),"BeginSession");
            Require(ReferenceEquals(Get(Get(items,"World"),"SessionPlayer"),p),"Production Items session binds this exact native Player.");
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
                    else Require((long)Get(host,"NativeActions")==actions && p.jump==0 && !p.canJumpAgain_Fart && !p.canJumpAgain_Cloud && p.inventory[17].type==53,"A mismatched Cloud cannot preserve stale Fart or create a new Cloud charge; no loan/action occurs.");
                }
                finally{End(p);Call(host,"ClearSession");}
            }
        }
        private static void CursedJump()
        {
            Vector2 manualVelocity=default(Vector2);int manualJump=0;
            for(int side=0;side<2;side++)
            {
                bool help=side==1;Select(SmartFallFamily.DoubleJump);Call(movement,"SetEnabled",3,help);
                var p=PlayerAt(5);p.armor[3].SetDefaults(53);p.UpdateEquips(p.whoAmI);p.canJumpAgain_Cloud=true;
                p.buffType[0]=23;p.buffTime[0]=600;p.UpdateBuffs(p.whoAmI);
                Require(p.noItems && p.cursed && !p.CCed,"Original cursed buff forbids item use without forbidding movement.");
                if(help)Begin(p);else{p.controlJump=true;p.releaseJump=true;}
                try
                {
                    Eligibility(p);Motion(p);if(help)Call(host,"BeforeGrapple",p);
                    Require(p.jump>0 && p.velocity.Y<0 && !p.canJumpAgain_Cloud,"Original JumpMovement spends the worn unused Cloud even under curse; no refill side="+side);
                    if(!help){manualVelocity=p.velocity;manualJump=p.jump;}
                    else Require(p.velocity==manualVelocity && p.jump==manualJump && !(bool)Get(host,"HasPending"),"Helper matches the real cursed manual Jump and retires its input.");
                }
                finally{if(help)End(p);}
            }
            Select(SmartFallFamily.TeleportRod);var rod=PlayerAt(15);rod.inventory[0].SetDefaults(5335);rod.buffType[0]=23;rod.buffTime[0]=600;rod.UpdateBuffs(rod.whoAmI);
            Vector2 position=rod.position;long actions=(long)Get(host,"NativeActions");Begin(rod);
            try{rod.controlUseItem=true;rod.releaseUseItem=true;rod.ItemCheck();Require(rod.position==position && rod.itemAnimation==0 && (long)Get(host,"NativeActions")==actions,"Original cursed rod remains unusable and no helper item action bypasses it.");}
            finally{End(rod);}
            Console.WriteLine("SMART-FALL CURSED original-buff23 manual-jump=helper native-charge-spent=true rod-use-blocked=true");
        }
        private static void BusyPreparation()
        {
            Select(SmartFallFamily.Horseshoe);var p=PlayerAt(15);p.inventory[17].SetDefaults(158);p.inventory[0].SetDefaults(3507);p.itemAnimation=p.itemTime=3;
            Begin(p);End(p);long scans=(long)Get(host,"SourceScans"),plans=(long)Get(host,"Plans"),actions=(long)Get(host,"NativeActions");
            for(int i=0;i<64;i++){Begin(p);End(p);}
            Require((long)Get(host,"SourceScans")==scans && (long)Get(host,"Plans")==plans && (long)Get(host,"NativeActions")==actions,"64 unchanged native busy snapshots do not rescan/replan/retry.");
            // Original ItemCheck counts both actual timers down; no source or
            // revision mutation is manufactured to notify the Host.
            for(int i=0;i<8 && (p.UsingOrReusingItem || p.itemTime!=0);i++)p.ItemCheck();
            Require(!p.UsingOrReusingItem && p.itemTime==0,"Original item consumer actually releases the busy gates.");
            Begin(p);End(p);Require((long)Get(host,"SourceScans")==scans+1,"Actual busy-to-ready transition requalifies the unchanged source once.");
            for(int i=0;i<4;i++){Begin(p);try{Damage(p);p.UpdateEquips(p.whoAmI);Motion(p);p.DryCollision(false,false);p.SlopingCollision(false,false);}finally{End(p);}}
            Require(p.statLife==5000 && (long)Get(host,"NativeActions")==actions && !(bool)Get(host,"HasPending"),"Requalified passive is equipped by its real endpoint and protects original landing, with no synthetic item action.");
            Console.WriteLine("SMART-FALL BUSY unchanged=64 native-unblock=true refreshes=1 original-landing-life="+p.statLife);
        }
        private static SmartFallCandidate Choice()
        {
            var array=(SmartFallCandidate[])Get(host,"candidates");int count=(int)Get(host,"count");var contact=(SmartFallContact)Get(host,"contact");
            int choice=SmartFallDecision.Choose(array,count,contact.Ticks);Require(choice>=0,"Production facts contain a real ready joint plan.");return array[choice];
        }
        private static void DynamicInterference()
        {
            Select(SmartFallFamily.RocketBoots);Call(movement,"SetSmartFallPermission",(int)SmartFallFamily.Horseshoe,true);
            var p=PlayerAt(200);p.armor[3].SetDefaults(128);p.UpdateEquips(p.whoAmI);p.rocketTime=7;p.inventory[17].SetDefaults(158);
            Begin(p);End(p);var plentiful=Choice();Require(plentiful.Family==SmartFallFamily.RocketBoots,"Already-worn plentiful native resource can beat real temporary equipment exchange/return.");
            p.rocketTime=1;Begin(p);End(p);var scarce=Choice();Require(scarce.Family==SmartFallFamily.Horseshoe,"Real remaining fuel changes the comparison with the same two sources and permissions.");
            var facts=(Array)Get(host,"preparations");object preparation=facts.GetValue((int)SmartFallFamily.Horseshoe);
            Require((int)Get(preparation,"BorrowWrites")==2 && ReferenceEquals(Get(preparation,"Source"),p.inventory) && (int)Get(preparation,"SourceSlot")==17,"Dynamic comparison consumes actual borrowing endpoints, not a family rank.");
            int prior=scarce.Interference;Item moved=p.inventory[17];p.inventory[17]=p.inventory[0];p.inventory[0]=moved;Call(Get(items,"World"),"InvalidateObservation");
            Begin(p);End(p);var candidates=(SmartFallCandidate[])Get(host,"candidates");int current=Array.FindIndex(candidates,0,(int)Get(host,"count"),c=>c.Family==SmartFallFamily.Horseshoe);
            Require(current>=0 && candidates[current].Interference>prior,"Exchange of the currently held source has real additional interference without a selection-index write.");
            Console.WriteLine("SMART-FALL JOINT dynamic-resource="+plentiful.Family+"->"+scarce.Family+" real-selected-source-cost="+prior+"->"+candidates[current].Interference);
        }
        private static void GrappleOrdinaryShort()
        {
            Select(SmartFallFamily.Grapple);Call(movement,"SetEnabled",3,false);var p=PlayerAt(100);p.miscEquips[4].SetDefaults(84);
            for(int x=35;x<65;x++)for(int y=40;y<Floor;y++)Main.tile[x,y]=new Tile();
            for(int x=35;x<65;x++){Main.tile[x,Floor].type=19;Main.tile[x,Floor].frameY=0;}
            foreach(var projectile in Main.projectile)if(projectile!=null)projectile.active=false;
            int oldX=Main.mouseX,oldY=Main.mouseY;Projectile q=null;int firstContact=-1,firstProtection=-1,health=p.statLife;
            try
            {
                Main.mouseX=(int)(p.Center.X-Main.screenPosition.X);Main.mouseY=(int)(Floor*16+8-Main.screenPosition.Y);
                for(int tick=0;tick<20;tick++)
                {
                    Damage(p);Motion(p);if(tick==0){p.QuickGrapple();q=Main.projectile.FirstOrDefault(x=>x!=null && x.active && x.owner==p.whoAmI && x.aiStyle==7);Require(q!=null,"Original ordinary short-window control really fires.");}
                    p.GrappleMovement();if(p.fallStart!=0 && firstProtection<0)firstProtection=tick;
                    bool ignore=p.GoingDownWithGrapple;p.DryCollision(ignore,ignore);p.SlopingCollision(ignore,ignore);
                    if(p.position.Y+p.height>=Floor*16-.01f && firstContact<0)firstContact=tick;
                    Housekeeping(p);if(q.active)q.Update(q.whoAmI);
                    if(p.statLife<health)break;
                }
                Require(firstContact>=0 && (firstProtection<0 || firstProtection>firstContact) && p.statLife<health,"Same legal 100px window cannot be saved by the ordinary 84; first contact/Hurt precedes protection.");
                Console.WriteLine("SMART-FALL ORDINARY-SHORT first-contact="+firstContact+" first-protection="+firstProtection+" life="+p.statLife+" paired-Queen-uses-same-gap=100");
            }
            finally{Main.mouseX=oldX;Main.mouseY=oldY;if(q!=null)q.active=false;Call(movement,"SetEnabled",3,true);}
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
            for(int tick=0;tick<3;tick++){Begin(p);try{p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);if(tick==1)Require(p.gravDir==-1 && p.releaseUp,"Whole second native update sees Up released and rearms before return.");}finally{End(p);}}
            Require(p.gravDir==1 && !(bool)Get(host,"HasPending") && !p.controlUp,"After a full release update, second legal native Up restores original gravity and retires borrowing/input.");
            foreach(bool forced in new[]{false,true})
            {
                Select(SmartFallFamily.GravityGlobe);p=PlayerAt(5);p.armor[13].SetDefaults(1131);Item globe=p.armor[13];
                Begin(p);try{p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);}finally{End(p);}
                Require(p.gravDir==-1,"First exact native Up has reversed gravity before takeover.");
                long actions=(long)Get(host,"NativeActions");if(forced)p.forcedGravity=1;else p.controlUp=true;
                Begin(p);try{p.ResetEffects();p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);}finally{End(p);}
                Require(!(bool)Get(host,"HasPending") && (long)Get(host,"NativeActions")==actions && ReferenceEquals(p.armor[13],globe),"Forced gravity or manual Up ends old return; no synthetic reversal and exact item returns forced="+forced);
            }
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
            Require((long)Get(host,"Plans")==plans+1 && platform.Platform && platform.SafeSurface,"Type/frame changes refresh platform and safe-surface meaning through the finite queried contact band.");
            int nearY=(int)((p.position.Y+p.height)/16)+1,left=(int)(p.position.X/16),right=(int)((p.position.X+p.width-1)/16);
            var oldNear=new Tile[right-left+1];for(int x=left;x<=right;x++)oldNear[x-left]=Main.tile[x,nearY];
            Tile unrelated=Main.tile[60,nearY];
            try
            {
                plans=(long)Get(host,"Plans");scans=(long)Get(host,"SourceScans");long tileReads=(long)Get(host,"TileReads");
                var samples=new double[8];
                var allocated=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",F));long bytes=allocated();
                for(int i=0;i<8;i++){long started=Stopwatch.GetTimestamp();Begin(p);End(p);samples[i]=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;}
                bytes=allocated()-bytes;
                long rereads=(long)Get(host,"TileReads")-tileReads;
                Require((long)Get(host,"Plans")==plans && (long)Get(host,"SourceScans")==scans && rereads>0 && rereads<=4096*8,"Eight stable dangerous corridor checks perform bounded real tile rereads, without replanning or rescanning sources.");
                Main.tile[60,nearY]=new Tile();Main.tile[60,nearY].active(true);Main.tile[60,nearY].type=1;
                Begin(p);End(p);Require((long)Get(host,"Plans")==plans,"Unrelated tile outside the finite queried corridor does not wake planning.");
                for(int x=left;x<=right;x++){Main.tile[x,nearY]=new Tile();Main.tile[x,nearY].active(true);Main.tile[x,nearY].type=19;Main.tile[x,nearY].frameY=0;}
                Begin(p);
                try
                {
                    var nearer=(SmartFallContact)Get(host,"contact");Require((long)Get(host,"Plans")==plans+1 && nearer.Found && nearer.TileY==nearY && nearer.Y<platform.Y && !nearer.SafeSurface,"Newly active nearer platform invalidates the old far safe contact before body collision.");
                    p.UpdateEquips(p.whoAmI);Require(p.noFallDmg && p.position.Y+p.height<nearY*16,"Real passive protection is equipped before first new-platform contact.");
                    Motion(p);p.DryCollision(false,false);p.SlopingCollision(false,false);
                }
                finally{End(p);}
                for(int i=0;i<4;i++){Begin(p);try{Damage(p);p.UpdateEquips(p.whoAmI);Motion(p);p.DryCollision(false,false);p.SlopingCollision(false,false);}finally{End(p);}}
                Require(p.statLife==5000 && !(bool)Get(host,"HasPending"),"Original new-platform Hurt and exact return complete without lost health.");
                Array.Sort(samples);Console.WriteLine("SMART-FALL CORRIDOR stable-snapshots=8 real-tile-rereads="+rereads+" plans=0 source-scans=0 unrelated-wake=0 nearer-wake=1 sample-scope=production-Begin/Capture/End+reflection ms-p50="+samples[4]+" ms-p95="+samples[7]+" thread-bytes-with-fixture="+bytes+" no-render/no-FPS");
            }
            finally{for(int x=left;x<=right;x++)Main.tile[x,nearY]=oldNear[x-left];Main.tile[60,nearY]=unrelated;}
            // Resume a separate genuine dangerous snapshot for the permission
            // invalidation assertion; the prior near-platform rescue is over.
            Call(host,"ClearSession");p=PlayerAt(200);p.inventory[17].SetDefaults(158);Begin(p);End(p);
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
        private static void NativeCollision(Player p)
        {
            // Exact Update .8 flags, including last Update's stairFall. The
            // original SlopingCollision alone consumes and clears that state.
            bool ignore=p.gravDir<0 || p.mount.Active && (p.mount.Cart || p.mount.Type==12 || p.mount.Type==7 || p.mount.Type==8 || p.mount.Type==23 || p.mount.Type==44 || p.mount.Type==48) || p.GoingDownWithGrapple || p.pulley;
            bool through=p.controlDown || ignore;
            p.DryCollision(through,ignore);p.SlopingCollision(through,ignore);
        }
        private static void FlyingMountLateSlopeControl()
        {
            Select(SmartFallFamily.FlyingMount);Call(movement,"SetEnabled",3,false);
            var p=PlayerAt(15);p.miscEquips[3].SetDefaults(3367);p.velocity.X=2;
            for(int x=35;x<65;x++){Main.tile[x,Floor].type=19;Main.tile[x,Floor].frameY=0;Main.tile[x,Floor].slope(1);}
            p.QuickMount();Require(p.mount.Active,"Independent later-stage control truly summons the native hover mount.");
            p.UpdateEquips(p.whoAmI);Motion(p);NativeCollision(p);p.PlayerFrame();Require(p.stairFall,"Previous real mounted slope phase leaves native stairFall.");
            p.UpdateEquips(p.whoAmI);p.controlJump=true;Motion(p);p.controlJump=false;p.mount.TryDismount(p);
            Require(!p.mount.Active && p.stairFall,"Late real dismount does not itself clean the prior stair flag.");
            NativeCollision(p);
            for(int i=0;i<3;i++){Motion(p);NativeCollision(p);}
            Require(p.position.Y+p.height>Floor*16+16,"Same later native phase misses the original slope, independently of helper readiness/delay.");
            Console.WriteLine("SMART-FALL LATE-SLOPE original-mount=true prior-stair=true dismount-before-flags=true original-slope-missed=true");
            Call(movement,"SetEnabled",3,true);
        }
        private static void SocialEquipmentConsumers()
        {
            var capabilities=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Movement.SmartFallCapabilities",true);
            int[][] mapping={new[]{1,53,399,1250},new[]{2,857,983,1252},new[]{4,987,1163,1251},new[]{8,1724,1863,3250},new[]{16,3201,3241,3252},new[]{7,1164,5331}};
            foreach(var row in mapping)foreach(int id in row.Skip(1)){var item=new Item();item.SetDefaults(id);Require((int)capabilities.GetMethod("JumpFamilies",F).Invoke(null,new object[]{item})==row[0],"Exact five-family/upgrade/bundle mapping id="+id);}
            SocialConsumer(SmartFallFamily.Wings,493,13,false,false);
            SocialConsumer(SmartFallFamily.Horseshoe,5331,13,false,false);
            SocialConsumer(SmartFallFamily.GravityGlobe,1131,13,false,false);
            SocialConsumer(SmartFallFamily.DjinnLegs,3770,12,false,false);
        }
        private static void SocialConsumer(SmartFallFamily family,int type,int slot,bool inventory,bool triple)
        {
            Select(family);var p=PlayerAt(25);Item[] source=inventory?p.inventory:p.armor;source[slot].SetDefaults(type);
            Item item=source[slot];item.Prefix(62);item.favorited=true;int prefix=item.prefix,stack=item.stack;
            if(triple){p.armor[14].SetDefaults(857);p.armor[15].SetDefaults(987);}
            Item[] armor=(Item[])p.armor.Clone(),bag=(Item[])p.inventory.Clone();
            Require(!p.canJumpAgain_Cloud && !p.canJumpAgain_Sandstorm && !p.canJumpAgain_Blizzard && p.rocketTime==0 && !p.canCarpet && p.carpetTime==0 && p.wingTime==0,"Natural social resource initial state contains no precharged opportunity family="+family);
            bool loan=false,equipped=false,consumed=false,protectedNative=false,landed=false;int original=p.fallStart;
            var hooks=movement.GetType().Assembly.GetType("JueMingR.TerrariaHost.Movement.MovementHooks",true);
            for(int tick=0;tick<240;tick++)
            {
                hooks.GetMethod("BeginWorld",F).Invoke(null,null);
                Begin(p);
                try
                {
                    var borrow=Get(host,"borrow");if((bool)Get(borrow,"HasPending"))loan=true;
                    Damage(p);p.ResetEffects();p.UpdateEquips(p.whoAmI);
                    bool functional=Enumerable.Range(0,10).Any(i=>ReferenceEquals(p.GetEffectiveArmor(i),item));
                    equipped|=functional;
                    if(family==SmartFallFamily.Wings && functional && !landed)Require(p.wingTime==0,"Equipping passive social wings does not grant flight time before native landing.");
                    if(family==SmartFallFamily.DjinnLegs && functional)Require(ReferenceEquals(p.armor[2],item),"Social legs 12 enter only native functional legs 2.");
                    Eligibility(p);Motion(p);Call(host,"BeforeGrapple",p);
                    consumed|=family==SmartFallFamily.DoubleJump?p.jump>0 && p.velocity.Y<0:family==SmartFallFamily.RocketBoots?p.rocketDelay>0:family==SmartFallFamily.Carpet?p.carpetTime>0 && p.carpetFrame>=0:family==SmartFallFamily.GravityGlobe?p.gravDir<0:family==SmartFallFamily.DjinnLegs?p.slowFall && p.fallStart!=original:family==SmartFallFamily.Wings?p.hasWings:p.noFallDmg;
                    protectedNative|=(SmartFallResult)Get(host,"Result")==SmartFallResult.Protected;
                    NativeCollision(p);
                    if(p.velocity.Y==0){landed=true;Damage(p);}
                }
                finally{End(p);hooks.GetMethod("EndWorld",F).Invoke(null,new object[]{null});}
                Require(p.statLife==5000,"Exact social native contact chain stays unharmed family="+family+" tick="+tick);
                if(landed && !(bool)Get(host,"HasPending"))break;
            }
            Require(loan && equipped && consumed && protectedNative && landed && !(bool)Get(host,"HasPending"),"Real social selection/loan/equipment/native consumer/protection/landing/return all occur family="+family+" facts="+loan+","+equipped+","+consumed+","+protectedNative+","+landed+" result="+Get(host,"Result"));
            Require(ReferenceEquals(source[slot],item) && item.prefix==prefix && item.stack==stack && item.favorited,"Exact source item and prefix/stack/favorite return family="+family);
            Require(armor.Zip(p.armor,(a,b)=>ReferenceEquals(a,b)).All(x=>x) && bag.Zip(p.inventory,(a,b)=>ReferenceEquals(a,b)).All(x=>x),"Every original physical endpoint is conserved family="+family);
            if(family==SmartFallFamily.GravityGlobe)Require(p.gravDir==1,"Gravity returns before exact item return.");
            Console.WriteLine("SMART-FALL SOCIAL family="+family+" source="+(inventory?"inventory":"armor")+slot+" type="+type+" triple="+triple+" loan/equip/consumer/protection/contact/return=true life="+p.statLife+" exact-references=true natural-empty-resources=true");
        }
        // The old social ledger tests required an extra charge. These cases
        // deliberately start empty and compare the actual native equip path.
        private static void VanillaResourceBoundaries()
        {
            foreach(var family in new[]{SmartFallFamily.DoubleJump,SmartFallFamily.RocketBoots,SmartFallFamily.Carpet})
            foreach(bool inventory in new[]{false,true})
            {
                Select(family);var p=PlayerAt(40);int type=family==SmartFallFamily.DoubleJump?53:family==SmartFallFamily.RocketBoots?128:934;
                Item[] source=inventory?p.inventory:p.armor;int slot=inventory?17:13;source[slot].SetDefaults(type);Item item=source[slot];
                long scans=(long)Get(host,"SourceScans"),actions=(long)Get(host,"NativeActions");
                for(int t=0;t<8;t++){Begin(p);try{p.ResetEffects();p.UpdateEquips(p.whoAmI);Eligibility(p);Motion(p);Call(host,"BeforeGrapple",p);}finally{End(p);}}
                Require(ReferenceEquals(source[slot],item) && !(bool)Get(host,"HasPending") && (long)Get(host,"NativeActions")==actions && !p.canJumpAgain_Cloud && p.rocketTime==0 && !p.canCarpet && p.carpetTime==0 && p.jump==0,"Natural empty source never creates ability, borrows or consumes family="+family+" inventory="+inventory);
                Require((long)Get(host,"SourceScans")==scans+1,"Stable empty-source readiness does not repeat inventory scans.");
                Call(movement,"SetEnabled",3,false);
                // Exact native player right-click equip or vanity swap, then
                // the original eligibility and motion consumers.
                if(inventory)Terraria.UI.ItemSlot.SwapEquip(source,0,slot);
                else typeof(Terraria.UI.ItemSlot).GetMethod("SwapVanityEquip",F).Invoke(null,new object[]{source,11,slot,p});
                Require(Enumerable.Range(0,10).Any(i=>ReferenceEquals(p.GetEffectiveArmor(i),item)),"Independent manual native swap equips the same exact item.");
                p.ResetEffects();p.UpdateEquips(p.whoAmI);Eligibility(p);p.controlJump=true;Motion(p);
                Require(!p.canJumpAgain_Cloud && p.jump==0 && p.rocketTime==0 && p.rocketDelay==0 && !p.canCarpet && p.carpetTime==0,"Same empty initial resources remain empty after native manual air equip/press family="+family);
                Call(movement,"SetEnabled",3,true);
                Console.WriteLine("SMART-FALL VANILLA-EMPTY family="+family+" source="+(inventory?"inventory":"social")+" assisted-loans/actions=0 manual-native-equip-no-new-opportunity=true stable-scans=1");
            }
            foreach(var family in new[]{SmartFallFamily.RocketBoots,SmartFallFamily.Carpet})
            {
                Select(family);var p=PlayerAt(40);p.armor[13].SetDefaults(family==SmartFallFamily.RocketBoots?128:934);
                Begin(p);End(p);long scans=(long)Get(host,"SourceScans");
                if(family==SmartFallFamily.RocketBoots)p.rocketTime=3;else p.carpetTime=25;
                p.position.Y=Floor*16-p.height-(family==SmartFallFamily.RocketBoots?15:5);
                Begin(p);try{p.ResetEffects();p.UpdateEquips(p.whoAmI);Eligibility(p);Motion(p);Call(host,"BeforeGrapple",p);}finally{End(p);}
                Require((long)Get(host,"SourceScans")==scans+1 && (family==SmartFallFamily.RocketBoots?p.rocketTime==2 && p.rocketDelay>0:p.carpetTime==24 && p.carpetFrame>=0),"A single real readiness transition reopens the unchanged social source; residual is natively consumed family="+family);
            }
            // Native brooch refill is a legal producer, with no helper write.
            Select(SmartFallFamily.RocketBoots);var brooch=PlayerAt(15);brooch.armor[3].SetDefaults(128);brooch.armor[4].SetDefaults(4989);brooch.UpdateEquips(brooch.whoAmI);
            Require(brooch.empressBrooch && brooch.rocketTime==0,"Real equipped brooch starts empty before native refill.");
            Begin(brooch);try{Motion(brooch);Call(host,"BeforeGrapple",brooch);}finally{End(brooch);}
            Require(brooch.rocketDelay>0 && brooch.rocketTime==brooch.rocketTimeMax-1,"Original brooch consumer refills then spends real fuel.");
        }
        private static void LegalInputStages()
        {
            foreach(bool rocket in new[]{false,true})foreach(bool rearmed in new[]{false,true})
            {
                Player manual=null,assisted=null;
                for(int side=0;side<2;side++)
                {
                    bool help=side==1;Select(rocket?SmartFallFamily.RocketBoots:SmartFallFamily.DoubleJump);Call(movement,"SetEnabled",3,help);
                    var p=PlayerAt(rearmed?15:rocket?25:15);p.armor[3].SetDefaults(rocket?128:53);p.UpdateEquips(p.whoAmI);if(rocket)p.rocketTime=7;else p.canJumpAgain_Cloud=true;
                    p.controlJump=true;p.releaseJump=p.rocketRelease=rearmed;
                    for(int t=0;t<(rearmed?1:2);t++)
                    {
                        if(help)Begin(p);else p.controlJump=rearmed || t>0;
                        try
                        {
                            Motion(p);if(help)Call(host,"BeforeGrapple",p);
                            if(!rearmed && t==0)Require(p.jump==0 && p.velocity.Y>0 && p.releaseJump && p.rocketRelease && (!rocket || p.rocketTime==7 && p.rocketDelay==0),"Release reaches native Jump and all downstream flight consumers; no premature action side="+side+" rocket="+rocket);
                        }
                        finally{if(help)End(p);}
                    }
                    Require(rocket?p.rocketTime==6 && p.rocketDelay>0:p.jump>0 && !p.canJumpAgain_Cloud,"Existing native resource is consumed after legal Press side="+side);
                    if(help)assisted=p;else manual=p;
                }
                Require(assisted.velocity==manual.velocity && assisted.jump==manual.jump && assisted.rocketTime==manual.rocketTime && assisted.rocketDelay==manual.rocketDelay && assisted.releaseJump==manual.releaseJump && assisted.rocketRelease==manual.rocketRelease,"Equal initial state and manual Release/Press trajectory match native helper results.");
                Console.WriteLine("SMART-FALL LEGAL-INPUT rocket="+rocket+" initially-rearmed="+rearmed+" updates="+(rearmed?1:2)+" native-manual-trajectory-equal=true");
            }
            Call(movement,"SetEnabled",3,true);
            Select(SmartFallFamily.Carpet);var carpet=PlayerAt(5);carpet.armor[3].SetDefaults(934);carpet.UpdateEquips(carpet.whoAmI);carpet.canCarpet=true;carpet.releaseJump=false;carpet.controlJump=true;
            Begin(carpet);try{Motion(carpet);Call(host,"BeforeGrapple",carpet);}finally{End(carpet);}
            Require(carpet.carpetTime==299 && carpet.carpetFrame>=0,"Native Carpet needs held Jump, not a releaseJump qualification; do not add an artificial release delay.");
            Select(SmartFallFamily.DoubleJump);var tail=PlayerAt(15);tail.armor[3].SetDefaults(53);tail.UpdateEquips(tail.whoAmI);tail.canJumpAgain_Cloud=true;
            tail.controlJump=true;tail.releaseJump=false;tail.jump=5;tail.velocity.Y=-4;
            long before=(long)Get(host,"NativeActions");Begin(tail);try{Motion(tail);}finally{End(tail);}
            Require(tail.jump==4 && tail.velocity.Y<0 && tail.canJumpAgain_Cloud && (long)Get(host,"NativeActions")==before,"A legitimate rising held jump tail is neither released nor consumed by fall preparation.");
            // Turning OFF during a completed release update returns all owned
            // inputs/items; no future Press or resource rollback is retained.
            Select(SmartFallFamily.RocketBoots);var cancel=PlayerAt(25);cancel.armor[13].SetDefaults(128);Item boot=cancel.armor[13];cancel.rocketTime=3;cancel.controlJump=true;cancel.rocketRelease=false;
            Begin(cancel);try{cancel.UpdateEquips(cancel.whoAmI);Motion(cancel);}finally{End(cancel);}
            Require((bool)Get(host,"HasPending") && cancel.rocketTime==3,"A real unconsumed release plan is pending before explicit cancellation.");
            Call(movement,"SetEnabled",3,false);Begin(cancel);End(cancel);
            Require(!(bool)Get(host,"HasPending") && ReferenceEquals(cancel.armor[13],boot) && cancel.rocketTime==3 && cancel.controlJump,"OFF cancels finite preparation, returns exact item, and preserves real user held input and fuel.");
            Call(movement,"SetEnabled",3,true);
        }
        private static void MountReturnGates()
        {
            // Build the exact pending intention with a real original item and
            // real native QuickMount; no rescue success is fabricated here.
            foreach(string gate in new[]{"legal","CCed","tongued","gravity","dead","noItems","missing","same-type-new","selection","wet","space"})
            {
                Select(SmartFallFamily.SafeMount);var p=PlayerAt(100);p.miscEquips[3].SetDefaults(2428);Item original=p.miscEquips[3];
                p.QuickMount();Require(p.mount.Active,"Native initial ordinary mount activates.");p.mount.Dismount(p);
                int blockX=(int)(p.Center.X/16),blockY=(int)((p.Center.Y+p.velocity.Y)/16);Tile oldTile=Main.tile[blockX,blockY];
                Begin(p);
                try
                {
                    Set(host,"originalMount",original.mountType);Set(host,"originalMountItem",original);Set(host,"mountSelection",p.selectedItem);Set(host,"originalSelectionItem",p.HeldItem);
                    if(gate=="CCed")p.webbed=true;if(gate=="tongued")p.tongued=true;if(gate=="gravity")p.gravDir=-1;if(gate=="dead")p.dead=true;if(gate=="noItems")p.noItems=true;
                    if(gate=="missing")p.miscEquips[3]=new Item();if(gate=="same-type-new"){p.miscEquips[3]=new Item();p.miscEquips[3].SetDefaults(2428);}if(gate=="selection")p.selectedItemState.Select(1);
                    if(gate=="wet")p.wet=true;
                    if(gate=="space"){Main.tile[blockX,blockY]=new Tile();Main.tile[blockX,blockY].active(true);Main.tile[blockX,blockY].type=1;Require(!p.mount.CanMount(original.mountType,p),"Original CanMount independently rejects the occupied box.");}
                    Call(host,"ReturnOriginalMount",p);
                    bool allowed=gate=="legal" || gate=="wet";
                    Require(p.mount.Active==allowed && (int)Get(host,"originalMount")==-1,"Complete native mount gate/identity ends finite restore gate="+gate);
                    if(p.mount.Active)p.mount.Dismount(p);
                    p.webbed=p.tongued=p.dead=p.noItems=p.wet=false;p.gravDir=1;
                    if(gate=="space")
                    {
                        Main.tile[blockX,blockY]=new Tile();
                        Require(p.mount.CanMount(original.mountType,p),"Opening the original mount's space really restores native qualification, without renewing retired permission.");
                    }
                    Call(host,"ReturnOriginalMount",p);Require(!p.mount.Active,"A denied/completed intention cannot resurrect on later condition changes gate="+gate);
                }
                finally{End(p);Main.tile[blockX,blockY]=oldTile;}
            }
            DefaultCartReturn();
            Console.WriteLine("SMART-FALL MOUNT-RETURN native-entry-positive=true current-item/reference-and-use-gates=true denial-no-retry=true");
        }
        private static void PendingMountRecovery()
        {
            var ceiling=new Tile[30];for(int x=35;x<65;x++)ceiling[x-35]=Main.tile[x,Floor-3];
            bool stair=Collision.stair,stairFall=Collision.stairFall,sloping=Collision.sloping,up=Collision.up,down=Collision.down;
            try
            {
                foreach(string change in new[]{"space","CCed","takeover","death","generation"})
                {
                    Call(movement,"SetEnabled",3,true);
                    for(int x=35;x<65;x++)Main.tile[x,Floor-3]=new Tile();
                    Select(SmartFallFamily.FlyingMount);var p=PlayerAt(15);p.inventory[17].SetDefaults(5597);
                    Item source=p.inventory[17],prior=p.miscEquips[3];byte prefix=source.prefix;int stack=source.stack;
                    Begin(p);End(p);
                    Require(p.mount.Active && p.mount.Type==56 && (int)Get(host,"ownedMount")==56,"Real borrowed Bat QuickMount establishes rescue ownership.");
                    p.position.Y=Floor*16-p.height;p.velocity=Vector2.Zero;
                    if(change!="CCed")for(int x=35;x<65;x++){Main.tile[x,Floor-3].active(true);Main.tile[x,Floor-3].type=1;}
                    else p.webbed=true;
                    Require(!p.mount.CanDismount(p),"Independent original predicate rejects the real small mounted hitbox change="+change);
                    // The first attempted action remains immediate. Only its
                    // actual denial creates the later observation obligation.
                    Begin(p);try{Call(host,"Protected",p,true);}finally{End(p);}
                    Require((bool)Get(host,"mountReturnPending") && p.mount.Active,"Native first denial enters pending cleanup.");
                    Call(movement,"SetEnabled",3,false);
                    long actions=(long)Get(host,"NativeActions"),queries=(long)Get(host,"DismountObservations"),scans=(long)Get(host,"SourceScans"),plans=(long)Get(host,"Plans");
                    Vector2 position=p.position;long revision=(long)Get(Get(items,"World"),"Revision");
                    for(int tick=0;tick<8;tick++)
                    {
                        Collision.stair=true;Collision.stairFall=false;Collision.sloping=true;Collision.up=false;Collision.down=true;
                        Begin(p);
                        try
                        {
                            Call(host,"Capture",p,false);Call(host,"Protected",p,true);Call(host,"BeforeGrapple",p);
                            Require(Collision.stair && !Collision.stairFall && Collision.sloping && !Collision.up && Collision.down,"Extra denied native query restores all five Collision scratch flags.");
                        }
                        finally{End(p);}
                    }
                    long expected=change=="CCed"?0:8;
                    Require((long)Get(host,"DismountObservations")==queries+expected && (long)Get(host,"NativeActions")==actions,"OFF pending uses at most one query per real tick, zero failed native actions; native CCed shortcut only on its specified mount.");
                    Require((long)Get(host,"SourceScans")==scans && (long)Get(host,"Plans")==plans,"Pending cleanup does not scan inventory or run main contact.");
                    Require(p.position==position && (long)Get(Get(items,"World"),"Revision")==revision,"Stable denial leaves position and inventory revision unchanged.");
                    if(change=="takeover")p.mount.SetMount(0,p);
                    else if(change=="death")p.dead=true;
                    else if(change=="generation")
                    {
                        Call(host,"BeginUpdate",p,long.MaxValue,1L);
                        Require(!(bool)Get(host,"mountReturnPending"),"Successor generation clears old pending query clock.");
                        Call(host,"EndUpdate",p);continue;
                    }
                    else if(change=="CCed")p.webbed=false;
                    else for(int x=35;x<65;x++)Main.tile[x,Floor-3]=new Tile();
                    bool[] nativeScratch=null;
                    if(change=="space")
                    {
                        var control=new Player{whoAmI=Main.myPlayer,active=true};control.mount.SetMount(56,control);control.position=p.position;control.velocity=p.velocity;
                        Collision.stair=true;Collision.stairFall=false;Collision.sloping=true;Collision.up=false;Collision.down=true;
                        Require(control.mount.TryDismount(control),"Independent real native dismount accepts the newly opened standing space.");
                        nativeScratch=new[]{Collision.stair,Collision.stairFall,Collision.sloping,Collision.up,Collision.down};
                        Collision.stair=true;Collision.stairFall=false;Collision.sloping=true;Collision.up=false;Collision.down=true;
                    }
                    queries=(long)Get(host,"DismountObservations");
                    Begin(p);End(p);
                    if(nativeScratch!=null)Require(nativeScratch.SequenceEqual(new[]{Collision.stair,Collision.stairFall,Collision.sloping,Collision.up,Collision.down}),"Real assisted TryDismount retains the original native scratch side effects after isolated observation.");
                    bool recovered=change=="space" || change=="CCed";
                    Require((long)Get(host,"NativeActions")==actions+(recovered?1:0),"Only native-eligible recovery performs one real dismount change="+change);
                    Require((long)Get(host,"DismountObservations")==queries+(recovered?1:0),"Only still-owned pending mount is observed change="+change);
                    Require(!(bool)Get(host,"mountReturnPending"),"Recovery or ownership retirement clears the old dismount observation change="+change);
                    if(change=="death")
                    {
                        // The existing loan owner deliberately waits while dead;
                        // cleanup must not weaken its writable-player boundary.
                        Require((bool)Get(host,"HasPending"),"Death retires mount observation while preserving exact real item-return responsibility.");
                        p.dead=false;queries=(long)Get(host,"DismountObservations");
                        Begin(p);End(p);
                        Require((long)Get(host,"DismountObservations")==queries,"Becoming writable again returns only the item, never reviving dead rescue ownership.");
                    }
                    Require(!(bool)Get(host,"HasPending"),"Owned item responsibility finishes in a writable context change="+change);
                    Require(ReferenceEquals(p.inventory[17],source) && ReferenceEquals(p.miscEquips[3],prior) && source.prefix==prefix && source.stack==stack,"Exact real borrowed source returns without resource replacement change="+change);
                    if(change=="takeover")Require(p.mount.Active && p.mount.Type==0,"Player successor mount is never dismounted.");
                    else if(recovered)Require(!p.mount.Active,"Space-only or CCed-only recovery really dismounts with unchanged input position/revision before recovery.");
                    queries=(long)Get(host,"DismountObservations");actions=(long)Get(host,"NativeActions");
                    Call(host,"BeginUpdate",p,1L,10000L);Call(host,"Capture",p,false);Call(host,"BeforeGrapple",p);Call(host,"EndUpdate",p);
                    Require((long)Get(host,"DismountObservations")==queries && (long)Get(host,"NativeActions")==actions,"No owner/pending means zero observations or actions after completion.");
                }
                // Ordinary mounts have no native CCed dismount prohibition.
                Select(SmartFallFamily.FlyingMount);var ordinary=PlayerAt(100);ordinary.mount.SetMount(12,ordinary);ordinary.webbed=true;
                Call(host,"BeginUpdate",ordinary,1L,1L);Set(host,"ownedMount",12);Set(host,"mountReturnPending",true);
                long before=(long)Get(host,"DismountObservations");
                Require((bool)Call(host,"TryReturnMount",ordinary) && (long)Get(host,"DismountObservations")==before+1 && !ordinary.mount.Active,"CCed ordinary mount still asks and follows its original native qualification.");
                Call(host,"EndUpdate",ordinary);
                Console.WriteLine("SMART-FALL PENDING-MOUNT real-loan=true OFF-cleanup=true stable-low-ceiling=8 queries=8 failed-actions=0 same-tick-dedup=true space-only/CCed-only=true manual/death/generation-retire=true scratch-five-restored=true");
            }
            finally
            {
                Call(movement,"EndUpdate",Main.LocalPlayer);Call(host,"ClearSession");Call(movement,"SetEnabled",3,true);
                for(int x=35;x<65;x++)Main.tile[x,Floor-3]=ceiling[x-35];
                Collision.stair=stair;Collision.stairFall=stairFall;Collision.sloping=sloping;Collision.up=up;Collision.down=down;
            }
        }
        private static bool longOpposite;
        private static void OriginalOppositeHorizon(ref int horizon) { if(longOpposite && horizon==2)horizon=48; }
        private static void OppositeCaptureWork()
        {
            Type geometry=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Movement.SmartFallGeometry",true);
            MethodInfo contact=geometry.GetMethods(F).Single(m=>m.Name=="Contact" && m.GetParameters().Length==11);
            var baseline=new Harmony("JueMingR.Tests.SmartFall.OppositeBaseline");
            baseline.Patch(contact,prefix:new HarmonyMethod(typeof(NativeSmartFallActionsChecks),nameof(OriginalOppositeHorizon)));
            long[] reads=new long[2];
            try
            {
                for(int pass=0;pass<2;pass++)
                {
                    longOpposite=pass==0;long before=(long)Get(host,"OppositeTileReads");
                    for(int batch=0;batch<8;batch++)
                    {
                        Select(SmartFallFamily.GravityGlobe);var p=PlayerAt(300);p.inventory[17].SetDefaults(1131);
                        for(int step=0;step<12;step++)
                        {
                            long actions=(long)Get(host,"NativeActions");Begin(p);
                            try
                            {
                                Require(!(bool)Get(host,"action") && (long)Get(host,"NativeActions")==actions && ((SmartFallContact)Get(host,"contact")).Found,"Measured real dangerous Capture has an available globe/contact and is not yet Due.");
                                Require(Choice().Family==SmartFallFamily.GravityGlobe,"Repeated measured path actually builds the opposite-globe candidate.");
                            }
                            finally{End(p);}
                        }
                    }
                    reads[pass]=(long)Get(host,"OppositeTileReads")-before;
                }
                Require(reads[0]>reads[1] && reads[1]>0,"Actual not-Due Capture reduces opposite geometry reads against the original horizon, independently of main Plans.");
                Console.WriteLine("SMART-FALL OPPOSITE-CAPTURE iterations=96 per-pass original-horizon48-reads="+reads[0]+" horizon2-reads="+reads[1]+" native-actions=0 scope=Debug-tile-visits not FPS");
            }
            finally{longOpposite=false;baseline.Unpatch(contact,HarmonyPatchType.All,baseline.Id);Call(host,"ClearSession");}
        }
        private static void DefaultCartReturn()
        {
            Select(SmartFallFamily.SafeMount);var p=PlayerAt(100);p.velocity=Vector2.Zero;
            int x=(int)(p.Center.X/16),y=(int)(p.Center.Y/16);Tile old=Main.tile[x,y];
            for(int i=0;i<Main.projectile.Length;i++)if(Main.projectile[i]!=null)Main.projectile[i].active=false;
            Main.tile[x,y]=new Tile();Main.tile[x,y].active(true);Main.tile[x,y].type=314;Main.tile[x,y].frameX=0;Main.tile[x,y].frameY=-1;
            Begin(p);
            try
            {
                Set(host,"originalMount",13);Set(host,"originalMountItem",p.miscEquips[2]);Set(host,"mountSelection",p.selectedItem);Set(host,"originalSelectionItem",p.HeldItem);
                Call(host,"ReturnOriginalMount",p);
                var hook=Main.projectile.FirstOrDefault(q=>q!=null && q.active && q.owner==p.whoAmI && q.type==403);
                Require(hook!=null && !p.mount.Active && (int)Get(host,"originalMount")==-1,"Default cart uses native QuickMinecart fallback/real rail-hook; it never directly invents a mounted result.");
                hook.active=false;
            }
            finally{End(p);Main.tile[x,y]=old;}
        }
        private static bool captureFaultInput;
        private static void NativeConsumerFailure()
        {
            // Both Press and Release failures go through Player.Update's real
            // BeginPlayer/EndPlayer state and preserve the original exception.
            foreach(bool releasing in new[]{false,true})
            {
                Select(SmartFallFamily.RocketBoots);var p=PlayerAt(releasing?25:15);p.armor[13].SetDefaults(128);p.rocketTime=3;
                p.controlJump=releasing;p.rocketRelease=!releasing;
                Item item=p.armor[13],original=p.armor[3];
                var patch=new Harmony("JueMingR.Tests.SmartFall.ConsumerFailure");var jump=AccessTools.Method(typeof(Player),"JumpMovement");
                var horizontal=AccessTools.Method(typeof(Player),"HorizontalMovement");
                patch.Patch(horizontal,prefix:new HarmonyMethod(typeof(NativeSmartFallActionsChecks),nameof(CaptureConsumerFaultInput)){priority=Priority.First});
                patch.Patch(jump,prefix:new HarmonyMethod(typeof(NativeSmartFallActionsChecks),nameof(RejectSocialJump)){priority=Priority.Last});
                var hooks=movement.GetType().Assembly.GetType("JueMingR.TerrariaHost.Movement.MovementHooks",true);
                Exception observed=null;
                NativeQuickItemChecks.BeginWorldStep();NativeQuickItemChecks.Sample(input,new Keys[0]);
                hooks.GetMethod("BeginWorld",F).Invoke(null,null);captureFaultInput=true;
                try{Motion(p);}catch(InvalidOperationException error){observed=error;}
                finally{captureFaultInput=false;hooks.GetMethod("EndWorld",F).Invoke(null,new object[]{null});patch.Unpatch(jump,HarmonyPatchType.All,patch.Id);patch.Unpatch(horizontal,HarmonyPatchType.All,patch.Id);}
                Require(ReferenceEquals(observed,consumerFault) && !(bool)Call(movement,"CanAct",p),"Real EndPlayer preserves the exact original native exception and closes its scope releasing="+releasing);
                Require((SmartFallResult)Get(host,"Result")==SmartFallResult.Failed && !(bool)Get(host,"action") && !(bool)Get(host,"HasPending") &&
                    p.rocketTime==3 && p.controlJump==releasing && ReferenceEquals(p.armor[13],item) && ReferenceEquals(p.armor[3],original),
                    "Native exception retires Release/Press intent and returns exact objects/input without changing real fuel releasing="+releasing+" action="+Get(host,"action")+" pending="+Get(host,"HasPending"));
                long actions=(long)Get(host,"NativeActions");Begin(p);End(p);
                Require(!(bool)Get(host,"HasPending") && (long)Get(host,"NativeActions")==actions,"Failed intent cannot advance on the following ordinary scope releasing="+releasing);
                Console.WriteLine("SMART-FALL CONSUMER-FAULT releasing="+releasing+" real-EndPlayer=true exact-error=true action/input/loan-retired=true real-fuel-preserved=3");
            }
        }
        private static readonly InvalidOperationException consumerFault=new InvalidOperationException("social-native-consumer-fault");
        private static void CaptureConsumerFaultInput(Player __instance)
        {
            if(!captureFaultInput)return;
            // The existing Motion fragment omits Capture/Equips. Reproduce
            // those exact production stages before its first motion consumer;
            // the outer native Update still owns the actual finalizer state.
            Call(movement,"CaptureInput",__instance);__instance.ResetEffects();__instance.UpdateEquips(__instance.whoAmI);
        }
        private static void RejectSocialJump(){throw consumerFault;}
        private static void GravityPreparationTakeover()
        { foreach(bool loan in new[]{true,false})GravityPreparationTakeoverCase(loan); }
        private static void GravityPreparationTakeoverCase(bool loan)
        {
            Select(SmartFallFamily.GravityGlobe);var p=PlayerAt(15);int source=loan?13:3;p.armor[source].SetDefaults(1131);Item globe=p.armor[source],original=p.armor[3];p.UpdateEquips(p.whoAmI);
            p.releaseUp=false;
            Begin(p);try{p.ResetEffects();p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);}finally{End(p);}
            Require(p.gravDir==1 && p.releaseUp && (bool)Get(host,"action") && !(bool)Get(host,"returnGravity"),"Initial native release rearms Up while the first Globe press remains pending.");
            p.controlUp=true;long actions=(long)Get(host,"NativeActions");
            Begin(p);
            try
            {
                Require(!(bool)Get(host,"action") && !(bool)Get(host,"returnGravity") && !(bool)Get(host,"HasPending") &&
                    (SmartFallResult)Get(host,"Result")==SmartFallResult.TakenOver && p.controlUp && ReferenceEquals(p.armor[source],globe) && ReferenceEquals(p.armor[3],original),
                    "Manual Up during first-press preparation immediately retires old intent/loan, preserves real held Up, and returns exact objects.");
                // Keep the real item equipped through a manual native vanity
                // swap so later -1 preservation cannot be confused with
                // automatic restoration after the ability disappears.
                if(loan)typeof(Terraria.UI.ItemSlot).GetMethod("SwapVanityEquip",F).Invoke(null,new object[]{p.armor,11,13,p});
                p.ResetEffects();p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);
                Require(p.gravDir==-1,"The player's own native Up changes gravity with the same real Globe.");
            }
            finally{End(p);}
            p.controlUp=false;
            for(int t=0;t<3;t++){Begin(p);try{p.ResetEffects();p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);}finally{End(p);}}
            Require(p.gravDir==-1 && p.releaseUp && !p.controlUp && !(bool)Get(host,"HasPending") && (long)Get(host,"NativeActions")==actions,
                "Preparation takeover cannot schedule a later synthetic reverse after the player releases Up.");
            Console.WriteLine("SMART-FALL GLOBE-PREPARATION-TAKEOVER borrowed="+loan+" initial-release=true manual-Up-retired-intent=true same-real-globe-kept=true no-later-synthetic-reverse=true");
        }
        private static void CriticalLanding()
        {
            foreach(int gap in new[]{7,15,25})foreach(bool help in new[]{false,true})
            {
                Select(gap==25?SmartFallFamily.FlyingMount:SmartFallFamily.DoubleJump);Call(movement,"SetEnabled",3,help);
                var p=PlayerAt(gap);p.fallStart=Floor-(gap==15?28:29);
                if(gap==25)p.inventory[17].SetDefaults(3367);
                else{p.armor[3].SetDefaults(53);p.UpdateEquips(p.whoAmI);p.canJumpAgain_Cloud=true;}
                for(int x=35;x<65;x++){Main.tile[x,Floor].type=19;Main.tile[x,Floor].frameY=0;}
                long actions=(long)Get(host,"NativeActions");int health=p.statLife;int start=(int)(p.position.Y/16)-p.fallStart;
                for(int tick=0;tick<6;tick++)
                {
                    if(help)Begin(p);try{Damage(p);p.UpdateEquips(p.whoAmI);Motion(p);if(help)Call(host,"BeforeGrapple",p);NativeCollision(p);p.PlayerFrame();}finally{if(help)End(p);}
                }
                bool danger=gap!=15;
                Require(help && danger?p.statLife==health:danger?p.statLife<health:p.statLife==health,"Original damage consumer independently distinguishes critical danger/safe and assisted rescue gap="+gap+" help="+help+" start="+start);
                if(help && danger && gap==7)Require(!p.canJumpAgain_Cloud,"Critical current-update rescue consumes its real existing charge.");
                if(gap==15)Require((long)Get(host,"NativeActions")==actions,"Adjacent real safe landing does not submit extra actions.");
                Console.WriteLine("SMART-FALL CRITICAL gap="+gap+" help="+help+" start-tiles="+start+" final-top="+p.position.Y+" life="+p.statLife+" actions="+((long)Get(host,"NativeActions")-actions));
            }
            Call(movement,"SetEnabled",3,true);
        }
        private static void SafeShortFallBraking()
        {
            Select(SmartFallFamily.Horseshoe);var p=PlayerAt(12*16);p.velocity=new Vector2(3,.4f);p.fallStart=(int)(p.position.Y/16);
            p.inventory[17].SetDefaults(158);Item source=p.inventory[17];
            long scans=(long)Get(host,"SourceScans"),plans=(long)Get(host,"Plans"),reads=(long)Get(host,"TileReads"),actions=(long)Get(host,"NativeActions");
            int updates=0,braking=0;bool landed=false;
            for(int tick=0;tick<64;tick++)
            {
                p.controlLeft=true;p.controlRight=false;float before=p.velocity.X;
                Begin(p);try{Damage(p);p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);NativeCollision(p);}finally{End(p);}
                updates++;if(before>0 && before-p.velocity.X>.1f)braking++;
                if(p.velocity.Y==0){landed=true;Damage(p);break;}
            }
            long probes=(long)Get(host,"Plans")-plans,tiles=(long)Get(host,"TileReads")-reads;
            Require(landed && braking>=6 && p.statLife==5000 && !p.noFallDmg && ReferenceEquals(p.inventory[17],source),"Real ordinary twelve-tile fall brakes through native HorizontalMovement and lands safely without equipping its legitimate backpack source.");
            Require((long)Get(host,"NativeActions")==actions && probes<braking && (long)Get(host,"SourceScans")==scans+1,"Normal horizontal braking cannot repeatedly perform a complete Contact while the actual support strip still certifies this short safe fall.");
            Console.WriteLine("SMART-FALL SAFE-SHORT-BRAKING updates="+updates+" native-positive-braking="+braking+" source-scans="+((long)Get(host,"SourceScans")-scans)+" complete-contacts="+probes+" tile-reads="+tiles+" actions="+((long)Get(host,"NativeActions")-actions)+" original-safe-life="+p.statLife);
            SafeShortSupportRemoved();
        }
        private static void SafeShortSupportRemoved()
        {
            Select(SmartFallFamily.Horseshoe);var p=PlayerAt(12*16);p.velocity=new Vector2(3,.4f);p.fallStart=(int)(p.position.Y/16);p.inventory[17].SetDefaults(158);
            Begin(p);End(p);var first=(SmartFallContact)Get(host,"contact");long plans=(long)Get(host,"Plans");
            Require(first.Found && first.TileY==Floor,"Negative control begins with a real safe upper support contact.");
            const int lower=Floor+22;var upperTiles=new Tile[30];var lowerTiles=new Tile[30];
            for(int x=35;x<65;x++)
            {
                upperTiles[x-35]=Main.tile[x,Floor];lowerTiles[x-35]=Main.tile[x,lower];Main.tile[x,Floor]=new Tile();
                Main.tile[x,lower]=new Tile();Main.tile[x,lower].active(true);Main.tile[x,lower].type=1;
            }
            try
            {
                Begin(p);try{Require((long)Get(host,"Plans")>plans,"A removed support strip invalidates cached safety immediately, even while current integer distance is still safe.");}finally{End(p);}
                bool protectedNative=false,landed=false;
                for(int tick=0;tick<96;tick++)
                {
                    Begin(p);try{Damage(p);p.UpdateEquips(p.whoAmI);protectedNative|=p.noFallDmg;Motion(p);Call(host,"BeforeGrapple",p);NativeCollision(p);}finally{End(p);}
                    if(p.velocity.Y==0){landed=true;Damage(p);break;}
                }
                Require(landed && protectedNative && p.statLife==5000,"Changed unsafe lower landing still receives real timely passive protection; safe-contact reuse cannot conceal the new danger.");
                Console.WriteLine("SMART-FALL SAFE-SUPPORT-REMOVED immediate-reprobe=true actual-lower-landing-protected=true life="+p.statLife);
            }
            finally{for(int x=35;x<65;x++){Main.tile[x,Floor]=upperTiles[x-35];Main.tile[x,lower]=lowerTiles[x-35];}}
        }
        private static void MountRescue(SmartFallFamily family,int itemType,bool platform,bool disable=false,byte slope=0,float gap=15,float vx=0)
        {
            Select(family);var p=PlayerAt(gap);p.velocity.X=vx;p.inventory[17].SetDefaults(itemType);int health=p.statLife;
            if(platform)for(int x=35;x<65;x++){Main.tile[x,Floor].type=19;Main.tile[x,Floor].frameY=0;Main.tile[x,Floor].slope(slope);}
            bool priorStair=false,cleared=false;
            for(int tick=0;tick<8;tick++)
            {
                if(disable && tick==1)Call(movement,"SetEnabled",3,false);
                Begin(p);
                try
                {
                    Damage(p);p.selectedItemState.Update();p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);
                    if(disable && tick==1)Require(p.mount.Active,"Closing master cannot unload the safe mount's sole protection before real landing Hurt.");
                    if(platform && tick==0)Require(p.mount.Active,"New flying mount waits for its first real airborne frame, before contact.");
                    if(platform && tick==1)Require(!p.mount.Active,"Actual Hover rescue returns before this Update computes platform ignore flags.");
                    bool beforeStair=p.stairFall;NativeCollision(p);p.PlayerFrame();
                    if(tick==0 && platform)priorStair=p.stairFall;
                    if(platform && slope!=0 && beforeStair && !p.stairFall && !p.mount.Active && p.position.Y+p.height<Floor*16)cleared=true;
                    Console.WriteLine("SMART-FALL MOUNT-STATE family="+family+" tick="+tick+" result="+Get(host,"Result")+" active="+p.mount.Active+" type="+p.mount.Type+" vel="+p.velocity+" pos="+p.position+" start="+p.fallStart);
                }
                finally{End(p);}
                if(disable && tick>0 && !(bool)Get(host,"HasPending"))break;
            }
            Require(Math.Abs(p.velocity.Y)<.011f && p.position.Y+p.height<=Floor*16+16.01f,"Complete native mount action preserves its original contact platform="+platform+" slope="+slope);
            if(platform && slope!=0)Require(priorStair && cleared,"Actual prior mounted collision sets stairFall, and a later unmounted native slope pass clears it BEFORE the intended slope contact.");
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
                    p.controlJump=!help;p.releaseJump=p.rocketRelease=true;
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
        private static void GrappleDuplicateGeometry()
        {
            foreach(bool timely in new[]{true,false})
            {
                long singleSamples=0,singleReads=0;
                foreach(int copies in new[]{1,4})
                {
                    Select(SmartFallFamily.Grapple);var p=PlayerAt(timely?240:100);
                    for(int x=35;x<65;x++)for(int y=40;y<Floor;y++)Main.tile[x,y]=new Tile();
                    p.inventory[17].SetDefaults(1800);Item chosen=p.inventory[17];
                    if(copies>1)foreach(int slot in new[]{0,18,19})p.inventory[slot].SetDefaults(1800);
                    var geometry=Get(host,"grapple");long searches=(long)Get(geometry,"Searches"),samples=(long)Get(geometry,"Samples"),reads=(long)Get(host,"TileReads");
                    long started=Stopwatch.GetTimestamp();Begin(p);End(p);double milliseconds=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;
                    searches=(long)Get(geometry,"Searches")-searches;samples=(long)Get(geometry,"Samples")-samples;reads=(long)Get(host,"TileReads")-reads;
                    var plan=Get(host,"grapplePlan");
                    Require((int)Get(host,"hookCount")==copies && searches==1 && samples>0,"All real same-flight endpoints retain preparation but execute exactly one finite geometric search, including failure copies="+copies+" timely="+timely);
                    Require((bool)Get(plan,"Found")==timely,"Shared search preserves the original timely/untimely flight result.");
                    if(timely)Require(ReferenceEquals(Get(plan,"Item"),chosen) && !ReferenceEquals(p.inventory[0],chosen),"Shared geometry rebinds to the actual lower-interference endpoint17 even when endpoint0 was searched first.");
                    if(copies==1){singleSamples=samples;singleReads=reads;}
                    else Require(samples==singleSamples && reads==singleReads,"Adding three equivalent real hook objects does not multiply finite ray substeps or terrain reads, for both timely and failed search.");
                    Require(!(bool)Get(host,"HasPending"),"This first-plan workload observation does not pretend to be an already consumed native rescue.");
                    Console.WriteLine("SMART-FALL DUPLICATE-GEOMETRY timely="+timely+" real-endpoints="+copies+" finite-searches="+searches+" ray-substeps="+samples+" tile-reads="+reads+" first-plan-with-fixture-ms="+milliseconds+" full-rescue=separate-native-case no-FPS");
                }
            }
        }
        private static void GrappleRescue(int itemType,float gap,bool borrowed,bool busyPassive=false,bool duplicateSources=false,bool critical=false)
        {
            Select(SmartFallFamily.Grapple);var p=PlayerAt(gap);int health=p.statLife;
            if(critical)p.fallStart=Floor-29;
            for(int x=35;x<65;x++)for(int y=40;y<Floor;y++)Main.tile[x,y]=new Tile();
            for(int x=35;x<65;x++){var t=Main.tile[x,Floor];t.active(true);t.type=19;t.frameY=0;t.slope(0);t.halfBrick(false);}
            for(int i=0;i<Main.projectile.Length;i++){if(Main.projectile[i]==null)Main.projectile[i]=new Projectile();Main.projectile[i].active=false;}
            p.miscEquips[4].SetDefaults(borrowed?84:itemType);Item originalHook=p.miscEquips[4],selected=p.miscEquips[4];
            if(borrowed){p.inventory[17].SetDefaults(itemType);selected=p.inventory[17];}
            Item duplicate=null;
            if(duplicateSources){p.inventory[0].SetDefaults(itemType);p.inventory[18].SetDefaults(itemType);duplicate=p.inventory[0];}
            if(busyPassive)
            {Call(movement,"SetSmartFallPermission",(int)SmartFallFamily.Horseshoe,true);p.inventory[18].SetDefaults(158);p.inventory[0].SetDefaults(3507);p.itemAnimation=p.itemTime=30;}
            int birth=-1,attached=-1,protectedAt=-1,plannedDelay=-1;Projectile owned=null;long plansAtProtection=0,scansAtProtection=0;bool through=false;
            for(int tick=0;tick<64;tick++)
            {
                Begin(p);
                try
                {
                    if(busyPassive && tick==0)
                    {
                        var prepared=(Array)Get(host,"preparations");
                        Require(!(bool)Get(prepared.GetValue((int)SmartFallFamily.Horseshoe),"AvailableNow") && p.UsingOrReusingItem,"Real busy passive endpoint is not a ready plan while the worn native hook remains usable.");
                        Require(Choice().Family==SmartFallFamily.Grapple,"This busy competition starts with an actually timely ready native hook, not merely an existing item.");
                    }
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
                    if(birth<0){birth=tick;owned=q;plannedDelay=(int)Get(Get(host,"grapplePlan"),"Delay");Require(q.type==selected.shoot,"Actual native QuickGrapple uses the selected real hook, including temporary faster source item="+itemType);
                        if(critical)Require((int)(p.position.Y/16)-p.fallStart<=25 && plannedDelay>3,"Finite contact risk admits a genuinely slow native preparation before the current-distance gate becomes dangerous.");
                        if(duplicateSources)Require(ReferenceEquals(Get(host,"selected"),selected) && ReferenceEquals(p.miscEquips[4],selected) && ReferenceEquals(p.inventory[0],duplicate),"Native birth uses the lower-interference endpoint17, not the first equivalent geometry representative at selected endpoint0.");}
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
            if(duplicateSources)Require(ReferenceEquals(p.inventory[0],duplicate) && !ReferenceEquals(selected,duplicate),"Different same-flight objects keep their real endpoint identity after protection and return.");
            p.controlJump=true;p.releaseJump=true;p.GrappleMovement();Require(!owned.active,"Real manual Jump immediately releases the handed-off native hook.");
            Console.WriteLine("SMART-FALL GRAPPLE item="+itemType+" born="+birth+" attached-projectile-phase="+attached+" protected-player-phase="+protectedAt+" platform-through="+through+" manual-release=true life="+p.statLife+" new-plans-after-success=0");
            Call(host,"ClearSession");
        }
        private static void GrappleFailedReturnCache()
        {
            Select(SmartFallFamily.Grapple);var p=PlayerAt(240);p.inventory[17].SetDefaults(1800);
            for(int x=35;x<65;x++)for(int y=40;y<Floor;y++)Main.tile[x,y]=new Tile();
            for(int i=0;i<Main.projectile.Length;i++){if(Main.projectile[i]==null)Main.projectile[i]=new Projectile();Main.projectile[i].active=false;}
            // The already flying hook points into empty upper space. Its
            // original range/return AI can retire naturally; pointing down
            // would legitimately latch on this representative's real floor.
            var q=Main.projectile[0];q.SetDefaults(13);q.active=true;q.owner=p.whoAmI;q.ai[0]=0;q.position=p.Center;q.velocity=-Vector2.UnitY;
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
            // Advance only this already known original hook. Its own ai0
            // range/ai1 return kills it; no auxiliary Kill or pool scan frees
            // capacity and no unrelated inventory revision is supplied.
            for(int i=0;i<384 && q.active;i++)q.Update(0);
            Require(!q.active,"Known original flying hook naturally retires and releases the actual native capacity.");
            int protectedAt=-1;
            for(int tick=0;tick<24;tick++)
            {
                Begin(p);try{Damage(p);p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);p.GrappleMovement();p.DryCollision(p.GoingDownWithGrapple,p.GoingDownWithGrapple);p.SlopingCollision(p.GoingDownWithGrapple,p.GoingDownWithGrapple);Housekeeping(p);}
                finally{End(p);}
                foreach(var projectile in Main.projectile)if(projectile!=null && projectile.active && projectile.owner==p.whoAmI && projectile.aiStyle==7)projectile.Update(projectile.whoAmI);
                if((SmartFallResult)Get(host,"Result")==SmartFallResult.Protected){protectedAt=tick;break;}
            }
            Require(protectedAt>=0 && p.statLife==5000 && (long)Get(host,"NativeActions")==actions+1 && !(bool)Get(host,"HasPending"),"Only the real known-hook capacity release reopens one timely original rescue and finite return.");
            Require(ReferenceEquals(p.miscEquips[4],anchor) && ReferenceEquals(p.inventory[17],source),"Reopened original action also returns the exact two borrowed references.");
            Console.WriteLine("SMART-FALL CAPACITY unchanged=64 natural-retirement=true native-retry=1 protected="+protectedAt+" life="+p.statLife);
            }
            finally{q.active=false;}
        }
        private static void ClearHooks()
        { for(int i=0;i<Main.projectile.Length;i++){if(Main.projectile[i]==null)Main.projectile[i]=new Projectile();Main.projectile[i].active=false;} }
        private static void GrappleStep(Player p)
        {
            Begin(p);try{Damage(p);p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);p.GrappleMovement();NativeCollision(p);Housekeeping(p);}finally{End(p);}
            foreach(var q in Main.projectile)if(q!=null && q.active && q.owner==p.whoAmI && q.aiStyle==7)q.Update(q.whoAmI);
        }
        private static void GrappleNewUpperAnchor()
        {
            Select(SmartFallFamily.Grapple);var p=PlayerAt(100);p.miscEquips[4].SetDefaults(84);ClearHooks();
            for(int x=35;x<65;x++)for(int y=40;y<Floor;y++)Main.tile[x,y]=new Tile();
            var geometry=Get(host,"grapple");Begin(p);End(p);
            Require(!(bool)Get(Get(host,"grapplePlan"),"Found"),"Ordinary hook has no timely original floor anchor before local edit.");
            long searches=(long)Get(geometry,"Searches");Vector2 start=p.position;
            GrappleStep(p);
            var unrelated=Main.tile[60,60];Main.tile[60,60]=new Tile();Main.tile[60,60].active(true);Main.tile[60,60].type=1;
            try
            {
                GrappleStep(p);Require((long)Get(geometry,"Searches")==searches,"Unchanged dependencies and unrelated tile edit do not repeat a failed Find.");
                int x=(int)(p.Center.X/16)+2,y=(int)(start.Y/16)-1;
                // This is an empty side/upper sample read in the actual failed
                // search, outside its foot corridor. Keep displacement <128.
                var anchor=Main.tile[x,y];Main.tile[x,y]=new Tile();Main.tile[x,y].active(true);Main.tile[x,y].type=1;
                try
                {
                    bool protectedNative=false;
                    for(int i=0;i<14;i++){GrappleStep(p);if((SmartFallResult)Get(host,"Result")==SmartFallResult.Protected){protectedNative=true;break;}}
                    Require((long)Get(geometry,"Searches")==searches+1 && protectedNative && p.statLife==5000 && Vector2.Distance(start,p.position)<128,"Previously observed upper tile changes wake exactly one Find and its real native GrappleMovement rescue.");
                    Console.WriteLine("SMART-FALL NEW-UPPER-ANCHOR initial-failed=true stable-updates=2 unrelated-wake=0 relevant-searches=1 native-protection=true displacement="+Vector2.Distance(start,p.position));
                }
                finally{Main.tile[x,y]=anchor;}
            }
            finally{Main.tile[60,60]=unrelated;ClearHooks();}
        }
        private static void GrappleOwnFailure()
        {
            Select(SmartFallFamily.Grapple);var p=PlayerAt(240);p.miscEquips[4].SetDefaults(1800);ClearHooks();
            for(int x=35;x<65;x++)for(int y=40;y<Floor;y++)Main.tile[x,y]=new Tile();
            Projectile own=null;
            for(int i=0;i<24 && own==null;i++)
            {
                Begin(p);try{p.UpdateEquips(p.whoAmI);Motion(p);Call(host,"BeforeGrapple",p);NativeCollision(p);Housekeeping(p);}finally{End(p);}
                own=Main.projectile.FirstOrDefault(q=>q!=null && q.active && q.owner==p.whoAmI && q.aiStyle==7);
            }
            Require(own!=null && (int)Get(host,"blockingCount")==0,"First real own shot starts with no previous capacity hook.");
            ProjectileKey id=own.key;var floor=new Tile[30];for(int x=35;x<65;x++){floor[x-35]=Main.tile[x,Floor];Main.tile[x,Floor]=new Tile();}
            try
            {
                // Native flight sees the vanished target and returns naturally.
                // Stable snapshots intentionally retain one exact Player state;
                // no auxiliary code writes projectile phase/identity/results.
                for(int i=0;i<160 && own.active && own.ai[0]!=1;i++)own.Update(own.whoAmI);
                Require(own.active && own.ai[0]==1,"Own exact native hook really enters its return phase after target loss.");
                Begin(p);End(p);Require((SmartFallResult)Get(host,"Result")==SmartFallResult.Failed,"Returning own hook ends the action rather than claiming protection.");
                var geometry=Get(host,"grapple");long searches=(long)Get(geometry,"Searches"),actions=(long)Get(host,"NativeActions");
                for(int i=0;i<16;i++){Begin(p);End(p);}
                Require((long)Get(geometry,"Searches")==searches && (long)Get(host,"NativeActions")==actions,"Unchanged failed own hook does not repeatedly search or launch.");
                for(int i=0;i<384 && own.active;i++)own.Update(own.whoAmI);
                Require(!own.active,"Exact own hook naturally retires without scanning or clearing live game hooks.");
                for(int x=35;x<65;x++)Main.tile[x,Floor]=floor[x-35];
                bool rescued=false;
                for(int i=0;i<16;i++){GrappleStep(p);if((SmartFallResult)Get(host,"Result")==SmartFallResult.Protected){rescued=true;break;}}
                Require(rescued && p.statLife==5000 && (long)Get(host,"NativeActions")==actions+1,"Own-hook retirement reopens the rejected family before the endpoint early exit and permits one real later rescue.");
                Console.WriteLine("SMART-FALL OWN-FAILURE identity="+id+" no-old-hooks=true native-return=true unchanged=16 retired=true second-native-rescue=true");
            }
            finally{for(int x=35;x<65;x++)Main.tile[x,Floor]=floor[x-35];ClearHooks();}
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
