using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // A finite, isolated original-world scene, not a stationary NPC substitute.
    // The expected denominator is declared before Contact is read: a clear,
    // supplied gun against the same living ordinary slime inside 25 tiles.
    // A separate unassisted, explicitly aimed original run must produce native
    // births and damage. Missing candidate contacts never remove a frame.
    internal static class NativeCombatNaturalImpactChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Player player;
        private static NPC receiver;
        private static Vector2 physical;
        private static bool candidate;
        private static bool moveLeft,moveRight;
        private static int births,assistedBirths,hits;
        private static void MoveBefore(Player __instance)
        {
            if(!ReferenceEquals(__instance,player))return;
            // The existing Step samples Mouse1 before Player.Update. Supply
            // movement at that same native boundary, then let the original
            // ResetControls/CopyInto branch consume all three controls.
            PlayerInput.Triggers.Current.Left=moveLeft;PlayerInput.Triggers.Current.Right=moveRight;
        }
        private static void Born(Player __instance,Projectile __0)
        {
            if(!ReferenceEquals(__instance,player) || __0.owner!=player.whoAmI)return;
            births++;
            var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;
            bool changed=Vector2.DistanceSquared(cursor,physical)>4;
            if(changed)assistedBirths++;
            Require(candidate?changed:!changed,"natural slime native birth consumes the selected assisted/explicit original cursor");
        }
        private static void Hit(NPC __instance,int __result)
        {if(ReferenceEquals(__instance,receiver) && __result>0)hits++;}
        private static bool Red(Color p){return p.A>100 && p.R>190 && p.G<110 && p.B<110;}
        private static List<Vector2> RedPixels(Color[] pixels)
        {
            var result=new List<Vector2>(100);
            for(int i=0;i<pixels.Length;i++)if(Red(pixels[i]))result.Add(new Vector2(i%960,i/960));
            return result;
        }
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {RunCore(context,graphics,output,false,false);}
        internal static void RunReference(object context,ProbeGraphics graphics,string output)
        {RunCore(context,graphics,output,true,false);}
        internal static void RunCandidate(object context,ProbeGraphics graphics,string output)
        {RunCore(context,graphics,output,false,true);}
        private static void RunCore(object context,ProbeGraphics graphics,string output,bool referenceOnly,bool candidateOnly)
        {
            Require(graphics!=null,"natural slime red marker requires actual XNA pixels");
            Directory.CreateDirectory(output);
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");var world=Get(host,"World");var attack=Get(combat,"Attack");
            var audit=new Harmony("JueMingR.Tests.NaturalSlimeImpact");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeCombatNaturalImpactChecks).GetMethod(nameof(Born),Flags)));
            audit.Patch(AccessTools.Method(typeof(Player),"Update",new[]{typeof(int)}),prefix:new HarmonyMethod(typeof(NativeCombatNaturalImpactChecks).GetMethod(nameof(MoveBefore),Flags)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatNaturalImpactChecks).GetMethod(nameof(Hit),Flags)));
            bool originalFilm=Main.LocalPlayer.isControlledByFilm;
            var rows=new List<string>{"run,phase,frame,tick,expected,contact,visible,redPixels,pathCount,npcX,npcY,npcVx,npcVy,playerX,playerY,playerVx,playerVy,receiveTick,stop"};
            try
            {
                Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));Main.screenWidth=960;Main.screenHeight=640;Main.UIScale=1;
                foreach(bool moving in new[]{false,true})
                {
                    // Both runs receive the same initial original state,
                    // weapon, map and seed, with their own actual identities.
                    // Damage legitimately changes the
                    // attack window's future; no AI/receive state is restored
                    // during a run to make the candidate agree with reference.
                    foreach(bool assist in referenceOnly?new[]{false}:candidateOnly?new[]{true}:new[]{false,true})
                    {
                        candidate=assist;moveLeft=moveRight=false;NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                        player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,98,0,0);
                        NativeCombatLiveContextChecks.FlightWorld();
                        // A different world size is an actual isolated world
                        // replacement. Replace the WorldFileData token as well
                        // as its persistent ID; ItemSessionProbe compares the
                        // object, and footprints bind dimensions at session start.
                        Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(Path.Combine(Terraria.Program.SavePath,"natural-room.wld"),false){UniqueId=Guid.NewGuid()};
                        // Ordinary solid sidewalls bound the observation
                        // space. The first open-world reference naturally
                        // jumped beyond range (frame 260, native births 11,
                        // hits 2). This map keeps real AI/knockback/collision
                        // and free jump/landing, rather than freezing motion
                        // or filtering out candidate Contact failures.
                        // Reset's tiny-world floor 43 lies in vanilla's space
                        // gravity clamp (.075 instead of .3). Moving the map
                        // to tile 80 makes UpdateNPC_UpdateGravity naturally
                        // select ordinary surface gravity; no NPC field or
                        // physics/AI method is substituted.
                        for(int x=5;x<115;x++){Main.tile[x,43].ClearEverything();NativeToolsChecks.Tile(x,80,1);}
                        for(int y=55;y<80;y++){NativeToolsChecks.Tile(41,y,1);NativeToolsChecks.Tile(63,y,1);}
                        Console.WriteLine("NATURAL-MAP original Stone sidewalls x=41,63 y=55..79; Stone floor y=80/worldSurface=80, ordinary original height-derived gravity; same OFF/ON map; slime life=100000, player immunity for finite observation only");
                        player.position=new Vector2(700,1280-player.height);player.velocity=Vector2.Zero;player.isControlledByFilm=false;player.releaseUseItem=true;
                        player.statLife=player.statLifeMax=player.statLifeMax2=500;player.immune=true;player.immuneTime=100000;player.ResetFallDamage();
                        player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
                        for(int i=0;i<player.armor.Length;i++)player.armor[i].TurnToAir();Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);
                        player.inventory[54].SetDefaults(97);player.inventory[54].stack=999;
                        Main.screenPosition=new Vector2(480,1000);Main.GameViewMatrix.Zoom=Vector2.One;Main.rand=new Terraria.Utilities.UnifiedRandom(7193);Main.dayTime=false;Main.windPhysics=false;Main.worldSurface=80;
                        int target=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),930,1280,1,Start:2,Target:player.whoAmI);
                        Require(target>=0 && target<Main.maxNPCs,"original natural-spawn entry supplies the ordinary blue slime");receiver=Main.npc[target];
                        // Increased life only extends observation. Native AI,
                        // gravity, dimensions, knockback and damage gates stay
                        // intact. Long player immunity prevents scene death.
                        receiver.life=receiver.lifeMax=100000;
                        Require(receiver.aiStyle==1 && !receiver.noGravity,"blue slime retains its locked-original natural AI and gravity");
                        Call(context,"UpdateRuntime");
                        var footprints=Get(context,"Footprints");
                        var identity=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Map.LocalWorldIdentity",true).GetMethod("Observe",Flags).Invoke(null,null);
                        Require(identity!=null && (long)Get(footprints,"Session")>=0 && (int)Get(footprints,"width")==400 && (int)Get(footprints,"height")==240 && Equals(Get(footprints,"pair"),identity),"the real runtime observes the new isolated world and its matching footprint pair/dimensions");
                        Console.WriteLine("NATURAL-WORLD id="+Main.ActiveWorldFileData.UniqueId+" footprintPair="+identity+" footprintDimensions="+Get(footprints,"width")+"x"+Get(footprints,"height"));
                        // The idle candidate has Path ON and Aim OFF. A red
                        // preview is independent of permission to borrow input.
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,assist,false,false,false,25,assist,false));
                        Require(((ObservationSettings)Get(host,"Settings")).Value.Path==assist && !((ObservationSettings)Get(host,"Settings")).Value.Aim,"completed saved settings match the declared idle runtime demand");
                        births=assistedBirths=hits=0;int airborne=0,landings=0,normalGravitySteps=0,drawn=0,expected=0;float minPlayer=player.position.X,maxPlayer=minPlayer,priorVy=0;bool wasAirborne=false;
                        for(int frame=0;frame<300;frame++)
                        {
                            string phase=frame<180?"idle":frame<270?"attack":frame<274?"switch":"restore";
                            if(frame==180 && assist)NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,true));
                            if(frame==270)player.inventory[0].SetDefaults(3509);
                            if(frame==274)player.inventory[0].SetDefaults(98);
                            moveRight=moving && frame%80<40;moveLeft=moving && !moveRight;
                            physical=assist?player.Center+new Vector2(receiver.Center.X>=player.Center.X?-220:220,-60):receiver.Center;
                            NativeCombatSelectionPreviewChecks.Step(context,physical,phase=="attack" || phase=="restore",phase=="idle" || phase=="switch");
                            if(frame==12)
                            {
                                float gravity=(float)typeof(NPC).GetField("gravity",Flags).GetValue(null);
                                Require(Math.Abs(gravity-.3f)<.001f && Math.Abs(player.Bottom.Y-1280)<1 && Math.Abs(receiver.Bottom.Y-1280)<1,"original smoke: ordinary NPC gravity and player/NPC collision retain the real floor before the full window");
                                Console.WriteLine("NATURAL-SMOKE moving="+moving+" assist="+assist+" gravity="+gravity+" playerBottom="+player.Bottom.Y+" npcBottom="+receiver.Bottom.Y+" world="+Main.maxTilesX+"x"+Main.maxTilesY);
                            }
                            minPlayer=Math.Min(minPlayer,player.position.X);maxPlayer=Math.Max(maxPlayer,player.position.X);
                            bool flying=receiver.velocity.Y!=0;if(flying)airborne++;if(wasAirborne && !flying)landings++;
                            if(frame<180 && flying && wasAirborne && Math.Abs(receiver.velocity.Y-priorVy-.3f)<.001f)normalGravitySteps++;
                            wasAirborne=flying;priorVy=receiver.velocity.Y;
                            Require(receiver.active && receiver.type==1 && receiver.aiStyle==1 && receiver.life>0 && !receiver.dontTakeDamage && !receiver.immortal && !receiver.friendly,"declared natural receiver remains legal throughout the fixed window");
                            string state=" phase="+phase+" frame="+frame+" npc="+receiver.position+" velocity="+receiver.velocity+" player="+player.position+" playerVelocity="+player.velocity+" births="+births+" hits="+hits;
                            if(!assist)rows.Add(string.Join(",","original-"+(moving?"moving":"standing"),phase,frame,Main.GameUpdateCount,false,false,false,0,0,receiver.position.X,receiver.position.Y,receiver.velocity.X,receiver.velocity.Y,player.position.X,player.position.Y,player.velocity.X,player.velocity.Y,-1,"reference"));
                            Require(Vector2.Distance(player.Center,receiver.Center)<380,"declared open close-range window remains inside its independent 25-tile range"+state);
                            if(!assist)continue;
                            // The only excluded acquisition is the first two
                            // declared startup updates. Neither path nor
                            // Contact controls this expected-show denominator.
                            bool should=frame>=2 && phase!="switch";
                            var contact=(AttackContact)GetOptional(attack,"ExpectedPreview");var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
                            var pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                            var red=RedPixels(pixels);
                            bool visible=(bool)Get(Get(world,"Impact"),"Visible");
                            rows.Add(string.Join(",",moving?"moving":"standing",phase,frame,Main.GameUpdateCount,should,contact!=null,visible,red.Count,cache.Read(0)?.Count??0,receiver.position.X,receiver.position.Y,receiver.velocity.X,receiver.velocity.Y,player.position.X,player.position.Y,player.velocity.X,player.velocity.Y,contact?.ReceiveTick??-1,cache.Read(0)?.Stop.ToString()??"empty"));
                            if(should)
                            {
                                expected++;
                                Require(contact!=null && visible && red.Count>=16 && red.Count<=100,"every independently expected natural slime frame has a real visible red outline: "+phase+"/"+frame);
                                Require(ReferenceEquals(contact.Timeline,cache.Read(0)) && ReferenceEquals(contact.Timeline.Identity.Token,receiver),"natural red frame and selected path share the same live receiver/timeline");
                                var b=contact.Timeline[contact.ReceiveTick].Bounds;var center=Vector2.Transform(new Vector2(b.CenterX,b.CenterY)-Main.screenPosition,Main.GameViewMatrix.ZoomMatrix);
                                Require(red.All(v=>Math.Abs(v.X-center.X)<=6 && Math.Abs(v.Y-center.Y)<=6),"actual red pixels track the receive-time point on the same natural path");drawn++;
                            }
                            else if(phase=="switch")Require(contact==null && !visible && red.Count==0,"switching to a nonweapon removes the old precise marker");
                            if(frame==90 || frame==210 || frame==299)graphics.Image(Path.Combine(output,(moving?"moving":"standing")+"-"+phase+".png"),()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                        }
                        Console.WriteLine("NATURAL-IMPACT moving="+moving+" assist="+assist+" expected="+expected+" visible="+drawn+" airborne="+airborne+" landings="+landings+" normalGravitySteps="+normalGravitySteps+" playerDelta="+(maxPlayer-minPlayer)+" births="+births+" assistedBirths="+assistedBirths+" nativeHits="+hits);
                        Require(airborne>10 && landings>0 && normalGravitySteps>10,"natural slime actually jumps and lands with ordinary original gravity during the fixed observation window");
                        Require(!moving || maxPlayer-minPlayer>30,"moving scene contains real continuous original player movement");
                        Require(births>=2 && hits>0 && (assist?assistedBirths==births:assistedBirths==0),"explicit original and assisted gun scenes both produce real sources and positive native damage");
                    }
                }
                if(referenceOnly){Console.WriteLine("PASS independent original standing/moving blue-slime gun reference, fixed near-range windows, natural jump/landing and native birth/damage");return;}
                // An independently unreachable short blade has no credible
                // damage shape at this distance even if selection is legal.
                player.inventory[0].SetDefaults(3507);moveLeft=moveRight=false;player.controlLeft=player.controlRight=false;
                receiver.position=new Vector2(player.position.X+350,1280-receiver.height);
                for(int frame=0;frame<12;frame++)
                {
                    physical=player.Center-new Vector2(220,60);NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
                    Require(Vector2.Distance(player.Center,receiver.Center)>220,"short-blade negative window remains independently beyond its native reach");
                    Require((bool)Get(Get(host,"Selection"),"HasTarget"),"unreachable target remains selected");
                    var pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                    Require(GetOptional(attack,"ExpectedPreview")==null && !pixels.Any(Red),"unreachable natural receiver has no invented contact or red pixels");
                }
                Console.WriteLine("PASS natural blue slime idle/attack/movement/switch/restore actual World.Draw; independent original gun damage and unreachable blade negative");
            }
            finally
            {
                File.WriteAllLines(Path.Combine(output,"natural-slime-frames.csv"),rows);Main.LocalPlayer.isControlledByFilm=originalFilm;player=null;receiver=null;audit.UnpatchAll(audit.Id);
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            }
        }
    }
}
