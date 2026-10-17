using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Explicit native-hand COMPONENT allowance, never a Main scheduling test.
    // Player.Update can replace a previous-world test plan at its real entry.
    // Bind the existing synthetic121 fixture at the actual Shoot phase instead
    // of relying on a cold ordinary preparation or reviving an idle preview.
    // The unmodified consumer still decides permission, borrowing and birth.
    internal sealed class NativeCombatHandFixture:IDisposable
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NativeCombatHandFixture active;private static int sequence;
        private readonly NativeCombatHandFixture prior;private readonly Harmony audit;
        private readonly object context;private readonly Func<bool> enabled;private readonly Func<NPC> target;
        private readonly Action<AttackContact> captured;
        internal NativeCombatHandFixture(object context,Func<bool> enabled,Func<NPC> target,Action<AttackContact> captured=null)
        {
            this.context=context;this.enabled=enabled;this.target=target;this.captured=captured;prior=active;active=this;
            audit=new Harmony("JueMingR.Tests.NativeHandComponent."+(++sequence));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_Shoot",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHandFixture),nameof(Prepare)){priority=Priority.First});
        }
        private static void Prepare(Player __instance)
        {
            var owner=active;if(owner==null || !owner.enabled() || !ReferenceEquals(__instance,Main.LocalPlayer))return;
            var body=owner.target();if(body==null)return;var host=Get(owner.context,"CombatObservation");var attack=Get(Get(owner.context,"Combat"),"Attack");
            if(!(bool)Get(attack,"BasicPermission"))return; // Never create permission during OFF/UI/reset native actions.
            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,body,0,phaseName:"BeforeNpc");
            owner.captured?.Invoke(GetOptional(attack,"ExpectedImpact") as AttackContact);
        }
        public void Dispose(){audit.UnpatchAll(audit.Id);active=prior;}
    }
}
