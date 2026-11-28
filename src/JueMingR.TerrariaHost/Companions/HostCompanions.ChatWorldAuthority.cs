using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JueMingR.Features.MapMarkers;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private sealed class ChatWorldSite
        {
            internal string Destination, Reference;
            internal Point? Point;
            internal IReadOnlyDictionary<int,(string Reference,Point Point)> Ore;
        }

        private static readonly Regex RememberedPlace = new Regex(
            @"^(?:(?:之前|上次|刚才|此前|前面)?(?:记(?:下|住|录|过)|保存|标记)(?:过)?的|(?:之前|上次|刚才|此前|前面)(?:那个|那处|的)?)\s*",
            RegexOptions.CultureInvariant);

        private void CaptureChatWorldAuthority(ChatAuthority authority,FishingAnchor anchor)
        {
            if (authority.ReferencesLastShimmer)
            {
                authority.PriorShimmer=LastTaskMemoryShimmerReference();
                if (TryTaskMemoryDestination(authority.PriorShimmer,out var point,out _,out _)) authority.PriorShimmerPoint=point;
            }
            string location=authority.WorkLocation();
            if (location==null) return;
            authority.PreparePlacementName();
            switch(location)
            {
                case "这里": case "这儿": case "我脚边": case "我脚下": case "我身边": case "我附近": case "我刚才那里":
                    authority.WorldSite=new ChatWorldSite { Destination="here",Point=anchor.Feet.ToTileCoordinates() };
                    return;
                case "你脚边": case "你脚下": case "你身边": case "你附近": case "队友脚边": case "队友附近":
                    authority.WorldSite=new ChatWorldSite { Destination="companion" };
                    return;
                case "指定位置": case "指定的地点": case "选中的标记": case "选中的位置": case "地图选点": case "定位的标记":
                    var selected=TaskMarkers()?.FirstOrDefault(marker=>marker.Id==anchor.SelectedMarkerId);
                    authority.WorldSite=MarkerWorkSite("selected",selected);
                    return;
            }
            string title=RememberedPlace.Replace(location,"").Trim();
            bool genericOre=title=="矿点" || title=="矿脉" || title=="矿区" || title=="那个矿点" || title=="那处矿点";
            var matches=(TaskMarkers() ?? Array.Empty<MarkerRecord>()).Where(marker=>
                String.Equals(marker.Name,location,StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            var ores=SharedWorldMemory?.CaptureSavedOreTargets(genericOre?null:title);
            if (matches.Length==1 && (ores?.Count ?? 0)==0 && location==title && !genericOre)
                authority.WorldSite=MarkerWorkSite("marker",matches[0]);
            else authority.WorldSite=new ChatWorldSite { Destination="memory",Ore=matches.Length==0?ores:null };
        }

        private static ChatWorldSite MarkerWorkSite(string destination,MarkerRecord marker) => new ChatWorldSite
        {
            Destination=destination,Reference=marker==null?null:"marker:"+marker.Id,
            Point=marker==null?(Point?)null:new Point((int)marker.X,(int)marker.Y)
        };

        private static string StepText(IReadOnlyDictionary<string,object> args,string key) =>
            args.TryGetValue(key,out var field)?field as string:null;

        private bool AuthorizeChatWorldSite(ChatRun run,IReadOnlyList<CompanionTaskStep> steps,ChatAuthority.Cost cost,ref FishingAnchor anchor)
        {
            var site=run.Authority.WorldSite;
            if (site==null)
            {
                if (!steps.Any(step=>step.Operation=="mine_area" && (step.Each.Count!=0 ||
                    StepText(step.Arguments,"destination")!="here" && StepText(step.Arguments,"destination")!="companion"))) return true;
                cost.Denial="本条消息没有明确指定远处矿点。请指定已保存的地点或从当前位置查找。";
                return false;
            }
            string reference=site.Reference;
            Point? point=site.Point;
            int oreType=-1;
            if (site.Destination=="memory")
            {
                var mining=steps.FirstOrDefault(step=>step.Operation=="mine_area");
                if (mining==null || mining.Each.Count!=0 || !mining.Arguments.TryGetValue("tile_type",out var type) || !(type is int tileType) ||
                    site.Ore?.TryGetValue(tileType,out var remembered)!=true ||
                    SharedWorldMemory?.SavedOreTargetCurrent(remembered.Reference,remembered.Point,tileType)!=true)
                { cost.Denial="无法确认发话时指定的已保存矿点，请确认地点和矿物名称。"; return false; }
                reference=remembered.Reference;
                point=remembered.Point;
                oreType=tileType;
            }
            else if (site.Destination=="companion" && actors.TryGetValue(run.Boundary.Profile,out var mover))
                point=CompanionNativeContact.Feet(mover.Body).ToTileCoordinates();
            if (!point.HasValue || reference?.StartsWith("marker:",StringComparison.Ordinal)==true && !MarkerWorkSiteCurrent(reference,point.Value))
            { cost.Denial="发话时指定的位置无法确认，请重新指定。"; return false; }

            bool beganWork=false;
            foreach (var step in steps)
            {
                if (step.Operation=="follow_owner" && (!beganWork || !run.Authority.ReturnsToOwner))
                { cost.Denial="当前指令没有要求在作业前后切换为跟随主人。"; return false; }
                if (step.Operation=="move_to" && beganWork && run.Authority.ReturnsToOwner && StepText(step.Arguments,"destination")=="owner" &&
                    step.Each.Count==0) continue;
                if (step.Operation=="move_to" || step.Operation=="mine_area")
                {
                    string destination=StepText(step.Arguments,"destination");
                    if (step.Each.Count!=0 || !(destination==site.Destination || destination=="reference" && reference!=null) ||
                        reference!=null && destination!="selected" && StepText(step.Arguments,"reference")!=reference)
                    { cost.Denial="操作地点与本条消息指定的位置不一致。"; return false; }
                    if (step.Operation=="mine_area") beganWork=true;
                }
                if (step.Operation!="world_use") continue;
                if (site.Destination=="memory" || !actors.TryGetValue(run.Boundary.Profile,out var actor) ||
                    (step.Each.Count==0 ? !MatchesWorldTile(actor,step.Arguments,point.Value) :
                        step.Each.Any(binding=>!MatchesWorldTile(actor,MergeTaskArguments(step.Arguments,binding),point.Value))))
                { cost.Denial="需要发话时指定位置的实际图格引用。远处矿点请到场重新观察。"; return false; }
                beganWork=true;
            }
            anchor=new FishingAnchor(anchor,reference,point.Value,oreType);
            return true;
        }

        private bool MarkerWorkSiteCurrent(string reference,Point point) => TaskMarkers()?.Any(marker=>
            "marker:"+marker.Id==reference && (int)marker.X==point.X && (int)marker.Y==point.Y)==true;

        private static bool MatchesWorldTile(Actor actor,IReadOnlyDictionary<string,object> args,Point point)
        {
            string token=StepText(args,"target");
            return token!=null && actor.TaskTiles?.TryGetValue(token,out var target)==true && target.Point==point;
        }
    }
}
