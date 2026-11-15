using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionCoreWeaponChecks
    {
        private static Player originBody;
        private static Projectile currentPrism;
        private static int originSamples;
        private static bool originsCurrent,originDistinguished;
        private static void OriginStep(Projectile __instance){currentPrism=__instance.type==633 && __instance.owner==originBody.whoAmI?__instance:null;}
        private static void OriginStepEnd(){currentPrism=null;}
        private static void OriginInput(Vector2 __0)
        {
            if(currentPrism==null)return;var mounted=originBody.RotatedRelativePoint(originBody.MountedCenter);
            originSamples++;originsCurrent&=__0==mounted;originDistinguished|=Vector2.DistanceSquared(currentPrism.Center,mounted)>4;
        }
        internal static void Run(object host,Player first,Player second,object a,object b,List<byte[]> fa,List<byte[]> fb,List<byte[]> primary)
        {
            var original=((CompanionProfile)Get(a,"Profile")).Behavior;bool map=Main.mapFullscreen;int x=Main.mouseX,y=Main.mouseY,who=Main.myPlayer;var mouse=Main.mouseItem;
            NPC bowTarget=null;var ripple=new Harmony("JueMingR.Tests.CompanionCorePrismRipple");
            originBody=first;originSamples=0;originsCurrent=true;originDistinguished=false;
            ripple.Patch(AccessTools.Method(typeof(Projectile),"Update"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreWeaponChecks),nameof(OriginStep)),finalizer:new HarmonyMethod(typeof(NativeCompanionCoreWeaponChecks),nameof(OriginStepEnd)));
            ripple.Patch(AccessTools.Method(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostPrismSteering"),"PointInView"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreWeaponChecks),nameof(OriginInput)));
            ripple.Patch(AccessTools.Method(typeof(Projectile),"AI"),transpiler:new HarmonyMethod(typeof(NativeCombatBeamChecks).GetMethod("RippleOutlet",BindingFlags.NonPublic|BindingFlags.Static)));
            Action publish=()=>{NetMessage.SendData(23,-1,-1,null,18);Apply(primary.Last(p=>p[2]==23));};
            try
            {
                Call(host,"SetBehavior",((CompanionProfile)Get(b,"Profile")).Id,new CompanionBehavior{Follow=false,AttackEnabled=false});
                var target=Main.npc[18];target.SetDefaults(NPCID.Zombie);target.active=true;target.position=first.position+new Vector2(180,-20);target.velocity=Vector2.Zero;target.noGravity=target.noTileCollide=true;target.life=target.lifeMax=1000000;Main.npc[19].active=false;publish();
                var observed=(NPC[])Get(host,"observedNpcs");observed[19].active=false;
                first.inventory[0].SetDefaults(ItemID.LastPrism);first.selectedItemState.Select(0);first.itemAnimation=first.itemTime=0;first.releaseUseItem=true;first.statMana=first.statManaMax=first.statManaMax2=200;
                Call(host,"SetBehavior",((CompanionProfile)Get(a,"Profile")).Id,new CompanionBehavior{Follow=false,AttackEnabled=true,PreferOwner=false});
                for(int i=0;i<24;i++)Step(host,first);
                var prism=Main.projectile.FirstOrDefault(p=>p.active && p.owner==first.whoAmI && p.type==633);
                Require(prism!=null && first.channel && first.statMana<200 && Main.projectile.Count(p=>p.active && p.owner==first.whoAmI && p.type==632)==6,"real MP ItemCheck prism birth/mana/six children unchanged");
                fa.Clear();fb.Clear();primary.Clear();var velocity=prism.velocity;
                // Turn into open air, not below this fixture's stone floor:
                // native line-of-sight must still admit the held action.
                target.position+=new Vector2(0,-25);publish();Require(Collision.CanHitLine(first.Center,1,1,target.Center,1,1),"small turn remains a legally visible native target");
                first.velocity=new Vector2(1.2f,0);for(int i=0;i<8;i++)Step(host,first);
                Console.WriteLine("PRISM TAIL active="+prism.active+" key="+prism.key+" velocity="+velocity+" -> "+prism.velocity+" fa27="+fa.Count(p=>p[2]==27)+" own="+fa.Count(p=>p.Length>22 && p[2]==27 && BitConverter.ToInt32(p,3)==(int)prism.key)+" fb27="+fb.Count(p=>p[2]==27)+" primary27="+primary.Count(p=>p[2]==27)+" target="+GetOptional(a,"Target"));
                Require(prism.active && prism.velocity!=velocity && fa.Any(p=>p.Length>22 && p[2]==27 && BitConverter.ToInt32(p,3)==(int)prism.key) && !fb.Any(p=>p[2]==27) && !primary.Any(p=>p[2]==27 && BitConverter.ToInt32(p,3)==(int)prism.key),"real moving prism Update tail sends changed27 through exact actor stream only");
                // AI_075 writes mounted position, then outer Update moves the
                // projectile by velocity. Its final Center is not the aiming
                // origin. Observe the real steering call before that motion.
                Require(originSamples>0 && originsCurrent && originDistinguished,"real steering calls use current mounted origin, distinguished from previous projectile center");
                Call(host,"SetBehavior",((CompanionProfile)Get(a,"Profile")).Id,new CompanionBehavior{Follow=false,AttackEnabled=false});for(int i=0;i<10;i++)Step(host,first);
                Require(!prism.active && !Main.projectile.Any(p=>p.active && p.owner==first.whoAmI && p.type==632),"native MP prism stop retires parent/children");
                first.inventory[0].SetDefaults(ItemID.BallOHurt);first.itemAnimation=first.itemTime=0;first.releaseUseItem=true;
                Call(host,"SetBehavior",((CompanionProfile)Get(a,"Profile")).Id,new CompanionBehavior{Follow=false,AttackEnabled=true,PreferOwner=false});
                Main.mapFullscreen=true;for(int i=0;i<8;i++)Step(host,first);
                var flail=Main.projectile.FirstOrDefault(p=>p.active && p.owner==first.whoAmI && p.aiStyle==15);
                Require(flail!=null && first.channel && Main.mapFullscreen,"human fullscreen map cannot terminate real AI flail or get closed");
                Main.mapFullscreen=false;for(int i=0;i<3;i++)Step(host,first);Main.mapFullscreen=true;for(int i=0;i<3;i++)Step(host,first);
                Require(flail.active && Main.mapFullscreen,"AI continuous flail survives human map toggles without manual lifetime renewal");
                HumanFlail(host,first);
                Call(host,"SetBehavior",((CompanionProfile)Get(a,"Profile")).Id,new CompanionBehavior{Follow=false,AttackEnabled=false});for(int i=0;i<80 && flail.active;i++)Step(host,first);
                Require(!flail.active,"actual AI stop releases and natively retires flail despite human map");
                for(int i=0;i<8 && (first.itemAnimation>0 || first.itemTime>0);i++)Step(host,first);
                first.inventory[1].SetDefaults(ItemID.WoodenBow);first.inventory[54].SetDefaults(ItemID.WoodenArrow);first.inventory[54].stack=20;
                var beforeBow=observed[18];var beforeLease=((System.Collections.IDictionary)Get(host,"npcSources"))[18];
                Console.WriteLine("BOW PREMISE body="+first.Center+" observed="+beforeBow.Center+" active="+beforeBow.active+" life="+beforeBow.life+" predictedDead="+Call(host,"PredictionDead",a,beforeBow)+" visible="+Collision.CanHitLine(first.Center,1,1,beforeBow.Center,1,1)+" waiting="+Get(beforeLease,"Waiting")+" baseline="+Get(beforeLease,"AwaitBaseline"));
                // Prism/flail can legitimately exhaust the old target's
                // predicted health. The switch needs a current live enemy,
                // delivered once through the real primary23 consumer.
                bowTarget=Main.npc[20];bowTarget.SetDefaults(NPCID.Zombie);bowTarget.active=true;bowTarget.position=first.position+new Vector2(180,-20);bowTarget.velocity=Vector2.Zero;
                NetMessage.SendData(23,-1,-1,null,20);Apply(primary.Last(p=>p[2]==23));
                Require(observed[20].active && !(bool)Call(host,"PredictionDead",a,observed[20]) && Collision.CanHitLine(first.Center,1,1,observed[20].Center,1,1),"single native23 establishes a currently live and visible bow target after prior combat");
                Require((bool)Call(host,"SelectWeapon",((CompanionProfile)Get(a,"Profile")).Id,1),"native completed channel permits real buffered bow selection");
                Call(host,"SetBehavior",((CompanionProfile)Get(a,"Profile")).Id,new CompanionBehavior{Follow=false,AttackEnabled=true,PreferOwner=false});
                Projectile arrow=null;
                // Wait for the original use/reuse scheduling; a weapon switch
                // does not promise a shot within four arbitrary world steps.
                for(int i=0;i<first.HeldItem.useAnimation+first.reuseDelay+8 && arrow==null;i++)
                {Step(host,first);arrow=Main.projectile.FirstOrDefault(p=>p.active && p.owner==first.whoAmI && p.type==ProjectileID.WoodenArrowFriendly);}
                Console.WriteLine("BOW SWITCH selected="+first.selectedItem+" item="+first.HeldItem.type+" anim="+first.itemAnimation+" time="+first.itemTime+" reuse="+first.reuseDelay+" use="+first.controlUseItem+" ammo="+first.inventory[54].stack+" target="+GetOptional(a,"Target"));
                Require(arrow!=null && first.inventory[54].stack<20,"actual MP switch preserves native bow ammo payment");
                fa.Clear();for(int i=0;i<3;i++)Step(host,first);
                Require(arrow.active && !fa.Any(p=>p.Length>6 && p[2]==27 && BitConverter.ToInt32(p,3)==(int)arrow.key),"stable native arrow without netUpdate emits no unconditional27 per frame");
                Require(Main.myPlayer==who && Main.mouseX==x && Main.mouseY==y && ReferenceEquals(Main.mouseItem,mouse),"native sync and AI scopes restore exact human identity/mouse");
                Console.WriteLine("PASS core native prism current mounted origin/Update27 and flail fullscreen-map isolation.");
            }
            finally{if(bowTarget!=null){bowTarget.active=false;var observations=(NPC[])GetOptional(host,"observedNpcs");if(observations!=null)observations[20].active=false;}Main.mapFullscreen=map;Call(host,"SetBehavior",((CompanionProfile)Get(a,"Profile")).Id,original);ripple.UnpatchAll(ripple.Id);}
        }
        private static void Apply(byte[] frame)
        {var decoder=new MessageBuffer{whoAmI=256};Buffer.BlockCopy(frame,2,decoder.readBuffer,0,frame.Length-2);decoder.ResetReader();int id;decoder.GetData(0,frame.Length-2,out id);}
        private static void HumanFlail(object host,Player actor)
        {
            var human=Main.LocalPlayer;var item=human.inventory[0];int selected=human.selectedItem,wait=0;bool use=human.controlUseItem,release=human.releaseUseItem,channel=human.channel;
            try
            {
                Main.mapFullscreen=false;human.inventory[0]=new Item(ItemID.BallOHurt);human.selectedItemState.Select(0);human.controlUseItem=true;human.releaseUseItem=true;human.ItemCheck();
                wait=human.inventory[0].useAnimation+human.reuseDelay+4;
                var p=Main.projectile.FirstOrDefault(q=>q.active && q.owner==human.whoAmI && q.aiStyle==15);
                Require(p!=null,"human original native ItemCheck creates its own flail");Main.mapFullscreen=true;p.Update(p.whoAmI);
                Require(!p.active && Main.mapFullscreen,"human own flail still follows original map-open Kill without actor qualification");
            }
            finally{human.inventory[0]=item;human.selectedItemState.Select(selected);human.controlUseItem=use;human.releaseUseItem=release;human.channel=channel;}
            for(int i=0;i<wait && (human.itemAnimation>0 || human.itemTime>0);i++)Step(host,actor,true);
            Require(human.itemAnimation==0 && human.itemTime==0,"original human item cycle completes naturally after its map release");
        }
        private static void Step(object host,Player p,bool updateHuman=false)
        {
            NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);if(updateHuman)Main.LocalPlayer.Update(Main.myPlayer);p.Update(p.whoAmI);Call(host,"AdvanceNpcObservation");
            int old=Main.ProjectileUpdateLoopIndex;
            try{for(int i=0;i<Main.projectile.Length;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;Main.projectile[i].Update(i);}}
            finally{Main.ProjectileUpdateLoopIndex=old;}
            var actor=Call(host,"Owned",p);Require(!(bool)Get(actor,"Faulted"),"native weapon step has no swallowed actor error: "+Get(actor,"Status"));
        }
    }
}
