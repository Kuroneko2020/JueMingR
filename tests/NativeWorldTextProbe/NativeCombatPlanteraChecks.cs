using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPlanteraChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> beforeAction,afterAction;
        private static void Before(NPC __instance){beforeAction?.Invoke(__instance);}
        private static void After(NPC __instance){afterAction?.Invoke(__instance);}
        internal static void Boundaries()
        {
            var action=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcPlanteraMotion",true).GetMethod("Step",Flags);
            object token=new object();var boss=new NpcMotionState{Identity=new NpcIdentity(1,token,20,1,262,262),Style=51,Active=true,Life=1000,LifeMax=1001,TargetCaptured=true,HasPlayer=true,PlayerVelocityCaptured=true,PlayerCenterX=500,PlayerCenterY=400,PlayerArea=new MotionRect(490,379,20,42),PlayerJungle=true};
            var hook=new NpcMotionState{Identity=new NpcIdentity(1,new object(),10,1,263,263),Style=52,Active=true,Width=40,Height=40,X=100,Y=100,A0=8,A1=9,L0=2,PlantRelationsCaptured=true,PlantRelationsComplete=true,PlantBossIdentity=boss.Identity,PlantBossValid=true};
            var e=new PredictionEnvironment{WorldHeight=700,GravityWorldSurface=10};object[] args={hook,e,null,1,PredictionStop.None,new[]{hook,boss},2,false};
            Require((bool)action.Invoke(null,args) && ((NpcMotionState)args[0]).L0==1,"Chosen hook clock continues before exact next RNG action.");
            args[0]=(NpcMotionState)args[0];Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.RandomDestination,"Hook stops before native random destination, including moving-peer random delay.");
            hook.L0=20;hook.A0=0;hook.A1=0;e.Multiplayer=true;args[0]=hook;args[1]=e;
            Require((bool)action.Invoke(null,args) && ((NpcMotionState)args[0]).A0==7 && ((NpcMotionState)args[0]).A1==7,"Client hook preserves original X-for-Y zero-anchor rule.");
            e.Multiplayer=false;e.GoodWorld=true;hook.A0=8;hook.A1=8;hook.X=100;hook.Y=100;hook.L3=1;args[0]=hook;args[1]=e;
            Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.PhaseBoundary,"GoodWorld chosen hook stops precisely before real arrival thorns mutate terrain.");
            var second=boss;second.Identity=new NpcIdentity(1,new object(),30,1,262,262);second.Life=100;boss.Life=1000;
            hook.L3=0;hook.L0=100;e.GoodWorld=false;args[0]=hook;args[1]=e;args[5]=new[]{hook,boss,second};args[6]=3;
            Require((bool)action.Invoke(null,args) && ((NpcMotionState)args[0]).L0==99,"Before any body, first future retains observed global even when a higher body exists.");
            args[0]=hook;args[3]=2;Require((bool)action.Invoke(null,args) && ((NpcMotionState)args[0]).L0==95,"Subsequent early hook sees the previous action's last body and strict integer quarter clock.");
            var tentacle=new NpcMotionState{Identity=new NpcIdentity(1,new object(),40,1,264,264),Style=53,Active=true,L0=120,A0=0,A1=0,Life=1000,LifeMax=1000,PlantRelationsCaptured=true,PlantRelationsComplete=true,PlantBossValid=true,PlantBossIdentity=boss.Identity};
            args[0]=tentacle;args[3]=1;args[5]=new[]{boss,second,tentacle};
            Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.InvalidState,"Zero native tentacle offset cannot become an invented finite direction.");
            tentacle.A0=10;tentacle.A1=20;e.GoodWorld=true;args[0]=tentacle;args[1]=e;
            Require((bool)action.Invoke(null,args) && ((NpcMotionState)args[0]).ProjectileReflectionUnknown,"GoodWorld tentacle retains its deterministic motion while explicitly carrying the future random reflection receiving choice.");
            e.GoodWorld=false;args[0]=tentacle;args[1]=e;
            Require((bool)action.Invoke(null,args) && !((NpcMotionState)args[0]).ProjectileReflectionUnknown,"The ordinary tentacle has no invented reflection uncertainty.");
            tentacle.A0=10;tentacle.A1=20;tentacle.A3=11;tentacle.PositionParameter=-1;args[0]=tentacle;
            Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.Despawn,"Captured inactive encoded owner retires without demanding an unrelated owner AI.");
            tentacle.PositionParameter=0;tentacle.PositionOwner=new NpcIdentity(1,new object(),10,2,263,263);args[0]=tentacle;
            Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"Encoded position owner requires full identity, independently of global life owner.");
            Console.WriteLine("PASS Plantera chosen anchor/client/thorns/global slot-order/encoded-owner boundaries");
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            Require(GetOptional(source,"Native")==null,"Plantera uses the default scalar rolling producer.");
            var read=source.GetType().GetMethod("Read",Flags);
            var capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);
            var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var rows=new List<string>{"scene,frame,type,selected,published,count,stop,matched,longMatched,maxError,tangentError"};
            var failures=new List<string>();bool oldGood=Main.getGoodWorld;var oldPlayers=Main.player;
            Main.player=new Player[oldPlayers.Length];for(int i=0;i<Main.player.Length;i++)Main.player[i]=new Player{whoAmI=i};
            var hookOwner=new Harmony("JueMingR.Probe.PlanteraNaturalAction");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);
            hookOwner.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatPlanteraChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatPlanteraChecks).GetMethod(nameof(After),Flags)));
            int scenes=0,allMatches=0,bodyActions=0,hookActions=0,tentacleActions=0,randomBoundaries=0;
            try
            {
                // Genuine hits choose health phases; expert native births choose
                // both body-owned and hook-owned tentacle identities. Observer
                // hooks compare the normal schedule, never add a native action.
                for(int variant=0;variant<14;variant++)
                {
                    string scene="plant-"+variant;scenes++;
                    try
                    {
                        int ground=variant==10?80:variant==11?560:300;
                        Setup(context,variant==3 || variant==7,ground);
                        Main.GameMode=variant==1 || variant==2 || variant==5 || variant==8?1:0;Main.getGoodWorld=variant==9;
                        var body=Birth(context,step,1700,16,ground*16-300);
                        if(variant==2 || variant==4 || variant==5 || variant==8)
                        {body.StrikeNPCNoInteraction(body.lifeMax*(variant==4?9:6)/10+body.defense+100,0,0);Require(body.active && body.life<=body.lifeMax/2,"Original hit enters receiving Plantera health phase.");for(int i=0;i<4;i++){NativeCombatModeledImpactChecks.SampleMouse(context,body.Center);step();}}
                        if(variant==6 || variant==7 || variant==8)
                        {Birth(context,step,variant==7?4400:1900,body.whoAmI+8);Require(Count(51)>=2 && Count(52)>=6,"Two original bodies create more than three worldwide hooks.");}
                        if(variant==13)
                        {
                            var second=Birth(context,step,1900,24);body.StrikeNPCNoInteraction(1000000,0,0);
                            Require(!body.active && Count(52)>=6,"Original body death preserves the real low-slot hooks before their next action.");
                            int born=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,4500,NPCID.Plantera,Start:20,Target:Main.myPlayer);
                            Require(born==20 && NPC.plantBoss==20 && second.whoAmI==24,"Actual lower-slot replacement birth sets observed global differently from the highest body.");body=second;
                        }
                        if(variant==7)
                        {var other=new Player{whoAmI=1,active=true,isControlledByFilm=true};other.position=new Vector2(4500,4800-other.height);other.statLife=other.statLifeMax=other.statLifeMax2=5000;other.creativeGodMode=true;Main.player[1]=other;other.UpdateSceneMetrics();other.UpdateBiomes();}
                        NPC actor=body;
                        if(variant==4 || variant==5)
                        {actor=Tentacle(variant==5);Require(actor!=null && actor.active && !actor.dontTakeDamage,"Real receiving tentacle with the requested native attachment exists.");if(variant==5){actor.StrikeNPCNoInteraction(actor.lifeMax/3+actor.defense+20,0,0);Require(actor.active && actor.life<actor.lifeMax,"Real tentacle damage changes its expert own-life radius.");}}
                        if(variant==3)Jungle(false);
                        if(variant==10)Require(Main.LocalPlayer.position.Y<Main.worldSurface*16,"Surface scene uses the native player position, independently of Jungle.");
                        if(variant==11)Require(Main.LocalPlayer.position.Y>Main.UnderworldLayer*16,"Underworld scene uses the native height predicate.");
                        if(variant==12){Main.LocalPlayer.KillMe(Terraria.DataStructures.PlayerDeathReason.ByCustomReason("isolated Plantera dead target oracle"),10000,0);Require(Main.LocalPlayer.dead,"Original KillMe establishes the dead-player branch.");}
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();int selectedCount=0,published=0,matches=0,longMatches=0,bodyLocal=0,hookLocal=0,tentacleLocal=0,randomLocal=0,phaseBoundaries=0,earlyHookActions=0,maxWindow=0,distinctTargets=0,distinctRefusals=0;double maximum=0,tangent=0;
                        NPC pendingActor=null;NpcMotionState predicted=default(NpcMotionState);bool pendingValid=false;
                        beforeAction=n=>
                        {
                            pendingValid=false;pendingActor=n;
                            if(!n.active || n.aiStyle<51 || n.aiStyle>53)return;
                            if(n.aiStyle==52){bool earlier=false;for(int k=0;k<n.whoAmI;k++)if(Main.npc[k].active && Main.npc[k].aiStyle==51){earlier=true;break;}if(!earlier)earlyHookActions++;}
                            var states=Capture(read,capture,source,terrain,session);int index=-1;for(int k=0;k<states.Length;k++)if(ReferenceEquals(states[k].Identity.Token,n)){index=k;break;}if(index<0)return;
                            predicted=states[index];var player=Main.player[n.target>=0 && n.target<Main.player.Length?n.target:Main.myPlayer];
                            var e=new PredictionEnvironment{PlayerIndex=player.whoAmI,PlayerX=player.Center.X,PlayerY=player.Center.Y,PlayerWidth=player.width,PlayerHeight=player.height,PlayerVx=player.velocity.X,PlayerVy=player.velocity.Y,PlayerVelocityCaptured=true,PlayerDead=player.dead || !player.active,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Multiplayer=Main.netMode==1};
                            terrain.Reset();PredictionStop stop;pendingValid=NpcMotion.Step(ref predicted,states,states.Length,e,terrain,1,true,out stop);
                            if(!pendingValid){if(stop==PredictionStop.RandomDestination)randomLocal++;else if(stop==PredictionStop.PhaseBoundary)phaseBoundaries++;else Require(stop==PredictionStop.MissingDependency || stop==PredictionStop.InvalidState,"Explicit original-action dependency boundary: "+scene+" type="+n.type+" stop="+stop);}
                        };
                        afterAction=n=>
                        {
                            if(!pendingValid || !ReferenceEquals(pendingActor,n))return;
                            double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                            Require(error<=.08 && predicted.Vx==n.velocity.X && predicted.Vy==n.velocity.Y && predicted.ProjectileReflectionUnknown==(n.aiStyle==53 && Main.getGoodWorld) && (predicted.ProjectileReflectionUnknown || predicted.ReflectsProjectiles==n.reflectsProjectiles),"Normal native slot action Plantera "+scene+" type="+n.type+" slot="+n.whoAmI+" global="+NPC.plantBoss+" error="+error+" predictedV="+predicted.Vx+","+predicted.Vy+" nativeV="+n.velocity);
                            if(n.aiStyle==51)bodyLocal++;if(n.aiStyle==52)hookLocal++;if(n.aiStyle==53)tentacleLocal++;
                        };
                        for(int frame=0;frame<320 && actor.active;frame++)
                        {
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();
                            var selection=Get(host,"Selection");bool selected=(bool)Get(selection,"HasTarget") && ReferenceEquals(((NpcIdentity)Get(selection,"Target")).Token,actor);if(selected)selectedCount++;
                            var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor)){Require(selected && path.CaptureTick==Main.GameUpdateCount,"Actual default producer owns fresh Plantera publication.");published++;if(frame%5==0 && path.Count>1)freezes.Add(path);}
                            if(path!=null)maxWindow=Math.Max(maxWindow,path.Count);
                            if(variant==7)
                            {
                                // Global first-three hooks can bring both bodies
                                // back to the same player. Refusal is required
                                // only while their actual next queries differ.
                                var observed=Capture(read,capture,source,terrain,session);int first=-1;bool different=false;
                                for(int k=0;k<observed.Length;k++)if(observed[k].Style==51 && observed[k].HasClosestPlayer)
                                {int next=observed[k].ClosestPlayerIndex;if(first<0)first=next;else different|=first!=next;}
                                if(different){distinctTargets++;Require(path==null || path.Count<=1,"Distinct required next player timelines cannot publish a fabricated future.");distinctRefusals++;}
                            }
                            for(int k=freezes.Count-1;k>=0;k--)
                            {
                                var frozen=freezes[k];int offset=(int)((long)Main.GameUpdateCount-frozen.CaptureTick);if(offset>=frozen.Count){freezes.RemoveAt(k);continue;}if(offset<=0)continue;
                                var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maximum=Math.Max(maximum,error);matches++;
                                if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal) && point.NoTileCollide==actor.noTileCollide && point.ProjectileReflectionUnknown==(actor.aiStyle==53 && Main.getGoodWorld) && (point.ProjectileReflectionUnknown || point.ReflectsProjectiles==actor.reflectsProjectiles),"Frozen natural Plantera full producer "+scene+" type="+actor.type+" offset="+offset+" error="+error+" stop="+frozen.Stop);
                            }
                            rows.Add(string.Join(",",scene,frame,actor.type,selected,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,maximum.ToString("R",CultureInfo.InvariantCulture),tangent.ToString("R",CultureInfo.InvariantCulture)));
                        }
                        beforeAction=afterAction=null;
                        Require(bodyLocal>=100 && hookLocal>=100,"Natural original schedule verifies body and chosen-hook motors: "+scene);
                        if(variant==4 || variant==5)Require(tentacleLocal>=100,"Real naturally born tentacles exercise their owner/life motor.");
                        if(variant!=3 && variant!=7 && variant!=9 && variant!=10 && variant!=11 && variant!=12)Require(selectedCount>0 && published>0 && longMatches>=30 && tangent>8,"Natural Plantera full producer publishes and matches curved paths >=30 frames: "+scene+" published="+published+" long="+longMatches+" tangent="+tangent);
                        // Enraged hooks deduct eleven per action. Native moving
                        // peers choose delay 60..299, so a full closure often
                        // has fewer than thirty deterministic future actions.
                        // Keep the normal long-window tests above; here require
                        // genuine bounded publication and its original RNG edge.
                        if(variant==3 || variant==10 || variant==11)Require(selectedCount>0 && published>0 && matches>=100 && maxWindow>=10 && randomLocal>0,"Real enrage paths match their chosen windows and stop at original random hook decisions.");
                        if(variant==7)Require(selectedCount>0 && distinctTargets>=30 && distinctRefusals==distinctTargets,"Actual different body queries stop for the uncaptured second future timeline, including later shared-target recovery.");
                        if(variant==9)Require(phaseBoundaries>0,"Natural GoodWorld hook arrivals exercise their actual terrain-mutation boundary.");
                        if(variant==12)Require(published==0 && Main.LocalPlayer.dead,"Dead target actions match the original schedule, while the source does not fabricate an alive future timeline.");
                        if(variant==13)Require(earlyHookActions>=100,"Natural low-slot hooks actually read the observed or previous-action global before any body executes.");
                        allMatches+=matches;bodyActions+=bodyLocal;hookActions+=hookLocal;tentacleActions+=tentacleLocal;randomBoundaries+=randomLocal;
                        Console.WriteLine("PLANT scene="+scene+" type="+actor.type+" selected="+selectedCount+" published="+published+" matched="+matches+" long="+longMatches+" maxError="+maximum+" bodyActions="+bodyLocal+" hookActions="+hookLocal+" tentacleActions="+tentacleLocal+" random="+randomLocal+" terrainBoundary="+phaseBoundaries+" earlyHookActions="+earlyHookActions+" maxWindow="+maxWindow+" distinctTargets="+distinctTargets+" distinctRefusals="+distinctRefusals);
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){beforeAction=afterAction=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("PLANT FAIL "+scene+": "+failure);}
                }
                Require(failures.Count==0,"Natural Plantera scene failures="+failures.Count+": "+string.Join(" | ",failures));
                Require(randomBoundaries>0,"Chosen natural hook/tentacle windows reach actual random-choice boundaries.");
                Console.WriteLine("PASS Plantera natural scenes="+scenes+" matched="+allMatches+" bodyActions="+bodyActions+" hookActions="+hookActions+" tentacleActions="+tentacleActions+" randomBoundaries="+randomBoundaries);
            }
            finally{beforeAction=afterAction=null;hookOwner.Unpatch(update,HarmonyPatchType.All,hookOwner.Id);Main.getGoodWorld=oldGood;Main.player=oldPlayers;Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"plantera-motion-continuous.csv"),rows);}
        }
        private static NpcMotionState[] Capture(MethodInfo read,MethodInfo tracking,object source,IPredictionTerrain terrain,long session)
        {
            var list=new List<NpcMotionState>();
            for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].aiStyle>=51 && Main.npc[i].aiStyle<=53)
            {var n=Main.npc[i];var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,terrain};tracking.Invoke(null,args);list.Add((NpcMotionState)args[1]);}
            var states=list.ToArray();NpcIdentity global=default(NpcIdentity);bool valid=false;
            for(int i=0;i<states.Length;i++)if(states[i].Identity.Slot==NPC.plantBoss && states[i].EffectiveType==262){global=states[i].Identity;valid=true;break;}
            for(int i=0;i<states.Length;i++)
            {
                states[i].PlantRelationsCaptured=states[i].PlantRelationsComplete=true;states[i].PlantBossValid=valid;states[i].PlantBossIdentity=global;
                if(states[i].Style==53 && states[i].A3>0)
                {states[i].PositionRelation=10;states[i].PositionParameter=-1;int slot=(int)states[i].A3-1;for(int j=0;j<states.Length;j++)if(states[j].Identity.Slot==slot){states[i].PositionOwner=states[j].Identity;states[i].PositionParameter=0;break;}}
            }
            return states;
        }
        private static void Setup(object context,bool nonJungle,int ground)
        {
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.getGoodWorld=false;Main.dayTime=false;
            // Locked ClampedInWorld also clamps Y by maxTilesX. A tall,
            // narrower fixture would silently turn the Underworld scan into
            // Jungle. The actual terrain and native map share these dimensions.
            Main.maxTilesX=800;Main.maxTilesY=700;Main.rightWorld=12800;Main.bottomWorld=11200;Main.worldSurface=140;Main.rockLayer=200;Main.tile=new Tile[800,700];Main.Map=new Terraria.Map.WorldMap(800,700);
            for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<700;y++){var tile=new Tile();if(y>=ground){tile.active(true);tile.type=nonJungle?TileID.Stone:TileID.JungleGrass;}Main.tile[x,y]=tile;}
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}
            for(int i=0;i<Main.player.Length;i++)if(i!=Main.myPlayer)Main.player[i].active=false;
            var player=Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};player.position=new Vector2(1100,ground*16-player.height);player.statLife=player.statLifeMax=player.statLifeMax2=5000;player.creativeGodMode=true;player.immune=true;player.immuneTime=100000;player.fallStart=player.fallStart2=(int)(player.position.Y/16);
            for(int i=0;i<player.hurtCooldowns.Length;i++)player.hurtCooldowns[i]=100000;for(int i=0;i<3;i++)player.armor[i].SetDefaults(696+i);
            Main.PlayerSceneMetrics.Reset();player.UpdateSceneMetrics();player.UpdateBiomes();Require(player.ZoneJungle==(!nonJungle && ground<Main.UnderworldLayer),"Original tile/biome scan establishes true Jungle/Underworld presence.");
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
        private static void Jungle(bool enabled)
        {for(int x=0;x<Main.maxTilesX;x++)for(int y=300;y<340;y++)Main.tile[x,y].type=enabled?TileID.JungleGrass:TileID.Stone;Main.PlayerSceneMetrics.Reset();Main.LocalPlayer.UpdateSceneMetrics();Main.LocalPlayer.UpdateBiomes();Require(Main.LocalPlayer.ZoneJungle==enabled,"Real scene tiles establish requested Jungle predicate.");}
        private static NPC Birth(object context,Action step,int x,int start,int y=4500)
        {int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),x,y,NPCID.Plantera,Start:start,Target:Main.myPlayer);var actor=Main.npc[slot];actor.timeLeft=750;for(int i=0;i<3;i++){NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();}Require(actor.active && actor.localAI[0]!=0 && Count(52)>=3,"Native Plantera initialization creates its three chosen hooks.");return actor;}
        private static int Count(int style){int count=0;for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].aiStyle==style)count++;return count;}
        private static NPC Tentacle(bool hookOwner){for(int i=0;i<Main.maxNPCs;i++){var n=Main.npc[i];if(n.active && n.type==NPCID.PlanterasTentacle && (n.ai[3]>0)==hookOwner)return n;}return null;}
    }
}
