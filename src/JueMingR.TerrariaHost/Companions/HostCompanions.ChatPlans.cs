using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using JueMingR.Features.Llm;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.ItemBrowser;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private sealed class ChatStrategyGoal
        {
            internal string Goal, Boss, Destination, Reference, ReturnTo, Action, Npc;
            internal int ItemType, Quantity, Seconds, Budget;
            internal bool Nearby, Stay, DeliverToOwner;
        }

        private sealed class ChatStrategyCandidate
        {
            internal ChatStrategyGoal Intent;
            internal IReadOnlyList<CompanionTaskStep> Steps;
        }

        private readonly struct StrategyRecipe
        {
            internal StrategyRecipe(int id, int production, bool requiresNearby)
            { Id = id; Production = production; RequiresNearby = requiresNearby; }
            internal int Id { get; }
            internal int Production { get; }
            internal bool RequiresNearby { get; }
        }

        private List<CompanionTaskStep> BuildStrategySteps(ChatStrategyGoal goal, CompanionStrategy chosen,
            CompanionStrategyFacts facts, int quest, int alreadyOwned, StrategyRecipe? recipe, string source, Item equip)
        {
            var steps = new List<CompanionTaskStep>();
            var place = StrategyDestination(goal.Destination, goal.Reference);
            if (goal.Goal == "angler")
            {
                if (!facts.QuestAvailable) return steps;
                if (alreadyOwned == 0)
                {
                    if (!facts.HasBait || !facts.HasRod || place == null) return steps;
                    place["item_type"] = quest;
                    place["quantity"] = 1;
                    place["quest_fish"] = true;
                    place["notify_no_bait"] = true;
                    steps.Add(new CompanionTaskStep("fish_at", place));
                }
                if (goal.Action == "turn_in") steps.Add(new CompanionTaskStep("turn_in_angler", StrategyNpc(goal.Npc)));
            }
            else if (goal.Goal == "prepare_boss")
            {
                // The chosen branch owns both the advice and executable preparation.
                // A legal recipe alone cannot override the earlier combat/first-win/time decisions.
                switch (chosen.BossPreparation)
                {
                    case BossPreparationChoice.Mobility:
                        if (StrategyMobilityItem(equip)) steps.Add(StrategyEquip(equip));
                        else if (place != null) steps.Add(new CompanionTaskStep("move_to", place));
                        break;
                    case BossPreparationChoice.Healing:
                        if (ContentSamples.ItemsByType.TryGetValue(goal.ItemType, out var healing) && healing.healLife > 0)
                            AddStrategySupply(steps, goal, goal.Quantity - alreadyOwned, recipe, source);
                        break;
                    case BossPreparationChoice.Ready:
                        if (place != null) steps.Add(new CompanionTaskStep("move_to", place));
                        break;
                }
            }
            else if (goal.Goal == "craft" || goal.Goal == "gather" || goal.Goal == "supplies")
            {
                int amount = goal.Goal == "craft" ? goal.Quantity : goal.Quantity - alreadyOwned;
                AddStrategySupply(steps, goal, amount, recipe, source);
                if (goal.DeliverToOwner && steps.Count != 0)
                {
                    // Native recipes may make a whole batch larger than the requested delivery.
                    object delivered = recipe.Value.Production == amount ? (object)"$previous" : amount;
                    steps.Add(new CompanionTaskStep("transfer_item", new Dictionary<string, object>
                    {
                        ["item_type"] = goal.ItemType, ["amount"] = delivered,
                        ["source"] = "self_inventory", ["destination"] = "owner_inventory"
                    }));
                }
            }
            else if (goal.Goal == "improve" && equip != null || goal.Goal == "hardmode" && facts.HardMode && equip != null)
            {
                steps.Add(StrategyEquip(equip));
            }
            else if (goal.Goal == "arena" && goal.Action == "build")
            {
                var blueprint = TaskBlueprint();
                if (facts.TerrainKnown && (goal.Boss != "plantera" || facts.InJungle) && goal.Reference != null &&
                    blueprint?.ControlsEnabled == true && blueprint.ProjectionScene.Any(item =>
                        item.Positioned && item.Identity != null && ProjectionReference(item) == goal.Reference))
                    steps.Add(new CompanionTaskStep("build_projection",
                        new Dictionary<string, object> { ["reference"] = goal.Reference }));
            }
            else if (goal.Goal == "services" && goal.Action != null)
            {
                var service = StrategyNpc(goal.Npc);
                if (goal.Action == "heal")
                {
                    service["budget_copper"] = goal.Budget;
                    steps.Add(new CompanionTaskStep("nurse_heal", service));
                }
                else if (goal.Action == "buy" || goal.Action == "sell")
                {
                    service["item_type"] = goal.ItemType;
                    service["amount"] = goal.Quantity;
                    if (goal.Action == "buy") service["budget_copper"] = goal.Budget;
                    steps.Add(new CompanionTaskStep(goal.Action == "buy" ? "buy_item" : "sell_item", service));
                }
            }
            else if (goal.Goal == "explore" && facts.HasRoom && facts.HasLight && facts.HasReturn && facts.HasHealing ||
                goal.Goal == "arena" && facts.TerrainKnown && (goal.Boss != "plantera" || facts.InJungle) ||
                goal.Goal == "services")
            {
                if (place == null) return steps;
                steps.Add(new CompanionTaskStep("move_to", place));
            }
            else if ((goal.Goal == "fight" || goal.Goal == "lunar") && facts.InCombat && goal.Reference != null &&
                TaskNpcObserved(ReadTaskNpc(goal.Reference)))
            {
                steps.Add(new CompanionTaskStep("attack_npc", new Dictionary<string, object> { ["target"] = goal.Reference }));
            }
            if (steps.Count == 0 && (goal.Goal != "angler" || alreadyOwned == 0)) return steps;
            if (goal.Npc != null && !TaskNpcObserved(ReadTaskNpc(goal.Npc))) return new List<CompanionTaskStep>();
            var back = StrategyDestination(goal.ReturnTo, null);
            if (back != null) steps.Add(new CompanionTaskStep("move_to", back));
            if (goal.Stay) steps.Add(new CompanionTaskStep("stand_by", NoOperationArguments));
            // Strategy issuance and direct plans use the same parameter
            // definitions. Unknown operations remain a visible gap, even if
            // a playbook mentions them as a possible route.
            EnsureTaskRegistry();
            return steps.All(step =>
                taskOperations.TryGetValue(step.Operation, out var operation) &&
                TaskArgumentsValid(operation, step.Arguments, true, out _)) ? steps : new List<CompanionTaskStep>();
        }

        private static Dictionary<string, object> StrategyNpc(string npc) => npc == null
            ? new Dictionary<string, object>() : new Dictionary<string, object> { ["npc"] = npc };

        private static bool StrategyMobilityItem(Item item) => item != null &&
            (item.wingSlot > 0 || NativeKnowledgeQuery.Matches(item, "grapple", 0, null));

        private static CompanionTaskStep StrategyEquip(Item item) => new CompanionTaskStep("equip_item",
            new Dictionary<string, object> { ["item_type"] = item.type });

        private static void AddStrategySupply(List<CompanionTaskStep> steps, ChatStrategyGoal goal,
            int amount, StrategyRecipe? recipe, string source)
        {
            if (amount <= 0) return;
            if (goal.Goal != "craft" && source != null)
            {
                steps.Add(new CompanionTaskStep("transfer_item", new Dictionary<string, object>
                {
                    ["item_type"] = goal.ItemType, ["amount"] = amount,
                    ["source"] = source, ["destination"] = "self_inventory"
                }));
            }
            else if (recipe.HasValue)
            {
                var args = new Dictionary<string, object>
                { ["item_type"] = goal.ItemType, ["amount"] = amount, ["recipe_id"] = recipe.Value.Id };
                if (recipe.Value.RequiresNearby) args["allow_nearby"] = true;
                steps.Add(new CompanionTaskStep("craft_item", args));
            }
        }

        private static Dictionary<string, object> StrategyDestination(string place, string reference)
        {
            if (String.IsNullOrEmpty(place)) return null;
            var args = new Dictionary<string, object> { ["destination"] = place };
            if (reference != null) args["reference"] = reference;
            return args;
        }

        private StrategyRecipe? FindStrategyRecipe(ChatRun run, ChatStrategyGoal goal, int missing)
        {
            if (goal.ItemType == 0 || missing <= 0 ||
                !new[] { "craft", "gather", "supplies", "prepare_boss" }.Contains(goal.Goal, StringComparer.Ordinal)) return null;
            var args = new Dictionary<string, object> { ["item_type"] = goal.ItemType, ["amount"] = missing };
            var own = TryGetTaskObservation(TaskIdentity(run.Boundary), "craft_availability", args, run.Anchor);
            var ready = ReadStrategyRecipe(own, missing, false);
            if (ready.HasValue || !goal.Nearby) return ready;
            // Crafting readiness and authority come from the same registered
            // consumer that will acquire resources at actual execution.
            args["allow_nearby"] = true;
            return ReadStrategyRecipe(TryGetTaskObservation(TaskIdentity(run.Boundary), "craft_availability", args, run.Anchor),
                missing, true);
        }

        private static StrategyRecipe? ReadStrategyRecipe(CompanionTaskOutcome observed, int requested, bool nearby)
        {
            return observed.Completed >= requested && observed.Value?.StartsWith("recipe:", StringComparison.Ordinal) == true &&
                Int32.TryParse(observed.Value.Substring(7), NumberStyles.None, CultureInfo.InvariantCulture, out int id)
                ? new StrategyRecipe(id, observed.Completed, nearby) : (StrategyRecipe?)null;
        }

        private string FindStrategyResource(ChatRun run, ChatStrategyGoal intent, int missing)
        {
            if (intent.ItemType == 0 || missing <= 0 || !intent.Nearby ||
                intent.Goal != "gather" && intent.Goal != "supplies" && intent.Goal != "prepare_boss") return null;
            var current = TryGetTaskObservation(TaskIdentity(run.Boundary), "resource_sources",
                new Dictionary<string, object> { ["item_type"] = intent.ItemType }, run.Anchor);
            string source = current.Value;
            // Current plans never infer permission to take the owner's
            // inventory from a discovery result.
            return current.Completed >= missing && source != null &&
                (source == "self_piggy" || source == "self_safe" || source == "self_forge" || source == "self_void" ||
                 source.StartsWith("chest:", StringComparison.Ordinal)) ? source : null;
        }
    }
}
