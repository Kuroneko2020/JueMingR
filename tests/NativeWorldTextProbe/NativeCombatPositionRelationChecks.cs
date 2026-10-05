using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPositionRelationChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static void Aim(object context,object host,NPC child)
        {
            Main.screenPosition=child.position-new Vector2(500,300);PlayerInput.CacheOriginalScreenDimensions();var input=Get(context,"Input");
            Call(input,"BeginUpdate");PlayerInput.MouseInfo=new MouseState(500+child.width/2,300+child.height/2,0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
            Call(input,"AfterNativeMouse",new List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(host,"SampleMouse");
        }
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var read=source.GetType().GetMethod("Read",Flags);
            for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};Main.dayTime=false;Main.LocalPlayer.position=new Vector2(800,900);
            int cases=0;
            foreach(int type in new[]{384,396})foreach(int ownerSlot in new[]{1,3})foreach(int vx in new[]{-2,2})
            {
                foreach(var n in Main.npc)n.active=false;var owner=Main.npc[ownerSlot];owner.SetDefaults(type==384?383:398);owner.whoAmI=ownerSlot;owner.active=true;owner.position=new Vector2(900,800);owner.velocity=new Vector2(vx,0);owner.noTileCollide=owner.noGravity=true;
                var child=Main.npc[2];child.SetDefaults(type);child.whoAmI=2;child.active=true;child.position=owner.Center-new Vector2(child.width/2f,child.height/2f)+(type==396?new Vector2(0,-400):Vector2.Zero);child.velocity=new Vector2(17,4);child.dontTakeDamage=child.immortal=false;child.ai[type==384?0:3]=ownerSlot;child.target=Main.myPlayer;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,radius:0));Aim(context,host,child);
                // Establish the child through actual selection while its
                // parent is absent. Restoring the parent then preserves the
                // real prior child on the native equal-distance tie rule.
                owner.active=false;NativeCombatObservationChecks.Fresh(context,host);Require(ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,child),"Prior child identity is established by real selection.");owner.active=true;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);
                Console.WriteLine("RELATION GATE type="+type+" selected="+((NpcIdentity)Get(Get(host,"Selection"),"Target")).Type+" count="+(path?.Count.ToString()??"null")+" stop="+(path?.Stop.ToString()??"null")+" receives="+Get(host,"TargetPredictionFailed")+" childLife="+child.life);
                Require(path!=null && path.Count>1 && (type!=396 || path.Count==121) && path.Identity.Slot==2 && ReferenceEquals(path.Identity.Token,child),"Actual selected child future before native action; ground owner may honestly shorten: "+type);
                var relationStep=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcPositionMotion").GetMethod("Step",Flags);
                var childArgs=new object[]{child,read.Invoke(null,new object[]{child,Get(host,"Session")}),Get(host,"Session")};source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation").GetMethod("Capture",Flags).Invoke(null,childArgs);var childModel=(NpcMotionState)childArgs[1];
                int savedMode=Main.netMode;Main.netMode=1;
                try
                {
                    for(int future=1;future<=120;future++)
                    {
                        if(ownerSlot<2)owner.position+=owner.velocity;
                        if(type==384)
                        {
                            bool paused=Main.gamePaused;Main.gamePaused=true;try{child.AI();}finally{Main.gamePaused=paused;}var group=new[]{(NpcMotionState)read.Invoke(null,new object[]{owner,Get(host,"Session")})};var args=new object[]{childModel,group,1,future,default(PredictionEnvironment),PredictionStop.None};Require((bool)relationStep.Invoke(null,args),"Current complete scalar owner permits exact assignment.");childModel=(NpcMotionState)args[0];
                        }
                        else{typeof(NPC).GetMethod("AI_079_MoonLordHead",Flags).Invoke(child,null);child.position+=child.velocity;}
                        if(ownerSlot>2)owner.position+=owner.velocity;
                        if(future==15 || future==30 || future==60 || future==120)
                        {var predicted=type==384?new Vector2(childModel.X,childModel.Y):new Vector2(path[future].Bounds.X,path[future].Bounds.Y);float error=Vector2.Distance(child.position,predicted);Require(error<.015f,"Native direct assignment without child velocity carry: "+type+" / "+future);Console.WriteLine("RELATION FROZEN type="+type+" ownerSlot="+ownerSlot+" vx="+vx+" future="+future+" error="+error+" scope="+(type==384?"assignment-only/observed-owner":"Source-future/observed-owner"));cases++;}
                    }
                }
                finally{Main.netMode=savedMode;}
                owner.active=false;NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null,"Inactive direct position owner ends future.");
            }
            // Parent phase/velocity controllers are qualified trends, not
            // exact parent translations. Check actual relationship capture,
            // default consumer and retirement across every direct member.
            foreach(int type in new[]{36,128,129,130,131,397,401})
            {
                foreach(var n in Main.npc)n.active=false;var owner=Main.npc[1];owner.SetDefaults(type==36?35:type<=131?127:type==397?398:396);owner.whoAmI=1;owner.active=true;owner.position=new Vector2(900,1000);owner.velocity=new Vector2(2,0);owner.noGravity=owner.noTileCollide=true;
                var child=Main.npc[2];child.SetDefaults(type);child.whoAmI=2;child.active=true;child.position=owner.Center+new Vector2(500,-100);child.dontTakeDamage=child.immortal=false;child.velocity=new Vector2(1,0);child.target=Main.myPlayer;
                if(type==397){child.ai[3]=1;child.ai[2]=1;}else if(type==401)child.ai[0]=2;else child.ai[1]=1;
                var sampleArgs=new object[]{child,read.Invoke(null,new object[]{child,Get(host,"Session")}),Get(host,"Session")};source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation").GetMethod("Capture",Flags).Invoke(null,sampleArgs);var sampled=(NpcMotionState)sampleArgs[1];Require(ReferenceEquals(sampled.PositionOwner.Token,owner) && sampled.PositionRelation==(type==397?4:type==401?5:6),"Direct relationship is not generic realLife: "+type);
                Aim(context,host,child);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,child),"Actual default selection retains relationship child: "+type);
                if(type==401)Require(path==null,"Unknown necessary projectile future honestly refuses leech interpolation.");
                else Require(path!=null && path.Count>1 && path[1].Bounds.X!=path[0].Bounds.X+2,"Parent velocity must not hard-attach controller: "+type);
                owner.active=false;NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null,"Missing controller parent retires its current future: "+type);cases++;
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());Console.WriteLine("PASS DIRECT RELATIONS original exact center assignments and complete qualified parent consumer/lifecycle cases="+cases);
        }
    }
}
