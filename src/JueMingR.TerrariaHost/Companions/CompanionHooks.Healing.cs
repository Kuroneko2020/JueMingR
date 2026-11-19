using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal static partial class CompanionHooks
    {
        private static bool ReceivesSharedHealFromServer()
        {
            return Main.netMode == 1 && current?.Actor.Connection != null;
        }

        private static IEnumerable<CodeInstruction> DeferSharedProjectileHeal(IEnumerable<CodeInstruction> input, ILGenerator generator)
        {
            var code = input.ToList();
            var effect = AccessTools.Method(typeof(Player), nameof(Player.HealEffect), new[] { typeof(int), typeof(bool) });
            var playerArray = AccessTools.Field(typeof(Main), nameof(Main.player));
            var life = AccessTools.Field(typeof(Player), nameof(Player.statLife));
            var send = AccessTools.Method(typeof(NetMessage), nameof(NetMessage.SendData));
            int heal = code.FindIndex(c => c.Calls(effect));
            if (heal < 0 || code.FindIndex(heal + 1, c => c.Calls(effect)) >= 0)
                throw new InvalidOperationException("Companion projectile heal ABI: expected one native effect.");
            int begin = code.FindLastIndex(heal - 1, c => c.LoadsField(playerArray));
            int sendCall = code.FindIndex(heal + 1, c => c.Calls(send));
            int sendStart = sendCall < 0 ? -1 : code.FindLastIndex(sendCall - 1, c => c.LoadsConstant(66));
            if (begin < 0 || sendStart <= heal || code.Skip(heal + 1).Take(sendStart - heal - 1).Count(c => c.StoresField(life)) != 2)
                throw new InvalidOperationException("Companion projectile heal ABI: expected a life addition and cap before message 66.");

            // Native style 52 keeps its own owner, moon-leech and contact
            // checks. The secondary connection sends its ordinary 66; the
            // server broadcasts to the human connection, which owns the
            // shared target's life/effect once. Single player and human-origin
            // heals keep their original immediate effect and native send.
            var destination = generator.DefineLabel();
            code[sendStart].labels.Add(destination);
            var guard = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CompanionHooks), nameof(ReceivesSharedHealFromServer)));
            guard.labels.AddRange(code[begin].labels);
            code[begin].labels.Clear();
            guard.blocks.AddRange(code[begin].blocks);
            code[begin].blocks.Clear();
            code.InsertRange(begin, new[] { guard, new CodeInstruction(OpCodes.Brtrue, destination) });
            return code;
        }
    }
}
