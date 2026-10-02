using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;
using JueMingR.Features.Combat;

namespace NativeWorldTextProbe
{
    // Continuous original updates and an ordinary Session/worker. The only
    // hooks here observe successful strikes and actual mailbox consumption;
    // no fabricated reply, delayed parent, or replacement history is used.
    internal static class NativeCombatModeledImpactChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static object owner;
        private static Projectile source;
        private static readonly List<long> hits=new List<long>();
        private static readonly List<string> replies=new List<string>(),hitRows=new List<string>();
        private static int received,accepted,matchedHits,retiredHits;
        private static void Source(Projectile __instance,out Projectile __state){__state=source;source=__instance;}
        private static void EndSource(Projectile __state){source=__state;}
        private static void Strike(NPC __instance,int __result)
        {
            if(__result<=0 || source==null)return;
            hits.Add(Main.GameUpdateCount);
            hitRows.Add(Csv(Main.GameUpdateCount,source.whoAmI,source.type,(uint)source.key,source.owner,__instance.whoAmI,__instance.type,__result,__instance.life));
        }
        private static void Receiving(object __instance,object response,out object[] __state)
        {__state=ReferenceEquals(owner,__instance)?new[]{Get(owner,"pending"),Get(response,"Result")}:null;}
        private static void Received(object __instance,long tick,object[] __state)
        {
            if(__state==null || __state[0]==null)return;
            received++;var request=__state[0];long capture=(long)Get(request,"Tick");
            bool hadHit=hits.Any(t=>t>capture && t<=tick),ok=ReferenceEquals(request,Get(owner,"acceptedRequest"));
            if(ok){accepted++;if(hadHit)matchedHits++;}else if(hadHit && (bool)Get(request,"Retired"))retiredHits++;
            var frames=(Array)Get(__state[1],"Frames");
            replies.Add(Csv(capture,tick,tick-capture,frames?.Length,hadHit,Get(request,"Retired"),Get(request,"Impact"),ok,Get(owner,"Reason"),Get(__state[1],"Error")));
        }
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output,bool otherSource=false)
        {
            owner=native;hits.Clear();replies.Clear();hitRows.Clear();received=accepted=matchedHits=retiredHits=0;
            replies.Add("capture,arrive,age,frames,crossesHit,retired,impact,accepted,reason,workerError");
            hitRows.Add("tick,sourceSlot,sourceType,sourceKey,sourceOwner,targetSlot,targetType,damage,life");
            var rows=new List<string>{"phase,frame,tick,published,capture,count,pending,accepted,failed,reason"};
            var hooks=new Harmony("JueMingR.Tests.ModeledImpact");bool hard=Main.hardMode;
            object worker=Get(native,"Worker");int workerId=((System.Diagnostics.Process)Get(worker,"child")).Id;
            try
            {
                hooks.Patch(typeof(Projectile).GetMethod("Damage_PVE_Inner",Flags),prefix:Hook(nameof(Source)),finalizer:Hook(nameof(EndSource)));
                hooks.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags),postfix:Hook(nameof(Strike)));
                hooks.Patch(native.GetType().GetMethod("Receive",Flags),prefix:Hook(nameof(Receiving)),postfix:Hook(nameof(Received)));
                NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.hardMode=true;
                Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();
                Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
                var player=Main.LocalPlayer;player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
                player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;player.dead=false;
                player.wet=player.honeyWet=player.lavaWet=player.shimmerWet=false;player.fallStart=player.fallStart2=150;
                player.statLife=player.statLifeMax=player.statLifeMax2=500;player.immune=true;player.immuneTime=100000;
                Array.Clear(player.hurtCooldowns,0,player.hurtCooldowns.Length);Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);
                for(int i=0;i<10;i++)player.armor[i].TurnToAir();
                typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)},{"UpdateProjectiles",new UnifiedRandom(171)}});
                int target=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2400,otherSource?3:110,Start:16,Target:Main.myPlayer);
                var npc=Main.npc[target];npc.life=npc.lifeMax=100000;
                int otherSlot=-1;
                if(otherSource)
                {
                    int slot=Projectile.NewProjectile(new Terraria.DataStructures.EntitySource_DebugCommand(),npc.Center-new Vector2(120,0),Vector2.Zero,119,20,0,Main.myPlayer);
                    otherSlot=slot;
                    var shot=Main.projectile[slot];shot.aiStyle=0;shot.tileCollide=false;shot.timeLeft=10000;shot.penetrate=-1;
                }
                else for(int i=0;i<8;i++){var town=new NPC();town.SetDefaults(678);town.whoAmI=i;town.active=true;town.position=new Vector2(3000+i*30,2400-town.height);Main.npc[i]=town;}
                var host=Get(context,"CombatObservation");
                // Prediction-only proves source collection does not depend on
                // enabling the separate collision-display consumer.
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));
                Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
                long requests=(long)Get(native,"Requests"),rejected=(long)Get(native,"Rejected"),refused=(long)Get(native,"Refused");
                int allShown=0,attackShown=0,longest=0,blank=0;
                int duration=otherSource?600:1080;
                for(int frame=0;frame<duration;frame++)
                {
                    if(!otherSource && frame==360)for(int i=0;i<3;i++)player.armor[i].SetDefaults(3381+i);
                    if(!otherSource && frame==720)for(int i=0;i<3;i++)player.armor[i].TurnToAir();
                    SampleMouse(context,npc.Center);step();
                    // This axis starts from an already captured source page;
                    // friendly-projectile discovery is a separate contract.
                    if(otherSource)((SortedSet<int>)Get(native,"projectiles")).Add(otherSlot);
                    var path=cache.Read(0);
                    bool shown=path!=null;
                    if(shown)
                    {
                        Require(path.SampleTick==Main.GameUpdateCount && path.Count==121 && ReferenceEquals(path.Identity.Token,npc),"Modeled impact publishes true current+120 for the current instance.");
                        allShown++;blank=0;
                    }
                    else longest=Math.Max(longest,++blank);
                    if(hits.Count>0 && Main.GameUpdateCount>=hits[0] && frame<480 && shown)attackShown++;
                    Require(ReferenceEquals(worker,Get(native,"Worker")) && ((System.Diagnostics.Process)Get(worker,"child")).Id==workerId && !(bool)Get(native,"Failed"),"One healthy worker spans all attack and recovery updates.");
                    rows.Add(Csv(frame<360?"open":frame<720?"guardian":"return",frame%360,Main.GameUpdateCount,shown,path?.CaptureTick,path?.Count,Tick(Get(native,"pending")),Tick(Get(native,"acceptedRequest")),Get(native,"Failed"),Get(native,"Reason")));
                }
                Console.WriteLine("MODELED-IMPACT source="+(otherSource?"119":"623")+" requests="+((long)Get(native,"Requests")-requests)+" received="+received+" accepted="+accepted+" rejected="+((long)Get(native,"Rejected")-rejected)+" refused="+((long)Get(native,"Refused")-refused)+" published="+allShown+"/"+duration+" longest="+longest+" hits="+hits.Count+" hit-crossing-accepted="+matchedHits+" hit-crossing-retired="+retiredHits+" attack-shown="+attackShown+" worker="+workerId);
                Require(otherSource?hits.Count>0:hits.Count>=15 && hits.Zip(hits.Skip(1),(a,b)=>b-a).Count(d=>d==5)>=14,"The original configured source really attacks its target.");
                Require(matchedHits>0 && retiredHits==0 && attackShown>0,"Accurately modeled pending hits reach real acceptance and publication without unconditional impact retirement.");
            }
            finally
            {
                hooks.UnpatchAll(hooks.Id);Main.hardMode=hard;owner=null;source=null;
                string prefix=otherSource?"other-impact":"modeled-impact";
                File.WriteAllLines(Path.Combine(output,prefix+"-updates.csv"),rows);File.WriteAllLines(Path.Combine(output,prefix+"-replies.csv"),replies);File.WriteAllLines(Path.Combine(output,prefix+"-hits.csv"),hitRows);
            }
        }
        private static HarmonyMethod Hook(string name)=>new HarmonyMethod(typeof(NativeCombatModeledImpactChecks).GetMethod(name,Flags));
        private static object Get(object value,string name){if(value==null)return null;var field=value.GetType().GetField(name,Flags);return field!=null?field.GetValue(value):value.GetType().GetProperty(name,Flags).GetValue(value);}
        private static object Tick(object value)=>Get(value,"Tick");
        private static string Csv(params object[] values)=>string.Join(",",values.Select(value=>"\""+Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\""));
        private static void Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Flags).Invoke(value,args);
        internal static void SampleMouse(object context,Vector2 point)
        {
            Main.screenPosition=point-new Vector2(Main.screenWidth/2,Main.screenHeight/2);var input=Get(context,"Input");Call(input,"BeginUpdate");
            Terraria.GameInput.PlayerInput.MouseInfo=new Microsoft.Xna.Framework.Input.MouseState(Main.screenWidth/2,Main.screenHeight/2,0,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released);
            Call(input,"AfterNativeMouse",new List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(Get(context,"CombatObservation"),"SampleMouse");
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
