using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Only the friendly-policy read changes, at two verified gates. All owner,
    // collision, immunity and damage calculation remains in the original call.
    internal sealed class GoblinHitHooks : IDisposable
    {
        private static GoblinHitHooks current;
        private readonly HostCombat host;
        private readonly Harmony harmony=new Harmony("JueMingR.Combat.Goblin");
        private readonly List<MethodBase> patched=new List<MethodBase>();
        internal bool Ready {get;private set;}
        internal Exception Error {get;private set;}
        internal GoblinHitHooks(HostCombat host)
        {
            this.host=host;
            try
            {
                if(current!=null)throw new InvalidOperationException("Goblin hooks already installed.");
                if(typeof(Main).Module.ModuleVersionId!=new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083"))throw new InvalidOperationException("Goblin requires verified Terraria .8.");
                current=this;
                Install(typeof(Player),"ProcessHitAgainstNPC",nameof(Melee));
                Install(typeof(Projectile),"Damage_PVE_Inner",nameof(ProjectileGate));
                Ready=true;
            }
            catch(Exception e){Error=e;Dispose();}
        }
        private void Install(Type type,string name,string rewrite)
        {
            var method=AccessTools.DeclaredMethod(type,name);if(method==null)throw new MissingMethodException(type.FullName,name);
            patched.Add(method);harmony.Patch(method,transpiler:new HarmonyMethod(typeof(GoblinHitHooks),rewrite));
            if(!Harmony.GetPatchInfo(method).Owners.Contains(harmony.Id))throw new InvalidOperationException("Goblin patch missing.");
        }
        private static IEnumerable<CodeInstruction> Melee(IEnumerable<CodeInstruction> source)
        {return Rewrite(source,false);}
        private static IEnumerable<CodeInstruction> ProjectileGate(IEnumerable<CodeInstruction> source)
        {return Rewrite(source,true);}
        private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> source,bool projectile)
        {
            var code=source.ToList();var friendly=AccessTools.Field(typeof(NPC),"friendly");
            int guide=code.FindIndex(c=>c.LoadsField(AccessTools.Field(typeof(Player),"killGuide")));
            int clothier=code.FindIndex(c=>c.LoadsField(AccessTools.Field(typeof(Player),"killClothier")));
            var matches=Enumerable.Range(0,Math.Max(0,guide)).Where(i=>code[i].LoadsField(friendly)).ToArray();
            if(guide<0 || clothier<=guide || clothier>90 || matches.Length!=1)throw new InvalidOperationException("Goblin friendly gate shape changed.");
            int at=matches[0];
            // The stack already holds vanilla's friendly boolean. For melee
            // local 0 is the original Main.npc[npcIndex]; projectile arg 2 is
            // that call's target. No NPC field or later policy read is mutated.
            if(projectile ? code[at-1].opcode!=OpCodes.Ldarg_2 : code[at-1].opcode!=OpCodes.Ldloc_0)
                throw new InvalidOperationException("Goblin target identity load changed.");
            code.InsertRange(at+1,new[]{new CodeInstruction(OpCodes.Ldarg_0),new CodeInstruction(projectile?OpCodes.Ldarg_2:OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(GoblinHitHooks),projectile?nameof(FriendlyProjectile):nameof(FriendlyMelee)))});
            return code;
        }
        private static bool Allowed(Player player,NPC target)
        {
            var self=current;
            return self!=null && self.Ready && self.host.IsEnabled(7) && self.host.Runtime.IsSessionActive && Main.netMode!=2 &&
                ReferenceEquals(player,self.host.Player) && player.whoAmI==Main.myPlayer && target!=null && target.type==107;
        }
        private static bool FriendlyMelee(bool friendly,Player player,NPC target)
        {if(!friendly)return false;try{return !Allowed(player,target);}catch(Exception e){current?.Failed(e);return friendly;}}
        private static bool FriendlyProjectile(bool friendly,Projectile shot,NPC target)
        {if(!friendly)return false;try{return !(shot.owner==Main.myPlayer && shot.owner>=0 && shot.owner<Main.maxPlayers && Allowed(Main.player[shot.owner],target));}catch(Exception e){current?.Failed(e);return friendly;}}
        // A failed local policy must leave the original hit chain intact. Do
        // not recompile/unpatch a native method while it is on the stack.
        private void Failed(Exception error){Error=error;Ready=false;}
        public void Dispose()
        {
            Ready=false;
            foreach(var method in patched){try{harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);}catch(Exception e){if(Error==null)Error=e;}}
            patched.Clear();if(ReferenceEquals(current,this))current=null;
        }
    }
}
