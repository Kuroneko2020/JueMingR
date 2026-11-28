using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JueMingR.Features.MapMarkers;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal Func<IReadOnlyList<MarkerRecord>> TaskMarkers = () => Array.Empty<MarkerRecord>();
        internal Func<string> TaskSelectedMarker = () => null;
        private FishingAnchor CaptureTaskSendAnchor(Player player) =>
            new FishingAnchor(CaptureFishingAnchor(player), TaskSelectedMarker(), session);
        // The immutable request contains only an opaque key. Live inventory
        // objects remain in this bounded game-thread table.
        private readonly Dictionary<Guid,(Actor Actor,int Slot,Item Item,int Type,int Stack,bool OwnerMatched)> taskTalkItems =
            new Dictionary<Guid,(Actor,int,Item,int,int,bool)>();
        private readonly Queue<Guid> taskTalkItemOrder = new Queue<Guid>();

        private FishingAnchor CaptureChatTaskAnchor(Player player,Guid profileId)
        {
            var anchor=CaptureTaskSendAnchor(player);
            if (!actors.TryGetValue(profileId,out var actor) || !SupplyActorReady(actor)) return anchor;
            int slot=actor.Body.selectedItem;
            if (slot<0 || slot>=CompanionBehavior.MainInventorySlots) return anchor;
            var held=actor.Body.inventory[slot];
            if (held==null || held.IsAir || held.favorited || TaskResourceSlotBlocked(actor,false,CompanionInventoryArea.Inventory,slot)) return anchor;
            var key=Guid.NewGuid();
            taskTalkItems.Add(key,(actor,slot,held,held.type,held.stack,player.HeldItem?.type==held.type || Main.mouseItem?.type==held.type));
            taskTalkItemOrder.Enqueue(key);
            if (taskTalkItemOrder.Count>128) taskTalkItems.Remove(taskTalkItemOrder.Dequeue());
            return new FishingAnchor(anchor,anchor.SelectedMarkerId,anchor.MarkerSession,key);
        }

        private bool TryTaskTalkItem(Actor actor,FishingAnchor anchor,out int slot,out Item item)
        {
            slot=-1; item=null;
            if (anchor.MarkerSession!=session || !taskTalkItems.TryGetValue(anchor.TalkItemWitness,out var witness) ||
                !ReferenceEquals(witness.Actor,actor) || !ReferenceEquals(actor.Body.inventory[witness.Slot],witness.Item) ||
                witness.Item.type!=witness.Type || witness.Item.stack!=witness.Stack || witness.Item.favorited ||
                TaskResourceSlotBlocked(actor,false,CompanionInventoryArea.Inventory,witness.Slot)) return false;
            slot=witness.Slot; item=witness.Item; return true;
        }
        private readonly Dictionary<int,TaskNpcWitness> taskNpcWitnesses = new Dictionary<int,TaskNpcWitness>();

        private sealed class TaskNpcWitness
        {
            internal NPC Body;
            internal int Generation;
            internal long World;
            internal Guid Key;
        }

        internal sealed class TaskTravel
        {
            internal Vector2 Feet;
            internal bool FollowOwner, Temporary, Arrived;
            internal string Name;
        }

        internal sealed class TaskCombat
        {
            internal NPC Target;
            internal int Generation;
            internal float Radius;
            internal bool HoldFire, GuardOwner, LacewingEvent, AreaFriendly;
            internal Vector2 Center;
        }

        internal sealed class ArrivalSearch
        {
            internal int X, Y, Next;
            internal int HorizontalRadius, VerticalRadius, Side;
            internal Point? Awaiting;
            internal string Name, Reference;
            internal NPC Npc;
            internal int NpcGeneration;
            internal TeleportPylonInfo? Pylon;
            internal bool NearWater, OwnerTarget;
        }

        private void RegisterMovementTaskOperations()
        {
            TaskField destination = new TaskField("destination", "string", true, 1, 24, "here", "owner", "bed",
                "world_spawn", "left_ocean", "right_ocean", "marker", "selected", "task_target", "departure", "reference", "memory");
            TaskField reference = new TaskField("reference", "string", false, 1, 120);
            RegisterTaskOperation("move_to", "Reach an observed destination on the real body. Here means the send-time location. Use a returned reference for NPCs or a saved map marker.",
                TravelTask, CancelTravelTask, destination, reference);
            RegisterTaskOperation("stand_by", "Hold the companion's current location using normal movement and local safety.",
                StandByTask);
            RegisterTaskOperation("follow_owner", "Start following the real owner until the player changes the activity.", FollowTask);
            RegisterTaskOperation("recall_to_owner", "Use the native wormhole recall at the admitted payer slot and wait for its result.", RecallTask);
            RegisterTaskOperation("fish_at", "Fish at the resolved destination. Quantity zero waits for a real wet cast then keeps fishing. Positive quantities count actual catches. " +
                "Only on explicit request, finish_if_blocked ends this step with the observed catch count when out of bait or after actual inventory overflow.",
                FishTask, CancelFishingTask, destination, reference, new TaskField("item_type","integer", false, 1, ItemID.Count - 1),
                new TaskField("quantity","integer", false, 0, 100), new TaskField("quest_fish","boolean"),
                new TaskField("notify_no_bait","boolean"),new TaskField("finish_if_blocked","boolean"));
            RegisterTaskOperation("summon_duke", "Only on explicit request: fish with one eligible Truffle Worm and confirm Duke Fishron using the native coastal pond and pull rules.",
                FishTask,CancelFishingTask,destination,reference);
            RegisterTaskOperation("attack_npc", "Engage the observed enemy within a fixed pursuit radius. The NPC reference includes instance generation.",
                AttackTask, CancelCombatTask, new TaskField("target","string",true, 1), new TaskField("leash_tiles","integer",false,4,100));
            RegisterTaskOperation("guard", "Guard the owner or a fixed location with the existing combat system and a bounded pursuit radius.",
                GuardTask, null, new TaskField("destination","string",true,1,16,"owner","here","task_target"),
                new TaskField("leash_tiles","integer",false,4,100));
            RegisterTaskOperation("cease_fire", "Release combat input and temporarily hold fire without changing saved preferences.",
                HoldFireTask);
            RegisterAreaCombat();
            RegisterTaskObservation("observe_destinations", "Find valid landmark references and saved map names for this world.", DestinationObservation,
                new TaskField("filter"));
            RegisterTaskObservation("observe_npcs", "Read nearby observed target names and NPC instance references.", NpcTaskObservation,
                new TaskField("filter"),new TaskField("around","string",false,1,12,"here","companion"),
                new TaskField("radius","integer",false,1,100),new TaskField("offset","integer",false,0,200));
            RegisterTaskObservation("has_item", "Check actual main inventory for the requested quantity.", HasTaskItem,
                new TaskField("item_type","integer",true,1,ItemID.Count - 1), new TaskField("quantity","integer",false,1,9999));
            RegisterTaskObservation("observe_settings","查询当前队友的常规与钓鱼设置、可用名称和合法值。", ReadChatSettings);
        }

        private CompanionTaskOutcome DestinationObservation(TaskExecution execution)
        {
            string filter = execution.Text("filter", "");
            var places = TaskMarkers() ?? Array.Empty<MarkerRecord>();
            var matching = places.Where(marker => marker.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).Take(12).ToArray();
            var references = matching.Select(marker => (Name: marker.Name, Ref: "marker:" + marker.Id)).ToList();
            if (Main.PylonSystem?.Pylons != null)
                foreach (var pylon in Main.PylonSystem.Pylons)
                {
                    string name = PylonTaskName(pylon);
                    if (references.Count < 12 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        references.Add((name, PylonTaskReference(pylon)));
                }
            if (references.Count < 12)
                references.AddRange(FindTaskMemoryDestinations(filter, CompanionNativeContact.Feet(execution.Actor.Body).ToTileCoordinates())
                    .Take(12-references.Count));
            string details = String.Join("；", references.Select(place => place.Name + " → " + place.Ref));
            string defaults = filter.Length == 0 ?
                "可用地点引用：发送点 here、主人 owner、队友出发点 departure、床 bed、世界出生 world_spawn、左海岸 left_ocean、右海岸 right_ocean。" : "";
            return TaskOutcome("succeeded", defaults + (references.Count == 0 ? "没有匹配的已存地点或设施。" : details),
                references.Count, references.Count == 1 ? references[0].Ref : null);
        }

        private static string PylonTaskReference(TeleportPylonInfo pylon) => "pylon:" +
            pylon.PositionInTiles.X + ":" + pylon.PositionInTiles.Y + ":" + (int)pylon.TypeOfPylon;
        private static string PylonTaskName(TeleportPylonInfo pylon) => pylon.TypeOfPylon.ToString() + " 晶塔";

        private CompanionTaskOutcome NpcTaskObservation(TaskExecution execution)
        {
            string filter = execution.Text("filter", "");
            var actor = execution.Actor;
            bool here=execution.Text("around")=="here";
            if (here && !execution.Anchor.Valid) return TaskOutcome("rejected","发送时没有有效的位置。");
            Vector2 center=here?execution.Anchor.BodyCenter:actor.Body.Center;
            int radius=execution.Number("radius",100),offset=execution.Number("offset");
            float range=radius*16f;
            var nearby = new List<NPC>();
            var table = TaskNpcTable;
            if (table == null) return TaskOutcome("waiting", "正在等待当前世界的 NPC 观察。");
            for (int i = 0; i < table.Length; i++)
            {
                var npc = table[i];
                if (!TaskNpcObserved(npc) ||
                    Vector2.DistanceSquared(center,npc.Center+npc.netOffset)>range*range ||
                    npc.FullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                nearby.Add(npc);
            }
            var rows=nearby.OrderBy(npc=>Vector2.DistanceSquared(center,npc.Center+npc.netOffset)).Skip(offset).Take(12).ToArray();
            var cell=center.ToTileCoordinates();
            return TaskOutcome("succeeded", (rows.Length==0 ? "本页没有匹配生物。" : "已观察到 " + nearby.Count+" 个，显示：" +
                String.Join("；",rows.Select(npc=>npc.FullName+(npc.friendly?"（友好）":"（敌对）")+" → "+NpcTaskReference(npc))))+
                "；"+(here?"发送时主人":"当前队友")+"中心 ("+cell.X+","+cell.Y+")，半径 "+radius+" 格，当前世界刻 "+Main.GameUpdateCount+
                "；"+(Main.netMode==1?"仅当前联机观测。":"当前本地世界生物。")+(offset+12<nearby.Count?"下一页 offset="+(offset+12):""),
                nearby.Count, nearby.Count == 1 && rows.Length == 1 ? NpcTaskReference(rows[0]) : null);
        }

        private string NpcTaskReference(NPC npc)
        {
            int slot = npc.whoAmI;
            if (!taskNpcWitnesses.TryGetValue(slot,out var witness) || !ReferenceEquals(witness.Body,npc) ||
                witness.Generation != npc.generation || witness.World != session)
            {
                // Vanilla's per-slot byte can wrap. A chat reference witnesses
                // the actual NPC instance and world at the time of observation.
                witness = new TaskNpcWitness { Body=npc,Generation=npc.generation,World=session,Key=Guid.NewGuid() };
                taskNpcWitnesses[slot] = witness;
            }
            return "npc:" + slot + ":" + witness.Key.ToString("N");
        }

        private NPC[] TaskNpcTable => Main.netMode == 1 ? observedNpcs : Main.npc;

        private bool TaskNpcObserved(NPC npc) => npc?.active == true && (Main.netMode != 1 ||
            // Vanilla omits idle townsfolk from its proximity refresh. The
            // current baseline and original instance still gate every service.
            NpcBaselineCurrent(npc.whoAmI) && (npc.townNPC || Main.GameUpdateCount - observedTicks[npc.whoAmI] <= 600));

        private NPC ReadTaskNpc(string reference)
        {
            if (reference == null) return null;
            var parts = reference.Split(':');
            if (parts.Length != 3 || parts[0] != "npc" ||
                !Int32.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int slot) ||
                !Guid.TryParseExact(parts[2],"N",out Guid key) || slot < 0 || slot >= Main.maxNPCs ||
                !taskNpcWitnesses.TryGetValue(slot,out var witness) || witness.World != session || witness.Key != key) return null;
            var npc = TaskNpcTable?[slot];
            return npc?.active == true && ReferenceEquals(witness.Body,npc) && npc.generation == witness.Generation ? npc : null;
        }

        private CompanionTaskOutcome HasTaskItem(TaskExecution execution)
        {
            int count = CountTaskInventory(execution.Actor, execution.Number("item_type"));
            return TaskOutcome("succeeded", "当前主背包数量 " + count + "。", count,
                count >= execution.Number("quantity", 1) ? "true" : "false");
        }

        private static int CountTaskInventory(Actor actor, int type)
        {
            int count = 0;
            for (int slot = 0; slot < CompanionBehavior.MainInventorySlots; slot++)
                if (actor.Body.inventory[slot]?.type == type) count += actor.Body.inventory[slot].stack;
            return count;
        }

        private CompanionTaskOutcome TravelTask(TaskExecution execution)
        {
            var actor = execution.Actor;
            if (Main.GameUpdateCount - execution.StartedAt > 216000)
                return TaskOutcome("failed", "在本次通行期限内未到达目标。");
            if (execution.State == null)
            {
                if (!TryTaskArrival(execution, out var search, out var error)) return TaskOutcome("failed", error);
                if (actor.Fishing != null) EndFishingActivity(actor);
                SetTaskTravel(actor,null);
                actor.TaskControlId = execution.Job.Id;
                execution.State = search;
            }
            var site = (ArrivalSearch)execution.State;
            if (site.Pylon.HasValue && Main.PylonSystem?.Pylons?.Contains(site.Pylon.Value) != true)
                return TaskOutcome("failed", "目标晶塔已消失。");
            if (site.OwnerTarget)
            {
                if (owner?.active != true || owner.dead) return TaskOutcome("waiting","正在等主人重新出现。");
                var latest = CompanionNativeContact.Feet(owner).ToTileCoordinates();
                if (Math.Abs(latest.X-site.X) > 2 || Math.Abs(latest.Y-site.Y) > 2)
                {
                    site.X = latest.X; site.Y = latest.Y; site.Next = 0;
                    SetTaskTravel(actor,null);
                }
            }
            if (site.Npc != null && (site.Npc.active != true || site.Npc.generation != site.NpcGeneration ||
                !ReferenceEquals(TaskNpcTable?[site.Npc.whoAmI], site.Npc)))
                return TaskOutcome("failed", "目标 NPC 已离开。");
            if (site.Npc != null && !TaskNpcObserved(site.Npc))
                return TaskOutcome("waiting", "正在等待更新的目标 NPC 位置。");
            if (site.Npc != null && Vector2.DistanceSquared(site.Npc.Bottom, new Vector2(site.X * 16, site.Y * 16)) > 64 * 64)
            {
                var tile = site.Npc.Bottom.ToTileCoordinates();
                site.X = tile.X; site.Y = tile.Y; site.Next = 0;
                SetTaskTravel(actor, null);
            }
            if (actor.TaskTravel == null)
            {
                if (!FindTaskFooting(actor, site, out var feet, out bool exhausted))
                {
                    if (exhausted) return TaskOutcome("failed", "已知目标附近未找到可站立的落脚点。");
                    return TaskOutcome("waiting", site.Awaiting.HasValue ? "正在等待目标地形资料。" : "正在核对目标附近的落脚点。");
                }
                SetTaskTravel(actor, new TaskTravel { Feet = feet, Name = site.Name });
            }
            var travel = actor.TaskTravel;
            if (travel.Arrived && !travel.Temporary)
            {
                actor.TaskLastFeet = travel.Feet;
                return TaskOutcome("succeeded", "已抵达" + site.Name + "，正在待命。", 1, site.Reference);
            }
            if (travel.Temporary) return TaskOutcome("waiting", "正在避险或处理近处威胁，原目标保留。");
            switch (actor.Behavior.Navigator.QueryStatus)
            {
                case NavigationQueryStatus.AwaitingWorldData: return TaskOutcome("waiting", "正在等待沿途地形资料。");
                case NavigationQueryStatus.NoRouteInKnownGraph: return TaskOutcome("blocked", "已知地形暂未找到可行路段，等待新的地形或通路。");
                default: return TaskOutcome("running", "正在前往" + site.Name + "。");
            }
        }

        private bool TryTaskArrival(TaskExecution execution, out ArrivalSearch site, out string error)
        {
            var actor = execution.Actor;
            var body = actor.Body;
            string destination = execution.Text("destination", "here");
            string reference = execution.Text("reference");
            error = "未取得当前世界的有效地点。";
            Vector2 feet = Vector2.Zero;
            site = new ArrivalSearch { Name = "目标地点", HorizontalRadius = 6, VerticalRadius = 10, Reference = destination };
            switch (destination)
            {
                case "here":
                    if (!execution.Anchor.Valid) return false;
                    feet = execution.Anchor.Feet;
                    site.Name = "消息发送点";
                    break;
                case "owner":
                    if (owner?.active != true || owner.dead) { error = "当前玩家无法作为返回目标。"; return false; }
                    feet = CompanionNativeContact.Feet(owner);
                    site.Name = "玩家所在位置";
                    site.OwnerTarget = true;
                    break;
                case "departure":
                    if (execution.Job == null) return false;
                    feet = execution.Job.Departure;
                    site.Name = "队友本次出发点";
                    break;
                case "task_target":
                    if (!actor.TaskLastFeet.HasValue) return false;
                    feet = actor.TaskLastFeet.Value;
                    site.Name = "先前的任务地点";
                    break;
                case "bed":
                    if (!Player.CheckSpawn(body.SpawnX, body.SpawnY))
                    { error = "队友在当前世界没有可用床位。"; return false; }
                    feet = new Vector2(body.SpawnX * 16 + 8, body.SpawnY * 16);
                    site.Name = "队友床位";
                    break;
                case "world_spawn":
                    feet = new Vector2(Main.spawnTileX * 16 + 8, Main.spawnTileY * 16);
                    site.Name = "世界出生点";
                    break;
                case "left_ocean":
                case "right_ocean":
                    site.Side = destination == "left_ocean" ? -1 : 1;
                    site.HorizontalRadius = 48;
                    site.VerticalRadius = 90;
                    site.Name = destination == "left_ocean" ? "左海岸" : "右海岸";
                    int inner = Math.Min(WorldGen.beachDistance - 12, Main.maxTilesX / 3);
                    int x = site.Side < 0 ? inner : Main.maxTilesX - inner;
                    feet = new Vector2(x * 16 + 8, (float)Main.worldSurface * 16);
                    break;
                case "selected":
                    // Freeze selection at the player input edge. Current map
                    // records can invalidate that ID but cannot substitute it.
                    if (execution.Anchor.MarkerSession != session || execution.Anchor.SelectedMarkerId == null)
                    { error = "发送消息时没有在当前世界定位已保存的标记。"; return false; }
                    var selected = (TaskMarkers() ?? Array.Empty<MarkerRecord>()).FirstOrDefault(marker =>
                        marker.Id == execution.Anchor.SelectedMarkerId);
                    if (selected == null) { error = "发送时定位的地图标记已不可用。"; return false; }
                    feet = new Vector2((float)selected.X * 16, (float)selected.Y * 16);
                    site.Name = selected.Name; site.Reference = "marker:" + selected.Id;
                    break;
                case "marker":
                case "memory":
                case "reference":
                    if (destination == "memory" || destination == "reference" && reference?.StartsWith("memory:",StringComparison.Ordinal) == true)
                    {
                        if (!TryTaskMemoryDestination(reference,out var remembered,out string name,out error)) return false;
                        feet = remembered.ToWorldCoordinates(8,0); site.Name = name; site.Reference = reference;
                        break;
                    }
                    if (reference?.StartsWith("pylon:", StringComparison.Ordinal) == true && Main.PylonSystem?.Pylons != null)
                    {
                        foreach (var pylon in Main.PylonSystem.Pylons)
                        {
                            if (PylonTaskReference(pylon) != reference) continue;
                            site.Pylon = pylon;
                            site.Name = PylonTaskName(pylon);
                            site.Reference = reference;
                            feet = pylon.PositionInTiles.ToWorldCoordinates();
                            break;
                        }
                        if (!site.Pylon.HasValue) { error = "当前世界没有该已登记晶塔。"; return false; }
                        break;
                    }
                    var npc = ReadTaskNpc(reference);
                    if (TaskNpcObserved(npc))
                    {
                        site.Npc = npc; site.NpcGeneration = npc.generation; site.Name = npc.FullName;
                        feet = npc.Bottom; site.Reference = NpcTaskReference(npc);
                        break;
                    }
                    if (reference?.StartsWith("marker:", StringComparison.Ordinal) == true) reference = reference.Substring(7);
                    var matches = (TaskMarkers() ?? Array.Empty<MarkerRecord>()).Where(marker => marker.Id == reference ||
                        String.Equals(marker.Name, reference, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
                    if (matches.Length != 1)
                    { error = matches.Length == 0 ? "未找到当前世界中对应的标记或仍有效的 NPC。" : "同名标记不止一个，请使用查询返回的引用。"; return false; }
                    feet = new Vector2((float)matches[0].X * 16, (float)matches[0].Y * 16);
                    site.Name = matches[0].Name; site.Reference = "marker:" + matches[0].Id;
                    break;
                default: return false;
            }
            var point = feet.ToTileCoordinates();
            site.X = point.X; site.Y = point.Y;
            return WorldGen.InWorld(site.X, site.Y, 4);
        }

        private static int AlternatingOffset(int index) => index == 0 ? 0 : ((index + 1) / 2) * (index % 2 == 0 ? -1 : 1);

        private bool FindTaskFooting(Actor actor, ArrivalSearch area, out Vector2 feet, out bool exhausted)
        {
            feet = Vector2.Zero;
            int width = area.HorizontalRadius * 2 + 1;
            int total = width * (area.VerticalRadius * 2 + 1);
            int limit = Math.Min(total, area.Next + (area.NearWater ? 16 : 96));
            for (; area.Next < limit; area.Next++)
            {
                int x = area.X + AlternatingOffset(area.Next % width), y = area.Y + AlternatingOffset(area.Next / width);
                if (!WorldGen.InWorld(x, y, 5) || area.Side != 0 &&
                    (!WorldGen.oceanDepths(x,y) || (x < Main.maxTilesX / 2 ? -1 : 1) != area.Side))
                    continue;
                if (!CompanionNativeContact.Loaded(x,y))
                {
                    area.Awaiting = new Point(x,y);
                    exhausted = false;
                    return false;
                }
                area.Awaiting = null;
                var tile = Main.tile[x,y];
                if (tile?.active() != true || tile.inActive() || !Main.tileSolid[tile.type] || Main.tileSolidTop[tile.type]) continue;
                var candidate = new Vector2(x * 16 + 8, y * 16);
                if (actor.Body.gravDir < 0) candidate.Y += 16;
                var topLeft = CompanionNativeContact.TopLeft(actor.Body, candidate);
                // Player.BordersMovement clamps this body before collision can
                // reach the world-edge drawing margin, including at an ocean.
                const float border = 640f;
                if (topLeft.X < Main.leftWorld+border || topLeft.X+actor.Body.width > Main.rightWorld-border ||
                    topLeft.Y < Main.topWorld+border || topLeft.Y+actor.Body.height > Main.bottomWorld-border ||
                    !CompanionNativeContact.Supported(actor.Body, topLeft) ||
                    Collision.SolidCollision(topLeft, actor.Body.width, actor.Body.height) ||
                    area.NearWater && !CoastalWaterNear(x,y,area.Side)) continue;
                feet = candidate;
                exhausted = false;
                return true;
            }
            exhausted = area.Next == total;
            return false;
        }

        private static bool CoastalWaterNear(int x, int y, int side)
        {
            for (int distance = 1; distance <= 20; distance++)
            {
                int column = x + side * distance;
                for (int depth = -3; depth <= 8; depth++)
                {
                    int row = y + depth;
                    if (!CompanionNativeContact.Loaded(column,row)) continue;
                    var tile = Main.tile[column,row];
                    if (tile.liquid > 0 && !tile.lava() && !tile.honey() && !tile.shimmer() &&
                        WorldGen.oceanDepths(column,row)) return true;
                }
            }
            return false;
        }

        private void CancelTravelTask(TaskExecution execution) => SetTaskTravel(execution.Actor, null);

        private void SetTaskTravel(Actor actor, TaskTravel target)
        {
            if (ReferenceEquals(actor.TaskTravel,target)) return;
            actor.TaskTravel = target;
            actor.Behavior.Navigator.IntentChanged(actor.Mobility);
            actor.AutoTeleport?.ResetObservation();
        }

        private CompanionTaskOutcome StandByTask(TaskExecution execution)
        {
            var actor = execution.Actor;
            if (actor.Fishing != null) EndFishingActivity(actor);
            actor.TaskControlId = execution.Job.Id;
            execution.Job.KeepControl = execution.StepIndex == execution.Job.Steps.Count - 1;
            SetTaskTravel(actor, new TaskTravel { Feet = CompanionNativeContact.Feet(actor.Body), Arrived = true, Name = "当前位置" });
            return TaskOutcome("succeeded", "已切换为原地待命。");
        }

        private CompanionTaskOutcome FollowTask(TaskExecution execution)
        {
            var actor = execution.Actor;
            if (actor.Fishing != null) EndFishingActivity(actor);
            actor.TaskControlId = execution.Job.Id;
            execution.Job.KeepControl = execution.StepIndex == execution.Job.Steps.Count - 1;
            SetTaskTravel(actor, new TaskTravel { FollowOwner = true, Name = "主人" });
            return TaskOutcome("succeeded", "已开始跟随主人。");
        }

        private void ClearTaskControl(Actor actor)
        {
            // Restoring a persisted off preference must hold here, without
            // sending the body back to where it was before the independent job.
            if (actor.TaskControlId!=Guid.Empty && !actor.Profile.Behavior.Follow) actor.Behavior.ResumeStandbyAt(actor.Body);
            actor.TaskControlId = Guid.Empty;
            SetTaskTravel(actor,null);
            if (actor.TaskCombat != null)
            {
                actor.TaskCombat = null;
                actor.Combat.Reset();
            }
            if (actor.Fishing?.OwnerTaskId != null) EndFishingActivity(actor);
        }

        private bool RequestTaskTerrain()
        {
            foreach (var actor in actors.Values)
            {
                var state = actor.TaskWork?.Current?.State;
                var projection = state as BlueprintWork;
                var site = state as ArrivalSearch ?? (state as TaskFish)?.Site ?? (state as WorldUse)?.Arrival ??
                    (state as TaskCritter)?.Site ?? (state as TaskMineArea)?.Work?.Arrival ??
                    (state as TaskMineArea)?.Travel?.State as ArrivalSearch ?? (state as TaskClearArea)?.Travel?.State as ArrivalSearch ?? projection?.Work?.Arrival;
                if ((site?.Awaiting ?? projection?.Awaiting) is Point point && TryRequestTerrain(point.X, point.Y, actor.Body.team)) return true;
            }
            return false;
        }
    }
}
