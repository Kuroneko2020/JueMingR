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
            NativeCombatPhaseChecks.Run(context);
            OwnerOffsetOracle(context,combat,host,input);
            Starlight(context,combat,host,attack,input);
            Flail(context,combat,host,attack,input);
            NativeCombatReturnChecks.Run(context);
            foreach(var row in new[]{new[]{277,780,0},new[]{4911,800,0},new[]{284,1100,0},new[]{284,870,0},new[]{3473,900,0},new[]{277,780,1},new[]{277,780,2}})
            {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(700,646);p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;p.direction=1;
            // Run the real normal base-effects phase. Without it this fixture
            // has whipRange=0/maxTags=0 and native first tag indexes -1.
            p.ResetEffects();Main.screenPosition=new Vector2(600,500);Console.WriteLine("MELEE row start ownerCD="+p.meleeNPCHitCooldown[2]);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(row[1],646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            if(row[2]>0){for(int ty=20;ty<=42;ty++){Main.tile[46,ty].active(true);Main.tile[46,ty].type=Terraria.ID.TileID.Stone;}n.noTileCollide=row[2]==2;}
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            bool distant=row[0]==284 && row[1]==1100 || row[2]==1;var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");Console.WriteLine("MELEE premise: weapon="+row[0]+" shoot="+p.HeldItem.shoot+" speed="+p.HeldItem.shootSpeed+" animation="+p.itemAnimation+" whipRange="+p.whipRangeMultiplier+" failed="+Get(attack,"Failed"));Require(distant?contact==null:contact!=null,"native shape/animation respects outbound reach and owner wall: weapon="+row[0]+" wall="+row[2]);
            if(row[0]==3473)Require(contact.Confidence==AttackConfidence.Representative,"random solar arc remains a representative result");
            // Whip's false flag is a real repeated-shot branch: it rotates
            // velocity randomly and changes damage/duration, not merely audio.
            // The ordinary deterministic collision model uses the normal shot.
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),row[0]==4911});var q=Main.projectile.Single(s=>s.active && s.owner==p.whoAmI);int first=0;
            var audit=new Harmony("JueMingR.Tests.MeleeDamageFault");var methods=typeof(Projectile).GetMethods(Flags).Where(m=>m.Name.StartsWith("Damage_",StringComparison.Ordinal)).ToArray();
            foreach(var method in methods)audit.Patch(method,finalizer:new HarmonyMethod(typeof(NativeCombatMeleeChecks).GetMethod(nameof(Fault),BindingFlags.Static|BindingFlags.NonPublic)));
            try{for(int tick=1;tick<=(distant?60:row[0]==3473?29:contact.Tick+3) && q.active;tick++){if(tick>1)p.itemAnimation--;q.Update(q.whoAmI);if(row[0]==4911 && tick>=contact.Tick-1)Console.WriteLine("WHIP difference: tick="+tick+" origin="+q.Center+" velocity="+q.velocity+" ai="+q.ai[0]+" owner="+n.immune[0]+" local="+q.localNPCImmunity[2]+" bounds="+n.Hitbox+" net="+n.netOffset+" life="+n.life+" animation="+p.itemAnimation+" max="+p.itemAnimationMax+" arm="+p.GetArmPosition());if(n.life<10000){first=tick;break;}}}
            finally{foreach(var method in methods)audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
            Require(distant?first==0:row[0]==3473?first>0:first==contact.Tick,"first natural Damage agrees with declared pose/representative model: first="+first+" expected="+contact?.Tick+" weapon="+row[0]);
            Console.WriteLine("PASS native melee prepared shape/first Damage: weapon="+row[0]+" type="+q.type+" first="+first+" expected="+contact?.Tick);
            }
        }
        private static void LineOracle()
        {
            foreach(int type in new[]{3,414})
            {
                var key=new NpcIdentity(1,new object(),2,1,type,type);var state=new NpcMotionState{Identity=key,X=170.9f,Y=100.4f,NetOffsetX=-10.6f,NetOffsetY=-.5f,Width=18,Height=12,CanReceive=true};var timeline=new NpcTrajectory(key,0,1,PredictionAssumption.None,PredictionStop.None,new[]{new NpcTrajectoryPoint(0,state)},1,PredictionStrategy.RollingConditional);
                var b=timeline[0].ProjectileReceiveBounds;var rectangle=new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height);var delta=rectangle.ClosestPointInRect(new Vector2(100,100))-new Vector2(100,100);delta.Y/=.8f;bool expected=delta.Length()<=55;
                Require(BitConverter.ToInt32(BitConverter.GetBytes(timeline[0].OwnerBounds.X),0)==BitConverter.ToInt32(BitConverter.GetBytes(state.X+state.NetOffsetX),0) && BitConverter.ToInt32(BitConverter.GetBytes(timeline[0].OwnerBounds.Y),0)==BitConverter.ToInt32(BitConverter.GetBytes(state.Y+state.NetOffsetY),0),"compact timeline retains floating offset body distinct from integer damage rectangle");
                Require((AttackIntercept.EllipseContact(timeline,0,900,900,100,100,55,.8f,.4f)!=null)==expected,"flail ellipse uses original integer offset/type414 projectile receive frame: "+type);
            }
            int comparisons=0;
            foreach(float angle in new[]{0f,.2f,-.7f,1.57f})foreach(float length in new[]{5f,100f,700f})foreach(float offset in new[]{-22f,-8f,0f,7.9f,8f,16f,35f})
            {
                var origin=new Vector2(100,100);var end=origin+angle.ToRotationVector2()*length;var box=new Vector2(120,100+offset);var dimensions=new Vector2(18,12);float collision=0;
                bool expected=Collision.CheckAABBvLineCollision(box,dimensions,origin,end,16,ref collision);var key=new NpcIdentity(1,new object(),2,1,3,3);var point=new NpcTrajectoryPoint(0,new NpcMotionState{Identity=key,X=box.X,Y=box.Y,Width=18,Height=12,CanReceive=true});var timeline=new NpcTrajectory(key,0,1,PredictionAssumption.None,PredictionStop.None,new[]{point},1);
                var contact=AttackIntercept.LineContact(timeline,0,900,900,origin.X,origin.Y,end.X,end.Y,16);Require((contact!=null)==expected,"finite strip matches original pure geometry at rectangle edge: "+angle+"/"+length+"/"+offset);comparisons++;
            }
            Console.WriteLine("PASS original pure finite-strip geometry: comparisons="+comparisons);
        }
        private static void OwnerOffsetOracle(object context,object combat,object host,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,277,0,0);p.position=new Vector2(700,646);p.ResetEffects();
            var n=new NPC();n.SetDefaults(3);n.position=new Vector2(780.9f,646.4f);n.netOffset=new Vector2(-10.6f,-.5f);var q=new Projectile();q.SetDefaults(47);q.owner=0;q.Center=p.MountedCenter;q.ownerHitCheck=true;
            var key=new NpcIdentity(1,n,2,1,3,3);var point=new NpcTrajectoryPoint(0,new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,NetOffsetX=n.netOffset.X,NetOffsetY=n.netOffset.Y,Width=n.width,Height=n.height,CanReceive=true});
            var terrain=Activator.CreateInstance(combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain"),true);var allows=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack").GetMethod("OwnerAllows",BindingFlags.Static|BindingFlags.NonPublic);
            float distance=Vector2.Distance(q.Center,n.Center+n.netOffset);
            foreach(float margin in new[]{-.05f,.05f})
            {
                q.ownerHitCheckDistance=distance+margin;var position=n.position;bool native;try{n.position+=n.netOffset;native=q.CanHitWithMeleeWeapon(n);}finally{n.position=position;}
                Require((bool)allows.Invoke(null,new object[]{p,q,point.OwnerBounds,false,terrain})==native,"owner gate preserves exact floating offset body at distance boundary: "+margin);
            }
            Require(!(bool)allows.Invoke(null,new object[]{p,q,point.Bounds,false,terrain}),"unshifted motion body is a distinct incorrect owner-distance premise");Console.WriteLine("PASS original owner gate floating netOffset/distance, distinct from integer Colliding");
        }
        private static void Starlight(object context,object combat,object host,object attack,object input)
        {
            foreach(bool wall in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,4923,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.itemAnimationMax=p.HeldItem.useAnimation;Main.screenPosition=new Vector2(600,500);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(790,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
                if(wall)for(int y=20;y<=42;y++){Main.tile[46,y].active(true);Main.tile[46,y].type=Terraria.ID.TileID.Stone;}
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(s=>s.active && s.type==927);NativeCombatAimChecks.Prepare(host,attack,n,0,true);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");
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
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(s=>s.active && s.aiStyle==15);q.Update(q.whoAmI);Require(n.life==10000,"initial flail spin cannot damage before original localAI warmup");
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");p.channel=p.controlUseItem=false;NativeCombatAimChecks.Prepare(host,attack,n,0,true);int x=Main.mouseX,y=Main.mouseY;var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");
            q.Update(q.whoAmI);Require(q.ai[0]==1 && q.velocity.X>0 && Main.mouseX==x && Main.mouseY==y,"flail's real release AI borrows prepared direction then returns physical coordinates");
            Require(plan!=null,"flail release has a finite native outbound contact");int first=n.life<10000?0:-1;for(int k=1;k<20 && first<0;k++){q.Update(q.whoAmI);if(n.life<10000)first=k;}
            Require(first==plan.Tick,"flail first native outbound Damage matches timeline: actual="+first+" expected="+plan.Tick);Console.WriteLine("PASS native flail warmup/release/contact: first="+first+" expected="+plan.Tick);
            foreach(var shot in Main.projectile)shot.active=false;p.channel=p.controlUseItem=true;n.position.X=755;n.life=10000;Array.Clear(n.immune,0,n.immune.Length);NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});q=Main.projectile.Single(s=>s.active && s.aiStyle==15);NativeCombatAimChecks.Prepare(host,attack,n,0,true);plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Console.WriteLine("FLAIL spin premise: plan="+plan?.Tick+" local="+q.localAI[1]+" state="+q.ai[0]+" channel="+p.channel+" mounted="+p.MountedCenter+" target="+n.Hitbox+" owner="+n.immune[0]+" localimmune="+q.localNPCImmunity[2]);Require(plan!=null && plan.Tick>=12,"spin contact respects original thirteen-step warmup");first=-1;
            for(int k=0;k<20 && first<0;k++){q.Update(q.whoAmI);if(n.life<10000)first=k;}
            Require(first==plan.Tick,"flail spin ellipse and actual warmup agree: first="+first+" expected="+plan.Tick);Console.WriteLine("PASS native flail warmup/ellipse contact: first="+first+" expected="+plan.Tick);
        }
        private static Exception Fault(Exception __exception,MethodBase __originalMethod)
        {if(__exception!=null)Console.WriteLine("MELEE original fault: "+__originalMethod.Name+" "+__exception);return __exception;}
    }
}
