using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Owner's 2026-10-17 oracle: production finds the point before a natural
    // ItemCheck/held-AI consumer; vanilla movement and Damage decide quality.
    // No supplied angle, Contact, timer reset or random-outcome override.
    internal static class NativeCombatBalancedAimChecks
    {
        private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static object attack;
        private static Player player;
        private static NPC receiver;
        private static Projectile damageSource;
        private static Vector2 physical;
        private static int item,frame,births,enhanced,basic,hits,firstHit,primaryHits,primaryEnhanced,handStarts,oldHeavyCalls,primaryType,sideChecks;
        private static readonly Dictionary<int,int> hitTypes=new Dictionary<int,int>();
        private static readonly List<string> facts=new List<string>();
        private static void Heavy(){oldHeavyCalls++;}
        private static void ArrowLane(int __6){if(item==2624 && __6!=2)sideChecks++;}
        private static void Hand(Player __instance,Item __0){if(ReferenceEquals(__instance,player) && __0.type==item)handStarts++;}
        private static void Born(int __result)
        {
            if(player==null || __result<0 || __result>=Main.maxProjectiles)return;
            var shot=Main.projectile[__result];if(shot.owner!=player.whoAmI)return;
            births++;
            bool current=(bool)Get(attack,"hasConsumedInput") && (uint)Get(attack,"consumedInputStep")==Main.GameUpdateCount;
            var packet=Get(attack,"consumedInput");string kind=current?Get(packet,"Kind").ToString():"Unbound";
            if(kind=="Enhanced")enhanced++;else if(kind=="Basic")basic++;
            if(kind=="Enhanced" && shot.type!=615 && shot.type!=630 && shot.type!=705 && (item!=3475 || shot.type==14))primaryEnhanced++;
            // A held parent's emission can legitimately follow its earlier AI
            // read. Record it rather than inventing a same-tick hand receipt.
            var point=current?(Vector2)Get(packet,"Point"):Vector2.Zero;
            facts.Add("birth frame="+frame+" guc="+Main.GameUpdateCount+" type="+shot.type+" key="+shot.key+" kind="+kind+" point="+point+" pos="+shot.Center+" velocity="+shot.velocity);
            if(current)Require(Vector2.DistanceSquared(point,physical)>4,"balanced native consumer must differ from the opposite physical mouse");
        }
        private static void Damage(Projectile __instance,out Projectile __state){__state=damageSource;damageSource=__instance;}
        private static void DamageDone(Projectile __state){damageSource=__state;}
        private static void Hit(NPC __instance,int __result)
        {
            if(!ReferenceEquals(__instance,receiver) || __result<=0)return;
            hits++;if(firstHit<0)firstHit=frame;
            int type=damageSource==null?-1:damageSource.type;
            int count;hitTypes.TryGetValue(type,out count);hitTypes[type]=count+1;
            // Vortex's independently homing rocket cannot prove its normal
            // selected bullet. Other rows retain the full per-type account.
            // Swordfish's 1107..1109 are the three legal visual variants of
            // the primary emitted shard, not secondary damage channels.
            if(primaryType==0 || type==primaryType || primaryType==1107 && type>=1107 && type<=1109)primaryHits++;
            facts.Add("hit frame="+frame+" type="+type+" key="+(damageSource==null?"none":damageSource.key.ToString())+" damage="+__result);
        }
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");attack=Get(Get(context,"Combat"),"Attack");
            var audit=new Harmony("JueMingR.Tests.BalancedAim20261017");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,F),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethods(F).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13),postfix:new HarmonyMethod(typeof(NativeCombatBalancedAimChecks),nameof(Born)));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_StartActualUse",F),postfix:new HarmonyMethod(typeof(NativeCombatBalancedAimChecks),nameof(Hand)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",F),prefix:new HarmonyMethod(typeof(NativeCombatBalancedAimChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatBalancedAimChecks),nameof(DamageDone)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",F,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatBalancedAimChecks),nameof(Hit)));
            var assembly=attack.GetType().Assembly;
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNativeArrowBirthAttack",true).GetMethod("Birth",F),prefix:new HarmonyMethod(typeof(NativeCombatBalancedAimChecks),nameof(ArrowLane)));
            foreach(var pair in new[]{new[]{"HostHeldAmmoFamilyAttack","KnownFamily"},new[]{"HostHeldAmmoFamilyAttack","IsolatedFamily"},new[]{"HostHeldAmmoFamilyAttack","CaptureForSolve"},new[]{"HostHeldAmmoFamilyAttack","ExplicitBirthBatch"}})
            {var method=assembly.GetType("JueMingR.TerrariaHost.Combat."+pair[0],true).GetMethod(pair[1],F);if(method!=null)audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatBalancedAimChecks),nameof(Heavy)));}
            try
            {
                if(Environment.GetEnvironmentVariable("JMR_BALANCED_SUITE")=="modes"){Modes(context);return;}
                if(Environment.GetEnvironmentVariable("JMR_BALANCED_SUITE")=="motion"){NativeCombatBallisticMotionChecks.Run(context);return;}
                if(Environment.GetEnvironmentVariable("JMR_BALANCED_SUITE")=="phase"){NativeCombatBalancedPhaseChecks.Run(context);return;}
                if(Environment.GetEnvironmentVariable("JMR_BALANCED_SUITE")=="safety")
                {
                    NativeCombatPlacementChecks.Run(context);NativeCombatMagicStringAimChecks.Run(context);
                    // MagicString's last scene deliberately leaves that accessory
                    // equipped. The following oracle explicitly starts ORDINARY
                    // yoyo; its fixture reset does not clear armor/effects.
                    foreach(var armor in Main.LocalPlayer.armor)armor.TurnToAir();Main.LocalPlayer.ResetEffects();
                    Require(!Main.LocalPlayer.magicString,"ordinary yoyo oracle starts without the prior scene's accessory");
                    NativeCombatYoyoChecks.Run(context);NativeCombatYoyoFamilyChecks.Run(context);return;
                }
                string selection=Environment.GetEnvironmentVariable("JMR_BALANCED_CASES")??"1229:40:700:0:0,1229:40:650:-90:1,1229:40:700:-90:0:1,1229:40:700:-160:0:2,2624:40:600:0:0,2624:40:600:0:0:3,3029:40:600:0:0,3029:40:600:0:0:1,4953:40:600:0:0,3475:97:650:0:0,3540:40:650:0:0,3854:40:550:0:0,3859:40:550:0:0,3019:40:550:0:0,1947:40:500:0:0,2331:40:450:0:0,2331:40:250:0:0,1947:40:70:0:0,2331:40:60:0:0";
                var failures=new List<string>();
                foreach(string spec in selection.Split(','))
                {
                    int[] fields=spec.Split(':').Select(int.Parse).ToArray();
                    try{Scene(context,fields[0],fields[1],fields[2],fields[3],fields[4]!=0,fields.Length>5?fields[5]:0);}
                    catch(Exception error){failures.Add(spec+": "+error.Message);Console.WriteLine("BALANCED FAIL case="+spec+" "+error);}
                }
                Require(failures.Count==0,string.Join(" | ",failures));
            }
            finally
            {
                player=null;receiver=null;damageSource=null;audit.UnpatchAll(audit.Id);
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
                Main.ProjectileUpdateLoopIndex=-1;
            }
        }
        private static void Modes(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");
            foreach(bool mouse in new[]{false,true})foreach(bool clear in new[]{false,true})foreach(string mode in new[]{"path","aim","both","off"})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),98,0,0);
                player.position=new Vector2(700,646);player.inventory[54].SetDefaults(97);player.inventory[54].stack=999;
                foreach(var target in Main.npc)target.active=false;
                // Legal native static targets carry their placed 2x3 tiles
                // and unmodified AI92, rather than inventing zombie aiStyle=-1.
                NativeCombatControlPreviewChecks.Dummy(2,52,40);NativeCombatControlPreviewChecks.Dummy(3,30,40);
                for(int y=3;y<Main.maxTilesY-3;y++)NativeToolsChecks.Tile(49,y,1);
                Main.screenPosition=new Vector2(480,400);physical=new Vector2(800,667);oldHeavyCalls=0;
                bool path=mode=="path" || mode=="both",aim=mode=="aim" || mode=="both";
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,path,clear,mouse,true,50,true,aim));
                int expected=mode=="off"?-1:clear?3:2;int displayed=0;
                for(int i=0;i<24;i++){Step(context,aim && i>=3);if(GetOptional(host,"DisplayPath")!=null)displayed++;}
                var selection=Get(host,"Selection");
                int actual=(bool)Get(selection,"HasTarget")?((NpcIdentity)Get(selection,"Target")).Slot:-1;
                Console.WriteLine("BALANCED modes mode="+mode+" mouse="+mouse+" clear="+clear+" selected="+actual+" expected="+expected+" displays="+displayed+" oldHeavy="+oldHeavyCalls);
                Require(actual==expected,"distance and practical clear-line selection must match the selected center and mode");
                Require(oldHeavyCalls==0,"Path-only/selection must not reopen full ammo replay");
                if(path)Require(displayed>0,"valid selected NPC path remains publishable independently from attack");
                for(int y=3;y<Main.maxTilesY-3;y++)Main.tile[49,y].active(false);
                for(int i=0;i<24;i++)Step(context,aim);
                if(mode!="off")Require(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot==2,"opening the near path must naturally reselect the nearer receiver");
            }
        }
        private static void Scene(object context,int weapon,int ammo,int distance,int height,bool moving,int modifier)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);
            player.position=new Vector2(700,646);player.ResetEffects();player.ResetFallDamage();player.releaseUseItem=true;
            player.inventory[54].SetDefaults(ammo);player.inventory[54].stack=999;player.statMana=player.statManaMax=player.statManaMax2=200;
            if(modifier==1){player.AddBuff(Terraria.ID.BuffID.Gravitation,600);player.gravDir=-1;}
            if(modifier==2){player.armor[3].SetDefaults(Terraria.ID.ItemID.MagicQuiver);player.AddBuff(Terraria.ID.BuffID.Archery,600);}
            // Clear only this already-isolated fixture. Rebuild the ordinary
            // floor, with an airborne static/moving receive body above it.
            for(int x=3;x<Main.maxTilesX-3;x++)for(int y=3;y<Main.maxTilesY-3;y++)Main.tile[x,y].ClearEverything();
            for(int x=3;x<Main.maxTilesX-3;x++)NativeToolsChecks.Tile(x,43,1);
            if(modifier==3)NativeToolsChecks.Tile(63,40,1);
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;
            receiver=Main.npc[2];receiver.SetDefaults(3);receiver.whoAmI=2;receiver.active=true;receiver.width=receiver.height=34;
            receiver.Center=player.Center+new Vector2(distance,height);receiver.aiStyle=-1;receiver.noGravity=receiver.noTileCollide=true;receiver.velocity=moving?new Vector2(-1,-.15f):Vector2.Zero;
            receiver.life=receiver.lifeMax=100000;receiver.knockBackResist=0;receiver.dontTakeDamage=receiver.friendly=receiver.immortal=false;Array.Clear(receiver.immune,0,receiver.immune.Length);
            Main.screenPosition=new Vector2(480.25f,400.75f);Main.GameViewMatrix.Zoom=Vector2.One;Main.rand=new Terraria.Utilities.UnifiedRandom(7193);Main.windPhysics=false;
            physical=player.Center+new Vector2(-240,-100);item=weapon;births=enhanced=basic=hits=primaryHits=primaryEnhanced=handStarts=oldHeavyCalls=sideChecks=0;firstHit=-1;facts.Clear();hitTypes.Clear();
            primaryType=item==3475?14:item==3540?1:item==1947?(distance>100?343:342):item==2331?(distance>100?1107:367):0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,50,true,true));
            for(frame=0;frame<210;frame++)Step(context,frame>=6 && frame<170 && (item!=1947 && item!=2331 || (frame-6)%40<30));
            foreach(string fact in facts)Console.WriteLine("BALANCED item="+item+" "+fact);
            Console.WriteLine("BALANCED summary item="+item+" ammo="+ammo+" dx="+distance+" dy="+height+" moving="+moving+" handStarts="+handStarts+" births="+births+" enhanced="+enhanced+" primaryEnhanced="+primaryEnhanced+" basic="+basic+" hits="+hits+" primaryHits="+primaryHits+" firstHit="+firstHit+" oldHeavy="+oldHeavyCalls+" types="+string.Join(";",hitTypes.Select(p=>p.Key+":"+p.Value)));
            Require(handStarts>0 && births>0,"balanced scenario must really attack through original ItemCheck");
            Require(primaryEnhanced>0,"production public preparation must provide and actually consume a main-channel future point");
            Require(primaryHits>0,"ordinary original primary motion must effectively receive the selected target");
            Require(oldHeavyCalls==0,"migrated primary mechanism must not fall back to the old full ammo world");
            if(modifier==3)Require(sideChecks>0,"blocked central Tsunami channel must receive a bounded side-lane check");
            Require(!(bool)Get(attack,"hasConsumedInput") || (uint)Get(attack,"consumedInputStep")<Main.GameUpdateCount,"released native input must have no current consumption receipt");
        }
        private static void Step(object context,bool left)
        {StepAt(context,physical,left);}
        internal static void StepAt(object context,Vector2 cursor,bool left)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),cursor,left);Call(combat,"Sample");Call(host,"SampleMouse");int x=Main.mouseX,y=Main.mouseY;
            NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);
            foreach(var target in Main.npc)if(target.active)target.UpdateNPC(target.whoAmI);
            // This original phase owns the installed BeforeProjectileWorld
            // Hook. A manual slot loop would silently omit held preparation.
            if(Main.instance.SpelunkerProjectileHelper==null)Main.instance.SpelunkerProjectileHelper=new Terraria.GameContent.SpelunkerProjectileHelper();
            if(Main.instance.ChumBucketProjectileHelper==null)Main.instance.ChumBucketProjectileHelper=new Terraria.GameContent.ChumBucketProjectileHelper();
            typeof(Main).GetMethod("UpdateWorld_Projectiles",F).Invoke(Main.instance,null);
            Call(context,"UpdateRuntime");Call(context,"UpdateShell");
            Require(Main.mouseX==x && Main.mouseY==y,"balanced complete original component phases restore physical input");
        }
    }
}
