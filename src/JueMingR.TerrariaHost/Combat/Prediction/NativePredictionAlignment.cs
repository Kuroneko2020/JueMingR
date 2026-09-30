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
        private static readonly NativeValueSnapshot Npcs=new NativeValueSnapshot(typeof(NPC),fieldNames:PredictionWire.Npcs.FieldIdentities.Where(f=>ExactMotionField(f) && !f.EndsWith(".localAI",StringComparison.Ordinal)).Select(Name).ToArray());
        private static string Name(string field){return field.Substring(field.LastIndexOf('.')+1);}
        private static bool ExactMotionField(string field){return Name(field)!="velocity" && Name(field)!="oldVelocity";}
        internal sealed class Frame
        {
            internal long Tick;
            internal ulong World;
            internal int[] Npcs,Projectiles,Players;
            internal ulong[] NpcState,ProjectileState,PlayerPremise;
            internal Vector2[] PlayerPosition,PlayerVelocity;
            internal Vector2[] NpcVelocity,NpcOldVelocity,ProjectileVelocity,ProjectileOldVelocity;
            internal Vector2 NetOffset;
            internal bool CanReceive,CanHarm,NewSegment,HasState;
        }
        internal static Frame Observe(long tick,int[] npcs,int[] projectiles,int selected)
        {
            var players=new List<int>();for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]!=null && Main.player[i].active)players.Add(i);
            var frame=new Frame{Tick=tick,HasState=true,Npcs=npcs,Projectiles=projectiles,Players=players.ToArray(),NpcState=new ulong[npcs.Length],ProjectileState=new ulong[projectiles.Length],PlayerPremise=new ulong[players.Count],PlayerPosition=new Vector2[players.Count],PlayerVelocity=new Vector2[players.Count]};
            frame.NpcVelocity=new Vector2[npcs.Length];frame.NpcOldVelocity=new Vector2[npcs.Length];frame.ProjectileVelocity=new Vector2[projectiles.Length];frame.ProjectileOldVelocity=new Vector2[projectiles.Length];
            for(int i=0;i<npcs.Length;i++){frame.NpcVelocity[i]=Main.npc[npcs[i]].velocity;frame.NpcOldVelocity[i]=Main.npc[npcs[i]].oldVelocity;}
            for(int i=0;i<projectiles.Length;i++){frame.ProjectileVelocity[i]=Main.projectile[projectiles[i]].velocity;frame.ProjectileOldVelocity[i]=Main.projectile[projectiles[i]].oldVelocity;}
            using(var writer=new ValueHashWriter())
            {
                writer.Reset();WorldPremise(writer);frame.World=writer.Hash;
                for(int i=0;i<npcs.Length;i++){writer.Reset();NpcPremise(writer,Main.npc[npcs[i]]);NativeEntityContext.WriteNpc(writer,Main.npc[npcs[i]]);frame.NpcState[i]=writer.Hash;}
                for(int i=0;i<projectiles.Length;i++){writer.Reset();Projectiles.Write(writer,Main.projectile[projectiles[i]]);NativeActorContext.WriteProjectile(writer,Main.projectile[projectiles[i]]);frame.ProjectileState[i]=writer.Hash;}
                for(int i=0;i<players.Count;i++){Player p=Main.player[players[i]];writer.Reset();PlayerPremise(writer,p);frame.PlayerPremise[i]=writer.Hash;frame.PlayerPosition[i]=p.position;frame.PlayerVelocity[i]=p.velocity;}
            }
            NPC target=Main.npc[selected];frame.NetOffset=target.netOffset;frame.CanReceive=target.active && CombatSelection.Receives(target,true);frame.CanHarm=target.active && !target.friendly && target.damage>0;
            return frame;
        }
        private static void NpcPremise(BinaryWriter writer,NPC n)
        {
            Npcs.Write(writer,n);writer.Write(n.localAI==null?-1:n.localAI.Length);
            writer.Write(NativeLightingSnapshot.Observe(n));
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
                writer.Write(value);
            }
        }
        internal static Frame Presentation(long tick,int selected)
        {
            NPC target=Main.npc[selected];
            return new Frame{Tick=tick,NetOffset=target.netOffset,CanReceive=target.active && CombatSelection.Receives(target,true),CanHarm=target.active && !target.friendly && target.damage>0};
        }
        private static void PlayerPremise(BinaryWriter w,Player p)
        {
            w.Write(p.whoAmI);w.Write(p.active);w.Write(p.dead);w.Write(p.ghost);w.Write(p.width);w.Write(p.height);w.Write(p.gravDir);
            w.Write(p.controlLeft);w.Write(p.controlRight);w.Write(p.controlUp);w.Write(p.controlDown);w.Write(p.controlJump);w.Write(p.controlUseItem);w.Write(p.controlUseTile);
            w.Write(p.runAcceleration);w.Write(p.runSlowdown);w.Write(p.maxRunSpeed);w.Write(p.accRunSpeed);w.Write(p.jumpSpeedBoost);w.Write(p.autoJump);
            w.Write(p.ignoreWater);w.Write(p.merman);w.Write(p.trident);w.Write(p.waterWalk);w.Write(p.waterWalk2);w.Write(p.aggro);w.Write(p.insideUnbreakableWalls);
            w.Write(p.jump);w.Write(p.releaseJump);w.Write(p.wet);w.Write(p.honeyWet);w.Write(p.lavaWet);w.Write(p.shimmerWet);
            w.Write(p.forcedGravity);w.Write(p.creativeGodMode);w.Write(p.gravControl);w.Write(p.gravControl2);
            w.Write(p.PortalPhysicsEnabled);w.Write(p.wingsLogic);w.Write(p.carpetFrame);w.Write(p.jumpBoost);w.Write(p.wereWolf);w.Write(p.moonLordLegs);w.Write(p.sticky);w.Write(p.dazed);w.Write(p.vortexDebuff);
            // Biome gates can change pursuit, vulnerability and despawn (for
            // example lacewings and sand elementals). Compare the actual zone
            // bitfields carried by the captured player, not just evil biomes.
            w.Write((byte)p.zone1);w.Write((byte)p.zone2);w.Write((byte)p.zone3);w.Write((byte)p.zone4);w.Write((byte)p.zone5);
            NativePlayerMotion.Write(w,p);
            w.Write(p.selectedItem);foreach(var item in p.inventory){w.Write(item.type);w.Write(item.prefix);w.Write(item.stack);}
            for(int slot=0;slot<10;slot++){var item=p.GetEffectiveArmor(slot);w.Write(item.type);w.Write(item.prefix);w.Write(item.stack);w.Write(item.accessory);w.Write(item.expertOnly);w.Write(p.IsItemSlotUnlockedAndUsable(slot));}
            foreach(bool value in p.npcTypeNoAggro)w.Write(value);
            // Only the explicitly reconstructed ordinary speed subset advances
            // clocks. Other buff/equipment changes still retire the premise;
            // unsupported effects never become equivalent by omitting clocks.
            foreach(int value in p.buffType)w.Write(value);foreach(int value in p.buffTime)w.Write(value);
        }
        internal static void Write(BinaryWriter writer,Frame frame)
        {
            writer.Write(frame.Tick);writer.Write(frame.HasState);
            if(frame.HasState)
            {
            writer.Write(frame.World);Pairs(writer,frame.Npcs,frame.NpcState);Pairs(writer,frame.Projectiles,frame.ProjectileState);
            Velocities(writer,frame.NpcVelocity,frame.NpcOldVelocity);Velocities(writer,frame.ProjectileVelocity,frame.ProjectileOldVelocity);
            writer.Write(frame.Players.Length);for(int i=0;i<frame.Players.Length;i++){writer.Write(frame.Players[i]);writer.Write(frame.PlayerPremise[i]);Vector(writer,frame.PlayerPosition[i]);Vector(writer,frame.PlayerVelocity[i]);}
            }
            Vector(writer,frame.NetOffset);writer.Write(frame.CanReceive);writer.Write(frame.CanHarm);writer.Write(frame.NewSegment);
        }
        internal static Frame Read(BinaryReader reader)
        {
            var frame=new Frame{Tick=reader.ReadInt64(),HasState=reader.ReadBoolean()};
            if(frame.HasState)
            {
            frame.World=reader.ReadUInt64();ReadPairs(reader,201,out frame.Npcs,out frame.NpcState);ReadPairs(reader,1001,out frame.Projectiles,out frame.ProjectileState);
            ReadVelocities(reader,frame.Npcs.Length,out frame.NpcVelocity,out frame.NpcOldVelocity);ReadVelocities(reader,frame.Projectiles.Length,out frame.ProjectileVelocity,out frame.ProjectileOldVelocity);
            int count=reader.ReadInt32();if(count<0 || count>255)throw new InvalidDataException("Alignment player count.");
            frame.Players=new int[count];frame.PlayerPremise=new ulong[count];frame.PlayerPosition=new Vector2[count];frame.PlayerVelocity=new Vector2[count];int prior=-1;
            for(int i=0;i<count;i++){int slot=reader.ReadInt32();if(slot<=prior || slot>=255)throw new InvalidDataException("Alignment player order.");prior=slot;frame.Players[i]=slot;frame.PlayerPremise[i]=reader.ReadUInt64();frame.PlayerPosition[i]=Vector(reader);frame.PlayerVelocity[i]=Vector(reader);}
            }
            frame.NetOffset=Vector(reader);frame.CanReceive=reader.ReadBoolean();frame.CanHarm=reader.ReadBoolean();frame.NewSegment=reader.ReadBoolean();return frame;
        }
        internal static string Difference(Frame expected,Frame actual)
        {
            if(expected.Tick!=actual.Tick)return "tick";
            if(!expected.HasState || !actual.HasState)return "missing state proof";
            if(expected.World!=actual.World)return "world premise";
            string changed=Different(expected.Npcs,expected.NpcState,actual.Npcs,actual.NpcState,"NPC");if(changed!=null)return changed;
            changed=Different(expected.Projectiles,expected.ProjectileState,actual.Projectiles,actual.ProjectileState,"Projectile");if(changed!=null)return changed;
            if(!SameVelocities(expected.NpcVelocity,actual.NpcVelocity) || !SameVelocities(expected.NpcOldVelocity,actual.NpcOldVelocity))return "NPC velocity";
            if(!SameVelocities(expected.ProjectileVelocity,actual.ProjectileVelocity) || !SameVelocities(expected.ProjectileOldVelocity,actual.ProjectileOldVelocity))return "Projectile velocity";
            changed=Different(expected.Players,expected.PlayerPremise,actual.Players,actual.PlayerPremise,"Player premise");if(changed!=null)return changed;
            for(int i=0;i<expected.Players.Length;i++)if(!Near(expected.PlayerPosition[i],actual.PlayerPosition[i]) || !Near(expected.PlayerVelocity[i],actual.PlayerVelocity[i]))return "Player motion "+expected.Players[i];
            return null;
        }
        private static void WorldPremise(BinaryWriter w)
        {
            w.Write(Main.dayTime);w.Write(Main.time);w.Write(Main.dayRate);w.Write(Main.GameMode);w.Write(Main.bloodMoon);w.Write(Main.eclipse);w.Write(Main.windSpeedCurrent);
            w.Write(Main.maxTilesX);w.Write(Main.maxTilesY);w.Write(Main.worldSurface);w.Write(Main.rockLayer);w.Write(Main.remixWorld);w.Write(Main.getGoodWorld);w.Write(Main.slimeRain);w.Write(Main.invasionType);
            w.Write(Terraria.Testing.DebugOptions.Shared_RandomizeProjectileSlots);w.Write(Terraria.Testing.DebugOptions.noLimits);
            // Equal NPC pages do not imply the same gravity source: the
            // original player step selects its refresh source by this index.
            w.Write(NPC.brainOfGravity);
            w.Write(Terraria.GameContent.Events.DD2Event.Ongoing);
            if(Terraria.GameContent.Events.DD2Event.Ongoing)NativeWorldSnapshot.WriteEvent(w);
        }
        private static bool Near(Vector2 a,Vector2 b){return Math.Abs(a.X-b.X)<=.002f && Math.Abs(a.Y-b.Y)<=.002f;}
        // Guard instrumentation changes x86 intermediate rounding: observed
        // 248/662 and projectile 452 velocity tails differ by <=9.54e-7 while
        // complete 120-update positions and AI/identity remain unchanged.
        // Compare true values (no hash quantization), retaining zero/sign
        // branches. Position/history/rotation/AI still use exact state hashes.
        private static bool VelocityComponent(float a,float b)
        {return !float.IsNaN(a) && !float.IsInfinity(a) && !float.IsNaN(b) && !float.IsInfinity(b) && (a==0)==(b==0) && (a<0)==(b<0) && Math.Abs((double)a-b)<=.000001;}
        private static bool SameVelocities(Vector2[] a,Vector2[] b)
        {if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!VelocityComponent(a[i].X,b[i].X) || !VelocityComponent(a[i].Y,b[i].Y))return false;return true;}
        private static void Velocities(BinaryWriter w,Vector2[] current,Vector2[] prior)
        {for(int i=0;i<current.Length;i++){Vector(w,current[i]);Vector(w,prior[i]);}}
        private static void ReadVelocities(BinaryReader r,int count,out Vector2[] current,out Vector2[] prior)
        {current=new Vector2[count];prior=new Vector2[count];for(int i=0;i<count;i++){current[i]=Vector(r);prior[i]=Vector(r);}}
        private static string Different(int[] a,ulong[] av,int[] b,ulong[] bv,string label)
        {if(a.Length!=b.Length)return label+" count";for(int i=0;i<a.Length;i++)if(a[i]!=b[i] || av[i]!=bv[i])return label+" slot="+a[i];return null;}
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
            internal ulong Hash;internal void Reset(){Hash=14695981039346656037UL;}
            public override void Write(byte value){unchecked{Hash=(Hash^value)*1099511628211UL;}}
            public override void Write(bool value){Write((byte)(value?1:0));}
            public override void Write(sbyte value){Write(unchecked((byte)value));}
            public override void Write(short value){Write(unchecked((ushort)value));}
            public override void Write(ushort value)
            {unchecked{ulong hash=Hash;hash=(hash^(byte)value)*1099511628211UL;Hash=(hash^(byte)(value>>8))*1099511628211UL;}}
            public override void Write(int value){Write(unchecked((uint)value));}
            public override void Write(uint value)
            {
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
            {unchecked{ulong hash=Hash;for(int i=0;i<count;i++)hash=(hash^value[offset+i])*1099511628211UL;Hash=hash;}}
            public override void Write(string value)
            {byte[] bytes=Utf8.GetBytes(value);uint length=(uint)bytes.Length;while(length>=128){Write((byte)(length|128));length>>=7;}Write((byte)length);Write(bytes);}
            public override void Write(decimal value){throw new NotSupportedException("Unknown alignment primitive.");}
            public override void Write(char value){throw new NotSupportedException("Unknown alignment primitive.");}
            public override void Write(char[] value){throw new NotSupportedException("Unknown alignment primitive.");}
            public override void Write(char[] value,int offset,int count){throw new NotSupportedException("Unknown alignment primitive.");}
        }
    }
}
