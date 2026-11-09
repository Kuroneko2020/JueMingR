using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBirthJitChecks
    {
        // Vanilla starts background ForceJITOnAssembly at engine preload. That
        // can precede Tools; reproduce this possible order in this isolated
        // native process; do not run ForceLoad, a game, or a synthetic birth.
        internal static void PrepareBeforeHost()
        {
            Require(!Harmony.HasAnyPatches("JueMingR.Tools"),"factory warm-up precedes all product Tools installation");
            var factories=typeof(Projectile).GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>m.Name=="NewProjectile").ToArray();
            var scalar=factories.Single(m=>m.GetParameters()[1].ParameterType==typeof(float));
            var vector=factories.Single(m=>m.GetParameters()[1].ParameterType==typeof(Vector2));
            var channel=typeof(Player).GetMethod("TryUpdateChannel",BindingFlags.Public|BindingFlags.Instance);
            Require(PatchProcessor.GetOriginalInstructions(scalar).Count(i=>i.Calls(channel))==1,"locked native scalar factory has exactly one actual channel call");
            RuntimeHelpers.PrepareMethod(channel.MethodHandle);RuntimeHelpers.PrepareMethod(scalar.MethodHandle);RuntimeHelpers.PrepareMethod(vector.MethodHandle);
            Console.WriteLine("PRISM JIT ORDER original channel/factories prepared before Host; scalarIL="+scalar.GetMethodBody().GetILAsByteArray().Length+" channelIL="+channel.GetMethodBody().GetILAsByteArray().Length+" channelFlags="+channel.GetMethodImplementationFlags()+"; no factory executed, no full ForceLoad");
        }
        internal static void RunOutlet(object context)
        {
            var tools=Get(context,"Tools");var control=Get(Get(Get(context,"Combat"),"Attack"),"Control");
            var priorBuild=(Action<Player,Projectile>)Get(tools,"BuildProjectile");var priorFishing=(Action<Player,Projectile>)Get(tools,"FishingProjectile");
            Require(priorBuild!=null,"the existing construction owner subscribes to the shared native birth outlet");
            var arrivals=new Dictionary<int,string>();int parents=0,children=0;
            Action<Player,Projectile> build=(p,q)=>
            {
                int key=(int)q.key;Require(!arrivals.ContainsKey(key),"factory birth reaches the construction-first subscription exactly once per full key");arrivals.Add(key,"B");
                if(q.type==633)
                {
                    parents++;var cache=Get(p,"_channelShotCache");
                    Require((int)Get(cache,"ProjectileTypeExpected")==633 && (int)Get(cache,"ProjectileIndexExpected")==q.whoAmI,"vanilla channel cache is updated BEFORE the source observers run");
                }
                if(q.type==632)children++;
            };
            Action<Player,Projectile> fishing=(p,q)=>
            {int key=(int)q.key;Require(arrivals.ContainsKey(key) && arrivals[key]=="B","shared consumers keep construction priority before the existing Tools/Fishing/Combat notifications");arrivals[key]="BF";};
            Set(tools,"BuildProjectile",priorBuild+build);Set(tools,"FishingProjectile",priorFishing+fishing);
            try
            {
                NativeCombatPrismChainChecks.Run(context);
                Require(parents==1 && children==6 && arrivals.Values.All(v=>v=="BF"),"one real prism receipt and six real child births notify each shared subscriber exactly once");
                Require(((System.Collections.IDictionary)Get(control,"shots")).Count==0,"normal release retires the natural controller");
                Set(tools,"BuildProjectile",priorBuild);Set(tools,"FishingProjectile",priorFishing);
                // A native factory call by itself cannot manufacture a Shoot
                // receipt merely because the local player holds that weapon.
                var player=Main.LocalPlayer;int slot=Projectile.NewProjectile(player.GetSource_Misc("source-authority-negative"),player.Center,Vector2.UnitX,633,1,0,player.whoAmI);
                var orphan=Main.projectile[slot];
                Require(!((System.Collections.IDictionary)Get(control,"shots")).Contains((int)orphan.key),"unreceipted local factory birth is observed but never adopted as an assisted source");orphan.Kill();
                Console.WriteLine("PASS actual factory outlet: single native channel update, construction-first shared subscriptions, real633/six632 direction, release and no source adoption");
            }
            finally{Set(tools,"BuildProjectile",priorBuild);Set(tools,"FishingProjectile",priorFishing);}
        }
    }
}
