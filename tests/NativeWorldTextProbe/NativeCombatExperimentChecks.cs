using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Real fixed-native consumers in an isolated component host. No desktop
    // gameplay, server authority, FPS or owner acceptance is inferred.
    internal static class NativeCombatExperimentChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context)
        {
            NativeCombatAimChecks.Initialize();NativeHiddenEntryChecks.RevealForComposition(context);
            var owner=Get(context,"CombatExperiments");var host=Get(context,"CombatObservation");
            var combat=Get(context,"Combat");var tools=Get(context,"Tools");var input=Get(context,"Input");
            var ready=(bool[])Get(owner,"ready");var errors=(Exception[])Get(owner,"errors");
            for(int i=0;i<3;i++)Require(ready[i],"independent native experiment hook "+i+" installed: "+errors[i]);
            Save(owner,0);NativeCombatObservationChecks.Save(host,new ObservationOptions());
            try
            {
                HardLock(context,owner,host,tools,input);
                Movement(context,owner,tools,input);
                SummonAttack(context,owner,tools,input);
                Beams(context,owner,tools,input);
                Timings(context,owner,tools,input);
                PredictionBoundary(context,owner,host,combat,tools,input);
                PrismSource(context,owner,host,combat,tools,input);
                Console.WriteLine("PASS hidden experiments: actual native steering, terrain, beam, timing and basic prediction boundary; isolated components.");
            }
            finally{Save(owner,0);NativeCombatObservationChecks.Save(host,new ObservationOptions());foreach(var p in Main.projectile)p.active=false;}
        }
        private static void Save(object owner,int mask)
        {
            var settings=(CombatExperimentSettings)Get(owner,"Settings");
            NativeQuickItemChecks.Until(()=>{Call(owner,"Poll");return settings.Ready;});
            Require(settings.Set(new CombatExperimentOptions(mask)),"experiment preference command accepted");
            NativeQuickItemChecks.Until(()=>{Call(owner,"Poll");return !settings.Busy;});
            Require(settings.CompletionSucceeded,"experiment preference committed: "+settings.Message);
        }
        private static Projectile Shot(int type,Vector2 position,Vector2 velocity)
        {
            // Use the real factory: defaults alone have zero damage and no
            // registered ProjectileKey, so cannot represent a live attack.
            int slot=Projectile.NewProjectile(Main.LocalPlayer.GetProjectileSource_Item(Main.LocalPlayer.HeldItem),position,velocity,type,25,0,Main.myPlayer);
            var p=Main.projectile[slot];p.position=position;return p;
        }
        private static void World(object owner,Action run)
        {
            var receipt=Call(owner,"BeginWorld");
            try{run();}finally{owner.GetType().GetMethod("EndWorld",Flags).Invoke(null,new[]{receipt});}
        }
        private static void HardLock(object context,object owner,object host,object tools,object input)
        {
            Save(owner,0);var player=NativeToolExecutionChecks.Reset(context,tools,input,95,0,0);
            player.position=new Vector2(580,646);var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(850,540);target.life=10000;
            Save(owner,1);Call(host,"Update",Main.GameUpdateCount);
            Require(!((ObservationOptions)Get(host,"Options")).Aim,"hard lock does not enable saved Aim");
            var p=Shot(14,new Vector2(600,600),new Vector2(12,0));float speed=p.velocity.Length();
            p.AI();Require(p.velocity.Y==0,"out-of-live-phase native AI retains ordinary velocity");
            World(owner,()=>p.AI());Require(p.velocity.Y<0 && Math.Abs(p.velocity.Length()-speed)<.001f,"existing real bullet turns after native AI without speed loss; mask="+Get(owner,"EffectiveMask")+" damage="+p.damage+" selected="+Get(Get(host,"Selection"),"HasTarget")+" velocity="+p.velocity);
            var clone=(Projectile)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(p,null);clone.velocity=new Vector2(12,0);
            World(owner,()=>clone.AI());Require(clone.velocity.Y==0,"same owner/slot/key clone cannot obtain a live write");
            p.owner=1;p.velocity=new Vector2(12,0);World(owner,()=>p.AI());Require(p.velocity.Y==0,"remote native object cannot obtain steering");
            p.owner=0;Save(owner,0);p.velocity=new Vector2(12,0);World(owner,()=>p.AI());Require(p.velocity.Y==0,"OFF native consumer does not steer");
        }
        private static void Movement(object context,object owner,object tools,object input)
        {
            Save(owner,0);var player=NativeToolExecutionChecks.Reset(context,tools,input,95,0,0);player.position=new Vector2(580,646);
            for(int y=30;y<43;y++)NativeToolsChecks.Tile(40,y,TileID.Stone);
            var ordinary=Shot(14,new Vector2(600,646),new Vector2(80,0));ordinary.HandleMovement(ordinary.velocity);
            Require(!ordinary.active || ordinary.position.X<640,"vanilla real bullet cannot pass solid wall");
            Save(owner,4);var p=Shot(14,new Vector2(600,646),new Vector2(80,0));
            World(owner,()=>p.HandleMovement(p.velocity));Require(p.active && p.position.X>=680 && p.tileCollide,"real movement passes wall with native tileCollide field intact");
            var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(800,650);target.life=10000;
            p.Center=new Vector2(800,650);p.ownerHitCheck=true;p.ownerHitCheckDistance=1000;
            Require(!p.CanHitWithMeleeWeapon(target),"unscoped owner LOS remains blocked");
            World(owner,()=>Require(p.CanHitWithMeleeWeapon(target),"actual owner-hit consumer bypasses terrain"));
            p.ownerHitCheckDistance=10;target.position.X+=100;
            World(owner,()=>Require(!p.CanHitWithMeleeWeapon(target),"owner-hit distance remains authoritative"));
            Require(!Collision.CanHitLine(player.Center,1,1,target.Center,1,1),"global terrain geometry remains blocked");
            var slime=Shot(266,new Vector2(610,650),new Vector2(64,8));Require(slime.minion,"native baby slime is a minion body");slime.position.Y=43*16-slime.height-2;float floor=43*16-slime.height;
            World(owner,()=>slime.HandleMovement(slime.velocity));Require(slime.active && slime.position.X>=670 && slime.position.Y<=floor+.01f,"walking summon passes wall while retaining floor support");
            var hook=Shot(13,new Vector2(600,600),new Vector2(80,0));World(owner,()=>Require(!(bool)Call(owner,"Wall",hook),"noncombat hook is not granted terrain bypass"));
            Save(owner,0);p=Shot(14,new Vector2(600,646),new Vector2(80,0));World(owner,()=>p.HandleMovement(p.velocity));Require(!p.active || p.position.X<640,"OFF next native movement restores wall behavior");
        }
        private static void Beams(object context,object owner,object tools,object input)
        {
            Save(owner,0);NativeToolExecutionChecks.Reset(context,tools,input,3541,0,0);
            for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(50,y,TileID.Stone);
            var cosmetic=new Harmony("JueMingR.Tests.ExperimentBeamRipple");
            cosmetic.Patch(typeof(Projectile).GetMethod("AI"),transpiler:new HarmonyMethod(typeof(NativeCombatBeamChecks).GetMethod("RippleOutlet",Flags)));
            try
            {
                foreach(int parentType in new[]{633,460})
                {
                    var parent=Shot(parentType,new Vector2(600,600),Vector2.UnitX);parent.ai[0]=200;
                    var beam=Shot(parentType==633?632:461,new Vector2(600,600),Vector2.UnitX);beam.ai[0]=2;beam.ai[1]=(float)parent.key;
                    for(int i=0;i<12;i++)beam.AI();float blocked=beam.localAI[1];Require(beam.active && blocked<600,"native beam scan is clipped by terrain: "+beam.type);
                    Save(owner,4);World(owner,()=>{for(int i=0;i<12;i++)beam.AI();});Require(beam.active && beam.localAI[1]>2300,"actual native beam consumes original maximum length: "+beam.type);
                    Save(owner,0);for(int i=0;i<12;i++)beam.AI();Require(beam.localAI[1]<600,"native beam restores terrain after OFF");
                }
            }
            finally{cosmetic.UnpatchAll(cosmetic.Id);}
        }
        private static void SummonAttack(object context,object owner,object tools,object input)
        {
            Save(owner,0);var player=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.CopperPickaxe,0,0);
            player.position=new Vector2(600,646);for(int y=30;y<43;y++)NativeToolsChecks.Tile(45,y,TileID.Stone);
            var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(820,620);target.life=10000;target.noGravity=true;
            var sentry=Shot(ProjectileID.FrostHydra,new Vector2(650,620),Vector2.Zero);
            int slot;float x,yAim;
            Require(!sentry.AI_053_HandleSentryNPCTargeting(Vector2.Zero,false,out slot,out x,out yAim),"native sentry targeting is blocked by wall");
            Save(owner,4);World(owner,()=>Require(sentry.AI_053_HandleSentryNPCTargeting(Vector2.Zero,false,out slot,out x,out yAim) && slot==2,"native sentry targeting actually sees wall-hidden enemy"));
            var imp=Shot(ProjectileID.FlyingImp,new Vector2(650,600),Vector2.Zero);player.impMinion=true;
            World(owner,()=>{for(int tick=0;tick<180;tick++)imp.AI();});
            Require(Main.projectile.Any(p=>p.active && p.owner==0 && p.type==ProjectileID.ImpFireball),"actual flying summon AI emits attack behind wall while owner holds a pick");
        }
        private static void Timings(object context,object owner,object tools,object input)
        {
            var baseline=Events(context,owner,tools,input,1553,0,32);Require(baseline.Count>=4 && baseline.Zip(baseline.Skip(1),(a,b)=>b-a).All(x=>x==5),"fixed SDMG actual ItemCheck cadence is five ticks: "+string.Join(",",baseline));
            foreach(int weapon in new[]{95,1929,434})
            {
                var events=Events(context,owner,tools,input,weapon,2,40);
                Require(events.Count>=5 && events.Zip(events.Skip(1),(a,b)=>b-a).All(x=>x==5),"real single/burst uses retain five-tick emissions: "+weapon+" / "+string.Join(",",events));
            }
            Save(owner,2);var player=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.CopperPickaxe,0,0);var item=player.HeldItem;
            player.ApplyItemTime(item);Require(player.itemTime==item.useTime,"damage-bearing pick retains native tool time");
            player.inventory[0].SetDefaults(95);item=player.HeldItem;Call(player,"ApplyItemAnimation",item);int duration=player.itemAnimationMax;
            Save(owner,0);player.ApplyItemTime(item);Require(player.itemTime==5 && player.itemAnimationMax==duration,"OFF during use leaves frozen current cycle intact");
            Call(player,"ApplyItemAnimation",item);player.ApplyItemTime(item);Require(player.itemTime==item.useTime,"next natural use restores original timing after OFF");
        }
        private static List<int> Events(object context,object owner,object tools,object input,int weapon,int mask,int ticks)
        {
            Save(owner,0);var p=NativeToolExecutionChecks.Reset(context,tools,input,weapon,0,0);p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=999;
            p.position=new Vector2(700,646);p.statMana=p.statManaMax2=10000;p.reuseDelay=0;p.releaseUseItem=true;
            Save(owner,mask);var seen=new HashSet<int>();var times=new List<int>();
            for(int tick=0;tick<ticks;tick++)
            {
                NativeToolExecutionChecks.Sample(context,input,new Vector2(1000,600),true);NativeQuickItemChecks.BeginWorldStep();p.controlUseItem=true;p.releaseUseItem=true;p.ItemCheck();
                bool born=false;foreach(var q in Main.projectile)if(q.active && q.owner==0 && seen.Add((int)q.key))born=true;
                if(born)times.Add(tick);
            }
            int spent=999-p.inventory[54].stack;
            // SDMG/Chain Gun have native 2/3 ammo conservation; zero spent in
            // this short sample is valid. Pistol spends once per use, whereas
            // Clockwork spends only on the actual first shot of each burst.
            Require(spent>=0 && spent<=times.Count,"native conservation cannot consume more ammo than legal uses: "+weapon);
            if(weapon==95)Require(spent==times.Count,"native pistol consumes exactly one ammo per actual use");
            if(weapon==434)Require(spent==(times.Count+2)/3,"native Clockwork still consumes once per original three-shot burst: uses="+times.Count+" spent="+spent);
            return times;
        }
        private static void PredictionBoundary(object context,object owner,object host,object combat,object tools,object input)
        {
            Save(owner,0);var player=NativeToolExecutionChecks.Reset(context,tools,input,95,0,0);player.inventory[54].SetDefaults(97);player.inventory[54].stack=999;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=new Vector2(950,646);n.life=10000;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,false,25,false,true));NativeToolExecutionChecks.Sample(context,input,new Vector2(500,500),true);Call(combat,"Sample");Call(host,"SampleMouse");Call(host,"Update",Main.GameUpdateCount);
            var attack=Get(combat,"Attack");
            foreach(int mask in new[]{1,2,4,7,0})
            {
                Save(owner,mask);Require((bool)Get(attack,"OriginalAttackPredictionAllowed")==(mask==0),"prediction follows effective mode mask");
                Call(host,"PrepareAction");var facts=NativeCombatAimChecks.QueryInput(attack,player);
                Require(facts.Allowed && (mask==0 || facts.Kind=="Basic"),"enhanced mode preserves selected-target Basic input");
                if(mask!=0)Require(GetOptional(attack,"ExpectedImpact")==null,"enhanced mode publishes no vanilla Contact");
            }
        }
        private static void PrismSource(object context,object owner,object host,object combat,object tools,object input)
        {
            Save(owner,0);NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var player=NativeToolExecutionChecks.Reset(context,tools,input,3541,0,0);player.position=new Vector2(700,688-player.height);player.releaseUseItem=true;
            player.statMana=player.statManaMax=player.statManaMax2=10000;Main.screenPosition=new Vector2(550,350);
            var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(1100,650);target.aiStyle=-1;target.noGravity=true;target.velocity=Vector2.Zero;target.life=target.lifeMax=100000;
            var mouse=new Vector2(520,420);var attack=Get(combat,"Attack");var registry=(IDictionary)Get(Get(attack,"Control"),"shots");
            var cosmetic=new Harmony("JueMingR.Tests.ExperimentPrismRipple");cosmetic.Patch(typeof(Projectile).GetMethod("AI"),transpiler:new HarmonyMethod(typeof(NativeCombatBeamChecks).GetMethod("RippleOutlet",Flags)));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,true,false,false,25,false,true));
                for(int i=0;i<40;i++)Step(context,owner,host,combat,input,mouse);
                var parent=Main.projectile.Single(p=>p.active && p.type==633 && p.owner==0);int key=(int)parent.key;
                Require(registry.Contains(key),"natural held prism birth is registered");var source=Get(registry[key],"Source");
                foreach(int mask in new[]{1,2,4,7,0})
                {
                    Save(owner,mask);
                    using(NativeCombatUsageChecks.ObserveDrawSteps())
                    {
                        int beforeSteps=NativeCombatUsageChecks.ObservedNpcSteps;
                        for(int i=0;i<16;i++)Step(context,owner,host,combat,input,mouse);
                        if(mask!=0)Require(NativeCombatUsageChecks.ObservedNpcSteps==beforeSteps,"enhanced real input/projectile chain performs no original NPC attack future steps");
                    }
                    Require(parent.active && (int)parent.key==key && registry.Contains(key) && ReferenceEquals(source,Get(registry[key],"Source")),"mode edge keeps same live prism and Source: "+mask);
                    float targetDot=Vector2.Dot(parent.velocity.SafeNormalize(Vector2.UnitY),(target.Center-player.MountedCenter).SafeNormalize(Vector2.UnitX));
                    float rawDot=Vector2.Dot(parent.velocity.SafeNormalize(Vector2.UnitY),(mouse-player.MountedCenter).SafeNormalize(Vector2.UnitY));
                    Require(targetDot>.95f && targetDot>rawDot+.5f,"prism keeps current target and realtime inertia through mode edge: "+mask);
                    if(mask!=0)Require(GetOptional(attack,"ExpectedImpact")==null,"held prism cannot publish original future during experiment");
                }
            }
            finally{cosmetic.UnpatchAll(cosmetic.Id);Save(owner,0);}
        }
        private static void Step(object context,object owner,object host,object combat,object input,Vector2 mouse)
        {
            NativeToolExecutionChecks.Sample(context,input,mouse,true);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);
            foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);
            Call(Get(combat,"Attack"),"PrepareControlledProjectileWorld");
            World(owner,()=>{for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;Main.projectile[i].Update(i);}});Main.ProjectileUpdateLoopIndex=-1;
            Call(context,"UpdateRuntime");
        }
    }
}
