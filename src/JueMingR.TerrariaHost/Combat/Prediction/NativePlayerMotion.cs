using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // A conditional continuation of observed controls and movement parameters,
    // not Player.Update: no equipment reset, pickup, item use, attacks, switches,
    // tile destruction or new input edges. Geometry remains the original game.
    internal sealed class NativePlayerMotion
    {
        private struct Profile { internal bool Complex; internal float DefaultGravity; }
        private readonly Profile[] profiles=new Profile[Main.maxPlayers];
        internal int Quality { get; private set; }
        internal static int Mechanism(Player p)
        {return (p.mount.Active?1:0)|(p.grappling!=null && p.grappling.Length>0 && p.grappling[0]>=0?2:0)|(p.pulley?4:0)|(p.sitting.isSitting?8:0)|(p.dashDelay<0?16:0)|(p.wingTime>0 && p.controlJump?32:0)|(p.shimmering?64:0)|(p.tongued?128:0);}
        private static bool SupportedHover(Player p)
        {return p.mount.Active && (p.mount.Type==Terraria.ID.MountID.WitchBroom || p.mount.Type==5) && !p.CCed && !p.pulley && !p.shimmering && !p.tongued && (p.grappling==null || p.grappling.Length==0 || p.grappling[0]<0);}
        internal static bool Conditional(Player p){return Mechanism(p)!=0 && !SupportedHover(p);}
        internal static void Write(BinaryWriter writer,Player player)
        {
            // The native sentinel is grappling[0]; unused trailing capacity may
            // contain zero and does not mean projectile slot 0 is attached.
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(writer,"NativePlayerMotion.Mechanism(player)!=0","context");}catch(Exception diagnosticError){AimDiagnostics.Missing("NativePlayerMotion",diagnosticError);}
#endif
writer.Write(Mechanism(player)!=0);}
            // jumpSpeed is a shared scratch field last written by whichever
            // player updated last. Never treat it as this player's observation.
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(writer,"NativePlayerMotion.Player.defaultGravity","context");}catch(Exception diagnosticError){AimDiagnostics.Missing("NativePlayerMotion",diagnosticError);}
#endif
writer.Write(Player.defaultGravity);}
        }
        internal void Read(BinaryReader reader,int slot)
        {
            var profile=new Profile{Complex=reader.ReadBoolean(),DefaultGravity=reader.ReadSingle()};
            if(!Finite(profile.DefaultGravity) || profile.DefaultGravity<0 || profile.DefaultGravity>128)throw new InvalidDataException("Player gravity premise.");
            profiles[slot]=profile;
        }
        internal void Begin(){Quality=0;}
        internal void Clear(){Array.Clear(profiles,0,profiles.Length);Quality=0;}
        internal void Advance()
        {
            using(NativeRandomSnapshot.Use("UpdatePlayers"))
                for(int slot=0;slot<Main.maxPlayers;slot++)
                {
                    var player=Main.player[slot];if(!player.active || !HasUpdateArea(player))continue;
                    // Native Player.Update decrements existing tags before its
                    // dead/ghost returns. It does not apply new future attacks.
                    player.TagEffectStack.Update();
                    if(player.dead || player.ghost)continue;
                    Step(player,profiles[slot]);
                }
        }
        private static bool HasUpdateArea(Player player)
        {
            player.outOfRange=false;if(player.whoAmI==Main.myPlayer)return true;
            int x=(int)(player.position.X+player.width/2)/16,y=(int)(player.position.Y+player.height/2)/16;
            if(WorldGen.InWorld(x,y,4) && NativeTileBoundary.Read(x,y)!=null && NativeTileBoundary.Read(x-3,y)!=null && NativeTileBoundary.Read(x+3,y)!=null && NativeTileBoundary.Read(x,y-3)!=null && NativeTileBoundary.Read(x,y+3)!=null)return true;
            player.outOfRange=true;player.numMinions=0;player.slotsMinions=0;player.itemAnimation=0;player.netOffset=Vector2.Zero;return false;
        }
        private void Step(Player p,Profile profile)
        {
            if(p.width<1 || p.height<1 || p.width>512 || p.height>512 || p.gravDir!=1f && p.gravDir!=-1f || !Finite(p.gravity) || !Finite(p.maxFallSpeed) || !Finite(p.runAcceleration) || !Finite(p.runSlowdown) || !Finite(p.maxRunSpeed))throw new InvalidDataException("Player movement premise.");
            float speed;bool ordinaryBuffs=AdvanceBuffClocks(p,out speed);
            if(!profile.Complex && ordinaryBuffs)AdvanceOrdinarySpeed(p,speed);
            Vector2 old=p.position;
            // ResetEffects decays the observed gravity timer before the
            // original nearby-brain refresh. The brain page is a real player
            // movement dependency, not permission to freeze this timer.
            bool hadForcedGravity=p.forcedGravity>0;
            if(hadForcedGravity)p.forcedGravity--;
            if(NPC.brainOfGravity>=0 && NPC.brainOfGravity<Main.maxNPCs && Vector2.Distance(p.Center,Main.npc[NPC.brainOfGravity].Center)<4000f)p.forcedGravity=10;
            if(p.forcedGravity>0)p.gravDir=-1;
            else if(hadForcedGravity && !p.gravControl && !p.gravControl2)p.gravDir=1;
            bool hover=SupportedHover(p),broom=hover && p.mount.Type==Terraria.ID.MountID.WitchBroom;
            if(hover && p.velocity.Y==0)p.mount.FatigueRecovery();
            if(profile.Complex)Quality|=2;
            if(!profile.Complex || hover)
            {
                int jumpHeight;float jumpSpeed=MovementParameters(p,profile.DefaultGravity,out jumpHeight);
                if(hover)
                {
                    // Fixed .8 broom/bee movement uses mount parameters after
                    // equipment, before horizontal motion. Hover owns vertical
                    // acceleration AND its tiny position compensation; freezing
                    // velocity or rounding it to zero loses that contract.
                    p.runSlowdown=.2f;p.runAcceleration=p.mount.Acceleration;
                    p.maxRunSpeed=p.mount.RunSpeed;p.accRunSpeed=p.mount.DashSpeed;
                    p.autoJump=p.mount.AutoJump;
                    jumpSpeed=p.mount.JumpSpeed(p.velocity.X);jumpHeight=p.mount.JumpHeight(p.velocity.X);
                    if(p.sticky){jumpSpeed/=5f;jumpHeight/=10;}if(p.dazed){jumpSpeed/=2f;jumpHeight/=5;}
                    if(p.forcedGravity<=0)p.gravDir=1;
                }
                // Fixed sampled ordinary controls; retain reversal braking and
                // the native distinction between ground and air deceleration.
                if(p.controlLeft && p.velocity.X>-p.maxRunSpeed)
                {if(p.velocity.X>p.runSlowdown)p.velocity.X-=p.runSlowdown;p.velocity.X-=p.runAcceleration;}
                else if(p.controlRight && p.velocity.X<p.maxRunSpeed)
                {if(p.velocity.X< -p.runSlowdown)p.velocity.X+=p.runSlowdown;p.velocity.X+=p.runAcceleration;}
                else if(p.controlLeft && p.velocity.X> -p.accRunSpeed && p.dashDelay>=0)
                {if(p.velocity.Y==0 || p.wingsLogic>0){if(p.velocity.X>p.runSlowdown)p.velocity.X-=p.runSlowdown;p.velocity.X-=p.runAcceleration*0.2f;if(p.wingsLogic>0)p.velocity.X-=p.runAcceleration*0.2f;}}
                else if(p.controlRight && p.velocity.X<p.accRunSpeed && p.dashDelay>=0)
                {if(p.velocity.Y==0 || p.wingsLogic>0){if(p.velocity.X< -p.runSlowdown)p.velocity.X+=p.runSlowdown;p.velocity.X+=p.runAcceleration*0.2f;if(p.wingsLogic>0)p.velocity.X+=p.runAcceleration*0.2f;}}
                // Native friction also runs when held input has reached the
                // running limit; omitting it changes the speed-limit cycle.
                else if(p.velocity.Y==0 || !p.PortalPhysicsEnabled)
                {
                    float slowdown=p.velocity.Y==0?p.runSlowdown:p.runSlowdown*0.5f;
                    p.velocity.X=p.velocity.X>slowdown?p.velocity.X-slowdown:p.velocity.X< -slowdown?p.velocity.X+slowdown:0;
                }
                if(hover)
                {
                    if(p.controlUp && p.releaseUp && p.velocity.Y==0)p.velocity.Y=-(p.mount.Acceleration+p.gravity+.001f);
                    p.releaseUp=!p.controlUp;
                }
                bool justJumped=false;
                if(p.controlJump)
                {
                    if(p.jump>0){if(p.velocity.Y==0)p.jump=0;else{p.velocity.Y=-jumpSpeed*p.gravDir;if(hover && p.merman){if(p.swimTime<=10)p.swimTime=30;}else p.jump--;}}
                    else if((p.velocity.Y==0 || hover && p.wet && p.accFlipper) && (p.releaseJump || p.autoJump && p.velocity.Y==0)){p.velocity.Y=-jumpSpeed*p.gravDir;p.jump=jumpHeight;justJumped=true;if(hover && p.wet && p.accFlipper && p.swimTime==0)p.swimTime=30;}
                    p.releaseJump=false;
                }
                else{p.jump=0;p.releaseJump=true;}
                // Flight time resets after jump state, while fatigue recovery
                // occurs before horizontal parameters. Reordering these loses
                // the bee's real fatigue-driven speed and landing lifecycle.
                if(hover && ((p.velocity.Y==0 || p.sliding) && p.releaseJump || p.autoJump && justJumped))p.mount.ResetFlightTime(p);
                if(hover)p.mount.Hover(p);else p.velocity.Y+=p.gravity*p.gravDir;
                if(p.velocity.Y*p.gravDir>p.maxFallSpeed)p.velocity.Y=p.maxFallSpeed*p.gravDir;
                if(hover && p.slowFall)
                {if(p.velocity.Y*p.gravDir>p.maxFallSpeed/3f && !p.TryingToHoverDown)p.velocity.Y=p.maxFallSpeed/3f*p.gravDir;if(p.velocity.Y*p.gravDir>p.maxFallSpeed/5f && p.TryingToHoverUp)p.velocity.Y=p.maxFallSpeed/10f*p.gravDir;}
            }
            // Liquid transitions use original occupancy tests. Holding sampled
            // movement parameters across a transition is explicitly approximate.
            bool oldWet=p.wet;
            p.wet=Collision.WetCollision(p.position,p.width,p.height);p.honeyWet=Collision.honey;p.shimmerWet=Collision.shimmer;
            p.lavaWet=p.wet && Collision.LavaCollision(p.position,p.width,p.height);
            if(hover && oldWet && !p.wet && p.wetSlime==0){int height=p.mount.JumpHeight(p.velocity.X);if(p.sticky)height/=10;if(p.dazed)height/=5;if(p.jump>height/5)p.jump=height/5;}
            if(p.wet || oldWet!=p.wet)Quality|=4;
            bool ignorePlatforms=broom || p.gravDir==-1f,fallThrough=broom || p.controlDown || ignorePlatforms;
            float movement=p.shimmerWet?0.375f:p.honeyWet && !p.ignoreWater?0.25f:p.wet && !p.ignoreWater && !p.merman && !p.trident?0.5f:1f;
            float length=p.velocity.Length(),limit=Math.Min(16f,Math.Min(p.width-0.5f,p.height-0.5f));
            if(!Finite(length) || limit<=0)throw new InvalidDataException("Invalid player movement magnitude.");
            int segments=Math.Max(1,(int)Math.Ceiling(length/limit));if(segments>32)throw new InvalidDataException("Player movement exceeds bounded collision continuation.");
            Vector2 velocity=p.velocity,total=Vector2.Zero;
            for(int part=0;part<segments;part++)
            {
                p.velocity=velocity/segments;
                if(!profile.Complex || hover)
                {
                    p.SlopeDownMovement();
                    if(p.velocity.Y==p.gravity)Collision.StepDown(ref p.position,ref p.velocity,p.width,p.height,ref p.stepSpeed,ref p.gfxOffY,(int)p.gravDir,p.waterWalk || p.waterWalk2);
                    if(p.gravDir==1f && (p.carpetFrame!=-1 || p.velocity.Y>=p.gravity) && !p.controlDown || p.gravDir==-1f && (p.carpetFrame!=-1 || p.velocity.Y<=p.gravity) && !p.controlUp)
                        Collision.StepUp(ref p.position,ref p.velocity,p.width,p.height,ref p.stepSpeed,ref p.gfxOffY,(int)p.gravDir,p.controlUp);
                }
                Vector2 proposed=p.velocity;p.velocity=p.TileCollision(p.position,proposed,fallThrough,ignorePlatforms);
                if(Collision.up && p.gravDir==1f || Collision.down && p.gravDir==-1f)p.jump=0;
                if(p.waterWalk || p.waterWalk2)p.velocity=Collision.WaterCollision(p.position,p.velocity,p.width,p.height,fallThrough,false,p.waterWalk);
                Vector2 displacement=p.velocity*movement;
                if(p.velocity.X!=proposed.X)displacement.X=p.velocity.X;
                if(p.velocity.Y!=proposed.Y)displacement.Y=p.velocity.Y;
                p.position+=displacement;p.SlopingCollision(fallThrough,ignorePlatforms);total+=p.velocity;
                // Once stopped on an axis, later bounded substeps cannot push
                // through the same surface using the original full velocity.
                if(p.velocity.X==0)velocity.X=0;if(p.velocity.Y==0)velocity.Y=0;
            }
            p.velocity=total;
            if(p.gravDir==1f && Collision.up || p.gravDir==-1f && Collision.down)
            {p.velocity.Y=0.01f*p.gravDir;if(!p.merman)p.jump=0;}
            // Player.Update applies the original world border after collision.
            // Without it, even a stationary edge player drifts by one native
            // acceleration step and changes NPC.targetRect's integer branch.
            // Native death/save exits remain fenced by NativeEffectBoundary.
            p.BordersMovement();
            // Locked broom/bee PlayerFrame branches own flight compensation
            // and the near-ground landing transition. Its UpdateFrame has no
            // idle RNG or light (unlike other mount types); never generalize
            // this call to arbitrary mounts or invoke their UpdateEffects.
            if(hover)
            {
                if(p.velocity.Y!=0 && p.mount.RunningGraceTime<=0)
                {if(p.wet)p.mount.UpdateFrame(p,4,p.velocity);else{p.mount.TryBeginningFlight(p,2);p.mount.UpdateFrame(p,2,p.velocity);p.mount.TryLanding(p);}}
                else p.mount.UpdateFrame(p,p.mount.GetIntendedGroundedFrame(p),p.velocity);
            }
            if(p.position!=old || p.controlLeft || p.controlRight || p.controlJump)Quality|=1;
            if(!Finite(p.position.X) || !Finite(p.position.Y) || !Finite(p.velocity.X) || !Finite(p.velocity.Y))throw new InvalidDataException("Nonfinite player continuation.");
        }
        private static bool AdvanceBuffClocks(Player p,out float speed)
        {
            // Buff clocks belong to Player.Update even when equipment or a
            // complex movement profile prevents speed reconstruction. Keep
            // the time 1 -> 0 effect for this step, then native forward-order
            // DelBuff compaction; unknown effects still retain exact premises.
            speed=1f;bool ordinary=true;
            for(int i=0;i<p.buffType.Length;i++)
            {
                int type=p.buffType[i];if(type<=0)continue;
                ordinary&=type==2 || type==3 || type==26 || type==206 || type==207;
                if(p.buffTime[i]<=0)continue;
                if(type==3)speed+=.25f;else if(type==26)speed+=.2f;else if(type==206)speed+=.3f;else if(type==207)speed+=.4f;
                if(p.whoAmI==Main.myPlayer && OrdinaryClock(type) && !Terraria.ID.BuffID.Sets.TimeLeftDoesNotDecrease[type])p.buffTime[i]--;
            }
            if(p.whoAmI==Main.myPlayer)for(int i=0;i<p.buffType.Length;i++)if(p.buffType[i]>0 && p.buffTime[i]<=0)p.DelBuff(i);
            return ordinary && !Main.dontStarveWorld;
        }
        private static bool OrdinaryClock(int type)
        {
            // Locked .8 UpdateBuffs: exclude renewal/deletion/type-changing
            // branches, pets/mounts, and adjacent environmental refreshes.
            // Remaining clocks use its common decrement; movement/effect
            // changes still require exact observed history, not frozen timers.
            if(type>=Terraria.ID.BuffID.Count)throw new InvalidDataException("Unknown player buff clock.");
            if(Terraria.ID.BuffID.Sets.MountType[type]!=-1 || Main.vanityPet[type] || Main.lightPet[type] || type>=95 && type<=100 || type>=170 && type<=181 || type>=332 && type<=334)return false;
            switch(type)
            {
                // Summon ownership and native legacy pet renewal.
                case 49:case 60:case 64:case 83:case 125:case 126:case 133:case 134:case 135:case 139:case 140:case 161:case 182:case 187:case 188:case 213:case 214:case 216:case 263:case 271:case 322:case 325:case 335:case 355:case 385:case 386:case 389:case 390:case 393:case 394:
                // Conditional removal, random insertion and phase refresh.
                case 28:case 34:case 37:case 38:case 62:case 103:case 148:case 151:case 353:
                // Player.Update scene refresh before UpdateBuffs.
                case 86:case 87:case 89:case 146:case 147:case 157:case 158:case 194:case 215:case 350:return false;
                default:return true;
            }
        }
        private static void AdvanceOrdinarySpeed(Player p,float speed)
        {
            // Fixed native subset with reconstructible equipment inputs. Never
            // divide the sampled speed by a buff multiplier: an expired buff
            // may already be absent at capture while its last-step effect is
            // still in the observed parameters. Frame zero remains untouched;
            // each future step rebuilds from the original base values instead.
            // Other effects retain the existing conditional continuation and
            // strict premise rejection; this is not a full Player.Update.
            if(Player.originalRunSpeed!=3f || p.slowOgreSpit || p.dazed || p.burned || p.slow || p.chilled || p.shieldRaised || p.sticky || p.powerrun || p.slippy || p.slippy2 || p.sandStorm || p.shadowArmor || p.hasMagiluminescence || p.empressBrooch || p.wingsLogic>0 || p.carpetFrame!=-1 || p.jumpBoost || p.frogLegJumpBoost || p.wereWolf || p.moonLordLegs || p.inventory[p.selectedItem].type==3106)return;
            for(int slot=0;slot<10;slot++)
            {
                var item=p.GetEffectiveArmor(slot);if(item.IsAir || !p.IsItemSlotUnlockedAndUsable(slot) || item.expertOnly && !Main.expertMode)continue;
                if(slot<3 || !item.accessory || item.type!=54 && item.type!=2423 || item.prefix!=0 && (item.prefix<73 || item.prefix>76))return;
            }
            p.moveSpeed=speed;p.runAcceleration=.08f;p.runSlowdown=.2f;p.maxRunSpeed=p.accRunSpeed=3f;p.autoJump=false;p.jumpSpeedBoost=0;
            for(int slot=3;slot<10;slot++)
            {
                var item=p.GetEffectiveArmor(slot);if(item.IsAir || !p.IsItemSlotUnlockedAndUsable(slot) || item.expertOnly && !Main.expertMode)continue;
                if(item.prefix>=73 && item.prefix<=76)p.moveSpeed+=item.prefix==73?.01f:item.prefix==74?.02f:item.prefix==75?.03f:.04f;
                if(item.type==54)p.accRunSpeed=6f;
                else if(item.type==2423){p.autoJump=true;p.jumpSpeedBoost+=1.6f;}
            }
            p.runAcceleration*=p.moveSpeed;p.maxRunSpeed*=p.moveSpeed;
        }
        private static float MovementParameters(Player p,float defaultGravity,out int jumpHeight)
        {
            // Native Update recomputes ordinary gravity before movement using
            // the prior liquid state and the current altitude. Sampled gravity
            // cannot be frozen while a player crosses the space boundary.
            float gravity=defaultGravity,fall=p.PortalPhysicsEnabled?35f:10f,jump=5.01f;jumpHeight=15;
            if(p.shimmerWet){gravity=0.15f;jump=5.51f;jumpHeight=23;}
            else if(p.wet)
            {
                if(p.honeyWet){gravity=0.1f;fall=3f;}
                else if(p.merman){gravity=0.3f;fall=7f;}
                else if(p.trident && !p.lavaWet){gravity=p.controlUp?0.1f:0.25f;fall=p.controlUp?2f:6f;jump=5.51f;jumpHeight=25;}
                else{gravity=0.2f;fall=5f;jump=6.01f;jumpHeight=30;}
            }
            if(p.vortexDebuff)gravity=0;
            float size=(float)Main.maxTilesX/4200f;size*=size;
            float altitude=(float)((double)(p.position.Y/16f-(60f+10f*size))/(Main.worldSurface/(Main.remixWorld?1.0:6.0)));
            altitude=Math.Max(Main.remixWorld?0.1f:0.25f,Math.Min(1f,altitude));
            p.gravity=gravity*altitude;p.maxFallSpeed=fall+0.01f;
            if(p.jumpBoost){jump=Math.Max(jump,6.51f);jumpHeight=Math.Max(jumpHeight,20);}if(p.wereWolf){jump+=0.2f;jumpHeight+=2;}if(p.moonLordLegs)jumpHeight++;
            // jumpSpeedBoost already includes the sampled accessory effects.
            jump+=p.jumpSpeedBoost;if(p.sticky){jump/=5f;jumpHeight/=10;}if(p.dazed){jump/=2f;jumpHeight/=5;}
            return jump;
        }
        private static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
    }
}
