using System;
using System.IO;
using System.Text;
using System.Runtime.CompilerServices;
using System.Reflection;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    internal static class PredictionWire
    {
        internal const int Protocol=PredictionPipeProtocol.Protocol, MaximumBytes=PredictionPipeProtocol.MaximumPayload, MaximumHorizon=180;
        internal const int MaximumAlignmentAge=60;
        internal static readonly NativeValueSnapshot Npcs=new NativeValueSnapshot(typeof(NPC));
        internal static readonly NativeValueSnapshot Players=new NativeValueSnapshot(typeof(Player));
        internal static void PrepareCaptureLayouts()
        {
            // Metadata and owned codec layouts only: none of these type
            // initializers samples Main, constructs an actor or reads assets.
            // Do this on the transport owner during finite preparation so the
            // first selected type does not compile field plans on a game frame.
            var types=new[]{typeof(PredictionWire),typeof(NativeWorldSnapshot),typeof(NativeEntityDirectory),typeof(NativeActorContext),typeof(NativeEntitySnapshot),typeof(NativePredictionAlignment),typeof(NativePredictionAlignment.ValueHashWriter),typeof(NativeEntityContext),typeof(NativeRandomSnapshot),typeof(NativeTagSnapshot),typeof(NativeValueSnapshot),typeof(NativeCapturedValues),typeof(NativeTerrainSnapshot)};
            foreach(var type in types)
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            // Compiling these host codecs does not execute their reads. Their
            // first request was otherwise still paying tens of milliseconds
            // of JIT on the game frame despite prepared metadata layouts.
            foreach(var type in types)foreach(var method in type.GetMethods(BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                if(!method.ContainsGenericParameters && !method.IsAbstract && method.GetMethodBody()!=null)RuntimeHelpers.PrepareMethod(method.MethodHandle);
        }

        // Complete small fixture entry. Product sampling uses a bounded region;
        // this convenience entry intentionally rejects full real-world copies.
        internal static byte[] Capture(int[] slots,int selected,long tick,int horizon)
        {
            if((long)Main.maxTilesX*Main.maxTilesY>65536)throw new InvalidOperationException("Full world capture is prohibited.");
            return CaptureRegion(slots,selected,tick,horizon,1,0,0,Main.maxTilesX-1,Main.maxTilesY-1,false);
        }
        internal static byte[] CaptureRegion(int[] slots,int selected,long tick,int horizon,long world,int left,int top,int right,int bottom,bool referencesOnly)
        {return CaptureCore(slots,new int[0],selected,tick,horizon,world,left,top,right,bottom,referencesOnly);}
        internal static byte[] CaptureSceneRegion(int[] slots,int[] projectiles,int selected,long tick,int horizon,long world,int left,int top,int right,int bottom,bool referencesOnly)
        {return CaptureCore(slots,projectiles,selected,tick,horizon,world,left,top,right,bottom,referencesOnly);}
        internal static byte[] CaptureScene(int[] slots,int[] projectiles,int selected,long tick,int horizon)
        {
            if((long)Main.maxTilesX*Main.maxTilesY>65536)throw new InvalidOperationException("Full world capture is prohibited.");
            return CaptureCore(slots,projectiles,selected,tick,horizon,1,0,0,Main.maxTilesX-1,Main.maxTilesY-1,false);
        }
        internal static byte[] CaptureProduction(int[] slots,int[] projectiles,int selected,long tick,int horizon,NativeTerrainSnapshot terrain,int[] assets,bool referencesOnly)
        {return CaptureCore(slots,projectiles,selected,tick,horizon,terrain.World,0,0,0,0,referencesOnly,terrain,assets);}
        private static byte[] CaptureCore(int[] slots,int[] projectiles,int selected,long tick,int horizon,long world,int left,int top,int right,int bottom,bool referencesOnly,NativeTerrainSnapshot preparedTerrain=null,int[] assets=null)
        {
            if(slots==null || slots.Length<1 || slots.Length>Main.maxNPCs+1 || horizon<1 || horizon>MaximumHorizon)
                throw new ArgumentOutOfRangeException();
            var terrain=preparedTerrain??NativeTerrainSnapshot.Capture(world,left,top,right,bottom);
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                WriteCore(writer,slots,projectiles,selected,tick,horizon,terrain,assets,referencesOnly);
                writer.Flush();if(stream.Length>MaximumBytes)throw new InvalidDataException("Snapshot exceeds frame limit.");return stream.ToArray();
            }
        }
        internal static NativeCapturedValues CaptureSceneValues(int[] slots,int[] projectiles,int selected,long tick,int horizon)
        {
            if((long)Main.maxTilesX*Main.maxTilesY>65536)throw new InvalidOperationException("Full world capture is prohibited.");
            return CaptureProductionValues(slots,projectiles,selected,tick,horizon,NativeTerrainSnapshot.Capture(1,0,0,Main.maxTilesX-1,Main.maxTilesY-1),null,false);
        }
        internal static NativeCapturedValues CaptureProductionValues(int[] slots,int[] projectiles,int selected,long tick,int horizon,NativeTerrainSnapshot terrain,int[] assets,bool referencesOnly)
        {
            return FillProductionValues(new NativeCapturedValues(),slots,projectiles,selected,tick,horizon,terrain,assets,referencesOnly);
        }
        internal static NativeCapturedValues FillProductionValues(NativeCapturedValues values,int[] slots,int[] projectiles,int selected,long tick,int horizon,NativeTerrainSnapshot terrain,int[] assets,bool referencesOnly)
        {
            if(slots==null || slots.Length<1 || slots.Length>Main.maxNPCs+1 || horizon<1 || horizon>MaximumHorizon)throw new ArgumentOutOfRangeException();
            try{WriteCore(values,slots,projectiles,selected,tick,horizon,terrain,assets,referencesOnly);values.Seal();return values;}
            catch{values.Dispose();throw;}
        }
        private static void WriteCore(BinaryWriter writer,int[] slots,int[] projectiles,int selected,long tick,int horizon,NativeTerrainSnapshot terrain,int[] assets,bool referencesOnly)
        {
                writer.Write(Protocol);writer.Write(tick);writer.Write(horizon);writer.Write(selected);
                writer.Write(Main.maxTilesX);writer.Write(Main.maxTilesY);writer.Write(Main.worldSurface);writer.Write(Main.rockLayer);
                writer.Write(Main.dayTime);writer.Write(Main.time);writer.Write(Main.GameMode);writer.Write(Main.myPlayer);
                writer.Write(Main.bloodMoon);writer.Write(Main.eclipse);writer.Write(Main.windSpeedCurrent);
                NativeWorldSnapshot.Write(writer);
                NativeEntityContext.WriteShared(writer);
                if(assets==null)NativeAssetSnapshot.WriteFixture(writer);else NativeAssetSnapshot.WriteKeys(writer,assets);
                NativeLightingSnapshot.Write(writer,slots);
                NativeEffectBoundary.WriteAllocation(writer);
                NativeRandomSnapshot.Write(writer);
                NativeEntityDirectory.Write(writer);
                int playerCount=0;for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]!=null && Main.player[i].active)playerCount++;
                writer.Write(playerCount);
                for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]!=null && Main.player[i].active){writer.Write(i);Players.Write(writer,Main.player[i]);NativeActorContext.WritePlayer(writer,Main.player[i]);NativePlayerMotion.Write(writer,Main.player[i]);}
                writer.Write(slots.Length);int prior=-1;bool found=false;
                foreach(int slot in slots)
                {
                    if(slot<=prior || slot>Main.maxNPCs || Main.npc[slot]==null || slot==selected && !Main.npc[slot].active)throw new InvalidDataException("Invalid native slot set.");
                    prior=slot;found|=slot==selected;writer.Write(slot);Npcs.Write(writer,Main.npc[slot]);
                    NativeEntityContext.WriteNpc(writer,Main.npc[slot]);
                }
                if(!found)throw new InvalidDataException("Selected slot absent.");
                NativeEntitySnapshot.Write(writer,projectiles);
                NativeImmunitySnapshot.Write(writer,slots);
                terrain.Write(writer,referencesOnly);
        }
        internal static byte[] ReadFrame(Stream input)
        {return PredictionPipeProtocol.ReadFrame(input);}
        internal static void WriteFrame(Stream output,byte[] payload)
        {PredictionPipeProtocol.WriteFrame(output,payload);}
    }
}
