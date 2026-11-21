using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    // A draft is never an actor, archive or source of starter items. Native
    // creation resources are issued once, only after validated submission.
    internal static class CompanionCreation
    {
        internal static readonly IReadOnlyList<int> Hairstyles = BuildHairstyles();
        private static IReadOnlyList<int> BuildHairstyles()
        {
            // 1.4.5.8 HairstyleUnlocksHelper's creation branch. Do not toggle
            // Main.gameMenu or borrow the world's current stylist unlock list.
            var values = new List<int>();
            for (int i = 0; i < 51; i++) values.Add(i);
            values.AddRange(new[] {136,137,138,139,140,141,142,143,144,147,148,149,150,151,154,155,157,158,161});
            for (int i = 51; i < 123; i++) values.Add(i);
            values.AddRange(new[] {134,135,146,152,153,156,159,165,160});
            for (int i = 166; i < 228; i++) values.Add(i);
            return values.AsReadOnly();
        }
        internal static Player Draft(string name, byte difficulty)
        { return new Player { whoAmI = 255, name = name ?? "", difficulty = difficulty, voiceVariant = 1, statLife = 100, statLifeMax = 100, statMana = 20, statManaMax = 20 }; }
        internal static void Validate(Player draft)
        {
            if (draft == null) throw new InvalidOperationException("请先创建人物草稿。");
            string name = (draft.name ?? "").Trim();
            if (name.Length == 0 || name.Length > Player.nameLen) throw new InvalidOperationException("请输入 1 至 " + Player.nameLen + " 个字符的人物名字。");
            if (draft.difficulty > 3 || !Hairstyles.Contains(draft.hair) || draft.skinVariant < 0 || draft.skinVariant >= 10 ||
                !PlayerVoiceID.VariantOrder.Contains(draft.voiceVariant) || Single.IsNaN(draft.voicePitchOffset) || Single.IsInfinity(draft.voicePitchOffset) || draft.voicePitchOffset < -1 || draft.voicePitchOffset > 1)
                throw new InvalidOperationException("人物外观、声音或难度无效，请重新选择。");
        }
        internal static void CopyAppearance(Player source, Player target)
        {
            target.hair = source.hair; target.hairDye = source.hairDye; target.skinVariant = source.skinVariant;
            target.hairColor = source.hairColor; target.eyeColor = source.eyeColor; target.skinColor = source.skinColor;
            target.shirtColor = source.shirtColor; target.underShirtColor = source.underShirtColor;
            target.pantsColor = source.pantsColor; target.shoeColor = source.shoeColor;
            target.voiceVariant = source.voiceVariant; target.voicePitchOffset = source.voicePitchOffset;
        }
        internal static void Randomize(Player draft)
        {
            if (draft == null) return;
            var random = Main.rand;
            draft.hair = Hairstyles[random.Next(Hairstyles.Count)]; draft.skinVariant = random.Next(10);
            draft.hairColor = RandomColor(); draft.eyeColor = Color.Lerp(Color.Black, RandomColor(), .5f);
            float shade = Math.Min(1f, random.Next(60,120) * .01f);
            draft.skinColor = new Color((int)(random.Next(240,255)*shade),(int)(random.Next(110,140)*shade),(int)(random.Next(75,110)*shade));
            draft.shirtColor = RandomColor(); draft.underShirtColor = RandomColor(); draft.pantsColor = RandomColor(); draft.shoeColor = RandomColor();
            draft.voiceVariant = PlayerVoiceID.VariantOrder[random.Next(PlayerVoiceID.VariantOrder.Length)];
            draft.voicePitchOffset = (float)random.NextDouble() * 2 - 1;
        }
        private static Color RandomColor() { return Main.hslToRgb((float)Main.rand.NextDouble(), (float)Main.rand.NextDouble(), .15f + .7f * (float)Main.rand.NextDouble()); }
    }

    internal sealed partial class HostCompanions
    {
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Player, object> submittedDrafts = new System.Runtime.CompilerServices.ConditionalWeakTable<Player, object>();
        internal Player NewCreationDraft() { return CompanionCreation.Draft("", (byte)(Main.GameMode == 3 ? 3 : 0)); }
        internal IReadOnlyList<int> GetCreationHairstyles() { return CompanionCreation.Hairstyles; }
        internal void RandomizeCreation(Player draft) { CompanionCreation.Randomize(draft); }
        internal void PreviewCreationVoice(Player draft)
        {
            // Explicit UI release already owns one request. World time may be
            // paused while the character editor remains interactive.
            if (draft == null) return;
            Vector2 prior = draft.position;
            try { draft.position = new Vector2(-1,-1); draft.PlayHurtSound(); }
            finally { draft.position = prior; }
        }
        internal bool CreateDraft(Player draft)
        {
            if (!CanCreate) return Result(false,ArchiveNotice??"队友档案尚未就绪。");
            if (draft == null) return Result(false,"请先创建人物草稿。");
            object accepted;
            if (submittedDrafts.TryGetValue(draft,out accepted)) return Result(false,"此人物已经创建。");
            try { var profile=profiles.CreateDraft(draft); submittedDrafts.Add(draft,new object()); return Created(profile); }
            catch (Exception e) { return Result(false,e.Message); }
        }
    }
}
