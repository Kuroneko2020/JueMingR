using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatNpcCoverageChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;

        // The native catalogue is independent of KnownMotion. This default-state
        // census exposes engineering gaps; it is NOT a valid-scene coverage or
        // precision claim for bosses, segments, event-only or invulnerable forms.
        internal static void Baseline(object context,string output,bool requireLongWindow)
        {
            // SetDefaults consults npcCatchable and other original type tables.
            // Without these, catchable critters become false hostile entries.
            foreach(string name in new[]{"Initialize_TileAndNPCData1","Initialize_TileAndNPCData2"})
                typeof(Main).GetMethod(name,Flags).Invoke(null,null);
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");
            var read=source.GetType().GetMethod("Read",Flags);
            var terrain=(IPredictionTerrain)Get(source,"Terrain");
            long session=(long)Get(host,"Session");
            Main.dayTime=false;Main.worldSurface=20;
            foreach(NPC entity in Main.npc)entity.active=false;
            Main.LocalPlayer.position=new Vector2(800,800);
            Main.LocalPlayer.dead=false;
            var env=new PredictionEnvironment{PlayerX=Main.LocalPlayer.Center.X,PlayerY=Main.LocalPlayer.Center.Y,
                PlayerWidth=Main.LocalPlayer.width,PlayerHeight=Main.LocalPlayer.height,WorldWidth=Main.maxTilesX,
                WorldHeight=Main.maxTilesY,WorldSurface=20,ClearLine=true};
            var rows=new StringBuilder("type,style,friendly,invulnerable,lifeMax,steps,stop,assumptions,classification\n");
            var stops=new SortedDictionary<string,int>();int catalog=0,defaultTargets=0,full=0;
            for(int type=1;type<NPCID.Count;type++)
            {
                var npc=new NPC();npc.SetDefaults(type);catalog++;
                string classification=npc.type!=type?"native-placeholder-or-remap":npc.friendly?"default-friendly":
                    npc.lifeMax<=0?"requires-valid-scene":npc.dontTakeDamage?"requires-selectable-phase":"default-hostile";
                int steps=-1;string reason="NotRun",assumptions="";
                if(classification=="default-hostile")
                {
                    defaultTargets++;npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(480,480);npc.timeLeft=750;Main.npc[0]=npc;
                    var state=(NpcMotionState)read.Invoke(null,new object[]{npc,session});
                    var cache=new NpcPredictionCache();cache.Demand(0,120);terrain.Reset();
                    cache.Prepare(new[]{state},1,0,100,env,terrain);
                    var result=cache.Read(0);steps=result[result.Count-1].TickOffset;reason=result.Stop.ToString();assumptions=((int)result.Assumptions).ToString();
                    if(steps==120)full++;
                    string key=reason+"/"+steps;if(!stops.ContainsKey(key))stops.Add(key,0);stops[key]++;
                    npc.active=false;
                }
                rows.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6},{7},{8}\n",type,npc.aiStyle,npc.friendly,npc.dontTakeDamage,npc.lifeMax,steps,reason,assumptions,classification);
            }
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"native-default-catalogue.csv"),rows.ToString(),new UTF8Encoding(false));
            Console.WriteLine("BASELINE native catalogue="+catalog+", default-hostile="+defaultTargets+", default snapshots reaching120="+full+" (NOT valid-scene coverage)");
            foreach(var pair in stops)Console.WriteLine("BASELINE "+pair.Key+"="+pair.Value);
            // Harpy is an ordinary independent flyer. A full horizon must be
            // real future updates, not a list relabelled with later timestamps.
            var harpy=new NPC();harpy.SetDefaults(NPCID.Harpy);harpy.active=true;harpy.whoAmI=0;harpy.target=0;
            harpy.position=new Vector2(480,480);harpy.velocity=new Vector2(2,-1);harpy.timeLeft=750;Main.npc[0]=harpy;
            var initial=(NpcMotionState)read.Invoke(null,new object[]{harpy,session});
            var forecast=new NpcPredictionCache();forecast.Demand(0,120);terrain.Reset();forecast.Prepare(new[]{initial},1,0,101,env,terrain);
            var trajectory=forecast.Read(0);
            Console.WriteLine("BASELINE harpy ticks="+trajectory[trajectory.Count-1].TickOffset+", stop="+trajectory.Stop);
            if(requireLongWindow)Require(trajectory.Count==121 && trajectory.Stop==PredictionStop.None,"ordinary harpy must have a complete120-update future; native-driven backend required");
        }
    }
}
