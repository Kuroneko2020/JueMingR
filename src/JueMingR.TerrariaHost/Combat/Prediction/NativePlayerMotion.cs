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
        internal static void Write(BinaryWriter writer,Player player)
        {
            // The native sentinel is grappling[0]; unused trailing capacity may
            // contain zero and does not mean projectile slot 0 is attached.
            bool hooked=player.grappling!=null && player.grappling.Length>0 && player.grappling[0]>=0;
            writer.Write(player.mount.Active || hooked || player.pulley || player.sitting.isSitting || player.dashDelay<0 || player.wingTime>0 && player.controlJump || player.shimmering || player.tongued);
            // jumpSpeed is a shared scratch field last written by whichever
            // player updated last. Never treat it as this player's observation.
            writer.Write(Player.defaultGravity);
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
            if(!profile.Complex)AdvanceOrdinarySpeed(p);
            Vector2 old=p.position;
            // ResetEffects decays the observed gravity timer before the
            // original nearby-brain refresh. The brain page is a real player
            // movement dependency, not permission to freeze this timer.
            bool hadForcedGravity=p.forcedGravity>0;
            if(hadForcedGravity)p.forcedGravity--;
            if(NPC.brainOfGravity>=0 && NPC.brainOfGravity<Main.maxNPCs && Vector2.Distance(p.Center,Main.npc[NPC.brainOfGravity].Center)<4000f)p.forcedGravity=10;
            if(p.forcedGravity>0)p.gravDir=-1;
            else if(hadForcedGravity && !p.gravControl && !p.gravControl2)p.gravDir=1;
            if(profile.Complex)Quality|=2;
            else
            {
                int jumpHeight;float jumpSpeed=MovementParameters(p,profile.DefaultGravity,out jumpHeight);
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
                if(p.controlJump)
                {
                    if(p.jump>0){if(p.velocity.Y==0)p.jump=0;else{p.velocity.Y=-jumpSpeed*p.gravDir;p.jump--;}}
                    else if(p.velocity.Y==0 && (p.releaseJump || p.autoJump)){p.velocity.Y=-jumpSpeed*p.gravDir;p.jump=jumpHeight;}
                    p.releaseJump=false;
                }
                else{p.jump=0;p.releaseJump=true;}
                p.velocity.Y+=p.gravity*p.gravDir;
                if(p.velocity.Y*p.gravDir>p.maxFallSpeed)p.velocity.Y=p.maxFallSpeed*p.gravDir;
            }
            // Liquid transitions use original occupancy tests. Holding sampled
            // movement parameters across a transition is explicitly approximate.
            bool oldWet=p.wet;
            p.wet=Collision.WetCollision(p.position,p.width,p.height);p.honeyWet=Collision.honey;p.shimmerWet=Collision.shimmer;
            p.lavaWet=p.wet && Collision.LavaCollision(p.position,p.width,p.height);
            if(p.wet || oldWet!=p.wet)Quality|=4;
            bool ignorePlatforms=p.gravDir==-1f,fallThrough=p.controlDown || ignorePlatforms;
            float movement=p.shimmerWet?0.375f:p.honeyWet && !p.ignoreWater?0.25f:p.wet && !p.ignoreWater && !p.merman && !p.trident?0.5f:1f;
            float length=p.velocity.Length(),limit=Math.Min(16f,Math.Min(p.width-0.5f,p.height-0.5f));
            if(!Finite(length) || limit<=0)throw new InvalidDataException("Invalid player movement magnitude.");
            int segments=Math.Max(1,(int)Math.Ceiling(length/limit));if(segments>32)throw new InvalidDataException("Player movement exceeds bounded collision continuation.");
            Vector2 velocity=p.velocity,total=Vector2.Zero;
            for(int part=0;part<segments;part++)
            {
                p.velocity=velocity/segments;
                if(!profile.Complex)
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
            if(p.position!=old || p.controlLeft || p.controlRight || p.controlJump)Quality|=1;
            if(!Finite(p.position.X) || !Finite(p.position.Y) || !Finite(p.velocity.X) || !Finite(p.velocity.Y))throw new InvalidDataException("Nonfinite player continuation.");
        }
        private static void AdvanceOrdinarySpeed(Player p)
        {
            // Fixed native subset with reconstructible equipment inputs. Never
            // divide the sampled speed by a buff multiplier: an expired buff
            // may already be absent at capture while its last-step effect is
            // still in the observed parameters. Frame zero remains untouched;
            // each future step rebuilds from the original base values instead.
            // Other effects retain the existing conditional continuation and
            // strict premise rejection; this is not a full Player.Update.
            if(Player.originalRunSpeed!=3f || p.slowOgreSpit || p.dazed || p.burned || p.slow || p.chilled || p.shieldRaised || p.sticky || p.powerrun || p.slippy || p.slippy2 || p.sandStorm || p.shadowArmor || p.hasMagiluminescence || p.empressBrooch || p.wingsLogic>0 || p.carpetFrame!=-1 || p.jumpBoost || p.frogLegJumpBoost || p.wereWolf || p.moonLordLegs || p.inventory[p.selectedItem].type==3106)return;
            for(int i=0;i<p.buffType.Length;i++)if(p.buffType[i]!=0 && p.buffType[i]!=3)return;
            for(int slot=0;slot<10;slot++)
            {
                var item=p.GetEffectiveArmor(slot);if(item.IsAir || !p.IsItemSlotUnlockedAndUsable(slot) || item.expertOnly && !Main.expertMode)continue;
                if(slot<3 || !item.accessory || item.type!=54 && item.type!=2423 || item.prefix!=0 && (item.prefix<73 || item.prefix>76))return;
            }
            p.moveSpeed=1f;p.runAcceleration=.08f;p.runSlowdown=.2f;p.maxRunSpeed=p.accRunSpeed=3f;p.autoJump=false;p.jumpSpeedBoost=0;
            for(int i=0;i<p.buffType.Length;i++)if(p.buffType[i]==3 && p.buffTime[i]>0)
            {
                if(p.whoAmI==Main.myPlayer && !Terraria.ID.BuffID.Sets.TimeLeftDoesNotDecrease[3])p.buffTime[i]--;
                p.moveSpeed+=.25f; // Time 1 -> 0 still contributes this step.
            }
            if(p.whoAmI==Main.myPlayer)for(int i=0;i<p.buffType.Length;i++)if(p.buffType[i]>0 && p.buffTime[i]<=0)p.DelBuff(i);
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
