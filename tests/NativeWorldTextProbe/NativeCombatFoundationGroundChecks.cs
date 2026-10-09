using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatFoundationGroundChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object source)
        {
            var model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcGroundMotion");var known=model.GetMethod("Known",Flags);var ordinary=model.GetMethod("Step",Flags);var fallback=model.GetMethod("Fallback",Flags);
            var read=source.GetType().GetMethod("Read",Flags);var ai=typeof(NPC).GetMethod("AI_003_Fighters",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");var p=Main.LocalPlayer;int cases=0;int network=Main.netMode;bool server=Main.dedServ;
            Main.netMode=1;Main.dedServ=true;Main.dayTime=false;Main.tileSolid[1]=true;Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;typeof(NPC).GetField("gravity",Flags).SetValue(null,.3f);
            try
            {
                foreach(int type in new[]{3,21,27,31,294,295,296,47,77,104,168,196,385,389,464,470,524,525,526,527,67,428,624,287,460,120,186,187,188,189,269,270,271,272,273,274,275,276,277,278,279,280,331,332,109,199,257,258,580,508,415,532,582,425,586,163,164,236,239,530,290,391,427,426})
                foreach(int direction in new[]{-1,1})foreach(int scene in new[]{0,1,2,3,4,5})
                {
                    for(int x=35;x<70;x++)for(int y=39;y<=60;y++){var cell=Main.tile[x,y];cell.active(y==60);cell.type=1;cell.liquid=0;cell.halfBrick(false);cell.slope(0);}
                    var n=new NPC();n.SetDefaults(type);n.whoAmI=199;n.active=true;n.position=new Vector2(800,960-n.height);n.oldPosition=n.position-new Vector2(2*direction,0);n.velocity=new Vector2(2*direction,0);n.direction=n.spriteDirection=direction;n.target=p.whoAmI;n.ai[3]=0;n.ai[2]=0;n.life=n.lifeMax=100;n.scale=1;
                    n.localAI[3]=1;n.localAI[1]=1;n.alpha=0;if(type==425)n.spriteDirection=-direction;
                    // These families' independent ranged gate is kept in a
                    // declared cooldown so this local comparison reaches the
                    // common ground block; its clock is not a door assertion.
                    if(type==290 || type==426){n.ai[1]=30;n.velocity.X=direction*(type==290?1:4);n.oldPosition=n.position-new Vector2(n.velocity.X,0);}
                    p.active=true;p.dead=p.ghost=false;p.position=new Vector2(n.Center.X+direction*(scene==0?70:300)-p.width/2,n.Center.Y-p.height/2);p.velocity=Vector2.Zero;
                    var env=new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerDead=false,WorldSurface=20,Multiplayer=true};
                    int probe=(int)((n.Center.X+(Wide(type)?n.width/2+16:15)*direction)/16),row=59;
                    if(scene==1 || scene==2){for(int y=row-1;y>=row-scene;y--)Main.tile[probe,y].active(true);if(type==624 && scene==1)Main.tile[(int)n.Center.X/16,52].active(true);}
                    if(scene==3){int step=(int)((n.position.X+n.velocity.X+n.width/2+(n.width/2+1)*direction)/16);Main.tile[step,59].active(true);}
                    if(scene==4){Main.tile[probe,60].active(false);Main.tile[probe+direction,60].active(false);p.position.Y-=80;env.PlayerY=p.Center.Y;}
                    if(scene==5){Main.tile[probe,58].active(true);Main.tile[probe,58].type=10;n.ai[1]=4;n.ai[2]=58;}
                    if(type==290 || type==426){n.ai[1]=30;n.ai[2]=0;Require(p.Center.Y+100>=n.position.Y,"B426 independent high-target jump excluded explicitly.");}
                    var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});state.Gravity=.3f;terrain.Reset();bool isKnown=(bool)known.Invoke(null,new object[]{type});var method=isKnown?ordinary:fallback;
                    object[] args=isKnown?new object[]{state,env,terrain,false,PredictionStop.None}:fallback.GetParameters().Length==6?new object[]{state,env,terrain,false,1,PredictionStop.None}:new object[]{state,env,false,1,PredictionStop.None};
                    bool accepted=(bool)method.Invoke(null,args);state=(NpcMotionState)args[0];
                    try{ai.Invoke(n,null);}catch(Exception error){throw new InvalidOperationException("B original AI fixture type="+type+" direction="+direction+" scene="+scene,error);}
                    Require(accepted,"B common-ground local boundary type="+type+" scene="+scene+" stop="+args[args.Length-1]);
                    // A2 may also belong to an independent pre-motor attack
                    // (389 projectile timer). It is a common-action expected
                    // value only in the declared door-knocking scene.
                    Require(Math.Abs(state.Y-n.position.Y)<.001 && Math.Abs(state.Vx-n.velocity.X)<.0001 && Math.Abs(state.Vy-n.velocity.Y)<.0001 && state.Direction==n.direction && (scene!=5 || !DoorScene(type) || state.A1==n.ai[1] && state.A2==n.ai[2]),"B original finite ground chain type="+type+" dir="+direction+" scene="+scene+" expected="+n.position.Y+"/"+n.velocity+"/"+n.direction+"/"+n.ai[1]+"/"+n.ai[2]+" actual="+state.Y+"/"+state.Vx+"/"+state.Vy+"/"+state.Direction+"/"+state.A1+"/"+state.A2);cases++;
                }
                Console.WriteLine("PASS B original complete common-ground support/StepUp/obstacle/pounce/door finite cases="+cases);
                int steering=0;
                foreach(int direction in new[]{-1,1})foreach(float vx in new[]{-5f,-4.99f,-.1f,0f,.1f,4.99f,5f})foreach(float vy in new[]{-2f,3f})foreach(float blocked in new[]{0f,61f})foreach(int side in new[]{-1,0,1})
                {
                    var n=new NPC();n.SetDefaults(258);n.whoAmI=199;n.active=true;n.position=new Vector2(800,880);n.velocity=new Vector2(vx,vy);n.oldPosition=n.position-new Vector2(vx,0);n.direction=n.spriteDirection=direction;n.target=p.whoAmI;n.ai[3]=blocked;n.life=n.lifeMax=100;
                    p.position=new Vector2(n.Center.X+side*150-p.width/2,n.Center.Y-p.height/2);p.velocity=Vector2.Zero;
                    var env=new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,WorldSurface=20,Multiplayer=true};
                    var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});terrain.Reset();object[] args={state,env,terrain,false,1,PredictionStop.None};
                    bool accepted=(bool)fallback.Invoke(null,args);state=(NpcMotionState)args[0];ai.Invoke(n,null);
                    Require(accepted && Math.Abs(state.Vx-n.velocity.X)<.0001 && Math.Abs(state.Vy-n.velocity.Y)<.0001 && state.Direction==n.direction && state.SpriteDirection==n.spriteDirection,"258 airborne target/motor/threshold dir="+direction+" vx="+vx+" vy="+vy+" blocked="+blocked+" side="+side+" expected="+n.velocity+"/"+n.direction+" actual="+state.Vx+"/"+state.Vy+"/"+state.Direction);steering++;
                }
                foreach(bool support in new[]{false,true})foreach(bool los in new[]{false,true})foreach(float height in new[]{49f,51f,100f})
                {
                    for(int x=35;x<70;x++)for(int y=39;y<=60;y++){Main.tile[x,y].active(support && y==60);Main.tile[x,y].type=1;}
                    var n=new NPC();n.SetDefaults(258);n.whoAmI=199;n.active=true;n.position=new Vector2(800,960-n.height);n.velocity=new Vector2(2,0);n.oldPosition=n.position-new Vector2(2,0);n.direction=n.spriteDirection=1;n.target=p.whoAmI;n.life=n.lifeMax=100;
                    p.position=new Vector2(n.Center.X+150-p.width/2,n.position.Y-height-p.height/2);p.velocity=Vector2.Zero;
                    if(!los)for(int y=39;y<60;y++)Main.tile[54,y].active(true);
                    var env=new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,WorldSurface=20,Multiplayer=true};
                    var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});terrain.Reset();object[] args={state,env,terrain,false,1,PredictionStop.None};
                    bool accepted=(bool)fallback.Invoke(null,args);state=(NpcMotionState)args[0];ai.Invoke(n,null);
                    Require(accepted && Math.Abs(state.Vx-n.velocity.X)<.0001 && Math.Abs(state.Vy-n.velocity.Y)<.0001,"258 high-target LOS/support height="+height+" los="+los+" support="+support+" expected="+n.velocity+" actual="+state.Vx+"/"+state.Vy);steering++;
                }
                Console.WriteLine("PASS 258 finite airborne threshold and ground LOS/support cases="+steering);
                int tall=0;
                foreach(int type in new[]{104,163,164,236,239,530})foreach(int direction in new[]{-1,1})
                {
                    for(int x=35;x<70;x++)for(int y=39;y<=61;y++){Main.tile[x,y].active(y==61);Main.tile[x,y].type=1;Main.tile[x,y].halfBrick(false);}
                    var n=new NPC();n.SetDefaults(type);n.whoAmI=199;n.active=true;n.position=new Vector2(800,975-n.height);n.velocity=new Vector2(2*direction,0);n.oldPosition=n.position-new Vector2(2*direction,0);n.direction=n.spriteDirection=direction;n.localAI[1]=1;n.target=p.whoAmI;n.life=n.lifeMax=100;
                    p.position=new Vector2(n.Center.X+direction*300-p.width/2,n.Center.Y-p.height/2);p.velocity=Vector2.Zero;
                    int column=(int)((n.position.X+n.velocity.X+n.width/2+(n.width/2+1)*direction)/16);Main.tile[column,59].active(true);Main.tile[column,59].halfBrick(true);
                    var env=new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,Multiplayer=true};
                    var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});terrain.Reset();var method=(bool)known.Invoke(null,new object[]{type})?ordinary:fallback;
                    object[] args=method==ordinary?new object[]{state,env,terrain,false,PredictionStop.None}:new object[]{state,env,terrain,false,1,PredictionStop.None};bool accepted=(bool)method.Invoke(null,args);state=(NpcMotionState)args[0];ai.Invoke(n,null);
                    Require(accepted && Math.Abs(state.Y-n.position.Y)<.001 && Math.Abs(state.Vy-n.velocity.Y)<.0001,"B high StepUp23 boundary type="+type+" dir="+direction+" expected="+n.position.Y+"/"+n.velocity.Y+" actual="+state.Y+"/"+state.Vy);tall++;
                }
                foreach(int rows in new[]{6,7,8,9,10,11,13,14})
                {
                    for(int x=35;x<70;x++)for(int y=39;y<=61;y++){Main.tile[x,y].active(y==60);Main.tile[x,y].halfBrick(false);}
                    var n=new NPC();n.SetDefaults(586);n.whoAmI=199;n.active=true;n.alpha=0;n.position=new Vector2(800,960-n.height);n.velocity=new Vector2(.8f,0);n.oldPosition=n.position-new Vector2(.8f,0);n.direction=n.spriteDirection=1;n.target=p.whoAmI;n.life=n.lifeMax=100;
                    p.position=new Vector2(n.Center.X+30-p.width/2,944-rows*16-p.height);p.velocity=Vector2.Zero;
                    var env=new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,Multiplayer=true};
                    var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});terrain.Reset();object[] args={state,env,terrain,false,1,PredictionStop.None};bool accepted=(bool)fallback.Invoke(null,args);state=(NpcMotionState)args[0];ai.Invoke(n,null);
                    Require(accepted && Math.Abs(state.Vy-n.velocity.Y)<.0001,"B586 nonexpert high target segmented jump rows="+rows+" expected="+n.velocity.Y+" actual="+state.Vy);tall++;
                }
                Console.WriteLine("PASS B high StepUp23 ordinary exception and586 nonexpert segmented height cases="+tall);
            }
            finally{Main.netMode=network;Main.dedServ=server;for(int x=35;x<70;x++)for(int y=39;y<60;y++){Main.tile[x,y].active(false);Main.tile[x,y].liquid=0;}for(int x=35;x<70;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=1;}}
        }
        // Fixture placement follows the native forward-probe choice; the
        // expectation itself executes ORIGINAL AI_003, never this test list.
        private static bool Wide(int t){switch(t){case 109:case 163:case 164:case 199:case 236:case 239:case 257:case 258:case 290:case 391:case 425:case 427:case 426:case 580:case 508:case 415:case 530:case 532:case 582:return true;default:return false;}}
        // Declared ordinary door scenarios: other families' A2 can be an
        // independent action clock (415 decrements it even without a door).
        private static bool DoorScene(int t){return t==3 || t==21 || t==27 || t==31 || t>=294 && t<=296 || t==77 || t==104 || t==196 || t==389 || t>=186 && t<=189 || t>=269 && t<=280 || t==331 || t==332;}
    }
}
