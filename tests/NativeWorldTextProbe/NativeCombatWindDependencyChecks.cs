using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Microsoft.Xna.Framework;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Dependency qualification uses the locked original wind predicate and
    // real UpdateWeather. It does not replace AI or suppress actual wind.
    internal static class NativeCombatWindDependencyChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");
            var type=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileEnvironment",true);
            var potential=type.GetMethod("PotentialWind",Flags);var stateType=type.GetNestedType("State",Flags);
            var make=stateType.GetConstructor(Flags,null,new[]{typeof(bool),typeof(bool)},null);
            bool savedGate=Main.windPhysics;float savedSpeed=Main.windSpeedCurrent,savedTarget=Main.windSpeedTarget,savedStrength=Main.windPhysicsStrength;int savedCounter=Main.windCounter;
            bool pushed=Main.LocalPlayer.windPushed;
            try
            {
                int cases=0,sensitive=0,immune=0;
                Main.windPhysics=true;
                foreach(var sample in ContentSamples.ProjectilesByType.Values)
                {
                    bool actual=sample.ShouldUseWindPhysics(),dependency=(bool)potential.Invoke(null,new object[]{sample});
                    Require(actual==dependency,"wind dependency matches the original nullable type override/style predicate type="+sample.type+" style="+sample.aiStyle);
                    if(actual)sensitive++;else immune++;cases++;
                }
                Require(sensitive>0 && immune>0,"wind qualification includes actual sensitive and immune projectiles");
                Main.windPhysics=false;
                foreach(var sample in ContentSamples.ProjectilesByType.Values)
                    Require(!sample.ShouldUseWindPhysics(),"original global wind gate disables actual wind type="+sample.type);
                // Gate OFF is not evidence that a potential consumer never
                // needs wind: turning it ON must retire its saved lease.
                var potentialLease=make.Invoke(new object[]{true,true});Main.windPhysics=true;
                Require(!(bool)Get(potentialLease,"Current"),"potential wind lease rejects an actual OFF to ON gate change");
                Main.windSpeedCurrent=-.8f;Main.windSpeedTarget=.8f;Main.windCounter=7200;
                var related=make.Invoke(new object[]{true,true});var unrelated=make.Invoke(new object[]{true,false});
                int changes=0;var elapsed=TimeSpan.FromSeconds(1.0/60);
                for(int i=0;i<60;i++){float old=Main.windSpeedCurrent;Main.instance.UpdateWeather(new GameTime(TimeSpan.FromSeconds((i+1)/60.0),elapsed),0);if(Main.windSpeedCurrent!=old)changes++;}
                Require(changes>0 && !(bool)Get(related,"Current") && (bool)Get(unrelated,"Current"),"actual UpdateWeather retires related wind lease and preserves unrelated hand facts");
                var player=Main.LocalPlayer;var read=source.GetType().GetMethod("ReadPlayer",Flags);
                player.windPushed=false;var calm=(PredictionPlayerMotion)read.Invoke(null,new object[]{player});
                Require(!calm.WindPushed && calm.WindSpeed==0,"ordinary player observation excludes unrelated wind values");
                player.windPushed=true;
                Require(player.CanBePushedByWind(),"isolated original player admits actual wind pushing");
                var windy=(PredictionPlayerMotion)read.Invoke(null,new object[]{player});
                Require(windy.WindPushed && windy.WindSpeed==Main.windSpeedCurrent,"wind-pushed player captures the actual changed weather value");
                var item=new Item();item.SetDefaults(24);Require(item.shoot<=0,"pure hand counterexample uses an original non-projectile sword");
                Require((bool)type.GetMethod("HandWindRelevant",Flags).Invoke(null,new object[]{player,item}),"actual wind-pushed player qualifies even an otherwise independent hand");
                player.windPushed=false;
                Require(!(bool)type.GetMethod("HandWindRelevant",Flags).Invoke(null,new object[]{player,item}),"pure hand without native wind pushing has no unrelated weather dependency");
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                NativeCombatLiveContextChecks.FlightWorld();
                foreach(int npcType in new[]{1,371,594})
                {
                    foreach(var n in Main.npc)n.active=false;
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),930,900,npcType,Start:2,Target:player.whoAmI);
                    Require(slot>=0 && slot<Main.maxNPCs,"original wind counterexample allocates a real NPC type="+npcType);
                    var npc=Main.npc[slot];var before=CaptureEnvironment(source,host,npc);
                    float old=Main.windSpeedCurrent;Main.instance.UpdateWeather(new GameTime(TimeSpan.FromSeconds(2),elapsed),0);
                    Require(Main.windSpeedCurrent!=old,"wind counterexample includes another actual weather change");
                    // WindTarget is a separate authoritative weather input.
                    // Its change must be captured even if current wind is still
                    // interpolating, while ordinary slime omits both values.
                    Main.windSpeedTarget=Main.windSpeedTarget==.8f?-.6f:.8f;
                    var after=CaptureEnvironment(source,host,npc);
                    if(npcType==1)Require(before.Wind==0 && after.Wind==0 && before.WindTarget==0 && after.WindTarget==0,"ordinary slime capture excludes unrelated changed weather");
                    if(npcType==371)Require(before.Wind==old && after.Wind==Main.windSpeedCurrent && before.Wind!=after.Wind,"real bubble capture preserves actual changing current wind");
                    if(npcType==594)Require(after.WindTarget==Main.windSpeedTarget && before.WindTarget!=after.WindTarget,"real balloon capture preserves actual changed target wind");
                }
                Console.WriteLine("PASS wind dependencies original projectile cases="+cases+" sensitive="+sensitive+" immune="+immune+" actual UpdateWeather changes="+changes+"; gate toggle / player positive and negative / pure hand");
            }
            finally{Main.windPhysics=savedGate;Main.windSpeedCurrent=savedSpeed;Main.windSpeedTarget=savedTarget;Main.windPhysicsStrength=savedStrength;Main.windCounter=savedCounter;Main.LocalPlayer.windPushed=pushed;}
        }
        private static PredictionEnvironment CaptureEnvironment(object source,object host,NPC npc)
        {
            var type=source.GetType();var owner=Activator.CreateInstance(type,Flags,null,new object[]{null,false},null);
            try
            {
                var cache=(NpcPredictionCache)Get(owner,"Cache");cache.Demand(0,1,8);
                var state=(NpcMotionState)type.GetMethod("Read",Flags).Invoke(null,new object[]{npc,(long)Get(host,"Session")});
                type.GetMethod("Prepare",Flags).Invoke(owner,new object[]{state.Identity,(long)Main.GameUpdateCount});
                var rolling=Get(owner,"rolling");
                Require((int)Get(owner,"observedCount")>0 && (long)Get(rolling,"resultTick")==Main.GameUpdateCount,"real Source completes this NPC's dependency capture type="+npc.type);
                return (PredictionEnvironment)Get(rolling,"resultEnvironment");
            }
            finally{type.GetMethod("Clear",Flags).Invoke(owner,null);}
        }
    }
}
