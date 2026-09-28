using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using JueMingR.Features.Fishing;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.FishDropRules;
using Terraria.ID;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.Fishing
{
    internal sealed class FishingCandidates
    {
        internal readonly bool Known;
        internal readonly FishKey[] Keys;
        internal readonly string Message;
        internal FishingCandidates(bool known,FishKey[] keys,string message=null){Known=known;Keys=keys;Message=message;}
        internal static FishingCandidates Unknown(string message){return new FishingCandidates(false,Array.Empty<FishKey>(),message);}
    }

    // Current-water queries and the global search directory are separate. Neither
    // calls TryBuildFishingContext/GetDisplayableDrops: both advance native RNG.
    // The private context has no random source and never replaces Projectile._context.
    internal sealed class FishingCatalog
    {
        private const int WaterReadBudget=65536;
        private static readonly int[] SearchNpcs={586,587,620,621,618,682};
        private static readonly FieldInfo RulesField=typeof(FishDropRuleList).GetField("_rules",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly Type DelegateType=typeof(AFishDropRulePopulator).GetNestedType("DelegateFishingCondition",BindingFlags.NonPublic);
        private static readonly Type ClosureType=typeof(AFishDropRulePopulator).GetNestedType("<>c",BindingFlags.NonPublic);
        private static readonly FieldInfo ConditionField=DelegateType?.GetField("_condition",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly HashSet<MethodInfo> Conditions=NativeConditions();
        private readonly FishingContext context=new FishingContext{Random=null};
        private readonly HashSet<FishKey> seen=new HashSet<FishKey>();
        private readonly List<FishKey> collected=new List<FishKey>();
        private FishKey[] directory=Array.Empty<FishKey>();
        private FishDropRuleList directoryOwner;
        private ulong directoryVersion;
        private Player player;
        private Projectile bobber;
        private int key,x,y;
        private uint refreshed;
        private FishingCandidates cached=FishingCandidates.Unknown("当前没有可用钓点。");
        private bool hasInputs;
        private InputSignature inputs;
        internal long TileReads {get;private set;}
        internal long RuleChecks {get;private set;}
        internal long CatalogBuilds {get;private set;}
        internal long ShapeReads {get;private set;}
        internal long VersionReads {get;private set;}

        internal static string Name(FishKey fish)
        {if(fish.Id>=(fish.Kind==FishKind.Item?ItemID.Count:NPCID.Count))return (fish.Kind==FishKind.Item?"未知物品 #":"未知 NPC #")+fish.Id;return fish.Kind==FishKind.Item?Lang.GetItemNameValue(fish.Id):Lang.GetNPCNameValue(fish.Id);}
        internal static bool Quest(FishKey fish){return fish.Kind==FishKind.Item && Array.IndexOf(Main.anglerQuestItemNetIDs,fish.Id)>=0;}
        internal static bool Crate(FishKey fish){return fish.Kind==FishKind.Item && fish.Id<ItemID.Sets.IsFishingCrate.Length && ItemID.Sets.IsFishingCrate[fish.Id];}
        internal static bool Sonar(Player p){if(p==null)return false;int i=p.FindBuffIndex(122);return i>=0 && p.buffTime[i]>0;}
        internal bool Keep(FishingOptions value,FishKey fish,Player p){return FishFilter.Keep(value,fish,Name(fish),Sonar(p),Crate(fish),Quest(fish));}
        internal void Clear(){if(player==null && bobber==null && !hasInputs)return;player=null;bobber=null;hasInputs=false;cached=FishingCandidates.Unknown("当前没有可用钓点。");}

        internal FishKey[] Search(string query)
        {
            if(string.IsNullOrWhiteSpace(query))return Array.Empty<FishKey>();
            query=query.Trim();var rules=Rules();if(rules==null)return Array.Empty<FishKey>();
            ulong version=Version(rules);
            if(!ReferenceEquals(directoryOwner,Main.FishDropsDB) || directoryVersion!=version)
            {
                Begin();foreach(var rule in rules)foreach(int id in rule.PossibleItems)Add(FishKind.Item,id);
                foreach(int id in SearchNpcs)Add(FishKind.Npc,id);
                directory=collected.ToArray();directoryOwner=Main.FishDropsDB;directoryVersion=version;CatalogBuilds++;
            }
            int idQuery;bool idOnly=query[0]=='#';bool numeric=int.TryParse(idOnly?query.Substring(1):query,NumberStyles.None,CultureInfo.InvariantCulture,out idQuery);
            var results=new List<FishKey>();
            foreach(var fish in directory)
            {
                if(numeric && fish.Id==idQuery){results.Add(fish);continue;}
                if(idOnly)continue;
                string internalName=fish.Kind==FishKind.Item?ItemID.Search.GetName(fish.Id):NPCID.Search.GetName(fish.Id);
                if(Name(fish).IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0 || internalName!=null && internalName.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)results.Add(fish);
            }
            return results.ToArray();
        }

        internal FishingCandidates Current(Player p,Projectile b,bool force)
        {
            if(p==null || b==null || !b.active || !b.bobber || b.owner!=p.whoAmI || (int)b.key==-1 || !b.wet || b.shimmerWet || b.ai[0]>=1 || p.HeldItem==null || p.HeldItem.fishingPole<=0)
            {Clear();return cached;}
            int bx=(int)(b.Center.X/16),by=(int)(b.Center.Y/16);
            // A finite polling interval observes water edits without scanning a pond
            // every frame. User-requested "添加当前" refreshes at the click itself.
            if(!force && ReferenceEquals(player,p) && ReferenceEquals(bobber,b) && key==(int)b.key && x==bx && y==by && unchecked(Main.GameUpdateCount-refreshed)<30)return cached;
            player=p;bobber=b;key=(int)b.key;x=bx;y=by;refreshed=Main.GameUpdateCount;
            FishingCandidates next;
            try{next=Build(p,b);}catch(Exception){next=FishingCandidates.Unknown("当前鱼获暂不可读取。");}
            if(!Same(cached,next))cached=next;
            return cached;
        }
        private static bool Same(FishingCandidates a,FishingCandidates b)
        {if(a.Known!=b.Known || a.Message!=b.Message || a.Keys.Length!=b.Keys.Length)return false;for(int i=0;i<a.Keys.Length;i++)if(!a.Keys[i].Equals(b.Keys[i]))return false;return true;}

        private FishingCandidates Build(Player p,Projectile b)
        {
            var rules=Rules();if(rules==null)return FishingCandidates.Unknown("当前鱼获规则暂不可读取。");
            if(p.wet && b.Center.Y<p.RotatedRelativePoint(p.MountedCenter).Y)return FishingCandidates.Unknown("当前钓点条件不适用。");
            var f=new FishingAttempt{X=x,Y=y,bobberType=b.type};
            if(!Pond(x,y,out f.inLava,out f.inHoney,out f.waterTilesCount,out f.chumsInWater))return FishingCandidates.Unknown("水域范围过大或暂不可读取。");
            if(f.waterTilesCount<75)return FishingCandidates.Unknown("当前水量不足。");
            // GetFishingConditions reads the native pole, bait slots/pouch, skill,
            // weather and sitting bonus. Guard its sitting helper's GetTileSafely.
            var seat=(p.Bottom-new Vector2(0,2)).ToTileCoordinates();
            if(p.sitting.isSitting && (seat.X<0 || seat.Y<0 || seat.X>=Main.maxTilesX || seat.Y>=Main.maxTilesY || Main.tile[seat.X,seat.Y]==null))return FishingCandidates.Unknown("当前坐姿位置暂不可读取。");
            f.playerFishingConditions=p.GetFishingConditions();
            int bait=f.playerFishingConditions.BaitItemType,pole=f.playerFishingConditions.PoleItemType;
            // This known empty result is outside the ordinary rule signature.
            // Do not reuse it when the player returns to the preceding bait.
            if(bait==2673){hasInputs=false;return new FishingCandidates(true,Array.Empty<FishKey>(),"松露虫用于召唤，不显示普通鱼获。");}
            f.fishingLevel=f.playerFishingConditions.FinalFishingLevel;
            if(f.fishingLevel<=0)return FishingCandidates.Unknown("当前没有有效鱼饵或渔力。");
            if(bait<0 || bait>=ItemID.Sets.IsLavaBait.Length || pole<0 || pole>=ItemID.Sets.CanFishInLava.Length || Main.worldSurface<=0)return FishingCandidates.Unknown("当前钓鱼条件暂不可读取。");
            f.CanFishInLava=ItemID.Sets.CanFishInLava[pole] || ItemID.Sets.IsLavaBait[bait] || p.accLavaFishing;
            f.fishingLevel+=f.chumsInWater>2?20:f.chumsInWater>1?17:f.chumsInWater>0?11:0;
            float size=Main.maxTilesX/4200f;
            f.atmo=(float)((b.position.Y/16f-(60+10*size*size))/(Main.worldSurface/6));
            f.atmo=Math.Max(.25f,Math.Min(1,f.atmo));f.waterNeededToFish=(int)(300*f.atmo);
            float quality=(float)f.waterTilesCount/f.waterNeededToFish;
            if(quality<1)f.fishingLevel=(int)(f.fishingLevel*quality);
            f.waterQuality=1-quality;
            f.questFish=Main.anglerQuest>=0 && Main.anglerQuest<Main.anglerQuestItemNetIDs.Length?Main.anglerQuestItemNetIDs[Main.anglerQuest]:-1;
            if(f.questFish<0 || Main.anglerQuestFinished || p.HasItem(f.questFish) || !NPC.AnyNPCs(369))f.questFish=-1;
            int height=y<Main.worldSurface*.5?0:y<Main.worldSurface?1:y<Main.rockLayer?(Main.remixWorld?3:2):y<Main.maxTilesY-300?(Main.remixWorld?2:3):4;
            bool junk=f.waterTilesCount<f.waterNeededToFish && f.fishingLevel<=(p.luck<0?81:p.luck>=1?44:48);
            var signature=new InputSignature{Height=height,Quest=f.questFish,Rules=Version(rules),Flags=
                Bit(f.inLava,0)|Bit(f.inHoney,1)|Bit(f.CanFishInLava,2)|Bit(junk,3)|Bit(f.waterTilesCount>1000,4)|
                Bit(x<380 || x>Main.maxTilesX-380,5)|Bit(y>=Main.rockLayer,6)|
                Bit(p.ZoneCorrupt,7)|Bit(p.ZoneCrimson,8)|Bit(p.ZoneJungle,9)|Bit(p.ZoneSnow,10)|Bit(p.ZoneDungeon && NPC.downedBoss3,11)|
                Bit(p.ZoneDesert,12)|Bit(p.ZoneHallow,13)|Bit(p.ZoneGlowshroom,14)|Bit(p.ZoneBeach,15)|
                Bit(Main.hardMode,16)|Bit(Main.remixWorld,17)|Bit(Main.notTheBeesWorld,18)|Bit(Main.bloodMoon,19)|
                Bit(Main.dayTime,20)|Bit(NPC.combatBookWasUsed,21)|Bit(NPC.unlockedSlimeRedSpawn,22)};
            // Weather/time/equipment changes first pass through native power and
            // pond sampling. Shape/version validation still visits the directory;
            // equal normalized conditions avoid reevaluating the rule predicates.
            if(hasInputs && cached.Known && inputs.Equals(signature))return cached;
            inputs=signature;hasInputs=true;
            Begin();context.Player=p;
            // Enumerate correlated reachable alternatives, never simultaneously
            // assert incompatible corruption/crimson or disregard native stoppers.
            for(int h=0;h<(Main.remixWorld && height==2?2:1);h++)
            for(int honey=0;honey<(Main.notTheBeesWorld && f.inHoney?2:1);honey++)
            for(int jungle=0;jungle<(Main.notTheBeesWorld && !Main.remixWorld && p.ZoneJungle?2:1);jungle++)
            {
                var variant=f;variant.heightLevel=h==1?1:height;variant.inHoney=honey==0 && f.inHoney;
                context.RolledJungle=jungle==0 && p.ZoneJungle;
                bool suppressEvil=Main.remixWorld && variant.heightLevel==0;
                bool both=!suppressEvil && p.ZoneCorrupt && p.ZoneCrimson;
                context.RolledDesert=p.ZoneDesert && !(p.ZoneDungeon && NPC.downedBoss3);
                for(int evil=0;evil<(both?2:1);evil++)
                for(int snow=0;snow<(p.ZoneSnow && context.RolledJungle?2:1);snow++)
                for(int desert=0;desert<(context.RolledDesert?2:1);desert++)
                for(int ocean=0;ocean<(Main.remixWorld && variant.heightLevel==1 && y>=Main.rockLayer?2:1);ocean++)
                for(int crate=0;crate<2;crate++)
                for(int j=0;j<(junk?2:1);j++)
                {
                    context.RolledCorruption=!suppressEvil && p.ZoneCorrupt && (!both || evil==0);
                    context.RolledCrimson=!suppressEvil && p.ZoneCrimson && (!both || evil==1);
                    context.RolledSnow=snow==0 && p.ZoneSnow;context.RolledInfectedDesert=desert==1;context.RolledRemixOcean=ocean==1;
                    variant.crate=crate==1;variant.junk=j==1;context.Fisher=variant;
                    foreach(var rule in rules)
                    {
                        RuleChecks++;if(!rule.MeetsConditions(context,true))continue;
                        foreach(int id in rule.PossibleItems)Add(FishKind.Item,id);
                        if(rule.IsStopper)break;
                    }
                    if(!variant.inLava && !variant.inHoney && Main.bloodMoon && !Main.dayTime)
                    {
                        Add(FishKind.Npc,586);Add(FishKind.Npc,587);
                        if(Main.hardMode){Add(FishKind.Npc,620);Add(FishKind.Npc,621);Add(FishKind.Npc,618);}
                        if(!NPC.unlockedSlimeRedSpawn)Add(FishKind.Npc,682);
                    }
                }
            }
            return new FishingCandidates(true,collected.ToArray());
        }

        private bool Pond(int px,int py,out bool lava,out bool honey,out int water,out int chum)
        {
            lava=honey=false;water=chum=0;
            if(Main.tile==null || Main.instance==null || px<=10 || py<0 || px>=Main.maxTilesX-10 || py>=Main.maxTilesY-10)return false;
            int budget=WaterReadBudget,left=px,right=px;
            while(left>10 && Wet(left,py,ref budget))left--;
            while(right<Main.maxTilesX-10 && Wet(right,py,ref budget))right++;
            for(int i=left;i<=right;i++)for(int j=py;j<Main.maxTilesY-10 && Wet(i,j,ref budget);j++)
            {
                water++;var tile=Main.tile[i,j];if(tile.lava())lava=true;else if(tile.honey())honey=true;
                chum+=Main.instance.ChumBucketProjectileHelper.GetChumsInLocation(new Point(i,j));
            }
            if(honey)water=(int)(water*1.5);
            return budget>=0;
        }
        private bool Wet(int tx,int ty,ref int budget)
        {
            if(--budget<0)throw new InvalidOperationException("pond budget exhausted");
            TileReads++;var tile=Main.tile[tx,ty];return tile!=null && tile.liquid>0 && !WorldGen.SolidTile(tx,ty);
        }
        private List<FishDropRule> Rules()
        {
            var rules=Main.FishDropsDB==null || RulesField==null?null:RulesField.GetValue(Main.FishDropsDB) as List<FishDropRule>;
            if(rules==null || Conditions.Count!=37 || ConditionField==null)return null;
            // The known fixed-assembly delegates and quest conditions are pure.
            // Unknown subclasses/delegates must not run: a null context RNG alone
            // would not stop an arbitrary condition from accessing Main.rand.
            var checkedConditions=new HashSet<AFishingCondition>();
            foreach(var rule in rules)
            {
                ShapeReads++;
                if(rule==null || rule.GetType()!=typeof(FishDropRule) || rule.PossibleItems==null || rule.Conditions==null || rule.Rarity==null || rule.ChanceDenominator<=0 || rule.ChanceNumerator<=0 || rule.ChanceNumerator>rule.ChanceDenominator)return null;
                foreach(int id in rule.PossibleItems){ShapeReads++;if(id<=0 || id>=ItemID.Count)return null;}
                foreach(var condition in rule.Conditions)
                {
                    ShapeReads++;
                    if(condition==null)return null;if(!checkedConditions.Add(condition))continue;
                    Type type=condition.GetType();
                    if(type==typeof(FishingConditions.QuestFishCondition) || type==typeof(FishingConditions.QuestFishConditionRemix))continue;
                    if(type!=DelegateType)return null;
                    var callback=ConditionField.GetValue(condition) as Delegate;
                    if(callback==null || callback.GetInvocationList().Length!=1 || callback.Target?.GetType()!=ClosureType || !Conditions.Contains(callback.Method))return null;
                }
            }
            return rules;
        }
        private static HashSet<MethodInfo> NativeConditions()
        {
            var result=new HashSet<MethodInfo>();if(ClosureType==null)return result;
            foreach(var method in ClosureType.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
            {var args=method.GetParameters();if(method.ReturnType==typeof(bool) && args.Length==1 && args[0].ParameterType==typeof(FishingContext))result.Add(method);}
            return result;
        }
        private ulong Version(List<FishDropRule> rules)
        {
            ulong hash=14695981039346656037UL;
            foreach(var rule in rules)
            {
                VersionReads++;
                Mix(ref hash,RuntimeHelpers.GetHashCode(rule));Mix(ref hash,rule.ChanceNumerator);Mix(ref hash,rule.ChanceDenominator);Mix(ref hash,rule.Rarity.HackedIsAny?1:0);
                Mix(ref hash,rule.PossibleItems.Length);foreach(int id in rule.PossibleItems){VersionReads++;Mix(ref hash,id);}
                Mix(ref hash,rule.Conditions.Length);foreach(var condition in rule.Conditions)
                {
                    VersionReads++;
                    Mix(ref hash,RuntimeHelpers.GetHashCode(condition));
                    var quest=condition as FishingConditions.QuestFishCondition;var remix=condition as FishingConditions.QuestFishConditionRemix;
                    if(quest!=null)Mix(ref hash,quest.CheckedType);else if(remix!=null)Mix(ref hash,remix.CheckedType);else Mix(ref hash,RuntimeHelpers.GetHashCode(ConditionField.GetValue(condition)));
                }
            }
            return hash;
        }
        private static void Mix(ref ulong hash,int value){hash=unchecked((hash^(uint)value)*1099511628211UL);}
        private static int Bit(bool value,int shift){return value?1<<shift:0;}
        private struct InputSignature
        {internal int Height,Quest,Flags;internal ulong Rules;internal bool Equals(InputSignature other){return Height==other.Height && Quest==other.Quest && Flags==other.Flags && Rules==other.Rules;}}
        private void Begin(){seen.Clear();collected.Clear();}
        private void Add(FishKind kind,int id){if(id<=0 || id>=(kind==FishKind.Item?ItemID.Count:NPCID.Count))return;var value=new FishKey(kind,id);if(seen.Add(value))collected.Add(value);}
    }
}
