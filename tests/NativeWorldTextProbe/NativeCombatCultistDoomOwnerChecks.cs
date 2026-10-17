using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Necessary selected AI101 owner lifetime, not a replay of AI84's matrix.
    // Actual full-world slots retire the owner; no native AI forecast obtains
    // that future for the model, and no unknown foreign body is frozen alive.
    internal static class NativeCombatCultistDoomOwnerChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly HashSet<NPC> outlets=new HashSet<NPC>();
        private static bool Network(int msgType,int number)
        {return (msgType!=23 && msgType!=28) || number<0 || number>=Main.npc.Length || !outlets.Contains(Main.npc[number]);}
        private static void Setup(object context)
        {typeof(NativeCombatCultistBossChecks).GetMethod("Setup",Flags).Invoke(null,new object[]{context,0});outlets.Clear();}
        private static NPC Birth(int type,int start,Vector2 position,int owner=-1)
        {
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),(int)position.X,(int)position.Y,type,Start:start,ai0:owner<0?0:owner,Target:Main.myPlayer);
            Require(slot>=0 && slot<Main.maxNPCs,"original Cultist/Doom birth allocates an actual slot");
            var n=Main.npc[slot];n.position=position;outlets.Add(n);
            if(type==439){n.localAI[0]=1;n.ai[0]=2;n.ai[1]=10;}
            return n;
        }
        private static NpcIdentity Identity(object context,NPC n)
        {
            var type=Get(Get(context,"CombatObservation"),"Prediction").GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true);
            return (NpcIdentity)type.GetMethod("Identity",Flags).Invoke(null,new object[]{n,(long)Get(Get(context,"CombatObservation"),"Session")});
        }
        private static object Owner(object context)
        {var type=Get(Get(context,"CombatObservation"),"Prediction").GetType();return Activator.CreateInstance(type,Flags,null,new object[]{null,false},CultureInfo.InvariantCulture);}
        private static NpcTrajectory Prepare(object context,object owner,NPC child,out NpcTrajectory accepted)
        {
            accepted=(NpcTrajectory)owner.GetType().GetMethod("TryCandidate",Flags).Invoke(owner,new object[]{Identity(context,child),(long)Main.GameUpdateCount,false,120});
            var source=Get(owner,"candidateSource");return (NpcTrajectory)Get(Get(source,"Cache"),"result");
        }
        private static void Roles(object owner,NPC child,NPC body)
        {
            var source=Get(owner,"candidateSource");int count=(int)Get(source,"observedCount");var states=(NpcMotionState[])Get(source,"states");var roles=(bool[])Get(source,"motionRoles");
            bool parent=false,selected=false;
            for(int i=0;i<count;i++)
            {
                if(ReferenceEquals(states[i].Identity.Token,body))parent=roles[i] && states[i].CultistRelationsCaptured;
                if(ReferenceEquals(states[i].Identity.Token,child))selected=roles[i] && states[i].PositionRelation==9 && states[i].PositionOwner.Equals(IdentityFrom(states,body));
            }
            Require(parent && selected,"selected Doom's full-identity439 is a real motor role, not a health-only Active snapshot");
        }
        private static NpcIdentity IdentityFrom(NpcMotionState[] rows,NPC n)
        {foreach(var row in rows)if(ReferenceEquals(row.Identity.Token,n))return row.Identity;return default(NpcIdentity);}
        internal static void Run(object context,Action step,string output)
        {
            var observer=new Harmony("JueMingR.Probe.CultistDoomOwner");var network=new HarmonyMethod(typeof(NativeCombatCultistDoomOwnerChecks),nameof(Network)){priority=Priority.First+100};
            var oldArray=Main.player;var oldPlayers=(Player[])oldArray.Clone();var oldActive=new bool[oldPlayers.Length];for(int i=0;i<oldPlayers.Length;i++)oldActive[i]=oldPlayers[i]!=null && oldPlayers[i].active;
            int mode=Main.netMode,game=Main.GameMode;bool day=Main.dayTime,good=Main.getGoodWorld;
            var records=new List<string>{"case,lowerOwner,alreadyRetired,frame,bodyActive,childActive,childClock,rawCount,rawStop,acceptedCount,freshCount"};int cases=0,retired=0,fresh=0;
            observer.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:network);
            try
            {
                foreach(bool lower in new[]{true,false})foreach(bool priorRetirement in new[]{false,true})
                {
                    Setup(context);
                    // Within the original6400 world. Player is at the native
                    // left border, body at6332center: a real >5600 exit, while
                    // selected523 is nearby and independently receivable.
                    var p=Main.player[Main.myPlayer];p.position.X=640;p.ResetFallDamage();
                    var body=Birth(439,lower?20:30,new Vector2(6320,2100));
                    var child=Birth(523,lower?30:20,new Vector2(820,2200),body.whoAmI);
                    Require(lower?body.whoAmI<child.whoAmI:child.whoAmI<body.whoAmI,"actual allocated NPC slots preserve the tested lifetime order");
                    Require(Vector2.Distance(body.Center,p.Center)>5600 && child.active && !child.dontTakeDamage && !child.friendly && !child.immortal,"actual far-owner predicate and independently selected Doom qualification");
                    if(priorRetirement)
                    {
                        // This is an actual observation transition, not a
                        // forecast. Original AI84 produces the inactive owner;
                        // child has not acted yet when the source samples it.
                        body.UpdateNPC(body.whoAmI);Require(!body.active,"original AI84 really retires the observed owner before this sample");
                    }
                    var owner=Owner(context);
                    try
                    {
                        NpcTrajectory accepted;var path=Prepare(context,owner,child,out accepted);
                        Require(path!=null && path.Stop==PredictionStop.Despawn,"raw selected Doom result ends at real dependency retirement; no read-minimum padding");
                        int expected=priorRetirement || lower?1:2;
                        Require(path.Count==expected,"earlier owner retires child this action; later owner preserves one actual child action before retirement");
                        if(!priorRetirement)Roles(owner,child,body);
                        int stopFrame=expected;
                        for(int frame=1;frame<=stopFrame;frame++)
                        {
                            step();
                            if(frame<stopFrame)
                            {
                                Require(child.active && Vector2.Distance(new Vector2(path[frame].Bounds.X,path[frame].Bounds.Y),child.position)<=.08,"later owner leaves a genuine same-frame child prefix");
                                Require(child.ai[1]==1,"original child's integer clock advances exactly once before later-slot owner exit");
                            }
                            else Require(!child.active && !body.active,"actual slot-ordered owner exit causes selected523 retirement at the predicted tail");
                            records.Add(string.Join(",",cases,lower,priorRetirement,frame,body.active,child.active,child.ai[1],path.Count,path.Stop,accepted?.Count??0,0));
                        }
                        retired++;
                        // Genuine new identities, same source/cache owner.
                        // Observed cast body is safely within5600 and will not
                        // query global Doom counts inside this120-action slice.
                        var nextBody=Birth(439,80,new Vector2(1000,2100));
                        var nextChild=Birth(523,90,new Vector2(820,2200),nextBody.whoAmI);
                        Require(!Identity(context,nextChild).Equals(path.Identity),"new actual birth is not the retired child generation");
                        var restored=Prepare(context,owner,nextChild,out accepted);
                        Roles(owner,nextChild,nextBody);
                        Require(accepted!=null && restored!=null && restored.Count>30 && restored.Identity.Equals(Identity(context,nextChild)),"fresh full source identity restores a useful selected Doom prefix after local retirement");
                        step();Require(nextChild.active && nextBody.active && Vector2.Distance(new Vector2(restored[1].Bounds.X,restored[1].Bounds.Y),nextChild.position)<=.08,"fresh recovery's first original child action agrees");
                        records.Add(string.Join(",",cases,lower,priorRetirement,stopFrame+1,nextBody.active,nextChild.active,nextChild.ai[1],restored.Count,restored.Stop,accepted.Count,restored.Count));fresh++;cases++;
                        Console.WriteLine("CULTIST DOOM owner lower="+lower+" alreadyRetired="+priorRetirement+" nativeTail="+stopFrame+" rawCount="+path.Count+" stop="+path.Stop+" fresh="+restored.Count);
                    }
                    finally{owner.GetType().GetMethod("Clear",Flags).Invoke(owner,null);}
                }
                Require(cases==4 && retired==4 && fresh==4,"both actual slot orders / already-inactive observations withdraw and restore the selected child");
                Console.WriteLine("PASS Cultist selected Doom owner cases="+cases+" originalRetire="+retired+" fresh="+fresh+" foreignCountCompletenessUnchanged=True");
            }
            finally
            {
                observer.UnpatchAll(observer.Id);outlets.Clear();File.WriteAllLines(Path.Combine(output,"cultist-doom-owner.csv"),records);
                Main.netMode=mode;Main.GameMode=game;Main.dayTime=day;Main.getGoodWorld=good;
                Main.player=oldArray;Array.Copy(oldPlayers,oldArray,oldPlayers.Length);for(int i=0;i<oldPlayers.Length;i++)if(oldPlayers[i]!=null)oldPlayers[i].active=oldActive[i];
                NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
            }
        }
    }
}
