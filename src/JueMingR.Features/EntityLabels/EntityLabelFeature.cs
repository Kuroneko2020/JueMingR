using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingR.Platform.Entities;
using JueMingR.Platform.Runtime;

namespace JueMingR.Features.EntityLabels
{
    public struct EntityLabel
    {
        public EntityLabelKind Kind;
        public int AnchorSlot, SourceSlot, Rgb, NameSize, HealthSize;
        public float X, Y, Height;
        public string Name, Health;
    }

    public sealed class EntityLabelFeature : IRuntimeFeature
    {
        private readonly IEntityObservationSource source;
        private readonly EnemyLabelGroups groups = new EnemyLabelGroups();
        private readonly List<EntityLabel> labels = new List<EntityLabel>();
        private readonly System.Collections.ObjectModel.ReadOnlyCollection<EntityLabel> view;
        private EntityLabelSettings settings = EntityLabelSettings.Default;
        private int[] lastLife = new int[0], lastMax = new int[0];
        private string[] healthText = new string[0];
        private bool active;
        public EntityLabelFeature(IEntityObservationSource source)
        { this.source = source ?? throw new ArgumentNullException(nameof(source)); view = labels.AsReadOnly(); }
        public IReadOnlyList<EntityLabel> Labels { get { return view; } }
        public bool Enabled { get { return settings.AnyEnabled && !HasFailed; } }
        public bool HasFailed { get; private set; }
        public string UnavailableReason { get; private set; }
#if DEBUG
        // Test-only observation; never part of the Release payload.
        public int GroupingPasses { get; private set; }
#endif
        public int UnresolvedGroups { get; private set; }
        public void Configure(EntityLabelSettings value)
        { settings = value ?? throw new ArgumentNullException(nameof(value)); if (!Enabled) Clear(); }
        public void OnSessionStarted() { active = true; Clear(); }
        public void OnSessionEnded() { active = false; Clear(); }
        public void FailClosed() { HasFailed = true; UnavailableReason = "entity-feature-failed"; Clear(); }
        private void Clear() { labels.Clear(); Array.Clear(healthText, 0, healthText.Length); UnresolvedGroups = 0; }
        public void Update(ulong tick)
        {
            labels.Clear(); UnresolvedGroups = 0;
            if (!active || !Enabled) return;
            var demand = (settings.EnemyEnabled ? EntityObservationDemand.Enemy : 0) |
                (settings.CritterEnabled ? EntityObservationDemand.Critter : 0) | (settings.NpcMode != NpcLabelMode.Off ? EntityObservationDemand.Npc : 0);
            EntityObservation observation;
            if (!source.TryObserve(demand, out observation)) { UnavailableReason = "entity-observation-unavailable"; return; }
            UnavailableReason = null;
            // The real Host is bounded by the engine slot array; reject an invalid
            // capability result instead of allocating arbitrary-size scratch.
            if (observation.Count > 4096) { FailClosed(); return; }
            if (healthText.Length != observation.Count)
            { healthText = new string[observation.Count]; lastLife = new int[observation.Count]; lastMax = new int[observation.Count]; }
            if (settings.EnemyEnabled)
            {
                groups.Build(observation);
#if DEBUG
                GroupingPasses++;
#endif
                UnresolvedGroups = groups.Unresolved;
            }
            for (int i = 0; i < observation.Count; i++)
            {
                EntityFact fact = observation[i];
                if (!fact.Active || fact.Excluded) continue;
                EntityLabelKind kind = fact.TownNpc || fact.SkeletonMerchant ? EntityLabelKind.Npc : fact.Critter ? EntityLabelKind.Critter : EntityLabelKind.Enemy;
                if (!settings.Enabled(kind) || kind == EntityLabelKind.Enemy && fact.Friendly) continue;
                if (kind == EntityLabelKind.Enemy)
                {
                    int root = groups.Owner(i);
                    if (root >= 0)
                    {
                        if (root != i) continue;
                        int selected = groups.Anchor(root);
                        if (selected >= 0) Add(kind, root, selected, fact, observation[selected]);
                        continue;
                    }
                    // A member with an unresolved relationship is not a standalone
                    // health source. Truly independent parts keep realLife=-1.
                    if (fact.HealthOwnerSlot >= 0 || fact.Role != EntitySegmentRole.None || !EnemyLabelGroups.Healthy(fact)) continue;
                }
                if (fact.DrawEligible && fact.Visible) Add(kind, i, i, fact, fact);
            }
        }
        internal static bool IsEnemy(EntityFact fact)
        { return fact.Active && !fact.Excluded && !fact.TownNpc && !fact.SkeletonMerchant && !fact.Critter && !fact.Friendly; }
        private void Add(EntityLabelKind kind, int sourceSlot, int anchorSlot, EntityFact fact, EntityFact anchor)
        {
            string name = kind == EntityLabelKind.Npc && settings.NpcMode == NpcLabelMode.Name && !String.IsNullOrWhiteSpace(fact.GivenName) ? fact.GivenName : fact.TypeName;
            if (String.IsNullOrWhiteSpace(name)) return;
            EntityLabelStyle style = settings.Style(kind);
            string health = null;
            if (kind == EntityLabelKind.Enemy)
            {
                if (healthText[sourceSlot] == null || lastLife[sourceSlot] != fact.Life || lastMax[sourceSlot] != fact.LifeMax)
                {
                    lastLife[sourceSlot] = fact.Life; lastMax[sourceSlot] = fact.LifeMax;
                    healthText[sourceSlot] = fact.Life.ToString(CultureInfo.InvariantCulture) + "/" + fact.LifeMax.ToString(CultureInfo.InvariantCulture);
                }
                health = healthText[sourceSlot];
            }
            labels.Add(new EntityLabel { Kind = kind, AnchorSlot = anchorSlot, SourceSlot = sourceSlot, Name = name, Health = health,
                X = anchor.X, Y = anchor.Y, Height = anchor.Height, Rgb = kind == EntityLabelKind.Critter && fact.Gold ? 0xFFD700 : style.Rgb,
                NameSize = style.NameSize, HealthSize = style.HealthSize });
        }
    }
}
