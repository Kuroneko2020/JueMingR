using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Events;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Version-bound development closure for scalar world/engine dependencies.
    // Engine arrays are restored independently; immutable native ID tables stay
    // initialized once. This is not a user-world/header/save serialization API.
    internal static class NativeWorldSnapshot
    {
        private static readonly NativeValueSnapshot[] Values={new NativeValueSnapshot(typeof(Main),true,false),
            new NativeValueSnapshot(typeof(NPC),true),new NativeValueSnapshot(typeof(WorldGen),true,false),
            new NativeValueSnapshot(typeof(Terraria.Testing.DebugOptions),true,false,new[]{"Shared_RandomizeProjectileSlots","noLimits"})};
        private static readonly NativeValueSnapshot Dd2=new NativeValueSnapshot(typeof(DD2Event),true,false);
        private static readonly FieldInfo DeadGoblins=typeof(DD2Event).GetField("_deadGoblinSpots",BindingFlags.Static|BindingFlags.NonPublic);
        internal static void AdvanceObservedWind()
        {
            // Main.UpdateWeather runs before players once per day-rate unit.
            // Continue only its deterministic convergence with the observed
            // target/rain driver; later driver changes retire exact history.
            // Do not consume weather RNG or create lightning/cloud effects.
            float target=Main.windSpeedTarget*(1f+5f/9f*Main.maxRaining);
            if(float.IsNaN(target) || float.IsInfinity(target) || float.IsNaN(Main.windSpeedCurrent) || float.IsInfinity(Main.windSpeedCurrent) || Main.dayRate<0 || Main.dayRate>86400)throw new InvalidDataException("Wind continuation premise.");
            for(int i=0;i<Main.dayRate && Main.windSpeedCurrent!=target;i++)
            {
                float delta=.0003f+Math.Abs(target-Main.windSpeedCurrent)*.0015f;
                if(Main.windSpeedCurrent<target){Main.windSpeedCurrent+=delta;if(Main.windSpeedCurrent>target)Main.windSpeedCurrent=target;}
                else{Main.windSpeedCurrent-=delta;if(Main.windSpeedCurrent<target)Main.windSpeedCurrent=target;}
            }
        }
        internal static void Write(BinaryWriter writer)
        {
            foreach(var value in Values)value.Write(writer,null);
            WriteEvent(writer);
        }
        internal static void WriteEvent(BinaryWriter writer)
        {
            Dd2.Write(writer,null);writer.Write(Main.CurrentFrameFlags.ActivePlayersCount);
            writer.Write(NPC.waveNumber);writer.Write(NPC.waveKills);writer.Write(NPC.totalInvasionPoints);
            var positions=(List<Vector2>)DeadGoblins.GetValue(null);if(positions.Count>2048)throw new InvalidDataException("DD2 dependency capacity.");
            writer.Write(positions.Count);foreach(var point in positions){writer.Write(point.X);writer.Write(point.Y);}
        }
        internal static void Read(BinaryReader reader)
        {
            foreach(var value in Values)value.Read(reader,null);
            Dd2.Read(reader,null);int players=reader.ReadInt32();if(players<0 || players>Main.maxPlayers)throw new InvalidDataException("DD2 player count.");Main.CurrentFrameFlags.ActivePlayersCount=players;
            NPC.waveNumber=reader.ReadInt32();NPC.waveKills=reader.ReadSingle();NPC.totalInvasionPoints=reader.ReadSingle();
            int count=reader.ReadInt32();if(count<0 || count>2048)throw new InvalidDataException("DD2 dependency count.");
            var positions=(List<Vector2>)DeadGoblins.GetValue(null);positions.Clear();
            for(int i=0;i<count;i++){float x=reader.ReadSingle(),y=reader.ReadSingle();if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y))throw new InvalidDataException("DD2 position.");positions.Add(new Vector2(x,y));}
            // This is an offline authority-conditioned future, never a socket
            // peer. The game snapshot cannot enable external host facilities.
            // Keep the observed client/server semantic flag: changing dedServ
            // changes FindFrame and NPC RNG branches, not just presentation.
            Main.netMode=0;Main.gameMenu=false;Main.gamePaused=false;
        }
    }
}
