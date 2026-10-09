using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeNpcLifetimeChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            // Reuse the supported original component prerequisites and only
            // suppress achievements/save/network outlets in this test process.
            var production=typeof(NativeCombatProductionPredictionChecks);
            production.GetMethod("Initialize",Flags).Invoke(null,null);
            var sink=new Harmony("JueMingR.Tests.NpcLifetimeOutlets");
            try
            {
                foreach(string name in new[]{"HandleSpecialEvent","HandleRunning"})sink.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(production.GetMethod("Skip",Flags)));
                foreach(var method in new[]{typeof(WorldGen).GetMethod("saveToonWhilePlaying",Flags),typeof(Player).GetMethod("SavePlayer",Flags),typeof(NetMessage).GetMethod("SendData",Flags)})sink.Patch(method,prefix:new HarmonyMethod(production.GetMethod("Refuse",Flags)));
                var host=Get(context,"CombatObservation");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));
                Main.netMode=0;Main.dedServ=true;
                Near(context,host,false);Near(context,host,true);Far(context,host);Slots();
            }
            finally {foreach(var method in sink.GetPatchedMethods().ToArray())sink.Unpatch(method,HarmonyPatchType.All,sink.Id);}
        }
        private static void World(int height,int floor)
        {
            Main.maxTilesX=120;Main.maxTilesY=height;Main.bottomWorld=height*16;Main.tileSolid[1]=true;
            Main.tile=new Tile[120,height];Main.Map=new Terraria.Map.WorldMap(120,height);
            for(int x=0;x<120;x++)for(int y=0;y<height;y++)Main.tile[x,y]=new Tile();
            for(int x=0;x<120;x++){Main.tile[x,floor].active(true);Main.tile[x,floor].type=1;}
            for(int i=0;i<Main.maxPlayers;i++){if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};Main.player[i].active=false;}
            foreach(var npc in Main.npc)npc.active=false;
        }
        private static NPC Dormant(Player player,int floor)
        {
            var n=Main.npc[2];n.SetDefaults(85);n.whoAmI=2;n.active=true;n.target=player.whoAmI;n.position=new Vector2(650,floor*16-n.height);n.velocity=n.oldVelocity=Vector2.Zero;
            return n;
        }
        private static void Stand(Player p,int floor)
        {
            p.active=true;p.dead=p.ghost=false;p.position=new Vector2(640,floor*16-p.height);p.velocity=Vector2.Zero;p.controlLeft=p.controlRight=p.controlJump=false;p.jump=0;p.gravDir=1;p.carpetFrame=-1;p.tankPet=-1;p.itemAnimation=p.itemTime=0;p.inventory[0].SetDefaults(1);p.fallStart=p.fallStart2=floor;
            for(int x=39;x<=42;x++){Main.tile[x,floor].active(true);Main.tile[x,floor].type=1;}
        }
        private static void Near(object context,object host,bool stationary)
        {
            World(200,85);var p=Main.LocalPlayer;Stand(p,43);var n=Dormant(p,85);p.Update(0);n.UpdateNPC(2);var origin=n.position;int initial=n.timeLeft;
            for(int i=0;i<initial-30;i++)
            {p.Update(0);n.UpdateNPC(2);Require(n.active && n.ai[0]==0 && n.position==origin && Math.Abs(p.position.Y-(688-p.height))<.01f,"N01 original supported player and dormant NPC history");}
            Require(n.timeLeft==30,"N01 natural timer after full original history");
            if(!stationary)for(int x=39;x<=42;x++)Main.tile[x,43].active(false);
            var playerPosition=p.position;var playerVelocity=p.velocity;var ai=(float[])n.ai.Clone();var localAi=(float[])n.localAI.Clone();var random=Main.rand;
            var seeds=(int[])random.GetType().GetField("SeedArray",Flags).GetValue(random);var seedCopy=(int[])seeds.Clone();var randomIndex=random.GetType().GetField("inext",Flags).GetValue(random);
            var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
            NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);
            Require(n.timeLeft==30 && n.position==origin && ai.SequenceEqual(n.ai) && localAi.SequenceEqual(n.localAI) && p.position==playerPosition && p.velocity==playerVelocity && ReferenceEquals(random,Main.rand) && seeds.SequenceEqual(seedCopy) && randomIndex.Equals(random.GetType().GetField("inext",Flags).GetValue(random)),"N01 Source leaves observed NPC/player/RNG values intact");
            Require(path!=null && (stationary?path.Count==30 && path.Stop==PredictionStop.Despawn:path.Count>40),"N01 actual default Source distinguishes natural expiry from falling near re-entry");
            for(int step=1;step<=40;step++)
            {
                p.Update(0);n.UpdateNPC(2);
                if(stationary && step==30){Require(!n.active,"N01 original natural expiry at30");break;}
                Require(n.active && n.ai[0]==0 && n.position==origin,"N01 original dormant receiver survives falling player");
                if(!stationary && step==27)Require(n.timeLeft==749,"N01 original re-enters near at27 before expiry");
                if(!stationary)Require(Math.Abs(path[step].Bounds.X-n.position.X)<.01f && Math.Abs(path[step].Bounds.Y-n.position.Y)<.01f,"N01 frozen path agrees with original receiver");
            }
            Console.WriteLine("PASS N01 default Source near="+(stationary?"natural-expiry":"falling-reentry")+" nativeHistory="+(initial-29)+" count="+path.Count+" stop="+path.Stop);
        }
        private static void Far(object context,object host)
        {
            World(400,65);var p=Main.LocalPlayer;Stand(p,202);var n=Dormant(p,65);
            p.Update(0);n.UpdateNPC(2);var origin=n.position;Require(n.active && n.ai[0]==0,"N01 genuine dormant far start");
            for(int x=39;x<=42;x++)Main.tile[x,202].active(false);
            Main.screenWidth=3840;Main.screenHeight=2160;Main.screenPosition=p.Center-new Vector2(1920,1080);
            NativeCombatObservationChecks.Fresh(context,host);var source=Get(host,"Prediction");var path=((NpcPredictionCache)Get(source,"Cache")).Read(0);
            Require((bool)Get(Get(host,"Selection"),"HasTarget") && path!=null && path.Stop==PredictionStop.Despawn && path.Count>30 && path.Count<=120,"N01 default selected far exit is a real lifecycle stop");
            int retired=0;
            for(int step=1;step<=120;step++){p.Update(0);n.UpdateNPC(2);if(!n.active){retired=step;break;}Require(n.position==origin && n.ai[0]==0,"N01 far exit keeps dormant geometry until retirement");}
            Require(retired==path.Count,"N01 far exit forecast stops at exact original action");
            Console.WriteLine("PASS N01 default Source far-exit nativeAction="+retired+" count="+path.Count);
        }
        private static bool Active(ref NpcMotionState n,PredictionEnvironment env)
        {object[] args={n,env,PredictionStop.None};bool result=(bool)typeof(NpcMotion).GetMethod("CheckActive",Flags).Invoke(null,args);n=(NpcMotionState)args[0];return result;}
        private static void Slots()
        {
            var outside=new MotionRect(9000,9000,20,42);var inside=new MotionRect(650,1000,20,42);
            var env=new PredictionEnvironment{PlayerIndex=42,PlayerX=inside.CenterX,PlayerY=inside.CenterY,PlayerWidth=20,PlayerHeight=42,PlayerTimelineActive=true,Players=new PredictionPlayers(new[]{outside,outside},2,new[]{7,42})};
            var initial=new NpcMotionState{X=650,Y=1000,Width=24,Height=24,Active=true,TimeLeft=1};var n=initial;
            Require(Active(ref n,env) && n.TimeLeft==749,"N01 actual slot42 replaces compact index1");
            n=initial;env.PlayerTimelineActive=false;Require(!Active(ref n,env),"N01 fixed observation does not invent a future player");
            n=initial;env.PlayerTimelineActive=true;env.Players=new PredictionPlayers(new[]{outside,outside},2);Require(!Active(ref n,env),"N01 absent slots cannot alias numbered timeline");
            n=initial;env.PlayerIndex=-1;Require(!Active(ref n,env),"N01 absent timeline identity cannot alias unknown slots");env.PlayerIndex=42;
            n=initial;env.PlayerX=9010;env.PlayerY=9021;env.Players=new PredictionPlayers(new[]{inside,outside},2,new[]{7,42});Require(Active(ref n,env) && n.TimeLeft==749,"N01 other active player retains frozen qualification");
            n=initial;env.Players=new PredictionPlayers(new[]{outside,outside},2,new[]{7,42});Require(!Active(ref n,env),"N01 genuine far exit still retires");
            n=initial;env.Multiplayer=true;Require(Active(ref n,env) && n.TimeLeft==0 && n.Active,"N01 client still waits for authority on expiry/far exit");
            Require(!env.Players.Equals(new PredictionPlayers(new[]{outside,outside},2,new[]{42,7})),"N01 cached geometry includes actual player identity");
            Console.WriteLine("PASS N01 compact slot/fixed/missing/other-player/far/client controls");
        }
    }
}
