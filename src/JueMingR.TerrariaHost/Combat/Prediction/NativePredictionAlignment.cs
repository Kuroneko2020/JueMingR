using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Only the request's owned entity pages are observed. Reuse the capture
    // schema: localAI, all buff slots, lifecycle and historical array values
    // cannot quietly disappear into the old motion-only cache equality rule.
    internal static class NativePredictionAlignment
    {
        private static readonly NativeValueSnapshot Projectiles=new NativeValueSnapshot(typeof(Projectile),fieldNames:new NativeValueSnapshot(typeof(Projectile)).FieldIdentities.Where(ExactMotionField).Select(Name).ToArray());
        // nameOver belongs exclusively to Main.DrawNPCDirect's name fade and
        // targetSetFrame to DrawAggro's color fade. They
        // remains in the complete capture, but different Draw/Update ratios
        // cannot change whether the captured AI continuation is still useful.
        private static readonly NativeValueSnapshot Npcs=new NativeValueSnapshot(typeof(NPC),fieldNames:PredictionWire.Npcs.FieldIdentities.Where(f=>ExactMotionField(f) && Name(f)!="nameOver" && Name(f)!="targetSetFrame" && Name(f)!="targetRect" && Name(f)!="localAI" && Name(f)!="ai").Select(Name).ToArray());
        private static string Name(string field){return field.Substring(field.LastIndexOf('.')+1);}
        private static bool ExactMotionField(string field){string name=Name(field);return name!="velocity" && name!="oldVelocity" && name!="position" && name!="oldPosition" && name!="oldPos";}
        internal sealed class Frame
        {
            internal long Tick;
#if JMR_AIM_DIAGNOSTICS
            internal long DiagnosticObservation;
#endif
            internal ulong World;
            internal int[] Npcs,Projectiles,Players;
            internal ulong[] NpcState,ProjectileState,PlayerPremise;
            internal float[] NpcShootClock;
            internal ulong[] NpcIdentity,ProjectileIdentity;
            internal bool[] NpcRequired,ProjectileRequired;
            internal bool IsSample;
            internal Vector2[] PlayerPosition,PlayerVelocity;
            internal bool[] PlayerConditional;
            internal Vector2[] NpcVelocity,NpcOldVelocity,ProjectileVelocity,ProjectileOldVelocity;
            internal Vector2[][] NpcPositions,ProjectilePositions;
            internal Vector2 NetOffset;
            internal bool CanReceive,CanHarm,NewSegment,HasState;
        }
        internal static Frame Observe(long tick,int[] npcs,int[] projectiles,int selected)
        {
            #if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.Active)AimDiagnostics.Observation=AimDiagnostics.NextObservation();
#endif
            var players=new List<int>();for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]!=null && Main.player[i].active)players.Add(i);
            var frame=new Frame{Tick=tick,HasState=true,Npcs=npcs,Projectiles=projectiles,Players=players.ToArray(),NpcState=new ulong[npcs.Length],ProjectileState=new ulong[projectiles.Length],PlayerPremise=new ulong[players.Count],PlayerPosition=new Vector2[players.Count],PlayerVelocity=new Vector2[players.Count]};
            frame.NpcVelocity=new Vector2[npcs.Length];frame.NpcOldVelocity=new Vector2[npcs.Length];frame.ProjectileVelocity=new Vector2[projectiles.Length];frame.ProjectileOldVelocity=new Vector2[projectiles.Length];
            frame.NpcPositions=new Vector2[npcs.Length][];frame.ProjectilePositions=new Vector2[projectiles.Length][];
            frame.PlayerConditional=new bool[players.Count];
            frame.NpcShootClock=new float[npcs.Length];
            frame.NpcIdentity=new ulong[npcs.Length];
            frame.ProjectileIdentity=new ulong[projectiles.Length];
            for(int i=0;i<npcs.Length;i++)frame.NpcShootClock[i]=Main.npc[npcs[i]].ai[1];
            for(int i=0;i<npcs.Length;i++){var n=Main.npc[npcs[i]];frame.NpcVelocity[i]=n.velocity;frame.NpcOldVelocity[i]=n.oldVelocity;frame.NpcPositions[i]=Positions(n.position,n.oldPosition,n.oldPos);}
            for(int i=0;i<projectiles.Length;i++){var p=Main.projectile[projectiles[i]];frame.ProjectileVelocity[i]=p.velocity;frame.ProjectileOldVelocity[i]=p.oldVelocity;frame.ProjectilePositions[i]=Positions(p.position,p.oldPosition,p.oldPos);}
            using(var writer=new ValueHashWriter())
            {
                writer.Reset();WorldPremise(writer);frame.World=writer.Hash;
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.Active)try{AimDiagnostics.Tape(writer,tick,"world");}catch(Exception diagnosticError){AimDiagnostics.Missing("NativePredictionAlignment",diagnosticError);}
#endif

                for(int i=0;i<npcs.Length;i++)
                {
                    var n=Main.npc[npcs[i]];writer.Reset();NpcPremise(writer,n);NativeEntityContext.WriteNpc(writer,n);frame.NpcState[i]=writer.Hash;
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.Active)try{AimDiagnostics.Tape(writer,tick,"npc-state slot="+npcs[i]);}catch(Exception diagnosticError){AimDiagnostics.Missing("NativePredictionAlignment",diagnosticError);}
#endif

                    // Vitality is an unconditional continuity condition even
                    // for an unused background. Ordinary water movement may
                    // be omitted; unobserved damage/healing may not. The Host
                    // hit fact additionally catches hit+heal between samples.
                    writer.Reset();{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"n.whoAmI","identity");
#endif
writer.Write(n.whoAmI);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"n.type","identity");
#endif
writer.Write(n.type);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"n.netID","identity");
#endif
writer.Write(n.netID);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"n.generation","identity");
#endif
writer.Write(n.generation);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"n.friendly","identity");
#endif
writer.Write(n.friendly);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"n.life","identity");
#endif
writer.Write(n.life);}frame.NpcIdentity[i]=writer.Hash;
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.Active)try{AimDiagnostics.Tape(writer,tick,"npc-identity slot="+npcs[i]);}catch(Exception diagnosticError){AimDiagnostics.Missing("NativePredictionAlignment",diagnosticError);}
#endif

                }
                for(int i=0;i<projectiles.Length;i++)
                {
                    var p=Main.projectile[projectiles[i]];writer.Reset();Projectiles.Write(writer,p);NativeActorContext.WriteProjectile(writer,p);frame.ProjectileState[i]=writer.Hash;
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.Active)try{AimDiagnostics.Tape(writer,tick,"projectile-state slot="+projectiles[i]);}catch(Exception diagnosticError){AimDiagnostics.Missing("NativePredictionAlignment",diagnosticError);}
#endif

                    // Even a currently unused shot must not silently become a
                    // different instance or damaging faction under this proof.
                    writer.Reset();{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"p.whoAmI","identity");
#endif
writer.Write(p.whoAmI);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"p.type","identity");
#endif
writer.Write(p.type);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"(uint)p.key","identity");
#endif
writer.Write((uint)p.key);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"p.owner","identity");
#endif
writer.Write(p.owner);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"p.friendly","identity");
#endif
writer.Write(p.friendly);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"p.hostile","identity");
#endif
writer.Write(p.hostile);}frame.ProjectileIdentity[i]=writer.Hash;
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.Active)try{AimDiagnostics.Tape(writer,tick,"projectile-identity slot="+projectiles[i]);}catch(Exception diagnosticError){AimDiagnostics.Missing("NativePredictionAlignment",diagnosticError);}
#endif

                }
                for(int i=0;i<players.Count;i++)
                {
                    Player p=Main.player[players[i]];writer.Reset();PlayerPremise(writer,p);
                    // Captured projectile AI may consume ammo (HasAmmo /
                    // PickAmmo). Pure NPC motion does not depend on an unused
                    // positive inventory stack's exact count; keep presence,
                    // type/prefix, selected item and all projectile counts.
                    if(projectiles.Length!=0)for(int slot=0;slot<p.inventory.Length;slot++){
#if JMR_AIM_DIAGNOSTICS
                        if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(writer,"p.inventory["+slot+"].stack.exact","Int32");}catch(Exception error){AimDiagnostics.Missing("diagnostic-arguments",error);}
#endif
                        writer.Write(p.inventory[slot].stack);}
                    frame.PlayerPremise[i]=writer.Hash;
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.Active)try{AimDiagnostics.Tape(writer,tick,"player-premise slot="+players[i]);}catch(Exception diagnosticError){AimDiagnostics.Missing("NativePredictionAlignment",diagnosticError);}
#endif
frame.PlayerPosition[i]=p.position;frame.PlayerVelocity[i]=p.velocity;frame.PlayerConditional[i]=NativePlayerMotion.Conditional(p);
                }
            }
            #if JMR_AIM_DIAGNOSTICS
            frame.DiagnosticObservation=AimDiagnostics.Observation;
#endif
            NPC target=Main.npc[selected];frame.NetOffset=target.netOffset;frame.CanReceive=target.active && CombatSelection.Receives(target,true);frame.CanHarm=target.active && !target.friendly && target.damage>0;
            #if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{using(var bytes=new MemoryStream())using(var encoded=new BinaryWriter(bytes)){Write(encoded,frame);encoded.Flush();AimDiagnostics.Event("alignment-frame",tick,"observation="+frame.DiagnosticObservation+";frame-owned observation before final usage mask",bytes.ToArray(),true);}}catch(Exception error){AimDiagnostics.Missing("frame-copy",error);}
#endif
            return frame;
        }
        private static void NpcPremise(BinaryWriter writer,NPC n)
        {
            Npcs.Write(writer,n);{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"n.ai.Length","premise");
#endif
writer.Write(n.ai.Length);}
            for(int i=0;i<n.ai.Length;i++)
            {float value=n.ai[i];bool representative=i==1 && RepresentativeShootClock(n,value);{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(writer,"n.ai["+i+"].representative","premise");}catch(Exception error){AimDiagnostics.Missing("diagnostic-arguments",error);}
#endif
writer.Write(representative);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(writer,"n.ai["+i+"].normalized","premise");}catch(Exception error){AimDiagnostics.Missing("diagnostic-arguments",error);}
#endif
writer.Write(representative?0f:value);}}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"n.localAI==null?-1:n.localAI.Length","premise");
#endif
writer.Write(n.localAI==null?-1:n.localAI.Length);}
            TargetRectangle(writer,n);
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"NativeLightingSnapshot.Observe(n)","premise");
#endif
writer.Write(NativeLightingSnapshot.Observe(n));}
            if(n.localAI==null)return;
            for(int i=0;i<n.localAI.Length;i++)
            {
                float value=n.localAI[i];
                // Locked .8 AI_037, body 135: +Next(4), then a minimum
                // trigger of 1400. Below this conservative full-horizon bound
                // neither clock can retarget or shoot in ANY represented
                // future. This is acceptance equivalence, not a changed live
                // value, snapshot, guard or general localAI/RNG exception.
                // Boundary clocks and all other fields remain bit-exact.
                if(i==0 && n.active && n.type==Terraria.ID.NPCID.TheDestroyerBody && n.aiStyle==37 && value>=0 && value+3*PredictionWire.MaximumHorizon<1400)value=0;
                {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(writer,"n.localAI["+i+"]","premise");}catch(Exception error){AimDiagnostics.Missing("diagnostic-arguments",error);}
#endif
writer.Write(value);}
            }
        }
        // Locked .8 AI_005's hornet mechanism accumulates a keyed random
        // firing timer, independently of position/turning. Returned futures
        // already declare RandomRepresentative. Keep sound marker101 and any
        // next-update crossing of130 exact; all other AI, motion, life, births
        // and dependencies still prove themselves. Full capture retains the
        // real timer; frame zero separately proves its raw bits. This never
        // certifies a changed sample or another mechanism. Classification
        // cannot depend on Main.netMode: the private worker is always offline,
        // while the request separately owns NetworkObservation provenance.
        private static bool RepresentativeShootClock(NPC n,float value)
        {
            bool mechanism=n.type==42 || n.type==176 || n.type>=231 && n.type<=235;
            if(!n.active || n.aiStyle!=5 || !mechanism || !(n.scale>0) || float.IsInfinity(n.scale)
                || value<=0 || float.IsNaN(value) || value==101)return false;
            int draws=1+(n.type==176?1:0)+(Main.getGoodWorld?1:0);
            return (double)value+1.900001*n.scale*draws+.0001<130;
        }
        private static void TargetRectangle(BinaryWriter writer,NPC n)
        {
            // TargetClosest and NPCUtils can select a player, tank pet or NPC.
            // Only a rectangle that currently equals the conditional player's
            // own hitbox has the same derived-motion contract as that player.
            // Keep a source discriminator: cached/other-source rectangles,
            // target identity, dimensions and all actual NPC AI/motion remain
            // strict. Full captures retain the unmodified rectangle.
            Player player=n.HasPlayerTarget?Main.player[n.target]:null;
            bool derived=player!=null && player.active && NativePlayerMotion.Conditional(player) && n.targetRect==player.Hitbox;
            if(derived)for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]!=null && Main.player[i].active && Main.player[i].tankPet>=0){derived=false;break;}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"derived","premise");
#endif
writer.Write(derived);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"n.targetRect.Width","premise");
#endif
writer.Write(n.targetRect.Width);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"n.targetRect.Height","premise");
#endif
writer.Write(n.targetRect.Height);}
            if(!derived){{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"n.targetRect.X","premise");
#endif
writer.Write(n.targetRect.X);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(writer,"n.targetRect.Y","premise");
#endif
writer.Write(n.targetRect.Y);}}
        }
        internal static Frame Presentation(long tick,int selected)
        {
            NPC target=Main.npc[selected];
            return new Frame{Tick=tick,NetOffset=target.netOffset,CanReceive=target.active && CombatSelection.Receives(target,true),CanHarm=target.active && !target.friendly && target.damage>0};
        }
        private static void PlayerPremise(BinaryWriter w,Player p)
        {
            bool conditional=NativePlayerMotion.Conditional(p);{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"conditional","premise");
#endif
w.Write(conditional);}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.whoAmI","premise");
#endif
w.Write(p.whoAmI);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.active","premise");
#endif
w.Write(p.active);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.dead","premise");
#endif
w.Write(p.dead);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.ghost","premise");
#endif
w.Write(p.ghost);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.width","premise");
#endif
w.Write(p.width);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.height","premise");
#endif
w.Write(p.height);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.gravDir","premise");
#endif
w.Write(p.gravDir);}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.immune && p.immuneTime>PredictionWire.MaximumHorizon","premise");
#endif
w.Write(p.immune && p.immuneTime>PredictionWire.MaximumHorizon);}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.controlLeft","premise");
#endif
w.Write(p.controlLeft);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.controlRight","premise");
#endif
w.Write(p.controlRight);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.controlUp","premise");
#endif
w.Write(p.controlUp);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.controlDown","premise");
#endif
w.Write(p.controlDown);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.controlJump","premise");
#endif
w.Write(p.controlJump);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.controlUseItem","premise");
#endif
w.Write(p.controlUseItem);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.controlUseTile","premise");
#endif
w.Write(p.controlUseTile);}
            // Special mounts may recompute these outputs from fatigue or
            // flight stage on every step (e.g. bee RunSpeed). Their equipment,
            // input and mechanism remain fixed premises, not these outputs.
            if(!conditional){{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.runAcceleration","premise");
#endif
w.Write(p.runAcceleration);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.runSlowdown","premise");
#endif
w.Write(p.runSlowdown);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.maxRunSpeed","premise");
#endif
w.Write(p.maxRunSpeed);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.accRunSpeed","premise");
#endif
w.Write(p.accRunSpeed);}}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.jumpSpeedBoost","premise");
#endif
w.Write(p.jumpSpeedBoost);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.autoJump","premise");
#endif
w.Write(p.autoJump);}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.ignoreWater","premise");
#endif
w.Write(p.ignoreWater);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.merman","premise");
#endif
w.Write(p.merman);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.trident","premise");
#endif
w.Write(p.trident);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.waterWalk","premise");
#endif
w.Write(p.waterWalk);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.waterWalk2","premise");
#endif
w.Write(p.waterWalk2);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.aggro","premise");
#endif
w.Write(p.aggro);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.insideUnbreakableWalls","premise");
#endif
w.Write(p.insideUnbreakableWalls);}
            if(!conditional){{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.jump","premise");
#endif
w.Write(p.jump);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.releaseJump","premise");
#endif
w.Write(p.releaseJump);}}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.wet","premise");
#endif
w.Write(p.wet);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.honeyWet","premise");
#endif
w.Write(p.honeyWet);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.lavaWet","premise");
#endif
w.Write(p.lavaWet);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.shimmerWet","premise");
#endif
w.Write(p.shimmerWet);}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.forcedGravity","premise");
#endif
w.Write(p.forcedGravity);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.creativeGodMode","premise");
#endif
w.Write(p.creativeGodMode);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.gravControl","premise");
#endif
w.Write(p.gravControl);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.gravControl2","premise");
#endif
w.Write(p.gravControl2);}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.PortalPhysicsEnabled","premise");
#endif
w.Write(p.PortalPhysicsEnabled);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.wingsLogic","premise");
#endif
w.Write(p.wingsLogic);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.carpetFrame","premise");
#endif
w.Write(p.carpetFrame);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.jumpBoost","premise");
#endif
w.Write(p.jumpBoost);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.wereWolf","premise");
#endif
w.Write(p.wereWolf);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.moonLordLegs","premise");
#endif
w.Write(p.moonLordLegs);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.sticky","premise");
#endif
w.Write(p.sticky);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.dazed","premise");
#endif
w.Write(p.dazed);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.vortexDebuff","premise");
#endif
w.Write(p.vortexDebuff);}
            // Biome gates can change pursuit, vulnerability and despawn (for
            // example lacewings and sand elementals). Compare the actual zone
            // bitfields carried by the captured player, not just evil biomes.
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"(byte)p.zone1","premise");
#endif
w.Write((byte)p.zone1);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"(byte)p.zone2","premise");
#endif
w.Write((byte)p.zone2);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"(byte)p.zone3","premise");
#endif
w.Write((byte)p.zone3);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"(byte)p.zone4","premise");
#endif
w.Write((byte)p.zone4);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"(byte)p.zone5","premise");
#endif
w.Write((byte)p.zone5);}
            NativePlayerMotion.Write(w,p);
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"NativePlayerMotion.Mechanism(p)","premise");
#endif
w.Write(NativePlayerMotion.Mechanism(p));}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.CCed","premise");
#endif
w.Write(p.CCed);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.teleporting","premise");
#endif
w.Write(p.teleporting);}
            if(p.grappling!=null && p.grappling.Length>0 && p.grappling[0]>=0){{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.grappling.Length","premise");
#endif
w.Write(p.grappling.Length);}foreach(int slot in p.grappling){
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.grappling","premise");
#endif
w.Write(slot);}}
            // Mount changes retire a conditional continuation even if both
            // old/new states satisfy the same broad Complex movement flag.
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.mount.Active","premise");
#endif
w.Write(p.mount.Active);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.mount.Type","premise");
#endif
w.Write(p.mount.Type);}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.selectedItem","premise");
#endif
w.Write(p.selectedItem);}for(int i=0;i<p.inventory.Length;i++){var item=p.inventory[i];{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(w,"p.inventory["+i+"].type","premise");}catch(Exception error){AimDiagnostics.Missing("diagnostic-arguments",error);}
#endif
w.Write(item.type);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(w,"p.inventory["+i+"].prefix","premise");}catch(Exception error){AimDiagnostics.Missing("diagnostic-arguments",error);}
#endif
w.Write(item.prefix);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(w,"p.inventory["+i+"].normalizedStack","premise");}catch(Exception error){AimDiagnostics.Missing("diagnostic-arguments",error);}
#endif
w.Write(i==p.selectedItem?item.stack:item.stack>0?1:0);}}
            for(int slot=0;slot<10;slot++){var item=p.GetEffectiveArmor(slot);{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(w,"p.effectiveArmor["+slot+"].type","premise");}catch(Exception error){AimDiagnostics.Missing("diagnostic-arguments",error);}
#endif
w.Write(item.type);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(w,"p.effectiveArmor["+slot+"].prefix","premise");}catch(Exception error){AimDiagnostics.Missing("diagnostic-arguments",error);}
#endif
w.Write(item.prefix);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(w,"p.effectiveArmor["+slot+"].stack","premise");}catch(Exception error){AimDiagnostics.Missing("diagnostic-arguments",error);}
#endif
w.Write(item.stack);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(w,"p.effectiveArmor["+slot+"].accessory","premise");}catch(Exception error){AimDiagnostics.Missing("diagnostic-arguments",error);}
#endif
w.Write(item.accessory);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)try{AimDiagnostics.Field(w,"p.effectiveArmor["+slot+"].expertOnly","premise");}catch(Exception error){AimDiagnostics.Missing("diagnostic-arguments",error);}
#endif
w.Write(item.expertOnly);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.IsItemSlotUnlockedAndUsable(slot)","premise");
#endif
w.Write(p.IsItemSlotUnlockedAndUsable(slot));}}
            foreach(bool value in p.npcTypeNoAggro){
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.npcTypeNoAggro","premise");
#endif
w.Write(value);}
            // Proven ordinary clocks advance independently of movement and
            // equipment support. Other effect changes still retire the premise;
            // unsupported effects never become equivalent by omitting clocks.
            foreach(int value in p.buffType){
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.buffType","premise");
#endif
w.Write(value);}foreach(int value in p.buffTime){
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"p.buffTime","premise");
#endif
w.Write(value);}
        }
        internal static void Write(BinaryWriter writer,Frame frame)
        {
            writer.Write(frame.Tick);writer.Write(frame.HasState);
            if(frame.HasState)
            {
            writer.Write(frame.World);Pairs(writer,frame.Npcs,frame.NpcState);Pairs(writer,frame.Projectiles,frame.ProjectileState);
            foreach(float clock in frame.NpcShootClock)writer.Write(clock);
            foreach(ulong identity in frame.NpcIdentity)writer.Write(identity);
            foreach(ulong identity in frame.ProjectileIdentity)writer.Write(identity);
            Velocities(writer,frame.NpcVelocity,frame.NpcOldVelocity);Velocities(writer,frame.ProjectileVelocity,frame.ProjectileOldVelocity);
            WritePositions(writer,frame.NpcPositions);WritePositions(writer,frame.ProjectilePositions);
            writer.Write(frame.Players.Length);for(int i=0;i<frame.Players.Length;i++){writer.Write(frame.Players[i]);writer.Write(frame.PlayerPremise[i]);writer.Write(frame.PlayerConditional[i]);Vector(writer,frame.PlayerPosition[i]);Vector(writer,frame.PlayerVelocity[i]);}
            }
            Vector(writer,frame.NetOffset);writer.Write(frame.CanReceive);writer.Write(frame.CanHarm);writer.Write(frame.NewSegment);
        }
        internal static Frame Read(BinaryReader reader)
        {
            var frame=new Frame{Tick=reader.ReadInt64(),HasState=reader.ReadBoolean()};
            if(frame.HasState)
            {
            frame.World=reader.ReadUInt64();ReadPairs(reader,201,out frame.Npcs,out frame.NpcState);ReadPairs(reader,1001,out frame.Projectiles,out frame.ProjectileState);
            frame.NpcShootClock=new float[frame.Npcs.Length];for(int i=0;i<frame.NpcShootClock.Length;i++)frame.NpcShootClock[i]=reader.ReadSingle();
            frame.NpcIdentity=new ulong[frame.Npcs.Length];for(int i=0;i<frame.NpcIdentity.Length;i++)frame.NpcIdentity[i]=reader.ReadUInt64();
            frame.ProjectileIdentity=new ulong[frame.Projectiles.Length];for(int i=0;i<frame.ProjectileIdentity.Length;i++)frame.ProjectileIdentity[i]=reader.ReadUInt64();
            ReadVelocities(reader,frame.Npcs.Length,out frame.NpcVelocity,out frame.NpcOldVelocity);ReadVelocities(reader,frame.Projectiles.Length,out frame.ProjectileVelocity,out frame.ProjectileOldVelocity);
            frame.NpcPositions=ReadPositions(reader,frame.Npcs.Length);frame.ProjectilePositions=ReadPositions(reader,frame.Projectiles.Length);
            int count=reader.ReadInt32();if(count<0 || count>255)throw new InvalidDataException("Alignment player count.");
            frame.Players=new int[count];frame.PlayerPremise=new ulong[count];frame.PlayerConditional=new bool[count];frame.PlayerPosition=new Vector2[count];frame.PlayerVelocity=new Vector2[count];int prior=-1;
            for(int i=0;i<count;i++){int slot=reader.ReadInt32();if(slot<=prior || slot>=255)throw new InvalidDataException("Alignment player order.");prior=slot;frame.Players[i]=slot;frame.PlayerPremise[i]=reader.ReadUInt64();frame.PlayerConditional[i]=reader.ReadBoolean();frame.PlayerPosition[i]=Vector(reader);frame.PlayerVelocity[i]=Vector(reader);}
            }
            frame.NetOffset=Vector(reader);frame.CanReceive=reader.ReadBoolean();frame.CanHarm=reader.ReadBoolean();frame.NewSegment=reader.ReadBoolean();return frame;
        }
        internal static string Difference(Frame expected,Frame actual)
        {
            if(expected.Tick!=actual.Tick)return "tick";
            if(!expected.HasState || !actual.HasState)return "missing state proof";
            if(expected.World!=actual.World)return "world premise";
            bool[] required=expected.IsSample?null:expected.NpcRequired;
            bool[] requiredProjectiles=expected.IsSample?null:expected.ProjectileRequired;
            string changed=Different(expected.Npcs,expected.NpcIdentity,actual.Npcs,actual.NpcIdentity,"NPC identity");if(changed!=null)return changed;
            changed=Different(expected.Projectiles,expected.ProjectileIdentity,actual.Projectiles,actual.ProjectileIdentity,"Projectile identity");if(changed!=null)return changed;
            changed=Different(expected.Npcs,expected.NpcState,actual.Npcs,actual.NpcState,"NPC",required);if(changed!=null)return changed;
            if(expected.IsSample)for(int i=0;i<expected.NpcShootClock.Length;i++)
                if(new ClockBits{Value=expected.NpcShootClock[i]}.Bits!=new ClockBits{Value=actual.NpcShootClock[i]}.Bits)return "NPC sampled shooting clock slot="+expected.Npcs[i];
            changed=Different(expected.Projectiles,expected.ProjectileState,actual.Projectiles,actual.ProjectileState,"Projectile",requiredProjectiles);if(changed!=null)return changed;
            if(!SameVelocities(expected.NpcVelocity,actual.NpcVelocity,required) || !SameVelocities(expected.NpcOldVelocity,actual.NpcOldVelocity,required))return "NPC velocity";
            if(!SameVelocities(expected.ProjectileVelocity,actual.ProjectileVelocity,requiredProjectiles) || !SameVelocities(expected.ProjectileOldVelocity,actual.ProjectileOldVelocity,requiredProjectiles))return "Projectile velocity";
            if(!SamePositions(expected.NpcPositions,actual.NpcPositions,required))return "NPC position";
            if(!SamePositions(expected.ProjectilePositions,actual.ProjectilePositions,requiredProjectiles))return "Projectile position";
            changed=Different(expected.Players,expected.PlayerPremise,actual.Players,actual.PlayerPremise,"Player premise");if(changed!=null)return changed;
            // Unsupported special movement is a sampled conditional future,
            // not a claim to replay its exact player kinematics. Its inputs,
            // mechanism and qualifications above stay strict; NPC/projectile
            // current motion and branches have already been checked. The
            // owner still refreshes every available third update, bounds age
            // at 60 and never translates points or renews CaptureTick.
            for(int i=0;i<expected.Players.Length;i++)
            {if(expected.PlayerConditional[i]!=actual.PlayerConditional[i])return "Player mechanism "+expected.Players[i];if(!expected.PlayerConditional[i] && (!Near(expected.PlayerPosition[i],actual.PlayerPosition[i]) || !Near(expected.PlayerVelocity[i],actual.PlayerVelocity[i])))return "Player motion "+expected.Players[i];}
            return null;
        }
        private static void WorldPremise(BinaryWriter w)
        {
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.dayTime","world");
#endif
w.Write(Main.dayTime);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.time","world");
#endif
w.Write(Main.time);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.dayRate","world");
#endif
w.Write(Main.dayRate);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.GameMode","world");
#endif
w.Write(Main.GameMode);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.bloodMoon","world");
#endif
w.Write(Main.bloodMoon);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.eclipse","world");
#endif
w.Write(Main.eclipse);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.windSpeedCurrent","world");
#endif
w.Write(Main.windSpeedCurrent);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.windSpeedTarget","world");
#endif
w.Write(Main.windSpeedTarget);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.maxRaining","world");
#endif
w.Write(Main.maxRaining);}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.maxTilesX","world");
#endif
w.Write(Main.maxTilesX);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.maxTilesY","world");
#endif
w.Write(Main.maxTilesY);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.worldSurface","world");
#endif
w.Write(Main.worldSurface);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.rockLayer","world");
#endif
w.Write(Main.rockLayer);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.remixWorld","world");
#endif
w.Write(Main.remixWorld);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.getGoodWorld","world");
#endif
w.Write(Main.getGoodWorld);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.slimeRain","world");
#endif
w.Write(Main.slimeRain);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Main.invasionType","world");
#endif
w.Write(Main.invasionType);}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Terraria.Testing.DebugOptions.Shared_RandomizeProjectileSlots","world");
#endif
w.Write(Terraria.Testing.DebugOptions.Shared_RandomizeProjectileSlots);}{
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Terraria.Testing.DebugOptions.noLimits","world");
#endif
w.Write(Terraria.Testing.DebugOptions.noLimits);}
            // Equal NPC pages do not imply the same gravity source: the
            // original player step selects its refresh source by this index.
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"NPC.brainOfGravity","world");
#endif
w.Write(NPC.brainOfGravity);}
            {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.DetailActive)AimDiagnostics.Field(w,"Terraria.GameContent.Events.DD2Event.Ongoing","world");
#endif
w.Write(Terraria.GameContent.Events.DD2Event.Ongoing);}
            if(Terraria.GameContent.Events.DD2Event.Ongoing)NativeWorldSnapshot.WriteEvent(w);
        }
        private static bool Near(Vector2 a,Vector2 b){return Math.Abs(a.X-b.X)<=.002f && Math.Abs(a.Y-b.Y)<=.002f;}
        [StructLayout(LayoutKind.Explicit)]private struct ClockBits
        {[FieldOffset(0)]internal float Value;[FieldOffset(0)]internal int Bits;}
        // The complete original 120-update oracle admits 0.002px numerical
        // drift from x86 instrumentation/rounding. Acceptance uses the same
        // absolute bound on actual continuous values, including copied motion
        // history; it never adds an allowance per tick or moves old points.
        // Zero/sign velocity branches and all identity, AI, collision flags,
        // animation/rotation and network corrections remain strict.
        private static bool VelocityComponent(float a,float b)
        {return !float.IsNaN(a) && !float.IsInfinity(a) && !float.IsNaN(b) && !float.IsInfinity(b) && (a==0)==(b==0) && (a<0)==(b<0) && Math.Abs((double)a-b)<=.002;}
        private static Vector2[] Positions(Vector2 position,Vector2 old,Vector2[] trail)
        {var values=new Vector2[2+(trail?.Length??0)];values[0]=position;values[1]=old;if(trail!=null)Array.Copy(trail,0,values,2,trail.Length);return values;}
        private static bool SamePositions(Vector2[][] a,Vector2[][] b,bool[] required=null)
        {if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++){if(required!=null && !required[i])continue;if(a[i].Length!=b[i].Length)return false;for(int j=0;j<a[i].Length;j++)if(!Near(a[i][j],b[i][j]))return false;}return true;}
        private static void WritePositions(BinaryWriter w,Vector2[][] values)
        {foreach(var positions in values){w.Write(positions.Length);foreach(var p in positions)Vector(w,p);}}
        private static Vector2[][] ReadPositions(BinaryReader r,int count)
        {var result=new Vector2[count][];for(int i=0;i<count;i++){int length=r.ReadInt32();if(length<2 || length>4098)throw new InvalidDataException("Alignment motion history count.");result[i]=new Vector2[length];for(int j=0;j<length;j++)result[i][j]=Vector(r);}return result;}
        private static bool SameVelocities(Vector2[] a,Vector2[] b,bool[] required=null)
        {if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if((required==null || required[i]) && (!VelocityComponent(a[i].X,b[i].X) || !VelocityComponent(a[i].Y,b[i].Y)))return false;return true;}
        private static void Velocities(BinaryWriter w,Vector2[] current,Vector2[] prior)
        {for(int i=0;i<current.Length;i++){Vector(w,current[i]);Vector(w,prior[i]);}}
        private static void ReadVelocities(BinaryReader r,int count,out Vector2[] current,out Vector2[] prior)
        {current=new Vector2[count];prior=new Vector2[count];for(int i=0;i<count;i++){current[i]=Vector(r);prior[i]=Vector(r);}}
        private static string Different(int[] a,ulong[] av,int[] b,ulong[] bv,string label,bool[] required=null)
        {if(a.Length!=b.Length)return label+" count";for(int i=0;i<a.Length;i++)if(a[i]!=b[i] || (required==null || required[i]) && av[i]!=bv[i])return label+" slot="+a[i];return null;}
        private static void Pairs(BinaryWriter w,int[] slots,ulong[] values){w.Write(slots.Length);for(int i=0;i<slots.Length;i++){w.Write(slots[i]);w.Write(values[i]);}}
        private static void ReadPairs(BinaryReader r,int maximum,out int[] slots,out ulong[] values)
        {int count=r.ReadInt32();if(count<0 || count>maximum)throw new InvalidDataException("Alignment entity count.");slots=new int[count];values=new ulong[count];int prior=-1;for(int i=0;i<count;i++){int slot=r.ReadInt32();if(slot<=prior || slot>=maximum)throw new InvalidDataException("Alignment entity order.");prior=slot;slots[i]=slot;values[i]=r.ReadUInt64();}}
        private static void Vector(BinaryWriter w,Vector2 p){w.Write(p.X);w.Write(p.Y);}
        private static Vector2 Vector(BinaryReader r){float x=r.ReadSingle(),y=r.ReadSingle();if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y))throw new InvalidDataException("Alignment nonfinite position.");return new Vector2(x,y);}
        internal sealed class ValueHashWriter : BinaryWriter
        {
            // Hash the identical BinaryWriter wire bytes directly. Large
            // necessary chains otherwise round-trip every primitive through
            // BinaryWriter's scratch array and virtual Stream.Write per field.
            // This changes neither the observed schema nor equality tolerance.
            [StructLayout(LayoutKind.Explicit)] private struct Bits
            {[FieldOffset(0)]internal float Single;[FieldOffset(0)]internal uint UInt32;[FieldOffset(0)]internal double Double;[FieldOffset(0)]internal ulong UInt64;}
            private static readonly Encoding Utf8=new UTF8Encoding(false,true);
            internal ValueHashWriter():base(Stream.Null){}
#if JMR_AIM_DIAGNOSTICS
            internal AimDiagnosticTape DiagnosticTape;
#endif

            internal ulong Hash;internal void Reset(){Hash=14695981039346656037UL;
#if JMR_AIM_DIAGNOSTICS
                try{DiagnosticTape=AimDiagnostics.DetailActive?new AimDiagnosticTape():null;}catch(Exception error){DiagnosticTape=null;AimDiagnostics.Missing("tape-allocation",error);}
#endif
}
            public override void Write(byte value){
#if JMR_AIM_DIAGNOSTICS
                DiagnosticTape?.Add("u8",value);
#endif
unchecked{Hash=(Hash^value)*1099511628211UL;}}
            public override void Write(bool value){Write((byte)(value?1:0));}
            public override void Write(sbyte value){Write(unchecked((byte)value));}
            public override void Write(short value){Write(unchecked((ushort)value));}
            public override void Write(ushort value)
            {
#if JMR_AIM_DIAGNOSTICS
                DiagnosticTape?.Add("u16",value);
#endif
unchecked{ulong hash=Hash;hash=(hash^(byte)value)*1099511628211UL;Hash=(hash^(byte)(value>>8))*1099511628211UL;}}
            public override void Write(int value){Write(unchecked((uint)value));}
            public override void Write(uint value)
            {
                #if JMR_AIM_DIAGNOSTICS
                DiagnosticTape?.Add("u32",value);
#endif
                // Four zero bytes still participate in the exact FNV stream:
                // their xor operations are identities, so prime^4 (mod 2^64)
                // replaces four multiplies. Immune/timer arrays often use zero;
                // this does not omit fields or weaken historical equality.
                unchecked{if(value==0){Hash*=11527715348014283921UL;return;}ulong hash=Hash;hash=(hash^(byte)value)*1099511628211UL;hash=(hash^(byte)(value>>8))*1099511628211UL;hash=(hash^(byte)(value>>16))*1099511628211UL;Hash=(hash^(byte)(value>>24))*1099511628211UL;}
            }
            public override void Write(long value){Write(unchecked((ulong)value));}
            public override void Write(ulong value){Write((uint)value);Write((uint)(value>>32));}
            public override void Write(float value){Write(new Bits{Single=value}.UInt32);}
            public override void Write(double value){Write(new Bits{Double=value}.UInt64);}
            public override void Write(byte[] value){Write(value,0,value.Length);}
            public override void Write(byte[] value,int offset,int count)
            {
#if JMR_AIM_DIAGNOSTICS
                for(int i=0;i<count;i++)DiagnosticTape?.Add("bytes",value[offset+i]);
#endif
unchecked{ulong hash=Hash;for(int i=0;i<count;i++)hash=(hash^value[offset+i])*1099511628211UL;Hash=hash;}}
            public override void Write(string value)
            {byte[] bytes=Utf8.GetBytes(value);uint length=(uint)bytes.Length;while(length>=128){Write((byte)(length|128));length>>=7;}Write((byte)length);Write(bytes);}
            public override void Write(decimal value){throw new NotSupportedException("Unknown alignment primitive.");}
            public override void Write(char value){throw new NotSupportedException("Unknown alignment primitive.");}
            public override void Write(char[] value){throw new NotSupportedException("Unknown alignment primitive.");}
            public override void Write(char[] value,int offset,int count){throw new NotSupportedException("Unknown alignment primitive.");}
        }
    }
}
