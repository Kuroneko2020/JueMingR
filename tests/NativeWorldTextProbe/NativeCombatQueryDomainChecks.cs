using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Collections;
using Microsoft.Xna.Framework;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;
using HarmonyLib;
using Terraria;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatQueryDomainChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Private(Assembly host,object sandbox,string output)
        {
            var method=typeof(NPC).GetMethod("AI_007_TownEntities",Flags);
            var code=PatchProcessor.GetOriginalInstructions(method);
            using(var sha=SHA256.Create())Console.WriteLine("DANGER IL "+BitConverter.ToString(sha.ComputeHash(method.GetMethodBody().GetILAsByteArray())).Replace("-","")+" instructions="+code.Count);
            File.WriteAllLines(Path.Combine(output,"danger-il.txt"),code.Select((c,i)=>i+" "+c));
            Console.WriteLine("PASS inspection of authenticated private original method");
            Console.WriteLine("LAYOUT EH="+method.GetMethodBody().ExceptionHandlingClauses.Count+" op1935="+code[1935].opcode+" op1918="+code[1918].opcode+" value="+code[1918].operand+" accessor="+code[1934].Calls(typeof(NPC).GetMethod("__JmrField_67109777_0",Flags)));
            if(Environment.GetEnvironmentVariable("JUEMINGR_QUERY_DOMAIN_PRIVATE")=="inspect")return;
            var directory=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEntityDirectory",true);
            var danger=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeNpcDangerQuery");
            File.WriteAllLines(Path.Combine(output,"danger-current-il.txt"),PatchProcessor.GetCurrentInstructions(method).Select((c,i)=>i+" "+c));
            int protocol=(int)host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetField("Protocol",Flags).GetRawConstantValue();
            if(danger!=null)Permissions(host,directory,danger);
            foreach(int tick in new[]{1139,1136,1131})
            {
                byte[] request=File.ReadAllBytes(Path.Combine(Program.Repository,".local/aim-tiered-window-20261004/n/native/sample-"+tick+"-request.bin"));
                Buffer.BlockCopy(BitConverter.GetBytes(protocol),0,request,0,4);File.WriteAllBytes(Path.Combine(output,"source-"+tick+"-request.bin"),request);
                Exception refused=null;try{sandbox.GetType().GetMethod("Predict",Flags).Invoke(sandbox,new object[]{request,true});}catch(TargetInvocationException e){refused=e.InnerException;}
                Require(refused is InvalidDataException,"Original request must still refuse the unknown page.");
                File.WriteAllText(Path.Combine(output,"source-"+tick+"-refusal.txt"),refused.ToString());
                object source=danger?.GetProperty("Missing",Flags).GetValue(null);
                int tag=source==null?0:Convert.ToInt32(source.GetType().GetField("Kind",Flags).GetValue(source));
                Console.WriteLine("SOURCE check tag="+tag+" bound="+(danger?.GetField("read",Flags).GetValue(null)!=null)+" caller0-full="+(danger==null?null:directory.GetMethod("IsCapturedNpc",Flags).Invoke(null,new object[]{Main.npc[0]})));
                Require(tag==(tick==1139?1:0),"Only the real first danger stinky refusal receives the typed source; tick="+tick+" actual="+tag);
                if(tick==1139)
                {
                    Require((int)directory.GetProperty("MissingField",Flags).GetValue(null)==67109777 && (int)directory.GetProperty("MissingSlot",Flags).GetValue(null)==1,"Exact native stinky field and slot unchanged.");
                    Require((int)source.GetType().GetField("CallerSlot",Flags).GetValue(source)==0,"The actual caller is full background 0, not selected 64.");
                }
                Console.WriteLine("PASS original refusal source tick="+tick+" tag="+tag+" field="+directory.GetProperty("MissingField",Flags).GetValue(null));
            }
        }
        private static void Permissions(Assembly host,Type directory,Type danger)
        {
            NativeCombatEligibilityChecks.Run(host);
            Require(Convert.ToInt32(danger.GetProperty("Missing",Flags).GetValue(null).GetType().GetField("Kind",Flags).GetValue(danger.GetProperty("Missing",Flags).GetValue(null)))==0,"Other native queries/damage/byrefs/writes/allocation cannot mint a danger source.");
            // A same-field read outside the native site remains a refusal.
            var fixture=typeof(NativeCombatEligibilityChecks);
            var candidate=(NPC)fixture.GetMethod("Scene",Flags).Invoke(null,null);fixture.GetMethod("Observe",Flags).Invoke(null,null);
            var read=typeof(NPC).GetMethod("__JmrField_67109777_0",Flags);bool failed=false;
            try{read.Invoke(null,new object[]{candidate});}catch(TargetInvocationException e){failed=e.InnerException is InvalidDataException;}
            Require(failed && Convert.ToInt32(danger.GetProperty("Missing",Flags).GetValue(null).GetType().GetField("Kind",Flags).GetValue(danger.GetProperty("Missing",Flags).GetValue(null)))==0,"Unrelated stinky read is not the authenticated site.");
            directory.GetMethod("KnowNpc",Flags).Invoke(null,new object[]{3});
            foreach(bool value in new[]{false,true}){candidate.stinky=value;Require((bool)danger.GetMethod("Read",Flags).Invoke(null,new object[]{candidate,candidate})==value,"Original full stinky value is preserved.");}
            directory.GetMethod("Reset",Flags).Invoke(null,null);
            Console.WriteLine("PASS exact source / real nonselected full caller / Damage_PVE / external stinky / original booleans / permissions");
        }
        private static object owner;private static NativeCombatAttackTrace observer;private static bool checkedSource,capturedFresh;private static long freshTick;private static int freshLife;
        private static readonly List<string> cases=new List<string>();
        internal static void Live(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            owner=native;checkedSource=capturedFresh=false;freshTick=-1;cases.Clear();cases.Add("case,outcome");
            var host=Get(context,"CombatObservation");NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.hardMode=true;
            Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
            var player=Main.LocalPlayer;player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;player.fallStart=player.fallStart2=(int)(player.position.Y/16);
            player.statLife=player.statLifeMax=player.statLifeMax2=100000;player.immune=true;player.immuneTime=100000;player.controlLeft=player.controlRight=player.controlJump=false;
            int target=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2400,176,Start:64,Target:Main.myPlayer);
            for(int i=0;i<32;i++){var town=new NPC();town.SetDefaults(678);town.whoAmI=i;town.active=true;town.position=new Vector2(3000+i*30,2400-town.height);Main.npc[i]=town;}
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
            var hooks=new Harmony("JueMingR.Tests.DangerDomain");var type=native.GetType();
            hooks.Patch(type.GetMethod("Receive",Flags),prefix:new HarmonyMethod(typeof(NativeCombatQueryDomainChecks),nameof(Receiving)));
            hooks.Patch(type.GetMethod("Capture",Flags),prefix:new HarmonyMethod(typeof(NativeCombatQueryDomainChecks),nameof(Capturing)),postfix:new HarmonyMethod(typeof(NativeCombatQueryDomainChecks),nameof(Captured)));
            using(observer=new NativeCombatAttackTrace(native,output))
            try
            {
                observer.Phase="domain-boundaries";observer.Selected=target;
                for(int frame=0;frame<180 && !capturedFresh;frame++){observer.Frame=frame;NativeCombatModeledImpactChecks.SampleMouse(context,Main.npc[target].Center);step();Require(observer.Fault==null,"Actual Host Prepare must not fault.");}
                Require(checkedSource && capturedFresh,"A real authenticated worker refusal must trigger a fresh ordinary Capture.");
                // Let the actual promoted request finish, retaining its raw
                // packet and full History through the usual observer.
                for(int i=0;i<60 && Get(native,"pending")!=null && (long)Tick(Get(native,"pending"))==freshTick;i++){observer.Frame++;NativeCombatModeledImpactChecks.SampleMouse(context,Main.npc[target].Center);step();}
                Console.WriteLine("PASS actual Host Receive, once-only fresh Capture and "+(cases.Count-1)+" negative/lifecycle checks; fresh="+freshTick+" life="+freshLife);
            }
            finally{hooks.UnpatchAll(hooks.Id);File.WriteAllLines(Path.Combine(output,"domain-boundaries.csv"),cases);File.WriteAllText(Path.Combine(output,"fresh-capture.txt"),freshTick+","+freshLife);owner=null;}
        }
        private static void Receiving(object __instance,object response,long tick)
        {
            if(!ReferenceEquals(owner,__instance) || checkedSource)return;
            var result=Get(response,"Result");var source=Get(result,"DangerSource");if(Convert.ToInt32(Get(source,"Kind"))!=1)return;
            var request=Get(owner,"pending");if((bool)Get(request,"Retired"))return;
            int caller=(int)Get(source,"CallerSlot");var callerNpc=Main.npc[caller];int member=(int)Get(result,"Slot");var actor=Main.npc[member];
            Require(caller!=((NpcIdentity)Get(request,"Identity")).Slot,"Exercise a real nonselected full caller.");
            Func<object> receive=()=>
            {
                var shadow=Activator.CreateInstance(owner.GetType(),Flags,null,new[]{Get(owner,"launch"),(object)new NpcPredictionCache()},null);
                Set(shadow,"current",Get(owner,"current"));Set(shadow,"pending",request);owner.GetType().GetProperty("Worker",Flags).SetValue(shadow,Get(owner,"Worker"));
                ((SortedSet<int>)Get(shadow,"npcs")).UnionWith((int[])Get(request,"Npcs"));
                Call(shadow,"Receive",response,tick);return shadow;
            };
            Action<string,Action,Action> denied=(name,mutate,restore)=>{try{mutate();Require(Get(receive(),"dangerTicket")==null,name);}finally{restore();}cases.Add(Csv(name,"PASS"));};
            denied("member same-slot same-type replacement",()=>{var n=new NPC();n.SetDefaults(actor.type);n.whoAmI=member;n.active=true;Main.npc[member]=n;},()=>Main.npc[member]=actor);
            var generation=typeof(NPC).GetField("<generation>k__BackingField",Flags);byte gen=actor.generation;
            denied("member generation",()=>generation.SetValue(actor,unchecked((byte)(gen+1))),()=>generation.SetValue(actor,gen));
            denied("caller same-slot same-type replacement",()=>{var n=new NPC();n.SetDefaults(callerNpc.type);n.whoAmI=caller;n.active=true;Main.npc[caller]=n;},()=>Main.npc[caller]=callerNpc);
            int life=actor.life,net=actor.netID,type=actor.type;
            denied("member death while still active",()=>actor.life=0,()=>actor.life=life);
            denied("member inactive",()=>actor.active=false,()=>actor.active=true);
            denied("member netID",()=>actor.netID++,()=>actor.netID=net);
            denied("member type",()=>actor.type=1,()=>actor.type=type);
            foreach(string field in new[]{"Retired","LifecycleInvalid","DangerInvalid"})denied(field,()=>Set(request,field,true),()=>Set(request,field,false));
            foreach(string field in new[]{"Impact","QueryChanged"})denied(field,()=>Set(request,field,1),()=>Set(request,field,0));
            var good=receive();var ticket=Get(good,"dangerTicket");Require(ticket!=null && ReferenceEquals(Get(ticket,"Source"),request),"Ticket uses the actual originating Request.");
            var members=((IEnumerable)Get(ticket,"Members")).Cast<object>().ToArray();var queries=((IEnumerable)Get(request,"Queries")).Cast<object>().ToArray();
            Require(members.Length==31 && members.All(m=>queries.Any(q=>ReferenceEquals(q,m))) && members.All(m=>Main.npc[(int)Get(m,"Slot")].type!=690),"Only source Queries' proven partial subset enters the ticket.");cases.Add(Csv("actual Request subset 31 and real caller","PASS"));
            Call(good,"ConsumeDanger",Get(owner,"current"),tick);Require(Get(good,"dangerTicket")==null && ((SortedSet<int>)Get(good,"npcs")).Count==33,"Consume once into full pages.");
            ((SortedSet<int>)Get(good,"npcs")).Clear();Call(good,"ConsumeDanger",Get(owner,"current"),tick);Require(((SortedSet<int>)Get(good,"npcs")).Count==0,"Cannot reuse a consumed ticket.");cases.Add(Csv("once only","PASS"));
            foreach(string boundary in new[]{"death-before-consume","replacement-before-consume","query-change-before-consume","caller-reset-before-consume","Hurt-clear","target-clear","OFF-clear","DetachWorld","Stop"})
            {
                var shadow=receive();Require(Get(shadow,"dangerTicket")!=null,"Valid ticket before negative boundary.");
                try
                {
                    if(boundary=="death-before-consume")actor.life=0;
                    else if(boundary=="replacement-before-consume")Main.npc[member]=new NPC{whoAmI=member,type=actor.type,netID=actor.netID,active=true,life=actor.life};
                    else if(boundary=="query-change-before-consume")Set(request,"QueryChanged",1);
                    else if(boundary=="caller-reset-before-consume")Call(shadow,"ObserveNpcReset",callerNpc);
                    else {owner.GetType().GetProperty("Worker",Flags).SetValue(shadow,null);Call(shadow,boundary=="DetachWorld" || boundary=="Stop"?boundary:"ClearTarget");}
                    var pages=(SortedSet<int>)Get(shadow,"npcs");pages.Clear();Call(shadow,"ConsumeDanger",Get(owner,"current"),tick);Require(pages.Count==0 && Get(shadow,"dangerTicket")==null,boundary);
                }
                finally{Main.npc[member]=actor;actor.life=life;Set(request,"QueryChanged",0);Set(request,"DangerInvalid",false);}
                cases.Add(Csv(boundary,"PASS"));
            }
            checkedSource=true;
        }
        private static void Capturing(object __instance,long tick)
        {
            if(!ReferenceEquals(owner,__instance) || !checkedSource || freshTick>=0 || Get(owner,"dangerTicket")==null)return;
            // A positive life change must be freshly sampled, not invalidate
            // the identity ticket or recover values from its source Request.
            Main.npc[1].life--;freshLife=Main.npc[1].life;freshTick=tick;observer.RequiredCapture=tick;
        }
        private static void Captured(object __instance)
        {
            if(!ReferenceEquals(owner,__instance) || freshTick<0 || capturedFresh)return;var request=Get(owner,"pending");if(request==null || (long)Tick(request)!=freshTick)return;
            Require(Get(owner,"dangerTicket")==null && ((int[])Get(request,"Npcs")).Length==33 && ((int[])Get(request,"Npcs")).Contains(1),"Real next Capture consumes the subset into original full pages.");
            Require(((IList)Get(request,"History")).Count==1 && Main.npc[1].life==freshLife,"New request owns fresh sample-zero history.");capturedFresh=true;cases.Add(Csv("actual fresh Capture 33 full pages","PASS"));
        }
        private static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Flags).Invoke(value,args);
        private static void Set(object value,string name,object data)=>value.GetType().GetField(name,Flags).SetValue(value,data);
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
