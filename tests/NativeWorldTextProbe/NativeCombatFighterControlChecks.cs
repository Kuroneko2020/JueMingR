using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatFighterControlChecks
    {
        internal static void Clocks(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int cases=0;
            foreach(int type in new[]{110,381,520,411,426,430,471})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,960-n.height);n.velocity=new Vector2(1,0);n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=n.spriteDirection=1;n.timeLeft=750;n.ai[1]=type==411?150:10;n.ai[2]=type==471?58:3;n.ai[3]=type==471?1:70;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"AI3 shared known cooldown/preparation real Source type="+type+" stop="+Get(source,"outcomeStop"));Native(n);Require(Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[1].Vx)<.01f,"AI3 shared cooldown/prepare type="+type+" native="+n.position+" V="+n.velocity+" model="+path[1].Bounds.X+","+path[1].Bounds.Y+" V="+path[1].Vx+","+path[1].Vy);cases++;
            }
            var prior=Main.player[1];var other=new Player{whoAmI=1,active=true,position=new Vector2(1400,p.position.Y),width=p.width,height=p.height,carpetFrame=-1,tankPet=-1};Main.player[1]=other;
            try
            {
                foreach(int type in new[]{110,411,426})foreach(int branch in new[]{0,1,2,3})
                {
                    other.dead=branch==0;foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,960-n.height);n.velocity=new Vector2(1,type==426?-1:0);n.oldVelocity=n.velocity;n.target=branch==0?1:p.whoAmI;n.direction=n.spriteDirection=-1;n.ai[1]=type==411?150:10;n.ai[2]=3;n.ai[3]=branch==3?30:70;n.justHit=branch==1;n.confused=branch==2;if(n.confused){n.buffType[0]=Terraria.ID.BuffID.Confused;n.buffTime[0]=200;}n.timeLeft=750;
                    // 411's no-face interval with blocked>=60 retains its old
                    // role; use a live old role there, rather than inventing a
                    // replacement which the original does not perform.
                    if(type==411 && branch==0){other.dead=false;n.target=1;}
                    NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"AI3 known clock state has actual role type="+type+" branch="+branch);int prepared=(int)Get(source,"targetPlayer");Native(n);Require(prepared==n.target && Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f,"AI3 clock target/order/JustHit/confused/positive counter type="+type+" branch="+branch+" prepared="+prepared+" actual="+n.target+" native="+n.position+" model="+path[1].Bounds.X+","+path[1].Bounds.Y);
                }
                Console.WriteLine("PASS AI3 clock first actual role/JustHit/confused/positive counter=12");
            }
            finally{Main.player[1]=prior;}
            Console.WriteLine("PASS AI3 shared cooldown/prepare first="+cases);
            foreach(int type in new[]{110,381,520,411,430,471})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,960-n.height);n.velocity=Vector2.UnitX;n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=n.spriteDirection=1;n.timeLeft=750;n.ai[1]=type==411?242:3;n.ai[2]=type==471?58:type==430?18:3;n.ai[3]=type==471?1:0;cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>3,"AI3 future known clock transitions type="+type);
                bool transition=false;
                for(int step=1;step<path.Count;step++){float previous=n.ai[2],clock=n.ai[1];Native(n);transition|=previous!=n.ai[2] || n.ai[1]>clock || type==411 && clock>=240 && n.ai[1]<240;Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f,"AI3 frozen shared phase type="+type+" step="+step+" native="+n.position+" V="+n.velocity+" AI="+n.ai[1]+","+n.ai[2]+","+n.ai[3]+" model="+path[step].Bounds.X+","+path[step].Bounds.Y+" V="+path[step].Vx+","+path[step].Vy);}
                Require(transition,"AI3 known phase really transitioned type="+type);if(type==471)Require(path.Count==33 && path.Stop==PredictionStop.UnsupportedMechanism,"AI471 actual preparation reaches90 before separate flight boundary");Console.WriteLine("PASS AI3 frozen clock transition type="+type+" actions="+(path.Count-1)+" stop="+path.Stop);
            }
        }
        internal static void Form(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int mode=Main.GameMode,net=Main.netMode;int cases=0;
            try
            {
                for(int i=0;i<Main.dust.Length;i++)if(Main.dust[i]==null)Main.dust[i]=new Dust();
                foreach(int difficulty in new[]{0,1,2})foreach(int network in new[]{0,1})foreach(bool blocked in new[]{false,true})
                {
                    Main.GameMode=difficulty;Main.netMode=network;foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(427);n.whoAmI=2;n.active=true;n.position=new Vector2(650,700);n.velocity=new Vector2(.5f,-1);n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=n.spriteDirection=1;n.ai[2]=1;n.localAI[0]=1198;n.timeLeft=750;int cellX=(int)n.Center.X/16-2,cellY=(int)n.Center.Y/16-3;Main.tile[cellX,cellY].active(blocked);Main.tile[cellX,cellY].type=Terraria.ID.TileID.Stone;
                    cache.Demand(0,1,12);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>3,"AI427 before-conversion Source prefix");bool changed=false;var sampled=(NpcMotionState)source.GetType().GetMethod("Read",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{n,0L});
                    for(int step=1;step<=3;step++){int prior=n.type;float bottom=n.Bottom.Y;Native(n);changed|=n.type!=prior;Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f && n.width==path[step].Bounds.Width && n.height==path[step].Bounds.Height,"AI427 form mode="+difficulty+" net="+network+" blocked="+blocked+" step="+step+" nativeType="+n.type+" native="+n.position+" size="+n.Size+" model="+path[step].Bounds.X+","+path[step].Bounds.Y+" size="+path[step].Bounds.Width+","+path[step].Bounds.Height);if(prior!=n.type)Console.WriteLine("DIAG AI427 mode="+difficulty+" life="+n.lifeMax+" captured="+sampled.FighterFormLifeMax+" bottomExactDelta="+(n.Bottom.Y-(bottom+n.velocity.Y)).ToString("R"));if(prior!=n.type)Require(n.type==426 && n.life==n.lifeMax && n.lifeMax==sampled.FighterFormLifeMax && path[step].NewSegment && ReferenceEquals(path.Identity.Token,n) && Math.Abs(n.Bottom.Y-bottom-n.velocity.Y)<.0001f,"AI427 full health/Bottom/same-origin identity conversion action life="+n.life+"/"+n.lifeMax+" segment="+path[step].NewSegment+" token="+ReferenceEquals(path.Identity.Token,n)+" bottom="+n.Bottom.Y+" expected="+(bottom+n.velocity.Y));}
                    Require(changed==(network==0 && !blocked),"AI427 server/client SolidTiles form gate");cases++;Main.tile[cellX,cellY].active(false);
                }
                Console.WriteLine("PASS AI427 finite same-object form mode0/1/2 net0/1 empty/solid cases="+cases);
                Main.GameMode=0;Main.netMode=0;foreach(var npc in Main.npc)npc.active=false;var confused=Main.npc[2]=new NPC();confused.SetDefaults(427);confused.whoAmI=2;confused.position=new Vector2(1000,700);confused.velocity=new Vector2(.5f,-1);confused.target=p.whoAmI;confused.direction=1;confused.localAI[0]=1199;confused.ai[3]=70;confused.confused=true;confused.buffType[0]=Terraria.ID.BuffID.Confused;confused.buffTime[0]=200;NativeCombatObservationChecks.Fresh(context,host);var first=cache.Read(0);Require(first!=null && first.Count>1,"AI427 confused before-transform actual role prefix");int prepared=(int)Get(source,"targetPlayer");Native(confused);Require(confused.type==426 && !confused.confused && confused.direction==-1 && prepared==confused.target && confused.buffTime[0]==200 && Math.Abs(confused.position.X-first[1].Bounds.X)<.01f && Math.Abs(confused.position.Y-first[1].Bounds.Y)<.01f,"AI427 SetDefaults clears this action confused facing but retains actual buff slots");
                var missing=Main.npc[2]=new NPC();missing.SetDefaults(427);missing.whoAmI=2;missing.position=new Vector2(650,700);missing.velocity=Vector2.UnitX;missing.target=p.whoAmI;missing.direction=1;missing.localAI[0]=1199;var tile=Main.tile[39,41];Main.tile[39,41]=null;try{NativeCombatObservationChecks.Fresh(context,host);var stopped=cache.Read(0);Require(stopped==null || stopped.Count<=1,"AI427 missing necessary SolidTiles fact cannot publish guessed transformation");}finally{Main.tile[39,41]=tile;}
                Console.WriteLine("PASS AI427 confused reset/buff retained and necessary transform terrain refusal");
            }
            finally{Main.GameMode=mode;Main.netMode=net;for(int x=39;x<44;x++)for(int y=41;y<46;y++)Main.tile[x,y].active(false);}
        }
        internal static void FacingPit(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;var position=p.position;int petSlot=p.tankPet;int cases=0;
            try
            {
                p.aggro=0;p.itemAnimation=0;
                foreach(int branch in new[]{0,1,2})
                {
                    foreach(var npc in Main.npc)npc.active=false;for(int x=43;x<=44;x++)Main.tile[x,60].active(false);for(int x=56;x<=58;x++){Main.tile[x,60].active(branch!=1);Main.tile[x,61].active(branch==1);Main.tile[x,61].type=Terraria.ID.TileID.Stone;}
                    p.position=new Vector2(900,(branch==1?976:960)-p.height);p.velocity=Vector2.Zero;p.tankPet=-1;var pet=Main.projectile[0];pet.active=false;
                    if(branch==2){pet.SetDefaults(625);pet.whoAmI=0;pet.owner=p.whoAmI;pet.active=true;pet.width=pet.height=20;pet.position=new Vector2(720,929);pet.velocity=Vector2.Zero;p.tankPet=0;}
                    var n=Main.npc[2]=new NPC();n.SetDefaults(426);n.whoAmI=2;n.position=new Vector2(636,960-n.height);n.velocity=Vector2.UnitX;n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=n.spriteDirection=1;n.directionY=1;n.ai[1]=10;n.ai[2]=3;n.timeLeft=750;NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"AI426 actual Source before same-bottom pit action");Native(n);Console.WriteLine("PIT branch="+branch+" nativeDirY="+n.directionY+" nativeV="+n.velocity+" modelV="+path[1].Vx+","+path[1].Vy);
                    Require((n.velocity.Y<-7)==(branch==0),"AI426 native same-bottom pit jump vs lower-player/guardian center controls");Require(Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.Y-path[1].Vy)<.01f,"AI3 shared Face preserves Bottom player/Center guardian before pit branch="+branch);cases++;
                }
                Console.WriteLine("PASS AI426 actual Source numbered-player bottom pit positive/lower negative/guardian-center cases="+cases);
            }
            finally{p.position=position;p.tankPet=petSlot;Main.projectile[0].active=false;for(int x=43;x<=44;x++)Main.tile[x,60].active(true);for(int x=56;x<=58;x++){Main.tile[x,60].active(true);Main.tile[x,61].active(false);}}
        }
        internal static void FormFacing(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;var position=p.position;int aggro=p.aggro,animation=p.itemAnimation,mode=Main.netMode;Main.netMode=0;
            try
            {
                foreach(int branch in new[]{0,1,2})
                {
                    foreach(var npc in Main.npc)npc.active=false;p.position=new Vector2(850,693);p.velocity=Vector2.Zero;p.aggro=branch==2?0:-400;p.itemAnimation=branch==1?10:0;var n=Main.npc[2]=new NPC();n.SetDefaults(427);n.whoAmI=2;n.position=new Vector2(1000,700);n.velocity=new Vector2(.5f,-1);n.target=p.whoAmI;n.direction=n.spriteDirection=1;n.directionY=1;n.localAI[0]=1199;n.ai[2]=1;n.timeLeft=750;NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>3,"AI427 actual negative-aggro transform source window");
                    for(int step=1;step<=3;step++){Native(n);Console.WriteLine("FORMFACE branch="+branch+" step="+step+" type="+n.type+" dir="+n.direction+" nativeV="+n.velocity+" modelV="+path[step].Vx+","+path[step].Vy);Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[step].Vx)<.01f,"AI427 real ClearTarget255 permits conversion facing and subsequent426 offset branch="+branch+" step="+step);}
                    Require(n.type==426 && n.direction==-1,"AI427 converter faces left from old right even idle negative aggro");
                }
                Console.WriteLine("PASS AI427 ClearTarget255 conversion/next2 finite actions idle-negative/active-negative/zero-aggro");
            }
            finally{p.position=position;p.aggro=aggro;p.itemAnimation=animation;Main.netMode=mode;}
        }
        private static void Native(NPC n)
        {
            object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;n.oldPosition=n.position;if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");n.justHit=false;
        }
        internal static void Premise(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int hook=p.grappling[0],count=p.grapCount;p.grappling[0]=0;p.grapCount=0;
            try
            {
                foreach(int type in new[]{461,586})
                {
                    foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=Vector2.UnitX;n.target=p.whoAmI;n.direction=1;n.alpha=0;n.wet=true;n.wetCount=1;n.timeLeft=750;for(int y=35;y<60;y++){Main.tile[50,y].active(true);Main.tile[50,y].type=Terraria.ID.TileID.Stone;}for(int x=39;x<46;x++)for(int y=42;y<49;y++){Main.tile[x,y].active(false);Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}Require(!Collision.CanHit(n.position,n.width,n.height,p.Center,1,1),"AI3 actual Stone wall obstructs native wet LOS");cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Console.WriteLine("WET PREMISE type="+type+" player="+p.Center+" nativeLOS="+Collision.CanHit(n.position,n.width,n.height,p.Center,1,1)+" count="+(path==null?-1:path.Count)+" stop="+Get(source,"outcomeStop")+" layer="+Get(source,"outcomeLayer"));Require(path!=null && path.Count>1,"AI3 known blocked wet patrol retains prefix without unavailable player future type="+type);
                    for(int step=1;step<path.Count;step++){object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f,"AI3 known noLOS wet own-vector prefix type="+type+" step="+step);}
                    Console.WriteLine("PASS AI3 blocked wet own-vector prefix type="+type+" actions="+(path.Count-1)+" stop="+path.Stop);
                    for(int y=35;y<60;y++)Main.tile[50,y].active(false);n.position=new Vector2(650,700);n.velocity=Vector2.UnitX;n.wet=true;n.ai[3]=0;NativeCombatObservationChecks.Fresh(context,host);path=cache.Read(0);Require(path==null || path.Count<=1,"AI3 real wet LOS chase cannot waive unavailable player future type="+type);
                    for(int x=39;x<46;x++)for(int y=42;y<49;y++)Main.tile[x,y].liquid=0;
                }
                foreach(var npc in Main.npc)npc.active=false;var reveal=Main.npc[2];reveal.SetDefaults(466);reveal.whoAmI=2;reveal.active=true;reveal.dontTakeDamage=reveal.immortal=reveal.friendly=false;reveal.position=new Vector2(650,700);reveal.velocity=Vector2.Zero;reveal.target=p.whoAmI;reveal.direction=1;reveal.alpha=200;reveal.ai[2]=-16;reveal.timeLeft=750;NativeCombatObservationChecks.Fresh(context,host);var prefix=cache.Read(0);Require(prefix!=null && prefix.Count==17 && prefix.Stop==PredictionStop.PhaseBoundary,"AI466 known sixteen reveal actions precede real common target dependency");Console.WriteLine("PASS AI466 reveal16 independent prefix then common-control PhaseBoundary; wet true chase dependency retained");
            }
            finally{p.grappling[0]=hook;p.grapCount=count;for(int y=35;y<60;y++)Main.tile[50,y].active(false);for(int x=39;x<46;x++)for(int y=42;y<49;y++)Main.tile[x,y].liquid=0;}
        }
        internal static void MovingVisibility(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;var saved=p.position;var velocity=p.velocity;bool left=p.controlLeft,right=p.controlRight;
            try
            {
                for(int y=35;y<=54;y++){Main.tile[50,y].active(true);Main.tile[50,y].type=Terraria.ID.TileID.Stone;}for(int x=30;x<=60;x++)for(int y=5;y<=56;y++){if(x!=50)Main.tile[x,y].active(false);Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(461);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=Vector2.UnitX;n.target=p.whoAmI;n.direction=1;n.wet=true;n.wetCount=1;n.timeLeft=750;p.position=new Vector2(900,960-p.height);p.velocity=new Vector2(-3,0);p.controlLeft=true;p.controlRight=false;Require(!Collision.CanHit(n.position,n.width,n.height,p.Center,1,1),"AI461 before-action actual moving player initially blocked");cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>30 && (path.Assumptions&PredictionAssumption.HeldPlayerControls)!=0 && (path.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)==0,"AI461 reliable moving player visibility uses original point0 timeline");bool clear=false;float max=0;
                for(int step=1;step<path.Count;step++){p.HorizontalMovement();p.velocity=Collision.TileCollision(p.position,p.velocity,p.width,p.height);p.position+=p.velocity;clear|=Collision.CanHit(n.position,n.width,n.height,p.Center,1,1);Native(n);max=Math.Max(max,Vector2.Distance(n.position,new Vector2(path[step].Bounds.X,path[step].Bounds.Y)));}
                Require(clear && max<.3f,"AI461 actual held player emerges from occlusion and chase anticipates target max="+max+" clear="+clear);Console.WriteLine("PASS AI461 reliable held moving player blocked→chase frozen actions="+(path.Count-1)+" max="+max);
            }
            finally{p.position=saved;p.velocity=velocity;p.controlLeft=left;p.controlRight=right;for(int y=35;y<=54;y++)Main.tile[50,y].active(false);for(int x=30;x<=60;x++)for(int y=5;y<=56;y++)Main.tile[x,y].liquid=0;}
        }
        internal static void Flight(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int cases=0;
            for(int i=0;i<Main.dust.Length;i++)if(Main.dust[i]==null)Main.dust[i]=new Dust();
            foreach(int type in new[]{425,427,426})foreach(int side in new[]{-1,1})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(side>0?650:1100,700);n.velocity=new Vector2(-side*.5f,-1);n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=side;n.directionY=1;n.ai[2]=1;n.ai[3]=-120;n.localAI[3]=1;n.timeLeft=750;NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && ReferenceEquals(path.Identity.Token,n),"AI3 flight real Source finite prefix type="+type);
                object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[1].Vx)<.01f && Math.Abs(n.velocity.Y-path[1].Vy)<.01f,"AI3 distinct425/427 flight native control type="+type+" side="+side+" native="+n.position+" V="+n.velocity+" model="+path[1].Bounds.X+","+path[1].Bounds.Y+" V="+path[1].Vx+","+path[1].Vy);cases++;
            }
            Console.WriteLine("PASS AI3 distinct425/427 flight first="+cases);
            var prior=Main.player[1];var remote=new Player{whoAmI=1,active=true,dead=true,position=new Vector2(1400,p.position.Y),width=p.width,height=p.height,carpetFrame=-1,tankPet=-1};Main.player[1]=remote;
            try
            {
                foreach(int type in new[]{425,427})
                {
                    foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(-.5f,-1);n.oldVelocity=n.velocity;n.target=1;n.direction=-1;n.ai[2]=1;n.ai[3]=70;n.localAI[3]=1;n.timeLeft=750;NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && (int)Get(source,"targetPlayer")==p.whoAmI,"AI3 flight preflight selects actual new numbered role type="+type);Native(n);Require(n.target==p.whoAmI && Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f,"AI3 actual old-dead→flight-target step type="+type);
                }
            }
            finally{Main.player[1]=prior;}
            ushort[] floor=new ushort[Main.maxTilesX];for(int x=1;x<Main.maxTilesX-1;x++){floor[x]=Main.tile[x,60].type;Main.tile[x,60].type=Terraria.ID.TileID.Platforms;}for(int x=38;x<=46;x++){Main.tile[x,75].active(true);Main.tile[x,75].type=Terraria.ID.TileID.Stone;}
            try
            {
                foreach(int type in new[]{425,427})foreach(bool launch in new[]{false,true})
                {
                    foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,(launch?1200:960)-n.height);n.velocity=new Vector2(.5f,0);n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=n.spriteDirection=1;n.ai[2]=1;n.ai[3]=-120;n.localAI[3]=1;n.timeLeft=750;Require(Collision.CanHit(n.position,n.width,n.height,p.position,p.width,p.height),"AI3 actual platform permits elevated-player launch LOS");NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"AI3 actual grounded launch or landing finite prefix type="+type);Native(n);Require(n.ai[2]==(launch?1:0) && (n.velocity.Y<0)==launch && Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f,"AI3 landed A2 reset / elevated-player true takeoff type="+type+" launch="+launch+" native="+n.position+" V="+n.velocity+" model="+path[1].Bounds.X+","+path[1].Bounds.Y);
                }
                Console.WriteLine("PASS AI3 flight old-dead actual role2 + landing/elevated platform takeoff4");
            }
            finally{for(int x=1;x<Main.maxTilesX-1;x++)Main.tile[x,60].type=floor[x];for(int x=38;x<=46;x++)Main.tile[x,75].active(false);}
            foreach(int type in new[]{425,427})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(-.5f,-1);n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=1;n.ai[2]=1;n.ai[3]=-120;n.localAI[3]=1;n.timeLeft=750;cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>20,"AI3 distinct flight anticipates bounded vector and landing type="+type);
                for(int step=1;step<path.Count;step++){Native(n);Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f,"AI3 frozen distinct flight type="+type+" step="+step+" native="+n.position+" V="+n.velocity+" model="+path[step].Bounds.X+","+path[step].Bounds.Y+" V="+path[step].Vx+","+path[step].Vy);}
                Console.WriteLine("PASS AI3 frozen distinct flight type="+type+" actions="+(path.Count-1)+" stop="+path.Stop);
            }
        }
        internal static void Entry(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;
            for(int i=0;i<Main.dust.Length;i++)if(Main.dust[i]==null)Main.dust[i]=new Dust();int cases=0;
            foreach(int type in new[]{466,461,586})foreach(int branch in new[]{0,1,2})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(.5f,0);n.target=p.whoAmI;n.direction=1;n.directionY=1;n.timeLeft=750;n.alpha=type==586 && branch==0?255:200;n.ai[2]=type==466?(branch==0?-1:branch==1?-16:0):0;n.ai[3]=branch==2?-.10101f:0;n.wet=type!=466 && branch==1;n.wetCount=(byte)(n.wet?1:0);n.oldVelocity=n.velocity;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && ReferenceEquals(path.Identity.Token,n),"AI3 finite entry real Source type="+type+" branch="+branch);
                object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[1].Vx)<.01f && Math.Abs(n.velocity.Y-path[1].Vy)<.01f && n.width==path[1].Bounds.Width && n.height==path[1].Bounds.Height,"AI3 finite birth/reveal/wet-dry control type="+type+" branch="+branch+" native="+n.position+" V="+n.velocity+" model="+path[1].Bounds.X+","+path[1].Bounds.Y+" V="+path[1].Vx+","+path[1].Vy);cases++;
            }
            Console.WriteLine("PASS AI3 466 reveal,461 resize/wet/dry,586 birth/wet/dry first="+cases);
            foreach(int type in new[]{466,461,586})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(.5f,0);n.target=p.whoAmI;n.direction=1;n.directionY=1;n.timeLeft=750;n.alpha=type==586?255:200;n.ai[2]=type==466?-16:0;n.wet=type==461;n.wetCount=(byte)(n.wet?1:0);n.oldVelocity=n.velocity;cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>20,"AI3 known entry transition has ahead finite prefix type="+type);
                for(int step=1;step<path.Count;step++)
                {
                    object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");n.justHit=false;
                    Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f && n.width==path[step].Bounds.Width && n.height==path[step].Bounds.Height,"AI3 frozen reveal/birth/wet-dry and continuing common control type="+type+" step="+step+" native="+n.position+" V="+n.velocity+" model="+path[step].Bounds.X+","+path[step].Bounds.Y+" V="+path[step].Vx+","+path[step].Vy);
                }
                Console.WriteLine("PASS AI3 frozen finite entry transition type="+type+" actions="+(path.Count-1)+" stop="+path.Stop);
            }
        }
    }
}
