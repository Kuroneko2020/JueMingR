using System;
using System.Collections;
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
    internal static class NativeCombatPredictionChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var read=source.GetType().GetMethod("Read",Flags);long session=(long)Get(host,"Session");
            Main.dayTime=false;Main.worldSurface=20;Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]==null)Main.player[i]=new Player();
            foreach(var n in Main.npc)n.active=false;
            var player=Main.LocalPlayer;player.position=new Vector2(640,640);
            var env=new PredictionEnvironment{PlayerX=player.Center.X,PlayerY=player.Center.Y,PlayerWidth=player.width,PlayerHeight=player.height,WorldWidth=Main.maxTilesX,WorldSurface=(float)Main.worldSurface,Enraged=true,ClearLine=true};
            Func<NPC,NpcMotionState> capture=n=>(NpcMotionState)read.Invoke(null,new object[]{n,session});
            var cache=new NpcPredictionCache();cache.Demand(0,60);
            var npc=Main.npc[0];npc.SetDefaults(372);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.ai[0]=0;npc.ai[1]=88;npc.ai[3]=-2;npc.direction=1;
            var states=new[]{capture(npc)};
            string initial=Stamp();cache.Prepare(states,1,0,100,env,terrain);Require(Stamp()==initial,"prediction preserves all native fields/arrays, tile values, RNG and Collision scratch");
            int computed=cache.Steps;var first=cache.Read(0);cache.Demand(1,30);cache.Prepare(states,1,0,100,env,terrain);
            Require(cache.Steps==computed && ReferenceEquals(first,cache.Read(1)),"two equivalent consumers share one result");
            cache.Demand(1,120);cache.Prepare(states,1,0,100,env,terrain);Require(cache.Builds==1 && cache.Steps<=120,"longer demand extends existing tail");
            cache.Release(0);Require(cache.Read(0)==null && cache.Read(1)!=null,"consumer cancellation is independent");
            cache.Release(1);Require(cache.Read(1)==null,"last release retires shared result");
            foreach(int type in new[]{2,6,42,49,93,137})
            {
                npc.SetDefaults(type);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.velocity=Vector2.Zero;npc.timeLeft=750;
                if(npc.aiStyle==14){npc.ai[1]=200;npc.ai[2]=0;}
                Compare(npc,capture,env,terrain,120,"flight type "+type,.12f);
            }
            Main.tileSolid[TileID.Stone]=true;for(int x=1;x<Main.maxTilesX-1;x++){Main.tile[x,75].active(true);Main.tile[x,75].type=TileID.Stone;}
            foreach(int timer in new[]{-2,-1002,-2002})
            {
                npc.SetDefaults(1);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,1200-npc.height);npc.velocity=Vector2.Zero;npc.ai[0]=timer;npc.ai[1]=-1;npc.ai[2]=1;npc.direction=1;npc.timeLeft=750;
                Compare(npc,capture,env,terrain,120,"slime jump/landing "+timer,.12f);
            }
            for(int x=1;x<Main.maxTilesX-1;x++)Main.tile[x,75].active(false);
            for(int x=24;x<=30;x++)for(int y=24;y<=30;y++)Main.tile[x,y].liquid=255;
            npc.SetDefaults(49);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.velocity=new Vector2(2,2);npc.timeLeft=750;
            Compare(npc,capture,env,terrain,90,"bat enters/leaves water",.12f);
            for(int x=24;x<=30;x++)for(int y=24;y<=30;y++)Main.tile[x,y].liquid=0;
            npc.SetDefaults(2);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(500,400);npc.buffType[0]=BuffID.Confused;npc.buffTime[0]=30;npc.timeLeft=750;
            Compare(npc,capture,env,terrain,20,"confused eye",.12f);
            Array.Clear(npc.buffType,0,npc.buffType.Length);Array.Clear(npc.buffTime,0,npc.buffTime.Length);npc.confused=false;
            Linked(capture,env,terrain);
            // The oracle is actual fixed .8 UpdateNPC in this isolated process.
            // No production model, collision or movement method is patched out.
            npc.SetDefaults(372);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.ai[1]=88;npc.ai[3]=-2;npc.direction=1;
            Compare(npc,capture,env,terrain,12,"shark preparation/dash",.02f);
            npc.SetDefaults(370);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.ai[0]=1;npc.ai[2]=28;npc.localAI[0]=1;npc.velocity=new Vector2(16,0);npc.timeLeft=750;
            Compare(npc,capture,env,terrain,10,"Duke dash to hover",.05f);
            Rolling(npc,capture,env,terrain);
            npc.SetDefaults(371);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.ai[0]=1;npc.ai[1]=4;npc.ai[3]=1;npc.timeLeft=750;
            Compare(npc,capture,env,terrain,2,"bubble explosion phase",1.0f);
            npc.SetDefaults(29);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.ai[2]=50;npc.ai[3]=45;npc.timeLeft=750;
            var tele=capture(npc);var group=new[]{tele};PredictionStop reason;terrain.Reset();Require(NpcMotion.Step(ref tele,group,1,env,terrain,1,out reason) && tele.NewSegment,"known teleport emits a discontinuity");
            bool dedicated=Main.dedServ;Main.dedServ=true;try{npc.UpdateNPC(0);}finally{Main.dedServ=dedicated;}
            Require(Vector2.Distance(new Vector2(tele.X,tele.Y),npc.position)<.02,"known teleport position follows native update, including post-AI gravity: model="+tele.X+","+tele.Y+" native="+npc.position+" style="+npc.aiStyle+" ai="+string.Join(",",npc.ai));
            npc.SetDefaults(488);npc.active=true;npc.whoAmI=0;npc.position=new Vector2(400,400);npc.velocity=Vector2.Zero;npc.timeLeft=750;cache=new NpcPredictionCache();cache.Demand(0,120);states=new[]{capture(npc)};terrain.Reset();cache.Prepare(states,1,0,200,env,terrain);int built=cache.Builds,stepped=cache.Steps;
            for(int i=1;i<=120;i++)cache.Prepare(states,1,0,200+i,env,terrain);
            Require(cache.Builds==built && cache.Reuses==120 && cache.Steps==stepped,"unchanged real dummy observations republish sample identity without any repeated prediction steps");
            var snapshot=cache.Read(0);states[0].TimeLeft=1;cache.Prepare(states,1,0,320,env,terrain);Require(cache.Builds==built+1 && cache.Read(0).Stop==PredictionStop.Despawn && snapshot.Count==121,"same-tick despawn changes invalidate; old published buffer remains immutable");
            states[0].TimeLeft=750;string unchanged=Stamp();bool threw=false;try{cache.Prepare(states,1,0,321,env,new FailedTerrain());}catch(InvalidOperationException){threw=true;}Require(threw && cache.Read(0)==null && Stamp()==unchanged,"failed preparation retires output and never changes native state");
            Console.WriteLine("PASS first batch prediction: native UpdateNPC oracle, isolated state, known teleport discontinuity, shared/extended demand and cancellation.");
        }
        private sealed class FailedTerrain : IPredictionTerrain
        {public bool Unchanged {get{throw new InvalidOperationException("isolated terrain failure");}}public void Reset(){throw new InvalidOperationException("isolated terrain failure");}public bool Move(ref NpcMotionState n,out PredictionStop stop){stop=PredictionStop.None;throw new InvalidOperationException();}public bool Solid(MotionRect b,out bool value,out PredictionStop stop){value=false;stop=PredictionStop.None;throw new InvalidOperationException();}}
        private static void Rolling(NPC npc,Func<NPC,NpcMotionState> capture,PredictionEnvironment env,IPredictionTerrain terrain)
        {
            npc.SetDefaults(370);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.ai[0]=1;npc.ai[2]=0;npc.localAI[0]=1;npc.velocity=new Vector2(16,0);npc.timeLeft=750;
            // Production samples completed updates: native CheckActive resets
            // the live timer then decrements it (750 -> 749) on the first step.
            bool prior=Main.dedServ;Main.dedServ=true;try{npc.UpdateNPC(0);}finally{Main.dedServ=prior;}
            var cache=new NpcPredictionCache();cache.Demand(0,12);var states=new[]{capture(npc)};float initialX=npc.position.X;terrain.Reset();cache.Prepare(states,1,0,500,env,terrain);var original=cache.Read(0);Main.dedServ=true;
            try{for(int i=1;i<=12;i++){npc.UpdateNPC(0);states[0]=capture(npc);cache.Prepare(states,1,0,500+i,env,terrain);Require(cache.Read(0).SampleTick==500+i,"rolling result carries current native sampling tick");}}
            finally{Main.dedServ=prior;}
            Require(cache.Builds==1 && cache.Rolls==12 && cache.Steps==36,"actual advancing native Duke must extend only its rolling tail: builds="+cache.Builds+" rolls="+cache.Rolls+" steps="+cache.Steps);
            Require(original.SampleTick==500 && original[0].Bounds.X==initialX,"rolling publication cannot overwrite a retained result");
            Console.WriteLine("WORKLOAD native moving Duke: 12 updates, builds="+cache.Builds+" rolls="+cache.Rolls+" steps="+cache.Steps+" (full rebuild would be 156).");
        }
        private static void Linked(Func<NPC,NpcMotionState> capture,PredictionEnvironment env,IPredictionTerrain terrain)
        {
            var head=Main.npc[5];var body=Main.npc[6];var tail=Main.npc[7];NPC[] native={head,body,tail};int[] types={7,8,9};
            for(int i=0;i<3;i++){var n=native[i];n.SetDefaults(types[i]);n.active=true;n.whoAmI=i+5;n.target=0;n.ai[0]=i==2?0:i+6;n.ai[1]=i==0?0:i+4;n.ai[3]=n.realLife=5;n.position=new Vector2(420-i*30,400+i*15);n.timeLeft=750;}
            var group=new[]{capture(head),capture(body),capture(tail)};bool prior=Main.dedServ;Main.dedServ=true;terrain.Reset();float maximum=0;
            try{for(int i=1;i<=60;i++){head.position+=new Vector2(2,(float)Math.Sin(i*.1));group[0]=capture(head);for(int j=1;j<3;j++){var n=group[j];PredictionStop stop;Require(NpcMotion.Step(ref n,group,3,env,terrain,i,out stop),"linked primitive available");group[j]=n;native[j].UpdateNPC(j+5);float error=Vector2.Distance(new Vector2(n.X,n.Y),native[j].position);maximum=Math.Max(maximum,error);Require(error<.05,"actual worm linked update error="+error);}}}
            finally{Main.dedServ=prior;foreach(var n in native)n.active=false;}
            Console.WriteLine("ORACLE worm body/tail 60 ticks with observed parent motion max="+maximum.ToString("F4")+"; head steering remains approximate");
        }
        private static void Compare(NPC npc,Func<NPC,NpcMotionState> capture,PredictionEnvironment env,IPredictionTerrain terrain,int ticks,string name,float maximum)
        {
            var model=capture(npc);var group=new[]{model};Vector2 start=npc.position,velocity=npc.velocity;double sum=0,baseline=0;float worst=0;int grounded=0;bool dedicated=Main.dedServ;
            terrain.Reset();Main.dedServ=true;
            try
            {
                for(int i=1;i<=ticks;i++)
                {
                    PredictionStop stop;Require(NpcMotion.Step(ref model,group,1,env,terrain,i,out stop),name+" model unexpectedly ended: "+stop);group[0]=model;
                    npc.UpdateNPC(npc.whoAmI);
                    if(npc.collideY && npc.velocity.Y==0)grounded++;
                    float error=Vector2.Distance(new Vector2(model.X,model.Y),npc.position);worst=Math.Max(worst,error);sum+=error;baseline+=Vector2.Distance(start+velocity*i,npc.position);
                    Require(error<=maximum,name+" t="+i+" error="+error+" native="+npc.position+" model="+model.X+","+model.Y);
                    Require(model.A0==npc.ai[0] && model.CanReceive==(!npc.dontTakeDamage && !npc.immortal),name+" phase/receive mismatch t="+i);
                }
            }
            finally{Main.dedServ=dedicated;}
            if(name.StartsWith("slime",StringComparison.Ordinal))Require(grounded>0,"slime oracle must actually land on the native solid floor: position="+npc.position+" velocity="+npc.velocity+" noGravity="+npc.noGravity+" noTile="+npc.noTileCollide+" ai="+string.Join(",",npc.ai));
            Console.WriteLine("ORACLE "+name+" ticks="+ticks+" mean="+(sum/ticks).ToString("F4")+" max="+worst.ToString("F4")+" inertialMean="+(baseline/ticks).ToString("F4"));
        }
        private static string Stamp()
        {
            var s=new StringBuilder();foreach(var n in Main.npc)Append(s,n);foreach(var p in Main.player)Append(s,p);foreach(var p in Main.projectile)Append(s,p);
            Append(s,Main.rand);for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++)Append(s,Main.tile[x,y]);
            foreach(var field in typeof(Collision).GetFields(Flags))if(field.IsStatic && (field.FieldType.IsPrimitive || field.FieldType.IsValueType))s.Append(field.Name).Append('=').Append(field.GetValue(null)).Append(';');
            return s.ToString();
        }
        private static void Append(StringBuilder s,object value)
        {
            if(value==null){s.Append("null;");return;}
            foreach(var field in value.GetType().GetFields(Flags))
            {
                if(field.IsStatic)continue;object data=field.GetValue(value);s.Append(field.Name).Append('=');
                var array=data as Array;if(array!=null){foreach(var item in array)s.Append(item).Append(',');}
                else if(data!=null && (data.GetType().IsValueType || data is string))s.Append(data);
                else s.Append(data==null?0:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(data));s.Append(';');
            }
        }
    }
}
