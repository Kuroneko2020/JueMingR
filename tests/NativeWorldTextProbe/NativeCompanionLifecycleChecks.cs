using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using JueMingR.Platform.Companions;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionLifecycleChecks
    {
        private static Guid failLoad,failSave;
        private static int companionDamageProbes;
        internal static void FailedLoad(object host,Guid id)
        {
            var profiles=Get(host,"profiles");var h=new Harmony("JueMingR.Tests.CompanionLoadFailure");failLoad=id;
            h.Patch(AccessTools.Method(profiles.GetType(),"LoadBody"),prefix:new HarmonyMethod(typeof(NativeCompanionLifecycleChecks),nameof(Load)));
            try{Require(!(bool)Call(host,"Join",id),"real LoadBody fault is reported by Join");}
            finally{h.UnpatchAll(h.Id);failLoad=Guid.Empty;}
            var profile=((System.Collections.Generic.IEnumerable<CompanionProfile>)Get(host,"Profiles")).Single(p=>p.Id==id);object[] args={profile,0L};
            var reserve=profiles.GetType().GetMethod("TryReserve",BindingFlags.Instance|BindingFlags.NonPublic);
            Require((bool)reserve.Invoke(profiles,args),"failed native load releases its reserved activation");Call(profiles,"Release",profile,(long)args[1]);
        }
        internal static void FailedSaveExit(object host,Player body,Guid id,Guid other)
        {
            var profiles=Get(host,"profiles");var manager=Get(profiles,"manager");string oldKey=(string)Get(manager,"ownerKey");var actor=Call(host,"Owned",body);int slot=body.whoAmI;
            var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");
            var enter=hooks.GetMethod("Enter",BindingFlags.Static|BindingFlags.NonPublic);int ordinary,controller;
            body.inventory[0].SetDefaults(39);body.selectedItemState.Select(0);
            using((IDisposable)enter.Invoke(null,new object[]{actor,body.Center,false}))ordinary=Projectile.NewProjectile(new EntitySource_ItemUse(body,body.HeldItem),body.Center,Vector2.UnitX,1,10,0f,slot);
            body.inventory[0].SetDefaults(3541);body.channel=true;
            using((IDisposable)enter.Invoke(null,new object[]{actor,body.Center,false}))controller=Projectile.NewProjectile(new EntitySource_ItemUse(body,body.HeldItem),body.Center,Vector2.UnitX,633,10,0f,slot);
            Require(Call(host,"ProjectileActor",Main.projectile[ordinary])!=null && Call(host,"ProjectileActor",Main.projectile[controller])!=null,"actual native factory registers both activation leases");
            var h=new Harmony("JueMingR.Tests.CompanionSaveFailure");var archive=Get(manager,"archive");failSave=id;
            h.Patch(AccessTools.Method(archive.GetType(),"Save"),prefix:new HarmonyMethod(typeof(NativeCompanionLifecycleChecks),nameof(Save)));
            h.Patch(AccessTools.Method(typeof(Projectile),"Damage_CanDealDamage"),prefix:new HarmonyMethod(typeof(NativeCompanionLifecycleChecks),nameof(CountDamageProbe)));
            var enemy=Main.projectile[997];enemy.SetDefaults(1);enemy.active=true;enemy.owner=255;enemy.hostile=true;enemy.friendly=false;enemy.damage=5;enemy.position=new Vector2(100,100);
            try
            {
                companionDamageProbes=0;enemy.Damage();Require(companionDamageProbes>0,"actual online hostile projectile enters companion damage eligibility probe");
                Require((bool)Call(host,"Recall",id),"recall captures latest body before asynchronous save");
                NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return (bool)Call(host,"SaveFailed",id);});
                Require(!body.active && GetOptional(actor,"Connection")==null && !(bool)Get(host,"HasActors"),"failed save retains snapshot without body/connection or native actor work");
                companionDamageProbes=0;enemy.Damage();Require(companionDamageProbes==0,"actual zero-actor hostile projectile bypasses companion reflection/hitbox work");
                Require(!Main.projectile[controller].active && Main.projectile[ordinary].active,"recall retires native channel controller and preserves ordinary in-flight arrow");
                Call(host,"OnSessionEnded");Call(profiles,"SetOwner","fixture-other-owner-after-failed-save");
                NativeQuickItemChecks.Until(()=>{Call(profiles,"Pump");return (bool)Get(profiles,"Ready");});
                Require(!body.active && GetOptional(actor,"Connection")==null,"owner/world exit cannot resurrect failed-save connection/body");
                Call(profiles,"SetOwner",oldKey);NativeQuickItemChecks.Until(()=>{Call(profiles,"Pump");return (bool)Get(profiles,"Ready");});
                Require((bool)Call(host,"SaveFailed",id),"returning owner can recover exact failed snapshot");
            }
            finally{enemy.active=false;h.UnpatchAll(h.Id);failSave=Guid.Empty;}
            Require((bool)Call(host,"RetrySave",id),"actual failed snapshot save can retry");
            NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return !((IDictionary)Get(host,"actors")).Contains(id);});
            Call(host,"OnSessionStarted");Require((bool)Call(host,"Join",other),"different saved character joins after failed snapshot recovered");
            var successor=(Player)Call(host,"GetBody",other);Require(successor.whoAmI==slot,"real native player slot reused in counterexample");
            Require(Call(host,"ProjectileActor",Main.projectile[ordinary])==null && !Main.projectile[controller].active,"old generation projectiles cannot acquire new character controller");
            Main.projectile[ordinary].AI();Require(Main.myPlayer==Main.LocalPlayer.whoAmI,"ordinary old arrow AI preserves real owner identity");
            Call(host,"Recall",other);NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return ((IDictionary)Get(host,"actors")).Count==0;});
            Console.WriteLine("PASS companions lifecycle: actual load-fault reserve rollback; native factory controller retirement/ordinary survival and slot reuse; asynchronous save-fault exit/owner change/retry.");
        }
        private static bool Load(CompanionProfile profile){if(profile.Id==failLoad)throw new InvalidDataException("injected exact native load failure");return true;}
        private static bool Save(CompanionProfile profile){if(profile.Id==failSave)throw new IOException("injected exact asynchronous archive write failure");return true;}
        private static void CountDamageProbe()
        {
            var frames=new System.Diagnostics.StackTrace().GetFrames();
            if(frames!=null && frames.Any(f=>f.GetMethod().DeclaringType?.FullName=="JueMingR.TerrariaHost.Companions.CompanionHooks" && f.GetMethod().Name=="ProjectileDamageAfter"))companionDamageProbes++;
        }
    }
}
