using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Caller owns a fully initialized original Main and a published final
    // matrix. The test invokes real DrawNPCs/DrawNPCDirect with native sprites;
    // neither admission nor pixel expectations call the production helper.
    internal static class NativeViewNpcChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Dictionary<NPC, int> submissions = new Dictionary<NPC, int>();
        private static readonly Color background = new Color(197, 19, 143, 255);

        internal static void Run(Main game, object view, string phase, string output)
        {
            Require(phase == "normal" || phase == "on" || phase == "retained", "explicit native NPC draw phase");
            bool wide = phase != "normal";
            Require(Main.screenWidth == 1920 && Main.screenHeight == 1080, "NPC boundary witness uses actual physical1920x1080");
            Require(Math.Abs(Main.GameViewMatrix.RenderZoom.X - (wide ? .5f : 1f)) < .0001f, "NPC witness consumes already published native final matrix");
            Require((bool)Get(view, "UnlockEnabled") == (phase == "on"), "NPC witness permission/Retained phase is genuine");
            var oldNpcs = Main.npc;
            var device = game.GraphicsDevice;
            var oldTargets = device.GetRenderTargets();
            var audit = new Harmony("JueMingR.Tests.View.NpcCulling");
            try
            {
                Main.npc = new NPC[oldNpcs.Length];
                for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i };
                Vector2 origin = Main.screenPosition;
                SetSlime(0, origin + new Vector2(-880, 520));
                SetSlime(1, origin + new Vector2(2800, 520));
                SetSlime(2, origin + new Vector2(940, 520));
                SetSlime(3, origin + new Vector2(940, 700)); Main.npc[3].active = false;
                SetSlime(4, origin + new Vector2(940, 800)); Main.npc[4].hide = true;
                SetSlime(5, origin + new Vector2(8000, 520));
                Require(!NPCID.Sets.MustAlwaysDraw[Main.npc[0].type], "ordinary slime cannot use native always-draw exemption");
                var nativeRectangle = new Rectangle((int)origin.X - 800, (int)origin.Y - 800, Main.screenWidth + 1600, Main.screenHeight + 1600);
                for (int i = 0; i < 2; i++)
                {
                    Require(!nativeRectangle.Intersects(Main.npc[i].Hitbox), "edge ordinary NPC lies beyond original coarse rectangle");
                    Vector2 physical = Project(Main.npc[i].Center);
                    Require((physical.X > 0 && physical.X < Main.screenWidth && physical.Y > 0 && physical.Y < Main.screenHeight) == wide, "independent native matrix places edge NPC inside only in wide view");
                    Console.WriteLine("VIEW NPC phase=" + phase + " edge=" + i + " physical=" + physical + " world=" + Main.npc[i].position);
                }
                submissions.Clear();
                audit.Patch(typeof(Main).GetMethod("DrawNPCDirect", F), prefix: new HarmonyMethod(typeof(NativeViewNpcChecks), nameof(Submitted)));
                using (var target = new RenderTarget2D(device, Main.screenWidth, Main.screenHeight, false, SurfaceFormat.Color, DepthFormat.None))
                {
                    device.SetRenderTarget(target); device.Clear(background);
                    Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    try { typeof(Main).GetMethod("DrawNPCs", F).Invoke(game, new object[] { false }); }
                    finally { Main.spriteBatch.End(); device.SetRenderTargets(oldTargets); }
                    var pixels = new Color[Main.screenWidth * Main.screenHeight]; target.GetData(pixels);
                    Directory.CreateDirectory(output);
                    using (var image = new Texture2D(device, Main.screenWidth, Main.screenHeight))
                    { image.SetData(pixels); using (var file = File.Create(Path.Combine(output, "view-npc-" + phase + ".png"))) image.SaveAsPng(file, Main.screenWidth, Main.screenHeight); }
                    for (int i = 0; i < 3; i++)
                    {
                        int count; submissions.TryGetValue(Main.npc[i], out count);
                        bool expected = wide || i == 2;
                        int changed = ChangedPixels(pixels, Project(Main.npc[i].Center));
                        Console.WriteLine("VIEW NPC actual phase=" + phase + " index=" + i + " submitted=" + count + " changedPixels=" + changed + " active=" + Main.npc[i].active);
                        Require((count == 1) == expected && (expected || count == 0), "native DrawNPCs submits exactly the independently expected ordinary NPC");
                        if (expected) Require(changed > 2, "submitted original NPC sprite produces actual local physical pixels");
                        Require(Main.npc[i].active, "native ordinary draw did not fail and retire fixture NPC");
                    }
                    for (int i = 3; i < 6; i++) Require(!submissions.ContainsKey(Main.npc[i]), "inactive/hidden/far-outside controls do not submit");
                    Require(!Main.npc[3].active && Main.npc[4].active && Main.npc[4].hide && Main.npc[5].active, "draw preserves inactive/hidden/outside lifecycle flags");
                    Require(Main.screenPosition == origin && Main.screenWidth == 1920 && Main.screenHeight == 1080, "NPC culling leaves physical camera fields intact");
                }
                Console.WriteLine("PASS VIEW NPC native DrawNPCs phase=" + phase + " original-rectangle-outside left/right submissions=" + (wide ? "1/1" : "0/0") + "; original sprite pixels, center and inactive/hidden/far controls; no simulation/sync claim.");
            }
            finally { audit.UnpatchAll(audit.Id); Main.npc = oldNpcs; device.SetRenderTargets(oldTargets); submissions.Clear(); }
        }

        private static void SetSlime(int index, Vector2 position)
        {
            NPC npc = Main.npc[index]; npc.SetDefaults(NPCID.GreenSlime); npc.active = true; npc.hide = false;
            npc.position = position; npc.netOffset = Vector2.Zero; npc.behindTiles = false; npc.alpha = 0;
            Main.instance.LoadNPC(npc.type); npc.FindFrame();
        }
        private static Vector2 Project(Vector2 world)
        { return Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix); }
        private static void Submitted(NPC rCurrentNPC)
        { int count; submissions.TryGetValue(rCurrentNPC, out count); submissions[rCurrentNPC] = count + 1; }
        private static int ChangedPixels(Color[] pixels, Vector2 center)
        {
            int changed = 0;
            for (int y = Math.Max(0, (int)center.Y - 24); y < Math.Min(Main.screenHeight, (int)center.Y + 25); y++)
                for (int x = Math.Max(0, (int)center.X - 24); x < Math.Min(Main.screenWidth, (int)center.X + 25); x++)
                    if (pixels[y * Main.screenWidth + x] != background) changed++;
            return changed;
        }
    }
}
