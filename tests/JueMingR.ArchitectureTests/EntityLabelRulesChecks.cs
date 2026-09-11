using System;
using System.Collections.Generic;
using JueMingR.Features.EntityLabels;
using JueMingR.Platform.Entities;

namespace JueMingR.ArchitectureTests
{
    internal static class EntityLabelRulesChecks
    {
        internal static void Check(IList<string> failures)
        {
            try
            {
                var source = new Source();
                var feature = new EntityLabelFeature(source);
                feature.OnSessionStarted(); feature.Update(1);
                Require(source.Reads == 0 && feature.Labels.Count == 0, "all off must not scan");
                source.Facts[0] = Fact("兔", 10, 10); source.Facts[0].Critter = true; source.Facts[0].Gold = true;
                source.Facts[1] = Fact("护士", 100, 100); source.Facts[1].TownNpc = true; source.Facts[1].Critter = true; source.Facts[1].GivenName = "小红";
                source.Facts[2] = Fact("受缚哥布林", 100, 100); source.Facts[2].Friendly = true;
                source.Facts[3] = Fact("史莱姆", 23, 30);
                feature.Configure(EntityLabelSettings.Default.WithEnabled(EntityLabelKind.Critter, true).WithNpcMode(NpcLabelMode.Name));
                feature.Update(2);
                Require(source.LastDemand == (EntityObservationDemand.Critter | EntityObservationDemand.Npc), "no enemy relationship work for name-only consumers");
                Require(feature.GroupingPasses == 0 && feature.Labels.Count == 2, "category overlap produces one label and excludes arbitrary friendly NPC");
                Require(feature.Labels[0].Rgb == 0xFFD700 && feature.Labels[1].Name == "小红", "gold and personal names use different rules");
                feature.Configure(EntityLabelSettings.Default.WithEnabled(EntityLabelKind.Enemy, true)); feature.Update(3);
                Require(feature.Labels.Count == 1 && feature.Labels[0].Health == "23/30", "enemy output uses actual current/max health");
                source.Facts[3].Life = 19; source.Facts[3].TypeName = "变形后的名字"; feature.Update(4);
                Require(feature.Labels[0].Name == "变形后的名字" && feature.Labels[0].Health == "19/30", "same slot dynamic name and health refresh");
                source.Facts[3].LifeMax = 0; feature.Update(5);
                Require(feature.Labels.Count == 0, "unknown health is not 0/0");

                Array.Clear(source.Facts, 0, source.Facts.Length);
                source.Facts[7] = Fact("毁灭者", 900, 1000); source.Facts[7].SharedFamily = 134; source.Facts[7].Role = EntitySegmentRole.Head;
                source.Facts[7].HealthOwnerSlot = 7; source.Facts[7].NextSlot = 2; source.Facts[7].Visible = false;
                source.Facts[2] = Fact("身体", 1000, 1000); source.Facts[2].SharedFamily = 134; source.Facts[2].Role = EntitySegmentRole.Body;
                source.Facts[2].HealthOwnerSlot = 7; source.Facts[2].PreviousSlot = 7; source.Facts[2].NextSlot = 5; source.Facts[2].X = 200;
                source.Facts[5] = Fact("尾", 1000, 1000); source.Facts[5].SharedFamily = 134; source.Facts[5].Role = EntitySegmentRole.Tail;
                source.Facts[5].HealthOwnerSlot = 7; source.Facts[5].PreviousSlot = 2; source.Facts[5].X = 100;
                feature.Update(6);
                Require(feature.Labels.Count == 1 && feature.Labels[0].Name == "毁灭者" && feature.Labels[0].Health == "900/1000" && feature.Labels[0].AnchorSlot == 5,
                    "offscreen owner remains health source; nearest visible member anchors regardless of array order");
                source.Facts[2].PreviousSlot = 3; feature.Update(7);
                Require(feature.Labels.Count == 0 && feature.UnresolvedGroups > 0, "bad reciprocal link cannot attach to reused owner slot");
                source.Facts[7].Visible = true; feature.Update(7);
                Require(feature.Labels.Count == 1 && feature.Labels[0].AnchorSlot == 7 && feature.Labels[0].Health == "900/1000", "missing client tail does not hide valid head's own life");
                source.Facts[7].Visible = false;
                source.Facts[2].PreviousSlot = 7; source.Facts[5].Role = EntitySegmentRole.Body; source.Facts[5].NextSlot = 2; feature.Update(8);
                Require(feature.Labels.Count == 0, "cycle cannot create a plausible shared health label");

                Array.Clear(source.Facts, 0, source.Facts.Length);
                source.Facts[0] = Fact("世界吞噬者头", 70, 100); source.Facts[1] = Fact("世界吞噬者体", 12, 50);
                feature.Update(9);
                Require(feature.Labels.Count == 2 && feature.Labels[0].Health == "70/100" && feature.Labels[1].Health == "12/50", "independent segments are never summed or boss-folded");
                feature.OnSessionEnded(); Require(feature.Labels.Count == 0, "world exit removes labels immediately");
                feature.OnSessionStarted(); source.Facts[0].TypeName = "新世界对象"; feature.Update(10);
                Require(feature.Labels[0].Name == "新世界对象", "new world never inherits slot text");
                feature.Configure(EntityLabelSettings.Default); int reads = source.Reads; feature.Update(11);
                Require(source.Reads == reads && feature.Labels.Count == 0, "last disable clears output without another scan");
            }
            catch (Exception e) { failures.Add("entity label rules: " + e.Message); }
        }
        private static EntityFact Fact(string name, int life, int max)
        { return new EntityFact { Active = true, DrawEligible = true, Visible = true, TypeName = name, Life = life, LifeMax = max,
            HealthOwnerSlot = -1, NextSlot = -1, PreviousSlot = -1 }; }
        private sealed class Source : IEntityObservationSource
        {
            internal readonly EntityFact[] Facts = new EntityFact[10];
            internal int Reads;
            internal EntityObservationDemand LastDemand;
            public bool TryObserve(EntityObservationDemand demand, out EntityObservation observation)
            { Reads++; LastDemand = demand; observation = new EntityObservation(Facts, 0, 0); return true; }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
