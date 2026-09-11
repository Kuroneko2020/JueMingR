using System;
using JueMingR.Features.EntityLabels;
using JueMingR.Platform.Entities;
using JueMingR.TerrariaHost.EntityLabels;
using Microsoft.Xna.Framework;

namespace Terraria
{
    internal static class EntityObservationChecks
    {
        internal static void Run()
        {
            Main.npc = new NPC[Main.maxNPCs]; Main.gameMenu = false; Main.dedServ = false; Main.netMode = 1;
            Main.LocalPlayer = new Player { active = true, dead = true }; Main.screenPosition = Vector2.Zero;
            var source = new EntityHostObservation(() => true);
            var feature = new EntityLabelFeature(source); feature.OnSessionStarted();
            Main.npc[0] = Make(17, "商人", 0); Main.npc[0].townNPC = true; Main.npc[0].GivenName = "史蒂夫";
            Main.npc[1] = Make(10, "可捕捉对象", 1); Main.npc[1].catchItem = 20; ID.NPCID.Sets.IsGoldCritter[10] = true;
            Main.npc[2] = Make(ID.NPCID.TargetDummy, "假人", 2);
            Main.npc[3] = Make(ID.NPCID.WindyBalloon, "气球", 3);
            Main.npc[4] = Make(ID.NPCID.BoundTownSlimePurple, "救援气球", 4);
            feature.Configure(EntityLabelSettings.Default.WithEnabled(EntityLabelKind.Enemy, true).WithEnabled(EntityLabelKind.Critter, true).WithNpcMode(NpcLabelMode.Name));
            feature.Update(1);
            Check(feature.Labels.Count == 2 && feature.Labels[0].Name == "史蒂夫" && feature.Labels[1].Rgb == 0xFFD700, "dead active client observes native names and gold metadata without balloon/dummy labels");
            Main.npc[0].netOffset = new Vector2(100, 12); feature.Update(1);
            Check(feature.Labels[0].X == 316 && feature.Labels[0].Y == 262, "world label follows the native interpolated NPC position");
            Array.Clear(Main.npc, 0, Main.npc.Length);
            Main.npc[10] = Make(398, "核心", 10); Main.npc[10].hide = true;
            Main.npc[11] = Make(396, "头", 11); Main.npc[11].hide = true; Main.npc[11].ai[3] = 10;
            Main.npc[12] = Make(397, "左手", 12); Main.npc[12].hide = true; Main.npc[12].ai[3] = 10;
            Main.npc[13] = Make(397, "右手", 13); Main.npc[13].hide = true; Main.npc[13].ai[3] = 10; Main.npc[13].ai[2] = 1;
            feature.Update(2); Check(feature.Labels.Count == 4, "complete active moon parts retain independent life");
            Main.npc[11].ai[0] = -2; feature.Update(3); Check(feature.Labels.Count == 3, "destroyed eye animation must not appear full health");
            Main.npc[10].ai[0] = -2; feature.Update(4); Check(feature.Labels.Count == 0, "teleport phase must not float moon labels");
            Main.npc[10] = Make(398, "新核心", 10); Main.npc[10].hide = true; feature.Update(4);
            Check(feature.Labels.Count == 1 && feature.Labels[0].Name == "新核心", "old moon parts do not attach to replaced core slot");
            Array.Clear(Main.npc, 0, Main.npc.Length);
            Main.npc[6] = Make(113, "血肉墙", 6); Main.npc[6].position.X = -2000;
            Main.npc[8] = Make(114, "眼", 8); Main.npc[8].realLife = 6; Main.wofNPCIndex = 6;
            feature.Update(5); Check(feature.Labels.Count == 1 && feature.Labels[0].AnchorSlot == 8, "wall eye anchors offscreen owner");
            Main.npc[6] = Make(113, "新血肉墙", 6); Main.npc[6].position.X = -2000;
            feature.Update(6); Check(feature.Labels.Count == 0, "same eye cannot silently rebind to replaced owner");
            feature.Configure(EntityLabelSettings.Default); feature.Update(7);
            feature.Configure(EntityLabelSettings.Default.WithEnabled(EntityLabelKind.Enemy, true)); feature.Update(8);
            Check(feature.Labels.Count == 0, "disable/enable does not erase known stale relationship");
            Main.npc[8] = Make(114, "新眼", 8); Main.npc[8].realLife = 6; feature.Update(9);
            Check(feature.Labels.Count == 1 && feature.Labels[0].Name == "新血肉墙", "new member instance can bind new owner");
            Main.npc[6].position.X = -76; feature.Update(10);
            Check(feature.Labels.Count == 1 && feature.Labels[0].AnchorSlot == 8, "offscreen root inside scan padding cannot displace a visible member");
            Array.Clear(Main.npc, 0, Main.npc.Length); source.EndSession();
            for (int start = 20; start <= 30; start += 10)
            {
                Main.npc[start] = Make(7, "吞噬者", start); Main.npc[start].position.X = -76;
                Main.npc[start + 1] = Make(8, "身体", start + 1); Main.npc[start + 2] = Make(9, "尾巴", start + 2);
                Main.npc[start].ai[0] = start + 1;
                Main.npc[start + 1].realLife = Main.npc[start + 2].realLife = start;
                Main.npc[start + 1].ai[0] = start + 2; Main.npc[start + 1].ai[1] = start; Main.npc[start + 2].ai[1] = start + 1;
            }
            feature.Update(11);
            Check(feature.Labels.Count == 2 && feature.Labels[0].SourceSlot == 20 && feature.Labels[1].SourceSlot == 30 && feature.Labels[0].AnchorSlot != 20, "two native worm chains retain separate owners and visible body anchors");
            Array.Clear(Main.npc, 0, Main.npc.Length);
            Main.npc[2] = Make(13, "世界吞噬者", 2); Main.npc[3] = Make(14, "独立身体", 3); feature.Update(12);
            Check(feature.Labels.Count == 2, "native EoW independent life does not fold by similar type");
            Main.npc[3].type = Main.npc[3].netID = 13; Main.npc[3].life = 35; feature.Update(13);
            Check(feature.Labels.Count == 2 && feature.Labels[1].Health == "35/100", "same-slot split transformation refreshes independent life");
            Main.GameViewMatrix.ZoomMatrix = Matrix.CreateTranslation(-400, -300, 0) * Matrix.CreateScale(2) * Matrix.CreateTranslation(400, 300, 0);
            Main.npc[2].position.X = 50; feature.Update(14); Check(feature.Labels.Count == 1, "actual Host candidate viewport follows centered game zoom");
            Main.GameViewMatrix.ZoomMatrix = Matrix.Identity;
            source.EndSession(); feature.OnSessionEnded(); Check(feature.Labels.Count == 0, "source and output release world references");
            Console.WriteLine("PASS: entity native observation, category facts, moon phases and replaced health owner boundaries.");
        }
        private static NPC Make(int type, string name, int slot)
        { return new NPC { type = type, netID = type, whoAmI = slot, friendly = false, TypeName = name, generation = 1, position = new Vector2(200, 250) }; }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
