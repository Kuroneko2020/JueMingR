using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionDukeTaskChecks
    {
        internal static void Run(object host,Player human,Player body,Action tick)
        {
            var actor = Call(host,"Owned",body);
            var profile = (CompanionProfile)Get(actor,"Profile");
            var identity = new CompanionTaskIdentity(profile.OwnerId,profile.Id,(long)Get(host,"session"),
                (long)Get(actor,"Generation"),profile.DataEpoch);
            var previousTiles = Main.tile;
            var previousOwner = human.Bottom;
            var previousBody = body.Bottom;
            var rod = body.inventory[0];
            var previousWorm = body.inventory[54];
            int OrdinaryBait() => body.inventory.Take(58).Where(item=>item!=null && item.type!=ItemID.TruffleWorm && item.bait>0)
                .Sum(item=>item.stack);
            int previousBait = OrdinaryBait();
            var previous = profile.Behavior;
            var npcTable = Main.npc.ToArray();
            try
            {
                var behavior = previous.Clone();
                behavior.Follow = false;
                behavior.AttackEnabled = false;
                Call(host,"SetBehavior",profile.Id,behavior);
                Main.tile = (Tile[,])previousTiles.Clone();
                for(int x=56;x<=103;x++)
                    for(int y=44;y<=91;y++)
                    {
                        var tile = Main.tile[x,y] = new Tile();
                        if (x<=64 && y==55 || y==90) NativeToolsChecks.Tile(x,y,TileID.Stone);
                        if (x>=65 && x<=101 && y>=56 && y<90) tile.liquid=255;
                    }
                body.Bottom = human.Bottom = new Vector2(62*16+8,55*16);
                body.velocity = Vector2.Zero;
                Call(Get(actor,"Behavior"),"PositionChanged",body);
                body.inventory[0] = new Item(ItemID.WoodFishingPole);
                body.inventory[54] = new Item(ItemID.TruffleWorm) { stack=2 };
                var capture = host.GetType().GetMethod("CaptureFishingAnchor",BindingFlags.Static | BindingFlags.NonPublic);
                var anchor = capture.Invoke(null,new object[] { human });
                var step = new CompanionTaskStep("summon_duke",new Dictionary<string,object> { ["destination"]="here" });
                var job = (CompanionTaskSnapshot)Call(host,"SubmitTask",identity,"原版猪鲨",new[] { step },anchor,Guid.NewGuid());
                Require(job.State=="accepted","explicit Duke task is accepted in the fixture");
                var foundNativeBite = false;
                for(int frame=0;frame<650 && job.IsActive;frame++)
                {
                    tick();
                    var bobber = NativeCompanionFishingLineChecks.WetBobber(body);
                    if (!foundNativeBite && bobber != null)
                    {
                        // Probe actual FishingCheck on the wet native projectile.
                        // It constructs and qualifies its own bite from the pond.
                        bobber.FishingCheck();
                        foundNativeBite = bobber.localAI[2] == ItemID.TruffleWorm && bobber.ai[1] < 0;
                    }
                    job = (CompanionTaskSnapshot)Call(host,"GetTask",profile.OwnerId,profile.Id,job.TaskId);
                }
                Require(foundNativeBite && job.State=="succeeded" && body.inventory[54].type==ItemID.TruffleWorm &&
                    body.inventory[54].stack==1 && NPC.CountNPCS(NPCID.DukeFishron)==1,
                    "one owned original bite, one Worm debit and actual Duke Fishron spawn: " + job.State + "/" + job.Detail +
                    " bite=" + foundNativeBite + " bait=" + body.inventory[54].stack);
                Console.WriteLine("PASS explicit Duke task: actual flooded coast, native owned bite, original pull, one Worm consumed and Duke spawned.");
            }
            finally
            {
                Call(host,"StopTask",profile.OwnerId,profile.Id,null);
                for(int frame=0;frame<45 && NativeCompanionFishingLineChecks.OwnedBobber(body)!=null;frame++) tick();
                for(int i=0;i<npcTable.Length;i++) if (Main.npc[i]?.type==NPCID.DukeFishron &&
                    !ReferenceEquals(Main.npc[i],npcTable[i])) Main.npc[i]=npcTable[i];
                body.inventory[0]=rod;
                body.inventory[54]=previousWorm;
                Main.tile=previousTiles;
                human.Bottom=previousOwner;
                body.Bottom=previousBody;
                body.velocity=Vector2.Zero;
                Call(Get(actor,"Behavior"),"PositionChanged",body);
                Call(host,"SetBehavior",profile.Id,previous);
            }
            Require((bool)Call(host,"SupplyActorReady",actor),"the original body remains available after the Duke capture probe: " +
                Get(actor,"Status") + " active=" + body.active + " dead=" + body.dead);
            Require((bool)Call(host,"CanStartFishing",profile.OwnerId,profile.Id) && ReferenceEquals(body.inventory[0],rod) &&
                OrdinaryBait()==previousBait,"Duke fixture restores the previous ordinary fishing prerequisites: rod=" +
                body.inventory[0].type + " ordinary bait=" + OrdinaryBait() + "/" + previousBait);
        }
    }
}
