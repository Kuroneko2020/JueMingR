using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionContinuousChecks
    {
        internal static void Run(object host,Player body,Guid id)
        {
            var actor=Call(host,"Owned",body);var original=((CompanionProfile)Get(actor,"Profile")).Behavior;
            var target=Main.npc[0];target.SetDefaults(3);target.whoAmI=0;target.active=true;target.Center=body.Center+new Vector2(180,0);target.velocity=Vector2.Zero;target.aiStyle=-1;target.noGravity=target.noTileCollide=true;target.life=target.lifeMax=1000000;
            body.inventory[0].SetDefaults(3541);body.inventory[1].SetDefaults(39);body.inventory[54].SetDefaults(40);body.inventory[54].stack=30;body.selectedItemState.Select(0);
            body.itemAnimation=body.itemTime=0;body.releaseUseItem=true;body.statMana=body.statManaMax=body.statManaMax2=200;
            var cosmetic=new Harmony("JueMingR.Tests.CompanionPrismRipple");
            cosmetic.Patch(typeof(Projectile).GetMethod("AI"),transpiler:new HarmonyMethod(typeof(NativeCombatBeamChecks).GetMethod("RippleOutlet",BindingFlags.NonPublic|BindingFlags.Static)));
            try
            {
                Require((bool)Call(host,"SetBehavior",id,new CompanionBehavior{Follow=false,MainWeapons=new[]{new CompanionWeaponPreference(3541,0,0)}}),"continuous weapon starts through real actor configuration");
                for(int i=0;i<24;i++)Step(host,body);
                Require(body.channel && Main.projectile.Any(p=>p.active && p.owner==body.whoAmI && p.type==633) && Main.projectile.Count(p=>p.active && p.owner==body.whoAmI && p.type==632)==6 && body.statMana<200,"real actor ItemCheck holds prism, consumes own mana and creates six native child beams");
                Require((bool)Call(host,"SetBehavior",id,new CompanionBehavior{Follow=false,AttackEnabled=false}),"real stop-fire command releases only this role");
                for(int i=0;i<10;i++)Step(host,body);
                Require(!body.channel && !Main.projectile.Any(p=>p.active && p.owner==body.whoAmI && (p.type==633 || p.type==632)),"native prism parent and children retire after actual role release");
                Require((bool)Call(host,"SetBehavior",id,new CompanionBehavior{Follow=false,MainWeapons=new[]{new CompanionWeaponPreference(39,0,1)}}),"bow preference commits without forcing a native slot change");int ammo=body.inventory[54].stack;
                for(int i=0;i<35;i++)Step(host,body);
                Require(body.HeldItem.type==39 && body.inventory[54].stack<ammo && !Main.projectile.Any(p=>p.active && p.owner==body.whoAmI && p.type==633),"switch after release uses new native weapon/ammo without resurrecting old channel");
                Console.WriteLine("PASS companion actual ItemCheck prism hold/mana/six children -> stop-fire/native release -> buffered bow switch/ammo; no real-game/FPS claim.");
            }
            finally
            {
                Call(host,"SetBehavior",id,new CompanionBehavior{Follow=false,AttackEnabled=false});for(int i=0;i<4;i++)Step(host,body);
                target.active=false;Call(host,"SetBehavior",id,original);cosmetic.UnpatchAll(cosmetic.Id);
            }
        }
        private static void Step(object host,Player body)
        {
            NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);body.Update(body.whoAmI);
            for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active)Main.npc[i].UpdateNPC(i);
            int previous=Main.ProjectileUpdateLoopIndex;
            try{for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;Main.projectile[i].Update(i);}}
            finally{Main.ProjectileUpdateLoopIndex=previous;}
            var actor=Call(host,"Owned",body);Require(!(bool)Get(actor,"Faulted"),"continuous native body has no hidden update exception: "+Get(actor,"Status"));
        }
    }
}
