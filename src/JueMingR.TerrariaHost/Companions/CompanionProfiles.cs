using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using JueMingR.Features.Companions;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Onboarding;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Creative;
using Terraria.IO;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed class CompanionProfiles : IDisposable
    {
        private const int NativeRelease = 326;
        private static readonly MethodInfo Serialize = AccessTools.Method(typeof(Player), "Serialize", new[] { typeof(PlayerFileData), typeof(Player), typeof(BinaryWriter) });
        private static readonly MethodInfo Deserialize = AccessTools.Method(typeof(Player), "Deserialize", new[] { typeof(PlayerFileData), typeof(Player), typeof(BinaryReader), typeof(int), typeof(bool).MakeByRefType() });
        private static readonly MethodInfo SaveTemporary = AccessTools.Method(typeof(Player), "SaveTemporaryItemSlotContents");
        private static readonly MethodInfo SaveRefunds = AccessTools.Method(typeof(CraftingRequests), "SavePossibleRefunds");
        private static readonly MethodInfo SaveCreative = AccessTools.Method(typeof(CreativePowerManager), "SaveToPlayer");
        private static readonly MethodInfo ResetCreative = AccessTools.Method(typeof(CreativePowerManager), "ResetDataForNewPlayer");
        [ThreadStatic] private static Player serializing;
        [ThreadStatic] private static bool loading;
        private static bool installed;
        private static CompanionProfiles migrationTarget;
        private readonly ConditionalWeakTable<Player, PlayerFileData> files = new ConditionalWeakTable<Player, PlayerFileData>();
        private readonly CompanionProfileManager manager;

        internal CompanionProfiles(ICompanionArchive archive)
        { manager = new CompanionProfileManager(archive); migrationTarget = this; }
        internal IReadOnlyList<CompanionProfile> Profiles { get { return manager.Profiles; } }
        internal Guid OwnerId { get { return manager.OwnerId; } }
        internal bool Ready { get { return manager.Ready && installed; } }
        internal string Error { get { return manager.Error; } }
        internal void SetOwner(string key) { manager.SetOwner(key); }
        internal void Pump() { manager.Pump(); }
        internal CompanionProfile Find(Guid id) { return manager.Find(id); }
        internal bool IsSaved(Guid id) { return manager.IsSaved(id); }
        internal bool SaveFailed(Guid id) { return manager.SaveFailed(id); }
        internal string SaveError(Guid id) { return manager.SaveError(id); }
        internal void RetrySave(Guid id) { manager.RetrySave(id); }
        internal void RetryLoad() { manager.RetryLoad(); }
        internal bool TryReserve(CompanionProfile profile, out long generation) { return manager.TryReserve(profile.Id, out generation); }
        internal void Release(CompanionProfile profile, long generation) { manager.Release(profile.Id, generation); }
        internal bool OwnsActivation(Guid id, long generation) { return manager.OwnsActivation(id, generation); }

        internal CompanionProfile Create(string name, bool journey)
        {
            if (!Ready) throw new InvalidOperationException("队友档案尚未就绪。");
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > Player.nameLen) throw new InvalidOperationException("请输入 1 至 20 个字符的人物名字。");
            var existing = manager.FindName(name);
            if (existing != null) throw new InvalidOperationException("已有此人物，请在左侧选择。");
            Player body = NewCharacter(name, journey);
            var profile = new CompanionProfile(Guid.NewGuid(), OwnerId, name, 1, 1, new CompanionBehavior(), Capture(body));
            manager.Add(profile); return profile;
        }

        internal Player LoadBody(CompanionProfile profile)
        {
            if (!installed) throw new InvalidOperationException("队友人物序列化接点不可用。");
            Player body = new Player { whoAmI = 255 };
            var file = new PlayerFileData { Player = body };
            using (var stream = new MemoryStream(profile.NativeData, false))
            using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
            {
                if (reader.ReadInt32() != NativeRelease) throw new InvalidDataException("队友人物数据与当前原版版本不符。");
                if (loading) throw new InvalidOperationException("Nested companion character load.");
                loading = true;
                try { Deserialize.Invoke(null, new object[] { file, body, reader, NativeRelease, false }); }
                finally { loading = false; }
                if (stream.Position != stream.Length) throw new InvalidDataException("队友人物数据含未读取内容。");
            }
            if (!String.Equals(body.name, profile.Name, StringComparison.Ordinal)) throw new InvalidDataException("队友人物名称与档案不符。");
            files.Add(body, file); body.PlayerFrame(); return body;
        }
        internal void BeginPlay(Player body) { File(body).StartPlayTimer(); }
        internal void EndPlay(Player body) { File(body).PausePlayTimer(); }
        internal CompanionProfile CaptureAndSave(Guid id, Player body, long activation)
        {
            var current = manager.Find(id);
            if (current == null || !manager.OwnsActivation(id, activation)) throw new InvalidOperationException("队友实例已失效，未保存旧身体。");
            var revised = current.Revise(Capture(body)); manager.Update(revised); return revised;
        }
        internal CompanionProfile SetBehavior(Guid id, CompanionBehavior behavior)
        {
            var current = manager.Find(id);
            if (current == null) throw new InvalidOperationException("未找到自己的队友。");
            var revised = current.Revise(current.NativeData, behavior); manager.Update(revised); return revised;
        }
        internal CompanionProfile Reset(Guid id)
        {
            var current = manager.Find(id);
            if (current == null) throw new InvalidOperationException("未找到自己的队友。");
            Player previous = LoadBody(current), body = NewCharacter(current.Name, previous.difficulty == 3);
            body.hair = previous.hair; body.hairDye = previous.hairDye; body.skinVariant = previous.skinVariant;
            body.hairColor = previous.hairColor; body.skinColor = previous.skinColor; body.eyeColor = previous.eyeColor;
            body.shirtColor = previous.shirtColor; body.underShirtColor = previous.underShirtColor;
            body.pantsColor = previous.pantsColor; body.shoeColor = previous.shoeColor;
            body.voiceVariant = previous.voiceVariant; body.voicePitchOffset = previous.voicePitchOffset;
            var revised = current.Revise(Capture(body), new CompanionBehavior(), reset: true);
            manager.Update(revised); return revised;
        }
        private PlayerFileData File(Player body)
        { return files.GetValue(body, player => new PlayerFileData { Player = player }); }
        private byte[] Capture(Player body)
        {
            if (!installed || serializing != null) throw new InvalidOperationException("队友人物序列化接点不可用。");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
            {
                writer.Write(NativeRelease); serializing = body;
                try { Serialize.Invoke(null, new object[] { File(body), body, writer }); }
                finally { serializing = null; }
                writer.Flush(); return stream.ToArray();
            }
        }
        private static Player NewCharacter(string name, bool journey)
        {
            var body = new Player { whoAmI = 255, name = name, difficulty = (byte)(journey ? 3 : 0), statLife = 100, statLifeMax = 100, statMana = 20, statManaMax = 20 };
            body.savedPerPlayerFieldsThatArentInThePlayerClass = new Player.SavedPlayerDataWithAnnoyingRules();
            // UICharacterCreation's 1.4.5.8 defaults, without constructing a menu
            // or invoking CreateAndSave against a real player's directory. This
            // path is reached only for initial creation and an explicit reset.
            int slot = 0;
            int[] starter = journey ? new[] { 6, 1, 10, 7, 4281 } : new[] { 3507, 3509, 3506 };
            foreach (int type in starter) { body.inventory[slot].SetDefaults(type); body.inventory[slot++].Prefix(-1); }
            if (journey)
            {
                body.inventory[slot].SetDefaults(8); body.inventory[slot++].stack = 100;
                body.inventory[slot].SetDefaults(965); body.inventory[slot++].stack = 100;
                body.inventory[slot++].SetDefaults(50); body.inventory[slot++].SetDefaults(84);
                body.armor[3].SetDefaults(4978); body.armor[3].Prefix(-1);
                if (name.Equals("wolf pet", StringComparison.OrdinalIgnoreCase) || name.Equals("wolfpet", StringComparison.OrdinalIgnoreCase)) body.miscEquips[3].SetDefaults(5130);
                body.AddBuff(216, 3600);
            }
            if (Main.runningCollectorsEdition) body.inventory[slot].SetDefaults(603);
            CreativePowerManager.Instance.ResetPowersForPlayer(body); return body;
        }

        internal static void InstallSerializationHooks(Harmony harmony)
        {
            if (installed) return;
            if (Serialize == null || Deserialize == null || SaveTemporary == null || SaveRefunds == null || SaveCreative == null || ResetCreative == null)
                throw new MissingMethodException("队友人物序列化接口与当前原版不符。");
            harmony.Patch(Serialize, transpiler: new HarmonyMethod(typeof(CompanionProfiles), nameof(SerializationCalls)));
            harmony.Patch(Deserialize, transpiler: new HarmonyMethod(typeof(CompanionProfiles), nameof(DeserializationCalls)));
            foreach (string method in new[] { "MoveToCloud", "MoveToLocal" })
                harmony.Patch(AccessTools.Method(typeof(PlayerFileData), method), prefix: new HarmonyMethod(typeof(CompanionProfiles), nameof(BeforeMove)), postfix: new HarmonyMethod(typeof(CompanionProfiles), nameof(AfterMove)));
            installed = true;
        }
        private static IEnumerable<CodeInstruction> SerializationCalls(IEnumerable<CodeInstruction> instructions)
        {
            int replacements = 0;
            foreach (var original in instructions)
            {
                var instruction = new CodeInstruction(original);
                if (instruction.Calls(SaveTemporary)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(CompanionProfiles), nameof(SaveTemporaryForActor)); replacements++; }
                else if (instruction.Calls(SaveRefunds)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(CompanionProfiles), nameof(SaveRefundsForActor)); replacements++; }
                else if (instruction.Calls(SaveCreative)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(CompanionProfiles), nameof(SaveCreativeForActor)); replacements++; }
                yield return instruction;
            }
            if (replacements != 3) throw new InvalidOperationException("队友人物保存接点不符。");
        }
        private static IEnumerable<CodeInstruction> DeserializationCalls(IEnumerable<CodeInstruction> instructions)
        {
            int replacements = 0;
            foreach (var original in instructions)
            {
                var instruction = new CodeInstruction(original);
                if (instruction.Calls(ResetCreative)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(CompanionProfiles), nameof(ResetCreativeForActor)); replacements++; }
                yield return instruction;
            }
            if (replacements != 1) throw new InvalidOperationException("队友人物读取接点不符。");
        }
        private static void SaveTemporaryForActor(Player body, BinaryWriter writer)
        {
            if (ReferenceEquals(serializing, body)) writer.Write((byte)0);
            else SaveTemporary.Invoke(body, new object[] { writer });
        }
        private static void SaveRefundsForActor(BinaryWriter writer)
        {
            if (serializing != null) writer.Write(0);
            else SaveRefunds.Invoke(null, new object[] { writer });
        }
        private static void SaveCreativeForActor(CreativePowerManager powers, Player body, BinaryWriter writer)
        {
            if (!ReferenceEquals(serializing, body)) { powers.SaveToPlayer(body, writer); return; }
            // Native IPersistentPerPlayerContent.Save reads Main.myPlayer and a
            // global slider. Use this character's loaded fields at that exact
            // seam; never borrow the owner's Journey powers or reset them.
            var fields = body.savedPerPlayerFieldsThatArentInThePlayerClass ?? new Player.SavedPlayerDataWithAnnoyingRules();
            writer.Write(true); writer.Write(powers.GetPowerId<CreativePowers.GodmodePower>()); writer.Write(fields.godmodePowerEnabled);
            writer.Write(true); writer.Write(powers.GetPowerId<CreativePowers.FarPlacementRangePower>()); writer.Write(fields.farPlacementRangePowerEnabled);
            writer.Write(true); writer.Write(powers.GetPowerId<CreativePowers.SpawnRateSliderPerPlayerPower>()); writer.Write(fields.spawnRatePowerSliderValue);
            writer.Write(false);
        }
        private static void ResetCreativeForActor(CreativePowerManager powers, Player body)
        { if (loading) powers.ResetPowersForPlayer(body); else powers.ResetDataForNewPlayer(body); }
        private static void BeforeMove(PlayerFileData __instance, out string __state)
        { __state = String.IsNullOrEmpty(__instance.Path) ? null : CharacterIdentity.Key(__instance.Path, __instance.IsCloudSave); }
        private static void AfterMove(PlayerFileData __instance, string __state)
        {
            if (migrationTarget == null || __state == null || String.IsNullOrEmpty(__instance.Path)) return;
            string next = CharacterIdentity.Key(__instance.Path, __instance.IsCloudSave);
            if (next != __state) migrationTarget.manager.MigrateOwner(__state, next);
        }
        public void Dispose()
        { if (ReferenceEquals(migrationTarget, this)) migrationTarget = null; manager.Dispose(); }
    }
}
