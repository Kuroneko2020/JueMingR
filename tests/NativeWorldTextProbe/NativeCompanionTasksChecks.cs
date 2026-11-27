using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionTasksChecks
    {
        internal static void Run(object context, object host, object input, Player human, Player first, Player second, Action tick)
        {
            var firstActor = Call(host, "Owned", first);
            var secondActor = Call(host, "Owned", second);
            var a = (CompanionProfile)Get(firstActor, "Profile");
            var b = (CompanionProfile)Get(secondActor, "Profile");
            long world = (long)Get(host,"session");
            CompanionTaskIdentity Identity(object actor, CompanionProfile profile) => new CompanionTaskIdentity(
                profile.OwnerId, profile.Id, world, (long)Get(actor, "Generation"), profile.DataEpoch);
            var aid = Identity(firstActor,a);
            var bid = Identity(secondActor,b);
            var humanBefore = human.Bottom;
            var bodyBefore = first.Bottom;
            var secondBefore = second.Bottom;
            var tileBefore = Main.tile;
            var rodBefore = first.inventory[0];
            var workSupplies = first.inventory.Skip(43).Take(6).ToArray();
            var before = (CompanionBehavior)((CompanionProfile)Get(firstActor,"Profile")).Behavior;
            CompanionTaskStep Node(string name, params (string Key, object Value)[] arguments) =>
                new CompanionTaskStep(name, arguments.ToDictionary(pair => pair.Key, pair => pair.Value));
            CompanionTaskSnapshot View(CompanionProfile profile) => (CompanionTaskSnapshot)Call(host,"GetTask",profile.OwnerId,profile.Id,null);
            CompanionTaskSnapshot Start(CompanionTaskIdentity identity, string name, object anchor, params CompanionTaskStep[] steps) =>
                (CompanionTaskSnapshot)Call(host,"SubmitTask",identity,name,steps,anchor,Guid.NewGuid());
            object Capture(Player player)
            {
                // Send-time helper is the same value constructor as chat admission.
                var method = host.GetType().GetMethod("CaptureFishingAnchor", System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.NonPublic);
                return method.Invoke(null,new object[] { player });
            }
            void Finish(CompanionProfile profile, int ticks = 240)
            {
                for (int frame = 0; frame < ticks && View(profile).IsActive; frame++) tick();
            }
            string MotionEvidence()
            {
                var navigator = Get(Get(firstActor,"Behavior"),"Navigator");
                var active = Get(navigator,"action");
                var transition = Get(active,"Edge");
                var delivery = GetOptional(navigator,"LastMovementDelivery");
                return " feet " + first.Bottom + " velocity " + first.velocity + " target " + Get(navigator,"Target") +
                    " route " + Get(navigator,"QueryStatus") + " support " + Get(navigator,"Supported") +
                    " native action " + Get(active,"Id") + "/" + Get(active,"Outcome") + " " + Get(transition,"Kind") +
                    " from " + Get(transition,"From") + " to " + Get(transition,"To") +
                    " move " + (delivery==null ? "?" : Convert.ToString(Get(delivery,"Move"))) +
                    " controls " + first.controlLeft + "/" + first.controlRight + " acceleration " + first.runAcceleration;
            }
            try
            {
                if ((bool)Call(host,"IsFishing",a.OwnerId,a.Id)) Call(host,"SetFishing",a.OwnerId,a.Id,false);
                if ((bool)Call(host,"IsFishing",b.OwnerId,b.Id)) Call(host,"SetFishing",b.OwnerId,b.Id,false);
                var manual = before.Clone();
                manual.Follow = false;
                manual.AttackEnabled = false;
                Call(host, "SetBehavior", a.Id, manual);
                Main.tile = (Tile[,])tileBefore.Clone();
                // Native Player.BordersMovement clamps the body's left edge
                // at 40 tiles. Keep this whole physical route inside it.
                for (int x = 53; x <= 84; x++)
                    for (int y = 46; y <= 56; y++)
                    {
                        Main.tile[x,y] = new Tile();
                        if (y == 55) NativeToolsChecks.Tile(x,y,TileID.Stone);
                    }
                first.Bottom = new Vector2(62*16+8,55*16);
                second.Bottom = new Vector2(58*16+8,55*16);
                human.Bottom = new Vector2(71*16+8,55*16);
                first.velocity = second.velocity = Vector2.Zero;
                Call(Get(firstActor,"Behavior"), "PositionChanged", first);
                Call(Get(secondActor,"Behavior"), "PositionChanged", second);
                first.inventory[0] = new Item(ItemID.WoodFishingPole);
                NativeCompanionServiceTaskChecks.Run(host,human,first,tick);
                NativeCompanionResourceTaskChecks.Run(host,human,first,tick);
                var frozen = Capture(human);
                var nearest = human.Bottom;
                var job = Start(aid,"去发送点",frozen,Node("move_to",("destination","here")),Node("follow_owner"));
                Require(job.State == "accepted" && job.StepCount == 2,"discovery is distinct from task admission");
                var independent = Start(bid, "第二个队友待命", Capture(human),Node("stand_by"));
                Require(independent.TaskId != job.TaskId,"parallel profiles have distinct runtime task ids");
                human.Bottom = new Vector2(80*16+8,55*16);
                for (int i=0;i<8 && View(a).Attempts.Count == 0;i++) tick();
                Require(View(a).IsActive && View(a).Attempts.Count == 1,
                    "the native pump actually begins the first step before replacing its successor: " + View(a).Detail);
                var oldPoint = first.Bottom;
                var paused = (CompanionTaskSnapshot)Call(host,"PauseTask",aid,job.TaskId,true);
                Require(paused.State == "paused","accepted work can pause independently of model reply");
                for (int i=0;i<30;i++) tick();
                Require(first.Bottom.X < oldPoint.X + 12 && View(b).State == "succeeded","paused actor holds while other body completes");
                NativeCompanionChatUiChecks.CheckTaskControls(context,host,a,b);
                var conditional = new CompanionTaskStep("stand_by",null,2,new CompanionTaskCondition("has_item",
                    new Dictionary<string,object> { ["item_type"] = (int)ItemID.WoodFishingPole, ["quantity"] = 1 }));
                var replaced = (CompanionTaskSnapshot)Call(host,"ReplaceTaskTail",aid,job.TaskId,
                    new[] { conditional },Capture(human));
                Require(replaced.TaskId == job.TaskId && replaced.StepCount == 2,
                    "edit preserves the accepted walk and replaces its unstarted follow step: " + replaced.State +
                    "/" + replaced.Detail + " (steps " + replaced.StepCount + ")");
                Call(host,"PauseTask",aid,job.TaskId,false);
                Finish(a);
                var done = View(a);
                Require(done.State == "succeeded" && Math.Abs(first.Bottom.X - nearest.X) <= 20 &&
                    Math.Abs(first.Bottom.X - human.Bottom.X) >= 100 && done.Attempts.Count == 3 &&
                    done.Attempts[1].Iteration == 0 && done.Attempts[2].Iteration == 1,
                    "native locomotion reaches the frozen send point and then executes conditional repeats on separate steps; " +
                    done.State + "/" + done.Detail + " feet " + first.Bottom + " wanted " + nearest);
                Require(Call(host, "GetTask", b.OwnerId,b.Id,job.TaskId) == null,
                    "a different selected profile cannot read the first profile's job receipt");
                var stop = (CompanionTaskSnapshot)Call(host,"StopTask",a.OwnerId,a.Id,job.TaskId);
                Require(stop.State == "cancelled" && View(b).TaskId == independent.TaskId,
                    "explicit stop releases only the intended continuous control");
                var notEnough = new CompanionTaskStep("follow_owner",null,condition:new CompanionTaskCondition("has_item",
                    new Dictionary<string,object> { ["item_type"] = (int)ItemID.WoodFishingPole, ["quantity"] = 9999 }));
                var skipped = Start(aid,"缺料则不行动",Capture(human),notEnough);
                Finish(a,12);
                Require(View(a).TaskId == skipped.TaskId && View(a).State == "skipped" &&
                    View(a).Attempts.Count == 1 && View(a).Attempts[0].State == "skipped" && GetOptional(firstActor,"TaskTravel") == null,
                    "a false known condition leaves a skip receipt, with no actual follow or success claim: " + View(a).Detail);
                var tripOrigin = first.Bottom;
                human.Bottom = new Vector2(64*16+8,55*16);
                var trip = Start(aid,"往返",Capture(human),Node("move_to",("destination","here")),
                    Node("move_to",("destination","departure")));
                human.Bottom = new Vector2(81*16+8,55*16);
                Finish(a,400);
                var returned = View(a);
                Require(returned.TaskId == trip.TaskId && returned.State == "succeeded" &&
                    returned.Attempts.Count == 2 && returned.Attempts[0].Detail.Contains("发送点") &&
                    returned.Attempts[1].Detail.Contains("出发点") && Vector2.Distance(first.Bottom,tripOrigin) < 22f,
                    "round trip uses distinct actual outbound and original companion return positions: " + returned.Detail + MotionEvidence());
                first.inventory[47] = new Item(ItemID.CopperPickaxe);
                first.inventory[48] = new Item(ItemID.StoneBlock) { stack = 3 };
                human.Bottom = first.Bottom;
                int px = (int)(first.Bottom.X/16) + 2, py = 54;
                var workAnchor = Capture(human);
                string Target(bool empty)
                {
                    int dx = px - (int)(human.Bottom.X / 16), dy = py - (int)(human.Bottom.Y / 16);
                    var args = new Dictionary<string,object>
                    {
                        ["around"] = "here", ["radius"] = 3, ["empty"] = empty, ["offset"] = (dy+3)*7+dx+3
                    };
                    var report = (CompanionTaskOutcome)Call(host,"TryGetTaskObservation",aid,"observe_tiles",args,workAnchor);
                    string row = report.Detail.Split('；').FirstOrDefault(record => record.StartsWith("(" + px + "," + py + ")"));
                    Require(row != null,"the planned test target is an actual known tile: " + report.Detail);
                    return row.Substring(row.LastIndexOf("· ",StringComparison.Ordinal) + 2).Split(' ')[0];
                }
                var place = Start(aid,"放一块石头",workAnchor,Node("world_use",("target",Target(true)),
                    ("action","place_tile"),("item_type",(int)ItemID.StoneBlock)));
                Finish(a,240);
                Require(View(a).TaskId == place.TaskId && View(a).State == "succeeded" &&
                    Main.tile[px,py].active() && Main.tile[px,py].type == TileID.Stone && first.inventory[48].stack == 2,
                    "task stages a real native placement and observes one debit at the exact selected tile: " + View(a).Detail);
                first.inventory[43] = new Item(ItemID.Paintbrush);
                first.inventory[44] = new Item(ItemID.RedPaint) { stack = 3 };
                first.inventory[45] = new Item(ItemID.Wrench);
                first.inventory[46] = new Item(ItemID.Wire) { stack = 3 };
                int red = first.inventory[44].paint;
                var artwork = Start(aid,"油漆和布线",workAnchor,
                    Node("world_use",("target",Target(false)),("action","paint_tile"),("paint",red)),
                    Node("world_use",("target",Target(false)),("action","place_wire"),("item_type",(int)ItemID.Wrench)));
                Finish(a,360);
                Require(View(a).TaskId == artwork.TaskId && View(a).State == "succeeded" &&
                    Main.tile[px,py].color() == red && Main.tile[px,py].wire() &&
                    first.inventory[44].stack == 2 && first.inventory[46].stack == 2,
                    "native paint and wire work only through their own selected material and debit once: " + View(a).Detail);
                var mine = Start(aid,"挖指定石头",workAnchor,Node("world_use",("target",Target(false)),
                    ("action","mine"),("item_type",(int)ItemID.CopperPickaxe)));
                Finish(a,360);
                Require(View(a).TaskId == mine.TaskId && View(a).State == "succeeded" && !Main.tile[px,py].active(),
                    "native tool input removes the same observed tile after real mining cooldown: " + View(a).Detail);
                CheckNativeCritter(host,aid,a,first,workAnchor,tick);
                var blueprint = Get(context,"Blueprint");
                var library = (LibraryState)Get(blueprint,"Library");
                var chair = new Item(ItemID.WoodenChair);
                int bx = (int)(first.Bottom.X/16) - 2;
                first.inventory[48] = new Item(ItemID.WoodenChair) { stack = 2 };
                Require(WorldGen.PlaceObject(58,54,chair.createTile,true,chair.placeStyle,direction:1),
                    "fixture creates a genuine two-cell chair with native frames");
                var blueprintEntry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,blueprint,input,library,tick,
                    58,53,1,2,"队友任务施工原版椅子");
                NativeBlueprintWorldMatchChecks.Place(blueprint,input,blueprintEntry,bx,53,tick);
                var projected = (CompanionTaskOutcome)Call(host,"TryGetTaskObservation",aid,"observe_blueprints",
                    new Dictionary<string,object>(),workAnchor);
                Require(projected.State == "succeeded" && projected.Value != null,"the task reads a genuine fixed projection: " + projected.Detail);
                var construction = Start(aid,"从蓝图施工椅子",workAnchor,Node("build_projection",("reference",projected.Value)));
                NativeQuickItemChecks.Until(() => { tick(); return !View(a).IsActive; });
                Require(View(a).TaskId == construction.TaskId && View(a).State == "succeeded" &&
                    Main.tile[bx,53].active() && Main.tile[bx,54].active() && Main.tile[bx,53].type == chair.createTile &&
                    Main.tile[bx,54].type == chair.createTile && first.inventory[48].stack == 1,
                    "the fixed blueprint produces a whole original chair with one actual item debit: " + View(a).Detail + "/" +
                    String.Join("/",View(a).Attempts.Select(attempt=>attempt.Detail)) + " stack=" + first.inventory[48].stack +
                    " target=" + Main.tile[bx,53] + "/" + Main.tile[bx,54] + " source=" + Main.tile[58,53] + "/" + Main.tile[58,54]);
                var stale = new CompanionTaskIdentity(a.OwnerId,a.Id,world,aid.Activation+1,a.DataEpoch);
                Require(Start(stale,"失效身份",frozen,Node("stand_by")).State == "rejected",
                    "an obsolete activation cannot start a task");
                int npcSlot=Main.maxNPCs-2;
                var oldNpc=Main.npc[npcSlot];
                try
                {
                    var oldTarget=new NPC(); oldTarget.SetDefaults(NPCID.BlueSlime); oldTarget.whoAmI=npcSlot;
                    oldTarget.active=true; oldTarget.Center=first.Center+new Vector2(30,0);
                    Main.npc[npcSlot]=oldTarget;
                    var observed=(CompanionTaskOutcome)Call(host,"TryGetTaskObservation",aid,"observe_npcs",
                        new Dictionary<string,object> { ["filter"]=oldTarget.FullName },workAnchor);
                    Require(observed.Value!=null,"real NPC observation supplies a reference");
                    var replacement=new NPC(); replacement.SetDefaults(NPCID.BlueSlime); replacement.whoAmI=npcSlot;
                    Require(replacement.generation==oldTarget.generation,"fixture exercises a reused byte instead of only a different generation");
                    replacement.active=true; replacement.Center=oldTarget.Center;
                    Main.npc[npcSlot]=replacement;
                    var obsolete=Start(aid,"旧草地目标",workAnchor,Node("attack_npc",("target",observed.Value)));
                    Finish(a,4);
                    Require(View(a).TaskId==obsolete.TaskId && View(a).State=="failed" && replacement.life==replacement.lifeMax,
                        "same original slot and wrapped generation cannot authorize the replacement NPC");
                }
                finally { Main.npc[npcSlot]=oldNpc; }
                Console.WriteLine("PASS companion tasks: native send-time walk and return, condition, repeat, replacement, pause, two-body identity, placement, paint, wire, mining and projected furniture.");
            }
            finally
            {
                Call(host,"StopTask",a.OwnerId,a.Id,null);
                Call(host,"StopTask",b.OwnerId,b.Id,null);
                Call(host,"SetBehavior",a.Id,before);
                human.Bottom = humanBefore; first.Bottom = bodyBefore; second.Bottom = secondBefore;
                Call(Get(firstActor,"Behavior"), "PositionChanged", first);
                Call(Get(secondActor,"Behavior"), "PositionChanged", second);
                first.inventory[0] = rodBefore;
                for(int slot=0;slot<workSupplies.Length;slot++) first.inventory[43+slot] = workSupplies[slot];
                Call(Get(context,"Blueprint"),"ClearProjection");
                Main.tile = tileBefore;
            }
        }

        private static void CheckNativeCritter(object host,CompanionTaskIdentity identity,CompanionProfile profile,Player body,
            object anchor,Action tick)
        {
            int slot=Main.maxNPCs-2;
            var oldTarget=Main.npc[slot];
            var oldBystander=Main.npc[slot-1];
            var oldDrops=Main.item;
            var stock=body.inventory.ToArray();
            try
            {
                Main.item=new WorldItem[oldDrops.Length];
                for(int i=0;i<Main.item.Length;i++) Main.item[i]=new WorldItem { whoAmI=i };
                var bunny=new NPC(); bunny.SetDefaults(NPCID.Bunny); bunny.whoAmI=slot;
                bunny.Center=body.Center+new Vector2(12,-1); bunny.active=true;
                Main.npc[slot]=bunny;
                var squirrel=new NPC(); squirrel.SetDefaults(NPCID.Squirrel); squirrel.whoAmI=slot-1;
                squirrel.Center=bunny.Center; squirrel.active=true;
                Main.npc[slot-1]=squirrel;
                body.inventory[47]=new Item(ItemID.BugNet);
                var observed=(CompanionTaskOutcome)Call(host,"TryGetTaskObservation",identity,"observe_npcs",
                    new Dictionary<string,object> { ["filter"]=bunny.FullName },anchor);
                Require(observed.Value!=null,"native critter observation selects the particular nearby bunny");
                int Quantity()=>body.inventory.Where(item=>item?.type==bunny.catchItem).Sum(item=>item.stack);
                int prior=Quantity();
                var step=new CompanionTaskStep("catch_critter",new Dictionary<string,object> { ["target"]=observed.Value });
                var job=(CompanionTaskSnapshot)Call(host,"SubmitTask",identity,"捕捉指定兔子",new[] { step },anchor,Guid.NewGuid());
                for(int frame=0;frame<420 && job.IsActive;frame++)
                {
                    tick();
                    for(int i=0;i<Main.item.Length;i++) if (Main.item[i].active) Main.item[i].UpdateItem(i);
                    job=(CompanionTaskSnapshot)Call(host,"GetTask",profile.OwnerId,profile.Id,job.TaskId);
                }
                var actor=Call(host,"Owned",body);
                var hand=actor==null?null:GetOptional(actor,"TaskWorldHand");
                Require(job.State=="succeeded" && !bunny.active && squirrel.active && Quantity()==prior+1,
                    "only the authorized critter is actually netted and picked up: " + job.State + "/" + job.Detail +
                    " bunny=" + bunny.active + " squirrel=" + squirrel.active + " quantity=" + Quantity() +
                    " selected=" + body.selectedItem + " held=" + body.HeldItem.type + " animation=" + body.itemAnimation +
                    " itemTime=" + body.itemTime + " use=" + body.controlUseItem + "/" + body.releaseUseItem +
                    " centers=" + body.Center + "/" + bunny.Center + " pulses=" + (hand==null ? "released" : GetOptional(hand,"Pulses")));
                Console.WriteLine("PASS native bug-net target and inventory receipt with overlapping uncaught bystander.");
            }
            finally
            {
                Call(host,"StopTask",profile.OwnerId,profile.Id,null);
                Main.npc[slot]=oldTarget; Main.npc[slot-1]=oldBystander;
                Main.item=oldDrops;
                Array.Copy(stock,body.inventory,stock.Length);
            }
        }
    }
}
