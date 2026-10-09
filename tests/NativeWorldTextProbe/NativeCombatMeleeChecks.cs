using System;
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
    internal static class NativeCombatMeleeChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            LineOracle();
            Starlight(context,combat,host,attack,input);
            Flail(context,combat,host,attack,input);
            foreach(var row in new[]{new[]{277,780,0},new[]{4911,800,0},new[]{284,1100,0},new[]{284,870,0},new[]{3473,900,0},new[]{277,780,1},new[]{277,780,2}})
            {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(700,646);p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;p.direction=1;
            // Run the real normal base-effects phase. Without it this fixture
            // has whipRange=0/maxTags=0 and native first tag indexes -1.
            p.ResetEffects();Main.screenPosition=new Vector2(600,500);Console.WriteLine("MELEE row start ownerCD="+p.meleeNPCHitCooldown[2]);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(row[1],646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            if(row[2]>0){for(int ty=20;ty<=42;ty++){Main.tile[46,ty].active(true);Main.tile[46,ty].type=Terraria.ID.TileID.Stone;}n.noTileCollide=row[2]==2;}
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0);
            bool distant=row[0]==284 && row[1]==1100 || row[2]==1;var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");Console.WriteLine("MELEE premise: weapon="+row[0]+" shoot="+p.HeldItem.shoot+" speed="+p.HeldItem.shootSpeed+" animation="+p.itemAnimation+" whipRange="+p.whipRangeMultiplier+" failed="+Get(attack,"Failed"));Require(distant?contact==null:contact!=null,"native shape/animation respects outbound reach and owner wall: weapon="+row[0]+" wall="+row[2]);
            if(row[0]==3473)Require(contact.Confidence==AttackConfidence.Representative,"random solar arc remains a representative result");
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(s=>s.active && s.owner==p.whoAmI);int first=0;
            var audit=new Harmony("JueMingR.Tests.MeleeDamageFault");var methods=typeof(Projectile).GetMethods(Flags).Where(m=>m.Name.StartsWith("Damage_",StringComparison.Ordinal)).ToArray();
            foreach(var method in methods)audit.Patch(method,finalizer:new HarmonyMethod(typeof(NativeCombatMeleeChecks).GetMethod(nameof(Fault),BindingFlags.Static|BindingFlags.NonPublic)));
            try{for(int tick=1;tick<=(distant?60:row[0]==3473?29:contact.Tick) && q.active;tick++){if(tick>1)p.itemAnimation--;q.Update(q.whoAmI);if(n.life<10000){first=tick;break;}}}
            finally{foreach(var method in methods)audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
            Require(distant?first==0:row[0]==3473?first>0:first==contact.Tick,"first natural Damage agrees with declared pose/representative model: first="+first+" expected="+contact?.Tick+" weapon="+row[0]);
            Console.WriteLine("PASS native melee prepared shape/first Damage: weapon="+row[0]+" type="+q.type+" first="+first+" expected="+contact?.Tick);
            }
        }
        private static void LineOracle()
        {
            int comparisons=0;
            foreach(float angle in new[]{0f,.2f,-.7f,1.57f})foreach(float length in new[]{5f,100f,700f})foreach(float offset in new[]{-22f,-8f,0f,7.9f,8f,16f,35f})
            {
                var origin=new Vector2(100,100);var end=origin+angle.ToRotationVector2()*length;var box=new Vector2(120,100+offset);var dimensions=new Vector2(18,12);float collision=0;
                bool expected=Collision.CheckAABBvLineCollision(box,dimensions,origin,end,16,ref collision);var key=new NpcIdentity(1,new object(),2,1,3,3);var point=new NpcTrajectoryPoint(0,new NpcMotionState{Identity=key,X=box.X,Y=box.Y,Width=18,Height=12,CanReceive=true});var timeline=new NpcTrajectory(key,0,1,PredictionAssumption.None,PredictionStop.None,new[]{point},1);
                var contact=AttackIntercept.LineContact(timeline,0,900,900,origin.X,origin.Y,end.X,end.Y,16);Require((contact!=null)==expected,"finite strip matches original pure geometry at rectangle edge: "+angle+"/"+length+"/"+offset);comparisons++;
            }
            Console.WriteLine("PASS original pure finite-strip geometry: comparisons="+comparisons);
        }
        private static void Starlight(object context,object combat,object host,object attack,object input)
        {
            foreach(bool wall in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,4923,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.itemAnimationMax=p.HeldItem.useAnimation;Main.screenPosition=new Vector2(600,500);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(790,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
                if(wall)for(int y=20;y<=42;y++){Main.tile[46,y].active(true);Main.tile[46,y].type=Terraria.ID.TileID.Stone;}
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(s=>s.active && s.type==927);NativeCombatAimChecks.Prepare(host,attack,n,0);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");
                Require(wall?plan==null:plan!=null,"starlight contact uses its native short shape and owner wall qualification");
                int first=-1;for(int k=0;k<12;k++){q.Update(q.whoAmI);if(n.life<10000){first=k;break;}}
                Require(wall?first<0:first==plan.Tick,"starlight first natural Damage matches its declared contact: actual="+first+" expected="+plan?.Tick);
                p.channel=false;q.AI();Require(!q.active,"starlight release retains original channel exit");Console.WriteLine("PASS native starlight contact/wall/release: first="+first+" expected="+plan?.Tick+" wall="+wall);
            }
        }
        private static void Flail(object context,object combat,object host,object attack,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,162,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;Main.screenPosition=new Vector2(600,500);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(850,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0);
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(s=>s.active && s.aiStyle==15);q.Update(q.whoAmI);Require(n.life==10000,"initial flail spin cannot damage before original localAI warmup");
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");p.channel=p.controlUseItem=false;NativeCombatAimChecks.Prepare(host,attack,n,0);int x=Main.mouseX,y=Main.mouseY;var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");
            q.Update(q.whoAmI);Require(q.ai[0]==1 && q.velocity.X>0 && Main.mouseX==x && Main.mouseY==y,"flail's real release AI borrows prepared direction then returns physical coordinates");
            Require(plan!=null,"flail release has a finite native outbound contact");int first=n.life<10000?0:-1;for(int k=1;k<20 && first<0;k++){q.Update(q.whoAmI);if(n.life<10000)first=k;}
            Require(first==plan.Tick,"flail first native outbound Damage matches timeline: actual="+first+" expected="+plan.Tick);Console.WriteLine("PASS native flail warmup/release/contact: first="+first+" expected="+plan.Tick);
            foreach(var shot in Main.projectile)shot.active=false;p.channel=p.controlUseItem=true;n.position.X=755;n.life=10000;Array.Clear(n.immune,0,n.immune.Length);NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0);
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});q=Main.projectile.Single(s=>s.active && s.aiStyle==15);NativeCombatAimChecks.Prepare(host,attack,n,0);plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Console.WriteLine("FLAIL spin premise: plan="+plan?.Tick+" local="+q.localAI[1]+" state="+q.ai[0]+" channel="+p.channel+" mounted="+p.MountedCenter+" target="+n.Hitbox+" owner="+n.immune[0]+" localimmune="+q.localNPCImmunity[2]);Require(plan!=null && plan.Tick>=12,"spin contact respects original thirteen-step warmup");first=-1;
            for(int k=0;k<20 && first<0;k++){q.Update(q.whoAmI);if(n.life<10000)first=k;}
            Require(first==plan.Tick,"flail spin ellipse and actual warmup agree: first="+first+" expected="+plan.Tick);Console.WriteLine("PASS native flail warmup/ellipse contact: first="+first+" expected="+plan.Tick);
        }
        private static Exception Fault(Exception __exception,MethodBase __originalMethod)
        {if(__exception!=null)Console.WriteLine("MELEE original fault: "+__originalMethod.Name+" "+__exception);return __exception;}
    }
}
