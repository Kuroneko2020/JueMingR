using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using JueMingR.Infrastructure.Companions;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Llm;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionCommandSemanticsChecks
    {
        internal static void Run(object host,Player human,Player body,CompanionProfile profile,Action tick)
        {
            var fixture=NativeCompanionChatChecks.Attach(host);
            var actor=Call(host,"Owned",body);
            var archive=Get(host,"profiles");
            var preferences=((CompanionProfile)Get(actor,"Profile")).Behavior;
            var previousTiles=Main.tile;
            var previousNpcs=Main.npc;
            var previousGore=Main.gore.ToArray();
            var previousBestiary=Main.BestiaryTracker;
            var previousBestiaryDatabase=Main.BestiaryDB;
            var previousDropDatabase=Main.ItemDropsDB;
            var previousDropSolver=Main.ItemDropSolver;
            var previousBag=body.inventory.ToArray();
            var previousHuman=human.Bottom;
            var previousBody=body.Bottom;
            string nativeCombatFault=null;
            EventHandler<FirstChanceExceptionEventArgs> traceCombat=(_,trace)=>
            {
                if (nativeCombatFault==null && trace.Exception is NullReferenceException) nativeCombatFault=trace.Exception.ToString();
            };
            object Chat() => Call(host,"GetChat",profile.OwnerId,profile.Id);
            CompanionProfile Profile() => (CompanionProfile)Get(actor,"Profile");
            CompanionTaskSnapshot View() => (CompanionTaskSnapshot)Call(host,"GetTask",profile.OwnerId,profile.Id,null);
            string Receipts() => String.Join(" | ",((IReadOnlyList<LlmHistoryEntry>)Get(Chat(),"Entries")).Reverse().Take(7).Select(row=>row.Text));
            int Follow() => (int)Call(host,"EffectiveFollow",profile);
            LlmToolResult toolReply=null;
            void Put(Player player,int x)
            {
                player.Bottom=new Vector2(x*16+8,55*16);
                player.velocity=Vector2.Zero;
                if (ReferenceEquals(player,body)) Call(Get(actor,"Behavior"),"PositionChanged",body);
            }
            void Pump(Func<bool> done) => NativeQuickItemChecks.Until(()=>{ Call(host,"PumpChat"); return done(); });
            void Send(string text,string tool,Dictionary<string,object> arguments,Action afterSend = null)
            {
                int rounds=0;
                bool advertised=false;
                toolReply=null;
                fixture.Remote.Reply=request=>
                {
                    if (++rounds!=1)
                    {
                        toolReply=request.Messages.LastOrDefault(message=>message.Kind==LlmMessageKind.ToolResults)?.Results.SingleOrDefault();
                        return Task.FromResult(new LlmTurn("请看实际操作回执。",Array.Empty<LlmToolCall>(),"{}"));
                    }
                    advertised=request.Tools.Any(definition=>definition.Name==tool);
                    return Task.FromResult(new LlmTurn("",new[]{new LlmToolCall("command-1",tool,"{}",arguments)},"{}"));
                };
                object[] args={profile.OwnerId,profile.Id,text,null};
                Require((bool)Call(host,"SendChat",args),"normal chat accepts command: "+args[3]);
                afterSend?.Invoke();
                Pump(()=>!(bool)Get(Chat(),"IsRunning"));
                Require(advertised,"normal chat advertised requested registered tool "+tool+": "+Receipts());
            }
            IReadOnlyDictionary<string,object> Node(string action,params (string Key,object Value)[] fields) =>
                new Dictionary<string,object>{["operation"]=action,["arguments"]=fields.ToDictionary(field=>field.Key,field=>field.Value)};
            void Plan(string text,Action afterSend,params IReadOnlyDictionary<string,object>[] nodes) => Send(text,"submit_task",
                new Dictionary<string,object>{["title"]=text,["steps"]=Array.AsReadOnly<object>(nodes)},afterSend);
            CompanionTaskSnapshot Finish(int frames)
            {
                for(int i=0;i<frames && View().IsActive;i++) tick();
                return View();
            }
            string Snapshot() => View().State+" · "+View().Detail+" · "+String.Join(" | ",View().Attempts.Select(attempt=>attempt.Detail));
            void Preference(bool follow,bool attack)
            {
                var setting=Profile().Behavior;
                setting.Follow=follow;
                setting.AttackEnabled=attack;
                Require((bool)Call(host,"SetBehavior",profile.Id,setting),"set original native preference for this fixture");
            }
            NPC Npc(int slot,int kind,int x)
            {
                var target=new NPC();
                target.SetDefaults(kind);
                target.whoAmI=slot;
                target.Bottom=new Vector2(x*16+8,55*16);
                target.life=1;
                target.active=true;
                Main.npc[slot]=target;
                return target;
            }
            try
            {
                Pump(()=>(bool)Get(Chat(),"CanSend"));
                Call(host,"RestartChatContext",profile.OwnerId,profile.Id);
                Main.tile=(Tile[,])previousTiles.Clone();
                for(int x=46;x<=114;x++) for(int y=45;y<=59;y++)
                {
                    Main.tile[x,y]=new Tile();
                    if (y>=55) NativeToolsChecks.Tile(x,y,TileID.Stone);
                }
                Main.npc=Enumerable.Range(0,previousNpcs.Length).Select(_=>new NPC()).ToArray();
                Preference(true,false);
                Put(body,58); Put(human,68);
                Plan("到这里。",()=>Put(human,93),Node("move_to",("destination","here")));
                Require(View()?.State=="accepted" && Follow()==0 && Profile().Behavior.Follow,"accepted travel immediately masks saved Follow on: "+Receipts());
                Require(Finish(300).State=="succeeded" && Follow()==1 && Math.Abs(body.Bottom.X-(68*16+8))<24,
                    "native travel arrived at the original location before saved Follow on resumes: "+Snapshot()+" · "+body.Bottom);

                Preference(false,false);
                Put(body,58); Put(human,68);
                Plan("到这里。",()=>Put(human,93),Node("move_to",("destination","here")));
                Require(Finish(300).State=="succeeded" && Follow()==0 && !Profile().Behavior.Follow,"saved Follow off is restored: "+Snapshot());
                var held=body.Bottom;
                for(int i=0;i<25;i++) tick();
                Require(Vector2.DistanceSquared(held,body.Bottom)<24*24,"restored standby stays near actual task finish");

                Put(body,58); Put(human,68);
                Plan("到这里，完成后回来跟我。",()=>Put(human,78),Node("move_to",("destination","here")),
                    Node("move_to",("destination","owner")),Node("follow_owner"));
                Require(Finish(460).State=="succeeded" && Math.Abs(body.Bottom.X-human.Bottom.X)<32 && Follow()==1 &&
                    !Profile().Behavior.Follow,"explicit return actually reaches the moved owner and live follow does not rewrite saved off: "+Snapshot());
                Put(body,58); Put(human,83);
                Plan("到这里。",null,Node("move_to",("destination","here")));
                Require((bool)Call(host,"SetManualFollow",profile.OwnerId,profile.Id,false) && View().State=="cancelled" && Follow()==0,
                    "manual follow owns the setting again even if old stored off is the same value");
                Console.WriteLine("PASS original movement reaches send-time point, restores both saved Follow values, returns to moved owner and yields to manual control.");

                Put(body,58); Put(human,58);
                NativeToolsChecks.Tile(61,54,TileID.Copper);
                body.inventory[36]=new Item(ItemID.CobaltDrill);
                var identity=new CompanionTaskIdentity(profile.OwnerId,profile.Id,(long)Get(host,"session"),(long)Get(actor,"Generation"),Profile().DataEpoch);
                var observed=(CompanionTaskOutcome)Call(host,"TryGetTaskObservation",identity,"observe_tiles",
                    new Dictionary<string,object>{["radius"]=4,["tile_type"]=(int)TileID.Copper},Call(host,"CaptureTaskSendAnchor",human));
                Require(observed.Value!=null,"actual query witnessed the single copper tile for the drill");
                Plan("用钴钻头挖掉这块铜矿。",null,Node("world_use",("action","mine"),("target",observed.Value),("item_type",(int)ItemID.CobaltDrill)));
                Require(Finish(350).State=="succeeded" && !Main.tile[61,54].active(),
                    "original channel tool breaks the intended observed tile: "+Snapshot()+" · "+body.Bottom);
                Console.WriteLine("PASS original Cobalt Drill channels through normal task and actually breaks the witnessed tile.");

                Preference(false,true);
                Call(host,"RestartChatContext",profile.OwnerId,profile.Id);
                var other=((IReadOnlyList<CompanionProfile>)Get(archive,"Profiles")).First(candidate=>candidate.Id!=profile.Id);
                Send("以后关掉你的自动攻击", "set_companion_setting",new Dictionary<string,object>{["setting"]="自动攻击",["value"]=0});
                NativeQuickItemChecks.Until(()=>{ tick(); return (bool)Call(archive,"IsSaved",profile.Id) && Receipts().Contains("设置已写入队友档案"); });
                NativeQuickItemChecks.Until(()=>{ tick(); return (bool)Call(archive,"IsSaved",other.Id); });
                var manager=Get(archive,"manager");
                var fileArchive=Get(manager,"archive");
                string archiveRoot=(string)Get(fileArchive,"root");
                // The probe has an isolated user folder per run. Keep the
                // secondary snapshot path short for the native .NET Framework.
                string readback=Path.Combine(Terraria.Program.SavePath,"rb");
                string copyRoot=Path.Combine(readback,"JueMingRData","companions");
                string ownerPath=Path.Combine("characters",profile.OwnerId.ToString("N"));
                Directory.CreateDirectory(Path.Combine(copyRoot,"owners"));
                Directory.CreateDirectory(Path.Combine(copyRoot,ownerPath));
                foreach(string source in Directory.GetFiles(Path.Combine(archiveRoot,"owners"),"*.bin"))
                    File.Copy(source,Path.Combine(copyRoot,"owners",Path.GetFileName(source)));
                foreach(Guid id in new[]{profile.Id,other.Id})
                {
                    string relative=Path.Combine(ownerPath,id.ToString("N")+".bin");
                    File.Copy(Path.Combine(archiveRoot,relative),Path.Combine(copyRoot,relative));
                }
                using (var reader=new FileCompanionArchive(readback))
                {
                    var reload=reader.LoadOwner((string)Get(manager,"ownerKey"));
                    Require(!reload.Profiles.Single(value=>value.Id==profile.Id).Behavior.AttackEnabled && !Profile().Behavior.AttackEnabled &&
                        reload.Profiles.Single(value=>value.Id==other.Id).Behavior.AttackEnabled==other.Behavior.AttackEnabled &&
                        !reload.Profiles.Single(value=>value.Id==profile.Id).Behavior.Follow,
                        "fresh real archive preserves auto attack off and the other actor and unrelated Follow settings");
                }
                Console.WriteLine("PASS raw persistent automatic-attack directive updates the real saved actor only, read by a fresh archive.");

                for(int i=0;i<50;i++) body.inventory[i]=new Item();
                // The CPU base fixture has no NPC death presentation state.
                // Native kill, drops, damage, and Bestiary registration still run.
                for(int i=0;i<Main.gore.Length;i++) Main.gore[i]=new Gore();
                Main.BestiaryTracker=new BestiaryUnlocksTracker();
                Main.BestiaryDB=new BestiaryDatabase();
                var populate=new BestiaryDatabaseNPCsPopulator();
                populate.Populate(Main.BestiaryDB);
                Main.ItemDropsDB=new ItemDropDatabase();
                Main.ItemDropsDB.Populate();
                Main.ItemDropSolver=new ItemDropResolver(Main.ItemDropsDB);
                Main.BestiaryDB.Merge(Main.ItemDropsDB);
                populate.AddDropOverrides(Main.BestiaryDB);
                body.inventory[0]=new Item(ItemID.Flymeal);
                body.inventory[49]=new Item(4767); // Vanilla Guide to Critter Companionship is a carried item.
                Put(body,58); Put(human,58);
                tick();
                Require(body.dontHurtCritters,"actual accessory grants native critter protection");
                var enemy=Npc(0,NPCID.BlueSlime,59);
                var friendly=Npc(1,NPCID.Guide,60);
                var protectedCritter=Npc(2,NPCID.Bunny,62);
                var outside=Npc(3,NPCID.BlueSlime,110);
                AppDomain.CurrentDomain.FirstChanceException+=traceCombat;
                Call(host,"RestartChatContext",profile.OwnerId,profile.Id);
                var previousTask=View().TaskId;
                Plan("那你把我附近半径50格的怪杀了，包括友好生物",null,Node("clear_area",("radius",51),("include_friendly",true)));
                Require(View().TaskId==previousTask && toolReply?.IsError==true && toolReply.Output.Contains("此次请求未提交"),
                    "model cannot expand the raw 50-tile authorization");
                Plan("那你把我附近半径50格的怪杀了，包括友好生物",()=>Put(human,109),Node("clear_area",("radius",50),("include_friendly",true)));
                Require(View()?.State=="accepted","exact screenshot command accepts one area task: "+Receipts());
                var cleared=Finish(1050);
                Require(cleared.State=="succeeded" && cleared.Attempts.Last().Completed==2 && enemy.life<=0 && friendly.life<=0 &&
                    protectedCritter.life==1 && outside.life==1 && cleared.Attempts.Last().Detail.Contains("原版保护或无合法武器 1"),
                    "actual bounded circle kills old-anchor enemy and native Flymeal-eligible friendly only: "+Snapshot()+" · "+body.Bottom+
                    " life="+enemy.life+","+friendly.life+","+protectedCritter.life+","+outside.life+
                    " · body fault="+GetOptional(actor,"TerminalFailure")+" · native fault="+nativeCombatFault);
                for(int i=0;i<90 && (body.itemAnimation>0 || !body.releaseUseItem);i++) tick();
                Require(body.itemAnimation==0 && body.releaseUseItem && GetOptional(actor,"TaskCombat")==null,
                    "accepted vanilla swing has settled without a follow-up attack");
                var late=Npc(4,NPCID.BlueSlime,60);
                for(int i=0;i<35;i++) tick();
                Require(late.life==1,"finished clear does not continue hunting later arrivals");
                Plan("守住我周围 50 格",null,Node("guard",("destination","owner"),("leash_tiles",50)));
                var guardResult=Finish(120);
                var guard=GetOptional(actor,"TaskCombat");
                Require(guardResult.State=="succeeded" && guard!=null && (bool)Get(guard,"GuardOwner") && (float)Get(guard,"Radius")==800f,
                    "guard installs a distinct continuing owner-centered activity with the requested range: "+Snapshot());
                Send("停止任务。","stop_task",new Dictionary<string,object>());
                Require(View().State=="cancelled" && GetOptional(actor,"TaskCombat")==null,"explicit stop retires continuing guard");
                Console.WriteLine("PASS real radius-50 screenshot command, native hostile and friendly defeats, protected and outside spared, later arrival and guard stop.");

                for(int i=0;i<5;i++) Main.npc[i]=new NPC();
                body.inventory[0]=new Item(ItemID.RottenEgg){stack=6};
                Put(body,58); Put(human,58);
                var eggTarget=Npc(0,NPCID.Merchant,61);
                var eggProtected=Npc(1,NPCID.Bunny,63);
                Plan("那你把我附近半径50格的怪杀了，包括友好生物",null,Node("clear_area",("radius",50),("include_friendly",true)));
                Require(Finish(680).State=="succeeded" && eggTarget.life<=0 && eggProtected.life==1 && body.inventory[0].stack<6,
                    "native Rotten Egg projectile actually damages allowed friendly only and consumes a real item: "+Snapshot());
                Console.WriteLine("PASS actual Rotten Egg projectile and resource debit, original protected critter remains unharmed.");
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException-=traceCombat;
                Call(host,"StopTask",profile.OwnerId,profile.Id,null);
                Call(host,"SetBehavior",profile.Id,preferences);
                fixture.Restore();
                Main.npc=previousNpcs;
                Array.Copy(previousGore,Main.gore,previousGore.Length);
                Main.BestiaryTracker=previousBestiary;
                Main.BestiaryDB=previousBestiaryDatabase;
                Main.ItemDropsDB=previousDropDatabase;
                Main.ItemDropSolver=previousDropSolver;
                Main.tile=previousTiles;
                Array.Copy(previousBag,body.inventory,previousBag.Length);
                human.Bottom=previousHuman;
                body.Bottom=previousBody;
                body.velocity=Vector2.Zero;
                Call(Get(actor,"Behavior"),"PositionChanged",body);
            }
        }
    }
}
