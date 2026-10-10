using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBoomerangCommonChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static readonly int[] types={6,19,33,52,106,113,272,320,333,902,867,1000,1052};
        private static Player Reset(object context,int type)
        {
            int weapon=ContentSamples.ItemsByType.Values.Where(i=>i.shoot==type && i.damage>0 && i.useTime>0 && i.useStyle>0).OrderBy(i=>i.type).First().type;
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);p.ResetEffects();Main.windPhysics=false;return p;
        }
        private static NPC Target(int slot,Vector2 center)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=32;n.height=40;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);return n;}
        private static NpcTrajectory Timeline(Assembly a,NPC n)
        {var key=(NpcIdentity)a.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});var s=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,s)).ToArray();return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static object Terrain(Assembly a)
        {var t=Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static Projectile Copy(Projectile p)
        {var q=(Projectile)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(p,null);q.ai=(float[])p.ai.Clone();q.localAI=(float[])p.localAI.Clone();q.localNPCImmunity=(int[])p.localNPCImmunity.Clone();return q;}
        internal static void Run(object context)
        {
            var a=Get(context,"Combat").GetType().Assembly;var model=a.GetType("JueMingR.TerrariaHost.Combat.HostBoomerangAttack",true);bool wind=Main.windPhysics;
            var audit=new Harmony("JueMingR.Tests.BoomerangCommon");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            try{Receives(context,a,model);WorldEdges(context,a,model);Contacts(context,a);Trees(context,a,model);}
            finally{audit.UnpatchAll(audit.Id);Main.windPhysics=wind;}
        }
        private static void Receives(object context,Assembly a,Type model)
        {
            int cases=0,actions=0;
            foreach(int type in types)foreach(bool blocked in new[]{false,true})foreach(bool returning in new[]{false,true})
            {
                var p=Reset(context,type);p.position=new Vector2(790,350);var target=Target(2,new Vector2(1250,350));var front=Target(1,new Vector2(920,350));if(blocked)front.immune[p.whoAmI]=200;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),front.Center,new Vector2(8,2),type,50,0,p.whoAmI);var q=Main.projectile[slot];if(returning)q.ai[0]=1;var shadow=Copy(q);var timeline=Timeline(a,target);var terrain=Terrain(a);
                var receive=Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,shadow,timeline,0,true,false},null);
                Call(receive,"BeginCandidate");Require((bool)Call(receive,"PassOuter",2,1,1,shadow.Center.X,shadow.Center.Y,0),"infinite common boomerang front receive leaves its real penetration budget: "+type+"/"+blocked+"/"+returning+" remaining="+Get(receive,"Remaining"));int hits=(int)Get(receive,"OtherHits");model.GetMethod("OnOtherHits",Flags).Invoke(null,new object[]{shadow,hits});q.Damage();
                Require((front.life<10000)==!blocked && hits==(blocked?0:1),"common OnHit is a real immunity-qualified receiver, not overlap: "+type+"/"+blocked);
                Require(q.ai[0]==shadow.ai[0] && q.velocity==shadow.velocity,"native common first OnHit reverses once and starts return;902 retains its separate vector: "+type+"/"+blocked+"/"+returning);
                if(!blocked && !returning)Require(q.ai[0]==1 && q.velocity==(type==902?new Vector2(8,2):new Vector2(-8,-2)),"outgoing actual first Damage consumes the native common return transition");
                front.active=target.active=false;q.damage=shadow.damage=0;bool alive=true;
                for(int outer=1;outer<=120 && q.active;outer++)
                {
                    int updates=shadow.extraUpdates+1;
                    for(int sub=0;sub<updates && alive;sub++){var args=new object[]{shadow,p,terrain,alive};Require((bool)model.GetMethod("Advance",Flags).Invoke(null,args),"post-receive native common return retains terrain facts");alive=(bool)args[3];actions++;}
                    q.Update(slot);Require(q.active==alive && (!alive || Vector2.DistanceSquared(q.Center,shadow.Center)<.002f && Vector2.DistanceSquared(q.velocity,shadow.velocity)<.0001f && q.ai[0]==shadow.ai[0] && q.ai[1]==shadow.ai[1]),"full native motor after real OnHit agrees: "+type+"/"+blocked+"/"+returning+"/"+outer);
                }
                cases++;
            }
            Console.WriteLine("COMMON BOOMERANG actual receives="+cases+" subsequentMotor="+actions);
        }
        private static void WorldEdges(object context,Assembly a,Type model)
        {
            int cases=0;
            foreach(int type in types)foreach(int edge in Enumerable.Range(0,4))foreach(bool returning in new[]{false,true})
            {
                var p=Reset(context,type);foreach(var n in Main.npc)n.active=false;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,8,2,type,0,0,p.whoAmI);var q=Main.projectile[slot];
                q.position=edge==0?new Vector2(Main.leftWorld-1,350):edge==1?new Vector2(Main.rightWorld-q.width+1,350):edge==2?new Vector2(950,Main.topWorld-1):new Vector2(950,Main.bottomWorld-q.height+1);
                p.Center=q.Center+(edge==0?new Vector2(180,0):edge==1?new Vector2(-180,0):edge==2?new Vector2(0,180):new Vector2(0,-180));q.velocity=edge==0?new Vector2(-8,2):edge==1?new Vector2(8,2):edge==2?new Vector2(2,-8):new Vector2(2,8);if(returning)q.ai[0]=1;
                var shadow=Copy(q);var terrain=Terrain(a);bool alive=true;
                for(int outer=1;outer<=4 && q.active;outer++)
                {int updates=shadow.extraUpdates+1;for(int sub=0;sub<updates && alive;sub++){var args=new object[]{shadow,p,terrain,alive};Require((bool)model.GetMethod("Advance",Flags).Invoke(null,args),"AI003 legal clipped edge retains native movement observations");alive=(bool)args[3];}q.Update(slot);Require(q.active==alive && q.ai[0]==shadow.ai[0] && q.ai[1]==shadow.ai[1] && Vector2.DistanceSquared(q.position,shadow.position)<.002f && Vector2.DistanceSquared(q.velocity,shadow.velocity)<.0001f,"AI003 world edge invokes its actual role collision while return skips tiles: "+type+"/"+edge+"/"+returning+"/"+outer+" native="+q.position+"/"+q.velocity+"/"+q.ai[0]+" model="+shadow.position+"/"+shadow.velocity+"/"+shadow.ai[0]);}
                cases++;
            }
            Console.WriteLine("COMMON BOOMERANG actual outbound/return world-edge cases="+cases);
        }
        private static void Contacts(object context,Assembly a)
        {
            int cases=0,positives=0,negatives=0;
            foreach(int type in types)foreach(bool blocked in new[]{false,true})
            {
                var p=Reset(context,type);var origin=p.RotatedRelativePoint(p.MountedCenter);var selected=Target(2,origin+new Vector2(190,0));var front=Target(1,origin+new Vector2(80,0));front.height=300;front.Center=origin+new Vector2(80,0);if(blocked)front.immune[p.whoAmI]=200;
                var timeline=Timeline(a,selected);var terrain=Terrain(a);var ammo=a.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true).GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});var policy=new Projectile();policy.SetDefaults(type);policy.owner=p.whoAmI;
                var receiver=Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,policy,timeline,0,true,false},null);
                var contact=(AttackContact)a.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack",true).GetMethod("Solve",Flags).Invoke(null,new object[]{p,p.HeldItem,ammo,timeline,0,true,terrain,0,receiver});
                Require((contact!=null)==blocked,"a real front hit starts return before the farther selected body; immune front remains a native positive control: "+type+"/"+blocked+" tick="+contact?.Tick);
                if(contact==null)
                {front.active=false;Call(terrain,"Reset");receiver=Activator.CreateInstance(receiver.GetType(),Flags,null,new object[]{p,policy,timeline,0,true,false},null);contact=(AttackContact)a.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack",true).GetMethod("Solve",Flags).Invoke(null,new object[]{p,p.HeldItem,ammo,timeline,0,true,terrain,0,receiver});front.active=true;Require(contact!=null,"negative common receive has an independently reachable selected control");}
                Vector2 velocity=(new Vector2(contact.AimX,contact.AimY)-origin).SafeNormalize(Vector2.UnitX)*(float)Get(ammo,"Speed");int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,velocity,type,50,0,p.whoAmI);var q=Main.projectile[slot];int first=0,other=0;
                var frontBox=front.Hitbox;var selectedBox=selected.Hitbox;
                // SetDefaults resets lifeRegen flags/counters but retains the
                // reused NPC's buff arrays. Prior33 poisons selected: a later
                //52 case must not count its sixth-update DOT as a52 hit.
                for(int outer=1;outer<=120 && q.active;outer++){NativeQuickItemChecks.BeginWorldStep();if(outer>1){NativeToolExecutionChecks.Sample(context,Get(context,"Input"),selected.Center,false);p.controlUseItem=false;p.Update(p.whoAmI);}front.UpdateNPC(1);selected.UpdateNPC(2);Require(front.Hitbox==frontBox && selected.Hitbox==selectedBox && front.active && front.aiStyle==-1,"actual frozen front/selected shape and active lifetime survive native NPC Update: "+type+"/"+blocked+"/"+outer+" front="+front.Hitbox+" selected="+selected.Hitbox);int life=front.life,selectedLife=selected.life;q.Update(slot);if(front.life<life)other++;if(selected.life<selectedLife){first=outer;break;}}
                Require(blocked?first==contact.Tick && other==0:first==0 && other>0,"actual native selected Damage or causal front-interception negative agrees: "+type+"/"+blocked+" first="+first+" other="+other);if(first>0)positives++;else negatives++;cases++;
            }
            Require(positives==types.Length && negatives==types.Length,"every common motor includes a useful damage control and real prior receive negative");Console.WriteLine("COMMON BOOMERANG selected cases="+cases+" useful="+positives+" intercepted="+negatives);
        }
        private static void Trees(object context,Assembly a,Type model)
        {
            int cases=0,cut=0,retained=0;
            foreach(int tree in new[]{5,72,583,584,585,586,587,588,589,596,616,634,323})foreach(bool returning in new[]{false,true})
            {
                var p=Reset(context,1052);Require(!Main.tileSolid[tree] && !Main.tileSolidTop[tree] && !Main.tileCut[tree],"native trunk is nonsolid and requires1052's outgoing explicit cut permission: "+tree);
                for(int x=55;x<65;x++){Main.tile[x,30].active(true);Main.tile[x,30].type=1;}var tile=Main.tile[60,25];tile.ClearEverything();tile.active(true);tile.type=(ushort)tree;
                var n=Target(2,new Vector2(968,408));int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),n.Center,new Vector2(8,0),1052,50,0,p.whoAmI);var q=Main.projectile[slot];if(returning)q.ai[0]=1;var shadow=Copy(q);
                q.Damage();Require(n.life<10000,"1052 real body Damage remains useful across a native tree trunk");Require(tile.active()==returning,"only original outgoing1052 CutTiles includes the full trunk set: "+tree+"/"+returning);model.GetMethod("OnOtherHits",Flags).Invoke(null,new object[]{shadow,1});Require(q.ai[0]==shadow.ai[0] && q.velocity==shadow.velocity,"tree-cutting1052 still uses the common native OnHit return motor");
                if(returning)retained++;else cut++;cases++;
            }
            Console.WriteLine("TREE BOOMERANG actual cut/Damage cases="+cases+" outboundCuts="+cut+" returnRetains="+retained);
        }
    }
}
