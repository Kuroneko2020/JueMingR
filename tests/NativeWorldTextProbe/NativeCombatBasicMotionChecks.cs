using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    // Legal initial conditions only; the ordinary original-update/Host seam
    // supplies each later observation. Never edit a prediction or its cache.
    internal static class NativeCombatBasicMotionChecks
    {
        internal static void Run(object context,NpcPredictionCache cache,Action step,string output)
        {
            var host=Get(context,"CombatObservation");
            var rows=new List<string>{"scene,frame,tick,slot,generation,type,netId,x,y,vx,vy,ai0,ai1,ai2,ai3,wet,selected,published,count,stop,strokes,text"};
            try{Ground(context,host,cache,step,rows);EmptyFutureReason(context,host,cache,step);}
            finally{File.WriteAllLines(Path.Combine(output,"basic-motion-updates.csv"),rows);}
        }
        private static void Ground(object context,object host,NpcPredictionCache cache,Action step,List<string> rows)
        {
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();
            Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};
            var p=Main.LocalPlayer;p.position=new Vector2(2200,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.statLife=p.statLifeMax=p.statLifeMax2=500;
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1870,2400,NPCID.ArmoredSkeleton,Start:16,Target:Main.myPlayer);var n=Main.npc[slot];
            for(int x=119;x<=120;x++)for(int y=137;y<150;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:25));
            int positive=0,missing=0,selectedCount=0,shown=0;bool movedAfter=false;
            for(int frame=0;frame<240;frame++)
            {
                // Release before vanilla's 60-update turn-away threshold, so
                // the measured segment includes pursuing through the obstacle.
                if(frame==60)for(int x=119;x<=120;x++)for(int y=137;y<150;y++)Main.tile[x,y].active(false);
                NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
                var selection=Get(host,"Selection");var key=(NpcIdentity)Get(selection,"Target");bool selected=(bool)Get(selection,"HasTarget") && ReferenceEquals(key.Token,n);
                var path=cache.Read(0);var layer=Get(host,"World");
                if(selected){selectedCount++;if(path!=null)shown++;if(n.ai[3]>0){positive++;if(path==null)missing++;}}
                if(path!=null && (!selected || !path.Identity.Equals(key) || path.CaptureTick!=Main.GameUpdateCount))throw new InvalidOperationException("Basic motion consumed a stale or different instance.");
                rows.Add(Csv("armored-block-recover",frame,Main.GameUpdateCount,slot,n.generation,n.type,n.netID,n.position.X,n.position.Y,n.velocity.X,n.velocity.Y,n.ai[0],n.ai[1],n.ai[2],n.ai[3],n.wet,selected,path!=null,path?.Count??0,path?.Stop.ToString()??"none",Get(layer,"StrokeCount"),Get(layer,"pathText")));
                movedAfter|=frame>60 && n.position.X>1920;
                if(!n.active || n.life<=0 || p.dead)throw new InvalidOperationException("Ground scenario ended before its planned original recovery.");
            }
            Console.WriteLine("BASIC-GROUND selected="+selectedCount+" shown="+shown+" positiveA3="+positive+" missingWithPositive="+missing+" recovered="+movedAfter);
            Require(positive>10 && movedAfter,"Original obstacle creates positive blocked counts and subsequently recovers.");
            Require(missing==0,"A positive ordinary original blocked count must not remove all predicted futures.");
        }
        private static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
        private static void EmptyFutureReason(object context,object host,NpcPredictionCache cache,Action step)
        {
            var selection=Get(host,"Selection");var key=(NpcIdentity)Get(selection,"Target");var n=Main.npc[key.Slot];
            n.buffImmune[BuffID.Slow]=false;n.AddBuff(BuffID.Slow,500);
            NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
            string text=(string)Get(Get(host,"World"),"pathText");
            Console.WriteLine("BASIC-REASON cacheEmpty="+(cache.Read(0)==null)+" text="+(text??"<null>"));
            Require(cache.Read(0)==null,"An unmodeled current motion state supplies no invented future.");
            Require(!string.IsNullOrEmpty(text) && text.Contains("状态"),"An exact selected target retains a player-readable first-step motion stop reason.");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:25));step();
            Require(Get(Get(host,"World"),"pathText")==null,"Disabling path display clears its empty-future reason.");
        }
        private static string Csv(params object[] values){var fields=new string[values.Length];for(int i=0;i<fields.Length;i++)fields[i]="\""+Convert.ToString(values[i],CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"";return string.Join(",",fields);}
    }
}
