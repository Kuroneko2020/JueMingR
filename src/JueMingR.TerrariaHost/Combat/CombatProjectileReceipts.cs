using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class CombatProjectileReceipts : IDisposable
    {
        private static CombatProjectileReceipts current;
        private readonly HostCombat host;
        private readonly Harmony harmony=new Harmony("JueMingR.Combat.ProjectileReceipts");
        private readonly List<MethodBase> patched=new List<MethodBase>();
        internal bool Ready {get;private set;}
        internal Exception Error {get;private set;}
        internal CombatProjectileReceipts(HostCombat host)
        {
            this.host=host;
            try
            {
                if(current!=null)throw new InvalidOperationException("Combat receipts already installed.");current=this;
                var hit=AccessTools.DeclaredMethod(typeof(Projectile),"Damage_PVE_Inner");
                var collide=AccessTools.DeclaredMethod(typeof(Projectile),"AI_015_HandleMovementCollision");
                if(hit==null || collide==null)throw new MissingMethodException("Combat receipt ABI");
                patched.Add(hit);harmony.Patch(hit,transpiler:new HarmonyMethod(typeof(CombatProjectileReceipts),nameof(Hit)));
                patched.Add(collide);harmony.Patch(collide,new HarmonyMethod(typeof(CombatProjectileReceipts),nameof(BeforeCollision)),new HarmonyMethod(typeof(CombatProjectileReceipts),nameof(AfterCollision)));
                Ready=true;
            }
            catch(Exception e){Error=e;Dispose();}
        }
        private static IEnumerable<CodeInstruction> Hit(IEnumerable<CodeInstruction> source)
        {
            var code=source.ToList();var strike=AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)});
            var calls=Enumerable.Range(0,code.Count).Where(i=>code[i].Calls(strike)).ToArray();
            if(calls.Length!=1)throw new InvalidOperationException("Combat StrikeNPC receipt is not unique.");
            code.InsertRange(calls[0]+1,new[]{new CodeInstruction(OpCodes.Dup),new CodeInstruction(OpCodes.Ldarg_0),new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(CombatProjectileReceipts),nameof(AcceptedHit)))});
            return code;
        }
        private static void AcceptedHit(int damage,Projectile shot)
        {
            // Positive return means local native acceptance, not a server ACK
            // or guaranteed HP loss. Observation cannot interrupt native damage.
            var self=current;if(self==null || !self.Ready || damage<=0)return;
            try{if(self.host.IsEnabled(1))self.host.Use.FlailReceipt(shot);}catch(Exception e){self.Failed(e);}
        }
        private static void BeforeCollision(Projectile __instance,ref Vector2 __1,out bool __state)
        {__state=false;var self=current;if(self==null || !self.Ready)return;try{__state=self.host.IsEnabled(1) && self.host.Use.TracksFlail(__instance) && __1!=__instance.velocity;}catch(Exception e){self.Failed(e);}}
        private static void AfterCollision(Projectile __instance,bool __state,bool __runOriginal)
        {var self=current;if(!__state || !__runOriginal || self==null || !self.Ready)return;try{self.host.Use.FlailReceipt(__instance);}catch(Exception e){self.Failed(e);}}
        private void Failed(Exception error){Error=error;Ready=false;host.Use.Stop();}
        public void Dispose()
        {Ready=false;foreach(var method in patched){try{harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);}catch(Exception e){if(Error==null)Error=e;}}patched.Clear();if(ReferenceEquals(current,this))current=null;}
    }
}
