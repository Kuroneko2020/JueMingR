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
    internal static class NativeCombatJoustingLanceChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Assembly assembly;private static Type model;
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static Player Reset(object context,int weapon)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);p.position=new Vector2(850,646);p.velocity=Vector2.Zero;p.ResetEffects();p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;p.channel=p.controlUseItem=true;p.direction=1;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<115;x++)for(int y=10;y<65;y++)Main.tile[x,y].ClearEverything();for(int x=35;x<115;x++)NativeToolsChecks.Tile(x,43,1);return p;
        }
        private static NPC Target(Vector2 center)
        {var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(NPC n)
        {
            var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static AttackContact Known(Player p,Projectile shot,NPC n,out object root)
        {
            var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,shot,path,0,true,false},null);return (AttackContact)Static(model,"KnownFamily",p,new[]{shot},path,clock,terrain,root);
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostJoustingLanceAttack",true);var audit=new Harmony("JueMingR.Tests.JoustingLance");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            try{Motor(context);Held(context);Consumer(context);Console.WriteLine("PASS JOUSTING LANCE original hand/held/release, dummy/channel clocks, native shape and owner speed damage.");}finally{audit.UnpatchAll(audit.Id);}
        }
        private static void Motor(object context)
        {
            var clone=typeof(object).GetMethod("MemberwiseClone",Flags);
            foreach(int weapon in new[]{4788,4789,4790})foreach(int facing in new[]{-1,1})
            {
                var p=Reset(context,weapon);p.direction=facing;var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.MountedCenter,new Vector2(facing*3,.3f),p.HeldItem.shoot,60,0,p.whoAmI)];var shadow=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostThornAttack",true),"Copy",q);var body=(Player)clone.Invoke(p,null);int actions=p.itemAnimationMax*3;
                for(int step=0;step<=actions;step++)
                {
                    if(step>0){p.itemAnimation=Math.Max(0,p.itemAnimation-1);body.itemAnimation=Math.Max(0,body.itemAnimation-1);}if(step==actions)p.channel=body.channel=false;
                    bool alive=(bool)Static(model,"Ai",shadow,body);q.AI();
                    Require(q.active==alive && Vector2.Distance(q.Center,shadow.Center)<.001f && q.ai[0]==shadow.ai[0] && q.rotation==shadow.rotation && p.itemAnimation==body.itemAnimation,"original AI019/channel/dummy/kill pose agrees for every action: "+weapon+"/"+facing+"/"+step);
                    if(alive)
                    {
                        for(int dx=-120;dx<=120;dx+=30)for(int dy=-45;dy<=45;dy+=30)
                        {var box=new Rectangle((int)q.Center.X+dx,(int)q.Center.Y+dy,18,40);bool actual=q.Colliding(q.Hitbox,box);bool expected=(bool)Static(model,"Collides",shadow,new MotionRect(box.X,box.Y,box.Width,box.Height));Require(actual==expected,"native lance shaft line and300px clip agree without substituting endpoint body");}
                    }
                }
                Require(!q.active,"original released lance retires after its held ai clock has exceeded animation maximum");
            }
            Console.WriteLine("PASS JOUSTING MOTOR 3 types x2 facing, three original animation durations held and native release; per-action collider grid");
        }
        private static void Held(object context)
        {
            foreach(int weapon in new[]{4788,4789,4790})
            {
                var p=Reset(context,weapon);Main.mouseX=1200-(int)Main.screenPosition.X;Main.mouseY=667-(int)Main.screenPosition.Y;
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.type==p.HeldItem.shoot);var velocity=q.velocity;
                for(int step=0;step<p.itemAnimationMax*3;step++){if(step>0)p.itemAnimation--;Main.mouseX=600-(int)Main.screenPosition.X;q.Update(q.whoAmI);Require(q.active && q.velocity==velocity,"real native held lance survives beyond its animation and never reads the later opposite mouse");}
                var angle=q.rotation-MathHelper.PiOver4-MathHelper.PiOver2-(q.spriteDirection==1?MathHelper.Pi:MathHelper.PiOver2);var n=Target(q.Center+angle.ToRotationVector2()*45);object root;var contact=Known(p,q,n,out root);Require(contact!=null && contact.Tick==1,"observed held lance retains useful next damage after its original finite animation ended");
                p.channel=false;Require(!(bool)Get(root,"Unchanged"),"same-GUC release revokes the channel/dummy lifecycle premise");p.channel=true;
                int life=n.life;q.Update(q.whoAmI);Require(n.life<life,"original held lance delivers real native Damage after the old finite model would stop");
                foreach(float speed in new[]{0f,7f,14f})
                {p.velocity=new Vector2(speed,0);float actual=(float)typeof(Projectile).GetMethod("Damage_GetProjectileSpecificDamageMultiplier",Flags).Invoke(q,null),expected=(float)Static(model,"DamageMultiplier",p.velocity);Require(actual==expected,"native speed damage is10%+90%*speed/7, distinct from knockback speed/7");}
                p.velocity=Vector2.Zero;p.channel=false;contact=Known(p,q,n,out root);Require(contact==null,"release cannot publish a damage stage after original AI019 will Kill");q.Update(q.whoAmI);Require(!q.active,"release keeps original Kill and creates no extra attack");
                Console.WriteLine("PASS JOUSTING HELD weapon="+weapon+" counter="+q.ai[0]+" launch="+velocity+" nativeLife="+n.life);
            }
        }
        private static void Consumer(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");
            foreach(int weapon in new[]{4788,4789,4790})for(int mode=0;mode<4;mode++)
            {
                var p=Reset(context,weapon);p.position=new Vector2(650,646);p.itemAnimation=p.itemTime=0;p.channel=false;p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);var n=Target(p.Center+new Vector2(140,0));n.width=80;n.height=80;n.Center=p.Center+new Vector2(140,0);var raw=n.Center-new Vector2(450,0);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");var lease=GetOptional(attack,"obstacles");Require(plan!=null && lease!=null,"actual public lance preparation retains its complete owner/channel root: "+weapon);
                if(mode==1)p.channel=!p.channel;if(mode==2)p.fullRotation+=.1f;if(mode==3)n.dontTakeDamage=true;
                if(mode!=0)Require(!(bool)Get(lease,"Unchanged"),"same-GUC channel/pose/receiver edit revokes actual lance preparation: "+weapon+"/"+mode);
                int mouse=Main.mouseX;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});Require(Main.mouseX==mouse && Main.projectile.Any(q=>q.active && q.type==p.HeldItem.shoot),"real lance hand retains birth and restores physical cursor");
                if(mode==0)
                {
                    Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","real lance hand consumes the public enhanced preparation");var q=Main.projectile.Single(s=>s.active && s.type==p.HeldItem.shoot);int life=n.life;p.channel=true;
                    for(int i=0;i<40 && n.life==life && q.active;i++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);q.Update(q.whoAmI);}Require(n.life<life,"actual enhanced lance hand reaches native Damage");
                }
                else Require(!(bool)Get(attack,"hasConsumedInput") || Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))!="Enhanced","revoked lance lease cannot be consumed as Enhanced");
            }
            Console.WriteLine("PASS JOUSTING public preparation/native Shoot/Enhanced+Damage and channel/pose/receiver guards (1000ms mechanism component allowance)");
        }
    }
}
