using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Testing;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace NativeWorldTextProbe
{
    // Original BordersMovement is an isolated scalar oracle. The continuous
    // cases use ordinary Player.Update, including collision and its tail;
    // production never receives or mutates these native fixture objects.
    internal static class NativeCombatPlayerBorderChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
        private static object Get(object o,string n){var t=o.GetType();var f=t.GetField(n,Flags);return f!=null?f.GetValue(o):t.GetProperty(n,Flags).GetValue(o);}
        private static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        internal static void Run(object context)
        {
            var outlets=new Harmony("JueMingR.Tests.PlayerBordersAchievement");
            try
            {
                // This entry previously needed only scalar collision data.
                // Its real Player.Update stream needs the same native host
                // initialization as the ordinary item-use component checks.
                NativeCombatAimChecks.Initialize();
                // This CPU fixture has no achievements host. Isolate only its
                // presentation/profile sink, preserving native movement/death.
                foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining","NotifyProgressionEvent"})
                    outlets.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
                Boundaries(context);Continuous(context);
            }
            finally{outlets.UnpatchAll(outlets.Id);}
        }
        internal static void Boundaries(object context)
        {
            var source=Get(Get(context,"CombatObservation"),"Prediction");var read=source.GetType().GetMethod("ReadPlayer",Flags);
            bool savedLimits=DebugOptions.noLimits,savedRemix=Main.remixWorld,savedSky=Main.skyblockWorld;float savedLeft=Main.leftWorld,savedTop=Main.topWorld;int cases=0;
            try
            {
                foreach(int variant in new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15})
                {
                    NativeCombatLiveContextChecks.FlightWorld();Main.netMode=0;Main.leftWorld=13.25f;Main.topWorld=17.5f;Main.remixWorld=variant>=5 && variant<=8;Main.skyblockWorld=variant>=10 && variant<=13;DebugOptions.noLimits=variant==14;
                    Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true};var p=Main.LocalPlayer;p.position=new Vector2(1200,1200);p.velocity=new Vector2(3.25f,-2.5f);p.creativeGodMode=variant==6 || variant==8 || variant==11 || variant==13;p.forcedGravity=variant==9?1:0;p.dead=variant==7 || variant==12;p.gravDir=variant==4?-1:1;
                    if(variant==0 || variant==14)p.position.X=Main.leftWorld+639.75f;
                    if(variant==1)p.position.X=Main.rightWorld-640-p.width+.25f;
                    if(variant==2 || variant==4)p.position.Y=Main.topWorld+639.75f;
                    if(variant==3)p.position.Y=Main.bottomWorld-640-p.height+.25f;
                    if(variant>=5 && variant<=9)p.position.Y=Main.topWorld+(variant==8?319:variant==9?639:500);
                    if(variant>=10 && variant<=13)p.position.Y=Main.bottomWorld-639;
                    if(variant==15){Main.rightWorld=Main.leftWorld+1000;p.position.X=0;}
                    var predicted=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});PredictionStop stop;bool continued=PlayerMotionContinuation.ClampBorders(ref predicted,out stop);
                    bool death=(Main.remixWorld || p.forcedGravity>0) && p.position.Y<Main.topWorld+640-p.height && !p.dead && !p.creativeGodMode || Main.skyblockWorld && p.position.Y>Main.bottomWorld-640 && !p.dead && !p.creativeGodMode;
                    // Native KillMe owns effects and health. This continuation
                    // stops before that known event instead of simulating it.
                    if(death)
                    {
                        Require(!continued && stop==PredictionStop.Despawn,"Known native border death ends player premise variant="+variant);
                        // A remote isolated fixture player avoids local profile
                        // notification writes while original KillMe still owns
                        // this receiving player's death and native effects.
                        int slot=p.whoAmI;p.whoAmI=slot==0?1:0;
                        try{p.BordersMovement();Require(p.dead,"Original border reaches the predicted death variant="+variant);}
                        finally{p.whoAmI=slot;}
                        cases++;continue;
                    }
                    p.BordersMovement();Require(continued && stop==PredictionStop.None && predicted.X==p.position.X && predicted.Y==p.position.Y && predicted.Vx==p.velocity.X && predicted.Vy==p.velocity.Y && predicted.GravityDirection==p.gravDir,"Original BordersMovement scalar variant="+variant+" predicted="+predicted.X+","+predicted.Y+" native="+p.position+" velocity="+predicted.Vx+","+predicted.Vy+"/"+p.velocity);
                    var changed=predicted;changed.BorderRight+=.25f;Require(!predicted.SameSample(changed),"World pixel edge participates in SameSample.");cases++;
                }
                Console.WriteLine("PASS original player border scalar / explicit death boundary cases="+cases);
            }
            finally{DebugOptions.noLimits=savedLimits;Main.remixWorld=savedRemix;Main.skyblockWorld=savedSky;Main.leftWorld=savedLeft;Main.topWorld=savedTop;}
        }
        internal static void Continuous(object context)
        {
            var source=Get(Get(context,"CombatObservation"),"Prediction");var read=source.GetType().GetMethod("ReadPlayer",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");bool savedLimits=DebugOptions.noLimits,savedRemix=Main.remixWorld,savedSky=Main.skyblockWorld;float savedLeft=Main.leftWorld,savedTop=Main.topWorld;int total=0;
            var randoms=typeof(Main).GetField("_rngs",Flags);var savedRandoms=randoms.GetValue(null);
            try
            {
                // Main.Initialize creates the keyed streams on its simulation
                // thread. This isolated CPU entry has no Main game loop.
                randoms.SetValue(null,new System.Collections.Generic.Dictionary<string,Terraria.Utilities.UnifiedRandom>());
                foreach(bool right in new[]{false,true})
                {
                    // Native NPC.ClearAll resets melee/tag data for all 255
                    // player slots. The small quick-item host creates slot0
                    // only; initialize its unused native receivers as Main
                    // normally does, without intercepting ClearAll/Update.
                    for(int i=0;i<255;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
                    NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();DebugOptions.noLimits=false;Main.remixWorld=Main.skyblockWorld=false;Main.leftWorld=Main.topWorld=0;Main.netMode=0;Main.windSpeedCurrent=Main.windSpeedTarget=0;
                    Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;p.position=new Vector2(right?Main.rightWorld-640-p.width-24:Main.leftWorld+664,2400-p.height);p.velocity.X=right?3:-3;p.controlRight=right;p.controlLeft=!right;p.immune=true;p.immuneTime=100000;p.creativeGodMode=true;p.fallStart=p.fallStart2=(int)p.position.Y/16;
                    using(Main.SwapRandom("UpdatePlayers"))p.Update(Main.myPlayer);
                    var predicted=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});var e=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface};bool reached=false;int tail=0;
                    for(int frame=0;frame<90;frame++)
                    {
                        terrain.Reset();PredictionStop stop;Require(PlayerMotionContinuation.Advance(ref predicted,e,terrain,out stop),"Ordinary player border continuation remains available right="+right+" frame="+frame+" stop="+stop);
                        using(Main.SwapRandom("UpdatePlayers"))p.Update(Main.myPlayer);
                        double error=Vector2.Distance(p.position,new Vector2(predicted.X,predicted.Y));Require(error<=.002 && predicted.Vx==p.velocity.X && predicted.Vy==p.velocity.Y,"Original Player.Update crosses border right="+right+" frame="+frame+" error="+error+" predicted="+predicted.X+","+predicted.Y+" native="+p.position+" velocity="+predicted.Vx+","+predicted.Vy+"/"+p.velocity);
                        float edge=right?Main.rightWorld-640-p.width:Main.leftWorld+640;if(p.position.X==edge && p.velocity.X==0)reached=true;if(reached)tail++;total++;
                    }
                    Require(reached && tail>=30,"Original sustained movement reaches the actual border and matches its completed tail right="+right+" tail="+tail);
                }
                Console.WriteLine("PASS original player continuous left/right border actions="+total);
            }
            finally{randoms.SetValue(null,savedRandoms);DebugOptions.noLimits=savedLimits;Main.remixWorld=savedRemix;Main.skyblockWorld=savedSky;Main.leftWorld=savedLeft;Main.topWorld=savedTop;}
        }
    }
}
