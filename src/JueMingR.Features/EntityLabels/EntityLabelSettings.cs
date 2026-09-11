using System;

namespace JueMingR.Features.EntityLabels
{
    public enum EntityLabelKind { Enemy, Critter, Npc }
    public enum NpcLabelMode { Off, Name, Type }

    public sealed class EntityLabelStyle : IEquatable<EntityLabelStyle>
    {
        public EntityLabelStyle(int rgb, int nameSize)
        {
            if (rgb < 0 || rgb > 0xFFFFFF || nameSize < 50 || nameSize > 180) throw new ArgumentOutOfRangeException();
            Rgb = rgb; NameSize = nameSize;
        }
        public int Rgb { get; private set; }
        // Hundredths are the stored user unit, avoiding repeated float additions.
        public int NameSize { get; private set; }
        public int HealthSize { get { return Math.Max(50, NameSize - 13); } }
        public EntityLabelStyle WithColor(int rgb) { return new EntityLabelStyle(rgb, NameSize); }
        public EntityLabelStyle StepSize(int direction)
        { return new EntityLabelStyle(Rgb, Math.Max(50, Math.Min(180, NameSize + Math.Sign(direction) * 10))); }
        public bool Equals(EntityLabelStyle other) { return other != null && other.Rgb == Rgb && other.NameSize == NameSize; }
        public override bool Equals(object obj) { return Equals(obj as EntityLabelStyle); }
        public override int GetHashCode() { return Rgb ^ NameSize; }
    }

    public sealed class EntityLabelSettings : IEquatable<EntityLabelSettings>
    {
        public static readonly EntityLabelSettings Default = new EntityLabelSettings(false, false, NpcLabelMode.Off, NpcLabelMode.Name,
            new EntityLabelStyle(0xCD5C5C, 90), new EntityLabelStyle(0x5DADEC, 90), new EntityLabelStyle(0x90EE90, 90));
        public EntityLabelSettings(bool enemyEnabled, bool critterEnabled, NpcLabelMode npcMode, NpcLabelMode lastNpcMode,
            EntityLabelStyle enemy, EntityLabelStyle critter, EntityLabelStyle npc)
        {
            if (npcMode < NpcLabelMode.Off || npcMode > NpcLabelMode.Type || lastNpcMode < NpcLabelMode.Name || lastNpcMode > NpcLabelMode.Type ||
                npcMode != NpcLabelMode.Off && npcMode != lastNpcMode) throw new ArgumentOutOfRangeException(nameof(npcMode));
            EnemyEnabled = enemyEnabled; CritterEnabled = critterEnabled; NpcMode = npcMode; LastNpcMode = lastNpcMode;
            EnemyStyle = enemy ?? throw new ArgumentNullException(nameof(enemy));
            CritterStyle = critter ?? throw new ArgumentNullException(nameof(critter));
            NpcStyle = npc ?? throw new ArgumentNullException(nameof(npc));
        }
        public bool EnemyEnabled { get; private set; }
        public bool CritterEnabled { get; private set; }
        public NpcLabelMode NpcMode { get; private set; }
        public NpcLabelMode LastNpcMode { get; private set; }
        public EntityLabelStyle EnemyStyle { get; private set; }
        public EntityLabelStyle CritterStyle { get; private set; }
        public EntityLabelStyle NpcStyle { get; private set; }
        public bool AnyEnabled { get { return EnemyEnabled || CritterEnabled || NpcMode != NpcLabelMode.Off; } }
        public bool Enabled(EntityLabelKind kind)
        { return kind == EntityLabelKind.Enemy ? EnemyEnabled : kind == EntityLabelKind.Critter ? CritterEnabled : NpcMode != NpcLabelMode.Off; }
        public EntityLabelStyle Style(EntityLabelKind kind)
        { return kind == EntityLabelKind.Enemy ? EnemyStyle : kind == EntityLabelKind.Critter ? CritterStyle : NpcStyle; }
        public EntityLabelSettings WithEnabled(EntityLabelKind kind, bool enabled)
        {
            if (kind == EntityLabelKind.Npc) return WithNpcMode(enabled ? LastNpcMode : NpcLabelMode.Off);
            return new EntityLabelSettings(kind == EntityLabelKind.Enemy ? enabled : EnemyEnabled,
                kind == EntityLabelKind.Critter ? enabled : CritterEnabled, NpcMode, LastNpcMode, EnemyStyle, CritterStyle, NpcStyle);
        }
        public EntityLabelSettings Toggle(EntityLabelKind kind) { return WithEnabled(kind, !Enabled(kind)); }
        public EntityLabelSettings WithNpcMode(NpcLabelMode mode)
        { return new EntityLabelSettings(EnemyEnabled, CritterEnabled, mode, mode == NpcLabelMode.Off ? LastNpcMode : mode, EnemyStyle, CritterStyle, NpcStyle); }
        public EntityLabelSettings WithStyle(EntityLabelKind kind, EntityLabelStyle style)
        { return new EntityLabelSettings(EnemyEnabled, CritterEnabled, NpcMode, LastNpcMode,
            kind == EntityLabelKind.Enemy ? style : EnemyStyle, kind == EntityLabelKind.Critter ? style : CritterStyle, kind == EntityLabelKind.Npc ? style : NpcStyle); }
        public EntityLabelSettings ResetStyle(EntityLabelKind kind) { return WithStyle(kind, Default.Style(kind)); }
        public bool Equals(EntityLabelSettings other)
        { return other != null && other.EnemyEnabled == EnemyEnabled && other.CritterEnabled == CritterEnabled && other.NpcMode == NpcMode &&
            other.LastNpcMode == LastNpcMode && other.EnemyStyle.Equals(EnemyStyle) && other.CritterStyle.Equals(CritterStyle) && other.NpcStyle.Equals(NpcStyle); }
        public override bool Equals(object obj) { return Equals(obj as EntityLabelSettings); }
        public override int GetHashCode()
        { return EnemyStyle.GetHashCode() ^ CritterStyle.GetHashCode() ^ NpcStyle.GetHashCode() ^ (int)NpcMode ^ ((int)LastNpcMode << 2) ^ (EnemyEnabled ? 16 : 0) ^ (CritterEnabled ? 32 : 0); }
    }
}
