using System;
using JueMingR.Features.Blueprint;
using Terraria;

namespace JueMingR.TerrariaHost.Items
{
    internal sealed partial class ItemMaterialPreparation
    {
        private bool converting;
        private int conversionSource, conversionBuffer;
        private Item conversionMaterial;
        internal bool PreparingSupply(Player p,int type,int targetDestination,BlueprintProjectionIdentity target)
        {
            // An Update can precede the first/next real Draw tail. During a
            // conversion Material still denotes A until B is fully confirmed;
            // that is pending B, not a mismatch requesting A's inverse path.
            return OnGameThread && converting && phase==Phase.Preparing && destination==targetDestination &&
                conversionMaterial!=null && conversionMaterial.type==type && conversionMaterial.stack>0 &&
                identity!=null && identity.Equals(target) && SameSession(p);
        }
        private static bool NonStacking(Item a, Item b)
        { return a != null && b != null && (a.IsAir || b.IsAir || !Item.CanStack(a, b) && !Item.CanStack(b, a)); }

        // Additional endpoints join the settled paint receipt's unique token
        // before its first click. Cancellation retains its old inverse program
        // followed by the actually confirmed prefix of this conversion.
        internal bool TryConvertSupply(Player p, int nextSource, BlueprintProjectionIdentity nextIdentity)
        {
            if (!OnGameThread || phase != Phase.Ready || destination != 54 || converting || nextIdentity == null ||
                nextIdentity.SessionGeneration != session || !tools.Runtime.IsSessionActive || nativeCalls != 0 || lifecycleCalls != 0 ||
                tailPending || tailOpen || ownsInventory || returnRequested || !SameSession(p) || !InputOwnershipCurrent() ||
                !ExpectedCurrent(false) || !PreparationItemsNotPending() || !Main.mouseItem.IsAir ||
                nextSource < 0 || nextSource >= 50 || At(nextSource) != null || !EndpointAvailable(p, nextSource) ||
                !tools.BuildInventoryEligible(nextSource, p.inventory[nextSource])) return false;
            var next = p.inventory[nextSource];
            if (next.IsAir || !next.PaintOrCoating || !next.FitsAmmoSlot() || material == null || material.IsAir ||
                MaterialSlot() != 54 || !NonStacking(material, next) || displaced == null || displaced.IsAir) return false;
            int buffer = -1;
            bool first = endpointCount == 2 && historyCount == 3 && history[0] == sourceSlot && history[1] == 54 &&
                history[2] == sourceSlot && ReferenceEquals(inventory[sourceSlot], displaced);
            if (first)
            {
                for (int pass = 0; pass < 2 && buffer < 0; pass++)
                    for (int slot = 0; slot < 50; slot++)
                    {
                        if (slot == nextSource || At(slot) != null || !EndpointAvailable(p, slot)) continue;
                        var candidate = inventory[slot];
                        if (!IsDisplaced(candidate) && (pass == 0 ? candidate.IsAir : !candidate.IsAir) && NonStacking(candidate, displaced) && NonStacking(candidate, next))
                        { buffer = slot; break; }
                    }
                if (buffer < 0 || !NonStacking(material, displaced)) return false;
            }
            else
            {
                if (endpointCount != 3 || historyCount != 4 || history[0] != 54 || history[2] != sourceSlot || history[3] != 54) return false;
                buffer = history[1];
                if (buffer < 0 || buffer >= 50 || !ReferenceEquals(inventory[buffer], displaced) ||
                    !NonStacking(inventory[sourceSlot], material) || !NonStacking(inventory[sourceSlot], next)) return false;
            }
            AddEndpoint(nextSource); if (first) AddEndpoint(buffer);
            tools.Items.Ownership.EndUse(session, token);
            long nextToken = tools.Items.Ownership.NewUseToken();
            if (!tools.Items.Ownership.TryBeginUse(session, sourceSlot, nextToken, Slots))
            { LoseIdentity("material-conversion-owner-handoff", false); return false; }
            token = nextToken; identity = nextIdentity; receiptEpoch++; conversionSource = nextSource; conversionBuffer = buffer;
            conversionMaterial = next; converting = true; forwardCount = forwardCursor = 0;
            if (first)
            { forward[forwardCount++] = 54; forward[forwardCount++] = sourceSlot; forward[forwardCount++] = buffer; forward[forwardCount++] = nextSource; forward[forwardCount++] = 54; }
            else
            {
                forward[forwardCount++] = nextSource; forward[forwardCount++] = 54; forward[forwardCount++] = sourceSlot;
                if (!inventory[sourceSlot].IsAir) forward[forwardCount++] = nextSource;
            }
            phase = Phase.Preparing; Reason = null; return true;
        }

        private bool CompleteSupplyConversion()
        {
            if (!ExpectedCurrent(false) || !Main.mouseItem.IsAir || !ReferenceEquals(inventory[54], conversionMaterial))
            { LoseIdentity("material-conversion-not-complete", false); return false; }
            var previous = At(sourceSlot);
            if (!Matches(inventory[sourceSlot], material, material.type, material.stack) || previous == null || !ReferenceEquals(previous.Original, material))
            { LoseIdentity("material-conversion-old-source-not-returned", false); return false; }
            var next = At(conversionSource); var buffer = At(conversionBuffer); var dest = At(54);
            material = conversionMaterial; materialType = material.type; sourceSlot = conversionSource;
            // Fold to the equivalent D,T,S,D cleanup program. No Item moves or
            // counts change here; NativeClicks remains actual observed clicks.
            for (int i = 0; i < endpointCount; i++) endpoints[i] = null;
            endpoints[0] = next; endpoints[1] = dest; endpoints[2] = buffer; endpointCount = 3;
            historyCount = 4; history[0] = 54; history[1] = conversionBuffer; history[2] = sourceSlot; history[3] = 54;
            converting = false; conversionMaterial = null;
            tools.Items.Ownership.EndUse(session, token);
            long nextToken = tools.Items.Ownership.NewUseToken();
            if (!tools.Items.Ownership.TryBeginUse(session, sourceSlot, nextToken, Slots))
            { LoseIdentity("material-conversion-compact-handoff", false); return false; }
            token = nextToken; return true;
        }
    }
}
