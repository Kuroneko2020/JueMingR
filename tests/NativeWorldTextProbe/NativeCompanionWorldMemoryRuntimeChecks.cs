using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JueMingR.Features.Llm;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Llm;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionWorldMemoryRuntimeChecks
    {
        internal static void Run(object host,Player human,Player body,CompanionProfile profile,Action tick)
        {
            var fixture=NativeCompanionChatChecks.Attach(host);
            var actor=Call(host,"Owned",body);
            var writer=Main.player.Where(player=>player?.active==true && player!=human && player!=body)
                .Select(player=>new { Player=player,Actor=Call(host,"Owned",player) }).Single(entry=>entry.Actor!=null);
            var writerProfile=(CompanionProfile)Get(writer.Actor,"Profile");
            var before=Main.tile;
            var earlierWorldId=Main.ActiveWorldFileData.UniqueId;
            var oldBag=body.inventory.ToArray();
            var oldDrops=new HashSet<WorldItem>(Main.item.Where(item=>item?.active==true));
            var humanFeet=human.Bottom;
            var bodyFeet=body.Bottom;
            var writerFeet=writer.Player.Bottom;
            var selected=body.selectedItem;
            int humanSlot=human.selectedItem;
            var humanHeld=human.inventory[humanSlot];
            var preferences=((CompanionProfile)Get(actor,"Profile")).Behavior;
            var ownerMemory=Get(host,"SharedWorldMemory");
            ulong memoryTick=0;
            void PumpMemory()=>Call(ownerMemory,"Update",++memoryTick);
            var identity=new CompanionTaskIdentity(profile.OwnerId,profile.Id,(long)Get(host,"session"),(long)Get(actor,"Generation"),profile.DataEpoch);
            object View() => Call(host,"GetChat",profile.OwnerId,profile.Id);
            CompanionTaskSnapshot TaskView() => (CompanionTaskSnapshot)Call(host,"GetTask",profile.OwnerId,profile.Id,null);
            string Receipts()=>String.Join(" | ",((IReadOnlyList<LlmHistoryEntry>)Get(View(),"Entries")).Reverse()
                .Take(8).Select(entry=>entry.Text));
            void Script(string playerText,Func<LlmRequest,int,LlmTurn> choose)
            {
                int rounds=0;
                Exception failure=null;
                fixture.Remote.Reply=request=>
                {
                    try { return Task.FromResult(choose(request,++rounds)); }
                    catch (Exception error) { failure=error; throw; }
                };
                object[] args={profile.OwnerId,profile.Id,playerText,null};
                Require((bool)Call(host,"SendChat",args),"native world request sent: "+args[3]);
                NativeQuickItemChecks.Until(()=>{ Call(host,"PumpChat"); return !(bool)Get(View(),"IsRunning"); });
                if (failure!=null) throw new InvalidOperationException("Native chat tool round " + rounds, failure);
                Require(rounds==4,"default model turns completed with compact tool batches: "+rounds+" · "+Receipts());
            }
            LlmTurn Tools(params LlmToolCall[] calls) => new LlmTurn("",calls,"{}");
            LlmTurn Done() => new LlmTurn("请看实际操作回执。",Array.Empty<LlmToolCall>(),"{}");
            LlmToolCall Tool(string id,string name,params (string Key,object Value)[] fields) =>
                new LlmToolCall(id,name,"{}",fields.ToDictionary(field=>field.Key,field=>field.Value));
            string Result(LlmRequest request,string id)
            {
                var result=request.Messages.Last().Results?.SingleOrDefault(value=>value.Id==id);
                Require(result!=null && !result.IsError,"normal tool receipt for "+id+": "+result?.Output);
                return result.Output;
            }
            string MemoryRef(string receipt)
            {
                var match=Regex.Match(receipt,"memory:[a-f0-9]{32}",RegexOptions.CultureInvariant);
                Require(match.Success,"shared lookup returns actionable memory reference: "+receipt);
                return match.Value;
            }
            LlmToolCall Plan(string id,params IReadOnlyDictionary<string,object>[] steps) => new LlmToolCall(id,"submit_task","{}",
                new Dictionary<string,object>{["title"]=id,["steps"]=Array.AsReadOnly<object>(steps)});
            IReadOnlyDictionary<string,object> Step(string operation,params (string Key,object Value)[] fields) =>
                new Dictionary<string,object>{["operation"]=operation,["arguments"]=fields.ToDictionary(field=>field.Key,field=>field.Value)};
            void WorldTick()
            {
                tick();
                for(int i=0;i<Main.maxItems;i++)
                    if (Main.item[i]?.active==true && !oldDrops.Contains(Main.item[i])) Main.item[i].UpdateItem(i);
                Call(host,"AdvanceTaskShimmerDrops");
                PumpMemory();
            }
            void Pool(byte amount)
            {
                for(int y=47;y<=54;y++) for(int x=79;x<=84;x++)
                {
                    var cell=new Tile { liquid=amount };
                    cell.shimmer(true);
                    Main.tile[x,y]=cell;
                }
            }
            void Position(Player player,object playerActor,int x)
            {
                player.Bottom=new Vector2(x*16+8,55*16);
                player.velocity=Vector2.Zero;
                Call(Get(playerActor,"Behavior"),"PositionChanged",player);
            }
            try
            {
                NativeQuickItemChecks.Until(()=>{ Call(host,"PumpChat"); return (bool)Get(View(),"CanSend"); });
                Call(host,"RestartChatContext",profile.OwnerId,profile.Id);
                var follow=preferences.Clone(); follow.Follow=true; follow.AttackEnabled=false;
                Call(host,"SetBehavior",profile.Id,follow);
                Main.tile=(Tile[,])before.Clone();
                for(int x=49;x<=95;x++) for(int y=46;y<=59;y++)
                {
                    Main.tile[x,y]=new Tile();
                    if (y>=55) NativeToolsChecks.Tile(x,y,TileID.Stone);
                }
                body.Bottom=human.Bottom=new Vector2(77*16+8,55*16);
                body.velocity=Vector2.Zero;
                Call(Get(actor,"Behavior"),"PositionChanged",body);
                for(int x=76;x<=78;x++) NativeToolsChecks.Tile(x,54,TileID.Copper);
                Pool(255);
                Position(writer.Player,writer.Actor,77);
                body.inventory[36]=new Item(ItemID.CopperPickaxe);
                body.inventory[37]=new Item(ItemID.GoldPickaxe);
                // The adjacent archive-protection check intentionally leaves a
                // corrupt document on disk. This separate fixture reopens its
                // own stable GUID so it tests the full save/restore path.
                Call(ownerMemory,"OnSessionEnded");
                Main.ActiveWorldFileData.UniqueId=Guid.NewGuid();
                Call(ownerMemory,"OnSessionStarted");
                NativeQuickItemChecks.Until(()=>{ PumpMemory(); return (bool)Get(ownerMemory,"Ready") &&
                    GetOptional(Get(ownerMemory,"live"),"Saved")!=null; });
                var anchor=Call(host,"CaptureTaskSendAnchor",human);
                var seen=(CompanionTaskOutcome)Call(host,"TryGetTaskObservation",identity,"observe_tiles",new Dictionary<string,object>
                { ["around"]="companion",["radius"]=4,["tile_type"]=(int)TileID.Copper },anchor);
                string miningRow=seen.Detail.Split('；').Single(row=>row.StartsWith("(77,54)",StringComparison.Ordinal));
                string observed=miningRow.Substring(miningRow.LastIndexOf("· ",StringComparison.Ordinal)+2);
                var memory=(CompanionTaskOutcome)Call(host,"WriteWorldMemory",profile.Id,null,0,"验收铜矿点","真实观察",observed,"ore",null);
                string mine=memory.Value;
                Require(mine?.StartsWith("memory:",StringComparison.Ordinal)==true,"actual observation queued durable ore memory: "+memory.Detail);
                var writerIdentity=new CompanionTaskIdentity(writerProfile.OwnerId,writerProfile.Id,(long)Get(host,"session"),
                    (long)Get(writer.Actor,"Generation"),writerProfile.DataEpoch);
                var wet=(CompanionTaskOutcome)Call(host,"TryGetTaskObservation",writerIdentity,"observe_tiles",new Dictionary<string,object>
                { ["around"]="companion",["radius"]=4,["liquid"]="shimmer" },anchor);
                string wetRow=wet.Detail.Split('；').First(row=>row.StartsWith("(79,",StringComparison.Ordinal));
                var written=(CompanionTaskOutcome)Call(host,"WriteWorldMemory",writerProfile.Id,null,0,"聊天微光池",
                    "西侧矿道下面，从东边下去方便",wetRow.Substring(wetRow.LastIndexOf("· ",StringComparison.Ordinal)+2).Split(' ')[0],"shimmer",null);
                string pool=written.Value;
                Require(pool?.StartsWith("memory:",StringComparison.Ordinal)==true,"other actor recorded the physically observed pool: "+written.Detail);
                NativeQuickItemChecks.Until(()=>
                {
                    PumpMemory();
                    return ((CompanionTaskOutcome)Call(host,"ReadWorldMemory",mine)).State=="succeeded" &&
                        ((CompanionTaskOutcome)Call(host,"ReadWorldMemory",pool)).State=="succeeded";
                });
                var connections=(LlmConnections)Get(host,"ChatConnections");
                Require(connections.TryResolve(writerProfile.OwnerId,writerProfile.Id,out var firstModel,out _),"writer's initial model resolved");
                var alternate=connections.Copy(firstModel.Id);
                connections.ApplyPreset(alternate,"openai_official_responses");
                alternate.ApiKey="local-second-model-key";
                alternate.Model="gpt-5";
                connections.SaveAndUseAsync(alternate,profile.OwnerId,profile.Id).GetAwaiter().GetResult();
                Require(connections.TryResolve(profile.OwnerId,profile.Id,out var secondModel,out _) &&
                    secondModel.Id!=firstModel.Id && secondModel.Model!=firstModel.Model &&
                    secondModel.CopySettings().Protocol!=firstModel.CopySettings().Protocol,
                    "switch to an independently configured model and protocol");
                Call(ownerMemory,"OnSessionEnded");
                Call(ownerMemory,"OnSessionStarted");
                NativeQuickItemChecks.Until(()=>
                {
                    PumpMemory();
                    return ((CompanionTaskOutcome)Call(host,"ReadWorldMemory",mine)).State=="succeeded" &&
                        ((CompanionTaskOutcome)Call(host,"ReadWorldMemory",pool)).State=="succeeded";
                });
                Position(writer.Player,writer.Actor,62);
                body.Bottom=new Vector2(64*16+8,55*16);
                human.Bottom=new Vector2(60*16+8,55*16);
                body.velocity=Vector2.Zero;
                Call(Get(actor,"Behavior"),"PositionChanged",body);
                int ore=body.inventory.Sum(item=>item.type==ItemID.CopperOre?item.stack:0);
                string foundMine=null;
                Script("去之前记下的矿点挖三块铜矿，挖完在那里等我",(request,round)=>
                {
                    if (round==1) return Tools(Tool("mine-index","find_game_skills",("query","挖矿")),
                        Tool("mine-memory","find_world_memory",("filter","验收铜矿点"),("kind","ore")));
                    if (round==2)
                    {
                        Require(Result(request,"mine-index").Contains("mining · 挖矿") && !request.Instructions.Contains("镐力不够时"),
                            "keyword returns a short index without an eagerly injected skill body");
                        foundMine=MemoryRef(Result(request,"mine-memory"));
                        Require(foundMine==mine,"discovery resolved the actual remembered ore");
                        return Tools(Tool("mine-skill","read_game_skill",("key","mining")),
                            Tool("mine-detail","read_world_memory",("reference",foundMine)));
                    }
                    if (round==3)
                    {
                        Require(Result(request,"mine-skill").Contains("实际入包") || Result(request,"mine-skill").Contains("真实入包"),
                            "demanded mining skill distinguishes breaking and collecting");
                        Require(Result(request,"mine-detail").Contains("验收铜矿点"),"read actual world note");
                        return Tools(Plan("验收真实三块铜矿",Step("mine_area",("destination","memory"),("reference",foundMine),
                            ("tile_type",(int)TileID.Copper),("product_type",(int)ItemID.CopperOre),("mode","collect"),("amount",3)),Step("stand_by")));
                    }
                    Result(request,"验收真实三块铜矿");
                    return Done();
                });
                var accepted=TaskView();
                Require(accepted?.State=="accepted" && ((CompanionProfile)Get(actor,"Profile")).Behavior.Follow &&
                    (int)Call(host,"EffectiveFollow",profile)==0,"native chat accepted remembered task and temporarily disabled effective Follow: "+Receipts());
                int heldTool=0;
                for(int i=0;i<550 && TaskView().IsActive;i++)
                {
                    WorldTick();
                    var hand=GetOptional(actor,"TaskWorldHand");
                    if (hand!=null && GetOptional(hand,"Type") is int tool) heldTool=tool;
                }
                var done=TaskView();
                Require(done.State=="succeeded" && done.Attempts.Any(result=>result.Operation=="mine_area" && result.Completed==3) &&
                    Enumerable.Range(76,3).All(x=>!Main.tile[x,54].active()) &&
                    body.inventory.Sum(item=>item.type==ItemID.CopperOre?item.stack:0)==ore+3 && heldTool==ItemID.GoldPickaxe,
                    "native movement picked stronger eligible tool, broke three real tiles and picked up exactly three copper ore: "+done.Detail+
                    " · "+String.Join(" | ",done.Attempts.Select(result=>result.Detail))+" · "+body.Bottom+" · tool="+heldTool);
                Require((int)Call(host,"EffectiveFollow",profile)==0 && Math.Abs(body.Bottom.X-human.Bottom.X)>100,
                    "explicit wait keeps the real actor at the mine while owner is elsewhere");
                Require((bool)Call(host,"SetManualFollow",profile.OwnerId,profile.Id,true) && (int)Call(host,"EffectiveFollow",profile)==1,
                    "manual Follow reacquires from real model standby without changing the original preference");
                Console.WriteLine("PASS native exact remembered copper order after reopening world memory and switching model ids, three tiles and items, stronger tool and manual Follow.");

                int product=ShimmerTransforms.GetTransformToItem(ItemID.AngelStatue);
                Require(product>0,"original build has an actual transformation for this fixture item");
                void AskForShimmer()
                {
                    string foundPool=null;
                    Script("去上次那个微光池，把这件物品丢进去",(request,round)=>
                    {
                        if (round==1)
                        {
                            Require(request.Tools.Any(tool=>tool.Name=="find_game_skills") &&
                                request.Tools.Any(tool=>tool.Name=="read_game_skill") && !request.Instructions.Contains("用已记微光池处理"),
                                "normal compact first request exposes skill discovery and read, without their content");
                            return Tools(Tool("pool-index","find_game_skills",("query","微光")),
                                Tool("pool-memory","find_world_memory",("filter","聊天微光池"),("kind","shimmer")),
                                Tool("pool-held","get_companion_state"));
                        }
                        if (round==2)
                        {
                            Require(Result(request,"pool-index").Contains("shimmer · 微光转化") &&
                                Result(request,"pool-held").Contains("当前持物 #"+ItemID.AngelStatue),"guide index and actual held type were visible");
                            foundPool=MemoryRef(Result(request,"pool-memory"));
                            Require(foundPool==pool,"chat discovered the other actor's reopened world memory");
                            return Tools(Tool("pool-skill","read_game_skill",("key","shimmer")),
                                Tool("pool-detail","read_world_memory",("reference",foundPool)),
                                Tool("pool-source","get_game_item_sources",("item_type",(int)ItemID.AngelStatue),("kind","shimmer")));
                        }
                        if (round==3)
                        {
                            Require(Result(request,"pool-skill").Contains("held_at_send=true") &&
                                Result(request,"pool-detail").Contains("西侧矿道下面，从东边下去方便"),"normal tools supply guide and the other actor's actual saved note");
                            Result(request,"pool-source");
                            return Tools(Plan("验收微光投放",Step("shimmer_item",("target",foundPool),("item_type",(int)ItemID.AngelStatue),
                                ("amount",1),("held_at_send",true))));
                        }
                        Result(request,"验收微光投放");
                        return Done();
                    });
                    Require(TaskView()?.State=="accepted" && (int)Call(host,"EffectiveFollow",profile)==0,
                        "native chat accepted the specific remembered pool and released Follow: "+Receipts());
                }

                human.Bottom=new Vector2(63*16+8,55*16);
                Position(body,actor,69);
                // The exact utterance names neither item nor whose hand. Give
                // it the native send-time same-type witness. Only the actor pays.
                human.inventory[humanSlot]=new Item(ItemID.AngelStatue);
                body.inventory[21]=new Item(ItemID.AngelStatue) { stack=2 };
                body.selectedItemState.Select(21);
                Require(body.HeldItem.CanShimmer(),"the actual selected item is natively eligible");
                AskForShimmer();
                for(int i=0;i<780 && TaskView().IsActive;i++) WorldTick();
                var converted=TaskView();
                Require(converted.State=="succeeded" && converted.Attempts.Last().Completed==1 && body.inventory[21].stack==1 &&
                    human.inventory[humanSlot].type==ItemID.AngelStatue && human.inventory[humanSlot].stack==1 &&
                    Main.item.Any(item=>item?.active==true && !oldDrops.Contains(item) && item.shimmered && item.type==product),
                    "exact order moved and transformed one held item through vanilla: "+converted.Detail+" · "+
                    String.Join(" | ",converted.Attempts.Select(result=>result.Detail))+" · feet="+body.Bottom+" · "+
                    ((CompanionTaskOutcome)Call(host,"ReadWorldMemory",pool)).Detail);

                Position(body,actor,65);
                body.selectedItemState.Select(21);
                AskForShimmer();
                WorldTick();
                Pool(0);
                for(int i=0;i<780 && TaskView().IsActive;i++) WorldTick();
                var dry=TaskView();
                Require(dry.State=="failed" && dry.Detail.Contains("干涸") && body.inventory[21].stack==1 &&
                    human.inventory[humanSlot].type==ItemID.AngelStatue && human.inventory[humanSlot].stack==1,
                    "draining the selected pool on the way rejects it without a second debit: "+dry.Detail);
                NativeQuickItemChecks.Until(()=>
                {
                    PumpMemory();
                    var corrected=(CompanionTaskOutcome)Call(host,"ReadWorldMemory",pool);
                    return corrected.State=="succeeded" && corrected.Detail.Contains("失效");
                });
                Console.WriteLine("PASS native exact remembered Shimmer order with a different actor and model protocol, on-demand guide, one original transformed item and dry no-debit correction.");
            }
            finally
            {
                Call(host,"StopTask",profile.OwnerId,profile.Id,null);
                Call(host,"SetBehavior",profile.Id,preferences);
                fixture.Restore();
                Array.Copy(oldBag,body.inventory,oldBag.Length);
                foreach (var item in Main.item) if (item?.active==true && !oldDrops.Contains(item)) item.TurnToAir();
                Call(ownerMemory,"OnSessionEnded");
                Main.ActiveWorldFileData.UniqueId=earlierWorldId;
                Main.tile=before;
                body.Bottom=bodyFeet;
                writer.Player.Bottom=writerFeet;
                human.Bottom=humanFeet;
                human.inventory[humanSlot]=humanHeld;
                body.velocity=Vector2.Zero;
                writer.Player.velocity=Vector2.Zero;
                body.selectedItemState.Select(selected);
                Call(Get(actor,"Behavior"),"PositionChanged",body);
                Call(Get(writer.Actor,"Behavior"),"PositionChanged",writer.Player);
                Call(ownerMemory,"OnSessionStarted");
            }
        }
    }
}
