using System;
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
    internal static class NativeCombatHitChecks
    {
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");
            Require(GetOptional(combat,"Goblin")!=null,"atomic native goblin gates are absent");
            Require((bool)Get(Get(combat,"Goblin"),"Ready"),"both goblin gates must install atomically: "+GetOptional(Get(combat,"Goblin"),"Error"));
            Require((bool)Get(Get(combat,"Receipts"),"Ready"),"actual flail hit and collision receipts must install");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return ((CombatSettings)Get(combat,"Settings")).Loaded;});
            for(int i=0;i<Main.player.Length;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            var player=Main.LocalPlayer;player.inventory[0].SetDefaults(ItemID.CopperBroadsword);player.itemAnimation=player.itemTime=0;player.selectedItemState.Select(0);player.selectedItemState.Update();
            foreach(bool enabled in new[]{false,true})
            {
                NativeCombatCadenceChecks.Save(combat,new CombatOptions(enabled?128:0));
                foreach(bool projectile in new[]{false,true})foreach(int type in new[]{107,105,106,17,1})
                {
                    bool hit=Hit(player,type,projectile,0);
                    Require(hit==(type==1 || type==107 && enabled),"native hit gate type="+type+" projectile="+projectile+" enabled="+enabled+" actual="+hit);
                }
                if(enabled)foreach(bool projectile in new[]{false,true})foreach(int denial in new[]{1,2,3})
                    Require(!Hit(player,107,projectile,denial),"native downstream denial remains projectile="+projectile+" denial="+denial);
            }
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            PartialInstall(combat);
            Console.WriteLine("PASS G11A both native goblin gates and projectile receipt seams installed.");
        }
        private static void PartialInstall(object combat)
        {
            var original=Get(combat,"Goblin");Type type=original.GetType();Call(original,"Dispose");
            var fixture=new Harmony("JueMingR.Tests.GoblinPartialInstall");var melee=AccessTools.Method(typeof(Player),"ProcessHitAgainstNPC");
            fixture.Patch(melee,prefix:new HarmonyMethod(typeof(NativeCombatHitChecks),nameof(ForeignPrefix)));
            fixture.Patch(AccessTools.Method(type,"Install"),prefix:new HarmonyMethod(typeof(NativeCombatHitChecks),nameof(FailSecondInstall)));
            try
            {
                var failed=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{combat},null);
                Require(!(bool)Get(failed,"Ready") && GetOptional(failed,"Error")!=null,"second-gate installation failure is unavailable");
                Require(!Harmony.GetPatchInfo(melee).Owners.Contains("JueMingR.Combat.Goblin") && Harmony.GetPatchInfo(melee).Owners.Contains(fixture.Id),"partial install removes its first gate while preserving foreign owner");
                Call(failed,"Dispose");
            }
            finally
            {
                foreach(var method in fixture.GetPatchedMethods().ToArray())fixture.Unpatch(method,HarmonyPatchType.All,fixture.Id);
                var restored=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{combat},null);Set(combat,"Goblin",restored);Require((bool)Get(restored,"Ready"),"fixture restores both native gates");
            }
        }
        private static void ForeignPrefix(){}
        private static void FailSecondInstall(string __1){if(__1=="Damage_PVE_Inner")throw new InvalidOperationException("isolated second gate failure");}
        internal static void FlailReceipts(object context)
        {
            object combat=Get(context,"Combat"),tools=Get(context,"Tools"),input=Get(context,"Input"),use=Get(combat,"Use");
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,162,0,0);NativeCombatCadenceChecks.Save(combat,new CombatOptions(2));
            for(int i=0;i<5 && GetOptional(use,"primary")==null;i++)NativeCombatCadenceChecks.Step(context,false,true,0);
            var shot=(Projectile)Get(use,"primary");Require((bool)Call(use,"TracksFlail",shot),"full native Player.Update created the exact tracked flail");
            foreach(var npc in Main.npc)npc.active=false;var target=Main.npc[0];target.SetDefaults(NPCID.BlueSlime);target.whoAmI=0;target.position=shot.position;target.active=true;target.life=target.lifeMax=10000;target.immune[0]=0;shot.localNPCImmunity[0]=0;
            Call(shot,"Damage_PVE",target.Hitbox,1f);Require(target.life<10000 && (bool)Get(use,"flailReceipt"),"real tracked flail native damage produces local accepted-hit receipt");
            Set(use,"flailReceipt",false);target.dontTakeDamage=true;Call(shot,"Damage_PVE",target.Hitbox,1f);Require(!(bool)Get(use,"flailReceipt"),"downstream immunity does not fabricate a hit receipt");
            // Isolate the original collision handler from graphical impact:
            // phase 2 and sub-four-pixel velocity still execute its real bounce.
            shot.ai[0]=2;shot.velocity=Vector2.Zero;object[] args={Vector2.Zero,new Vector2(2,0)};
            Call(shot,"AI_015_HandleMovementCollision",args);Require(shot.velocity.X<0 && (bool)Get(use,"flailReceipt"),"actual collision result produces tracked receipt");
            Set(use,"flailReceipt",false);shot.owner=1;Call(shot,"AI_015_HandleMovementCollision",args);Require(!(bool)Get(use,"flailReceipt"),"foreign owner cannot feed local flail cadence");
            NativeCombatCadenceChecks.Step(context,false,false,0);NativeCombatCadenceChecks.Save(combat,new CombatOptions());
        }
        private static bool Hit(Player player,int type,bool projectile,int denial)
        {
            foreach(var n in Main.npc)n.active=false;
            var target=Main.npc[0];target.SetDefaults(type);target.whoAmI=0;target.active=true;target.position=new Vector2(650,640);target.life=target.lifeMax=10000;
            bool friendly=target.friendly;target.dontTakeDamage=denial==1;
            Array.Clear(target.immune,0,target.immune.Length);if(denial==3)target.immune[0]=20;
            var rect=denial==2?new Rectangle(0,0,10,10):target.Hitbox;
            player.position=new Vector2(640,640);player.attackCD=0;Call(player,"ResetMeleeHitCooldowns");
            player.itemAnimation=player.itemAnimationMax=30;player.itemTime=0;
            if(!projectile)Call(player,"ItemCheck_MeleeHitNPCs",player.inventory[0],rect,80,1f);
            else
            {
                var shot=Main.projectile[0];shot.SetDefaults(ProjectileID.Bullet);shot.whoAmI=0;shot.owner=0;shot.active=true;shot.position=target.position;shot.damage=80;shot.maxPenetrate=shot.penetrate=10;
                Call(shot,"Damage_PVE",rect,1f);shot.active=false;
            }
            Require(target.friendly==friendly && target.type==type,"hit policy never changes NPC identity or faction");
            return target.life<10000;
        }
    }
}
