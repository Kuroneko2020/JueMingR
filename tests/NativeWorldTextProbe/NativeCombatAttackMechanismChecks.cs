using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace NativeWorldTextProbe
{
    // Investigation only. The original parent keeps advancing while the real
    // Session/worker learns pages. No response, acceptance or update is faked.
    internal static class NativeCombatAttackMechanismChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            var host=Get(context,"CombatObservation");var selection=Get(host,"Selection");
            var worker=Get(native,"Worker");var child=(System.Diagnostics.Process)Get(worker,"child");int workerId=child.Id;
            if(!(bool)native.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionPipeProtocol",true).GetField("Measure",Flags).GetValue(null))throw new InvalidOperationException("Attack evidence requires real measurement records.");
            string scene=Environment.GetEnvironmentVariable("JUEMINGR_ATTACK_CONTEXT")??"plain";
            int[] types=(Environment.GetEnvironmentVariable("JUEMINGR_ATTACK_TYPES")??"110").Split(',').Select(int.Parse).ToArray();
            string[] states=(Environment.GetEnvironmentVariable("JUEMINGR_ATTACK_PLAYER")??"normal,immune").Split(',');
            int frames=int.Parse(Environment.GetEnvironmentVariable("JUEMINGR_ATTACK_FRAMES")??"480",CultureInfo.InvariantCulture);
            if(frames<60 || frames>1800 || types.Length>24)throw new InvalidOperationException("Bounded attack investigation required.");
            var rows=new List<string>{"type,player,phase,frame,tick,target,selected,published,capture,count,ai0,ai1,ai2,ai3,canHit,canHitHookCallsTrue,canHitHookCallsFalse,npcX,npcY,npcLife,playerX,playerY,playerLife,immune,immuneTime,projectiles,fullNpcs,fullProjectiles,pending,accepted,failed,reason,workerId,context,npcTarget,localAI,guardian,selectedRngBefore,selectedRngAfter,dead,targetActive"};
            var summaries=new List<string>{"type,player,phase,updates,selected,published,longestBlank,requests,received,refused,rejected,firstPublished,lastPublished"};
            NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true,path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));
            Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
            bool finished=false,partial=false;
            using(var trace=new NativeCombatAttackTrace(native,output))
            try
            {
                foreach(int type in types)foreach(string state in states)
                {
                    trace.Phase="setup-"+type+"-"+state+"-"+scene;trace.Frame=-1;
                    NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.hardMode=true;
                    Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();
                    Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
                    var player=Main.LocalPlayer;player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
                    player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;player.dead=false;
                    player.wet=player.honeyWet=player.lavaWet=player.shimmerWet=false;player.fallStart=player.fallStart2=(int)(player.position.Y/16);
                    player.statLife=player.statLifeMax=player.statLifeMax2=500;
                    player.immune=state=="immune" || state=="short";player.immuneTime=state=="immune"?100000:state=="short"?20:0;
                    Array.Clear(player.hurtCooldowns,0,player.hurtCooldowns.Length);
                    Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);
                    for(int i=0;i<10;i++)player.armor[i].TurnToAir();
                    typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)},{"UpdateProjectiles",new UnifiedRandom(171)}});
                    int target=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2400,type,Start:16,Target:Main.myPlayer);
                    var n=Main.npc[target];n.life=n.lifeMax=100000;
                    if(n.noGravity)n.position=new Vector2(type==250?1100:1400,2140);
                    if(scene=="town")for(int i=0;i<8;i++){var town=new NPC();town.SetDefaults(678);town.whoAmI=i;town.active=true;town.position=new Vector2(3000+i*30,2400-town.height);Main.npc[i]=town;}
                    else if(scene=="bunny")NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),300,2400,46);
                    else if(scene!="plain")throw new InvalidOperationException("Unknown bounded background context.");
                    string[] phases=(Environment.GetEnvironmentVariable("JUEMINGR_ATTACK_PHASES")??"open,wall,reopen").Split(',');
                    var walls=new List<Point>();
                    foreach(string phase in phases)
                    {
                        foreach(var cell in walls)Main.tile[cell.X,cell.Y].active(false);walls.Clear();
                        if(phase=="wall")
                        {
                            // A legal empty room with roof and sides blocks
                            // airborne and ground LOS; never place in a player.
                            int cx=(int)(player.Center.X/16),top=Math.Min(146,(int)(player.position.Y/16)-3);
                            for(int x=cx-6;x<=cx+6;x++)walls.Add(new Point(x,top));
                            for(int y=top+1;y<150;y++){walls.Add(new Point(cx-6,y));walls.Add(new Point(cx+6,y));}
                            foreach(var cell in walls){if(Main.tile[cell.X,cell.Y].active())throw new InvalidOperationException("Barrier requires empty cells.");Main.tile[cell.X,cell.Y].active(true);Main.tile[cell.X,cell.Y].type=1;}
                        }
                        bool guardian=phase.StartsWith("guardian",StringComparison.Ordinal);
                        for(int i=0;i<3;i++){if(guardian)player.armor[i].SetDefaults(3381+i);else player.armor[i].TurnToAir();}
                        trace.Phase=type+"-"+state+"-"+scene+"-"+phase;trace.Selected=target;
                        long requests=(long)Get(native,"Requests"),rejected=(long)Get(native,"Rejected"),refused=(long)Get(native,"Refused"),received=trace.Received;
                        int shown=0,selectedCount=0,blank=0,longest=0,first=-1,last=-1,updates=0;bool ended=false;
                        for(int frame=0;frame<frames;frame++)
                        {
                            trace.Frame=frame;trace.CanHitTrue=trace.CanHitFalse=0;
                            SampleMouse(context,n.Center);step();
                            updates++;
                            if(!ReferenceEquals(worker,Get(native,"Worker")) || !ReferenceEquals(child,Get(worker,"child")) || child.Id!=workerId)throw new InvalidOperationException("Worker changed; do not claim continuous worker evidence.");
                            if(trace.Fault!=null)throw new InvalidOperationException("Observed original Prepare exception; this window is invalid.",trace.Fault);
                            if((bool)Get(host,"pathFailed"))throw new InvalidOperationException("Host path failed outside native outcome; stop this fixture.");
                            bool selected=(bool)Get(selection,"HasTarget") && ((NpcIdentity)Get(selection,"Target")).Slot==target;
                            if(selected)selectedCount++;
                            var path=cache.Read(0);bool published=path!=null && path.Identity.Slot==target;
                            if(published)
                            {
                                if(path.SampleTick!=Main.GameUpdateCount || path.Count<121 || !ReferenceEquals(path.Identity.Token,n))throw new InvalidOperationException("Investigation saw invalid publication identity/current+120.");
                                shown++;blank=0;if(first<0)first=frame;last=frame;
                            }
                            else longest=Math.Max(longest,++blank);
                            bool canHit=Collision.CanHit(n.position,n.width,n.height,player.position,player.width,player.height);
                            rows.Add(Csv(type,state,phase,frame,Main.GameUpdateCount,target,selected,published,path?.CaptureTick,path?.Count,n.ai[0],n.ai[1],n.ai[2],n.ai[3],canHit,trace.CanHitTrue,trace.CanHitFalse,n.position.X,n.position.Y,n.life,player.position.X,player.position.Y,player.statLife,player.immune,player.immuneTime,Main.projectile.Count(p=>p.active),Get(Get(native,"npcs"),"Count"),Get(Get(native,"projectiles"),"Count"),Tick(Get(native,"pending")),Tick(Get(native,"acceptedRequest")),Get(native,"Failed"),Get(native,"Reason"),workerId,scene,n.target,string.Join("|",n.localAI),string.Join(";",Main.projectile.Where(p=>p.active && p.type==623).Select(p=>p.whoAmI+":"+p.ai[0]+":"+p.ai[1]+":"+p.localAI[0])),trace.SelectedRngBefore,trace.SelectedRngAfter,player.dead,n.active));
                            if((bool)Get(native,"Failed"))throw new InvalidOperationException("Real Session Failed: "+Get(native,"Reason"));
                            if(player.dead || !n.active){partial=ended=true;Console.WriteLine("ATTACK partial scene: dead="+player.dead+" target-active="+n.active);break;}
                        }
                        summaries.Add(Csv(type,state,phase,updates,selectedCount,shown,longest,(long)Get(native,"Requests")-requests,trace.Received-received,(long)Get(native,"Refused")-refused,(long)Get(native,"Rejected")-rejected,first,last));
                        Console.WriteLine("ATTACK "+trace.Phase+" selected="+selectedCount+" published="+shown+"/"+updates+" longest="+longest+" reason="+Get(native,"Reason"));
                        if(ended)break;
                    }
                }
                finished=true;
            }
            finally
            {
                File.WriteAllLines(Path.Combine(output,"attack-updates.csv"),rows);
                File.WriteAllLines(Path.Combine(output,"attack-summary.csv"),summaries);
                string status=finished?(partial?"partial-scene-end":"configured-windows-completed"):"interrupted-or-invalid";
                File.WriteAllText(Path.Combine(output,"attack-status.txt"),status+" worker="+workerId+"; not a product correctness PASS");
                Console.WriteLine("ATTACK evidence written: "+status+"; no product correctness PASS is implied.");
            }
        }
        internal static object Get(object value,string name){if(value==null)return null;var f=value.GetType().GetField(name,Flags);return f!=null?f.GetValue(value):value.GetType().GetProperty(name,Flags)?.GetValue(value);}
        internal static object Tick(object request)=>Get(request,"Tick");
        internal static string Csv(params object[] values)=>string.Join(",",values.Select(v=>"\""+Convert.ToString(v,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\""));
        private static void Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Flags).Invoke(value,args);
        private static void SampleMouse(object context,Vector2 point)
        {
            Main.screenPosition=point-new Vector2(Main.screenWidth/2,Main.screenHeight/2);var input=Get(context,"Input");Call(input,"BeginUpdate");
            Terraria.GameInput.PlayerInput.MouseInfo=new Microsoft.Xna.Framework.Input.MouseState(Main.screenWidth/2,Main.screenHeight/2,0,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released);
            Call(input,"AfterNativeMouse",new List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(Get(context,"CombatObservation"),"SampleMouse");
        }
    }
}
