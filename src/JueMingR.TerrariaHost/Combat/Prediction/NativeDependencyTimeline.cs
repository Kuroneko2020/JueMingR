using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Dependency motion and identity belong to the same simulated instant as
    // the selected path. Active-set absence represents the end of that actor;
    // a recycled slot has a different generation/key. These are alignment
    // premises, not additional selectable or rendered combat targets.
    internal sealed class NativeDependencyTimeline : IDisposable
    {
        private readonly MemoryStream bytes=new MemoryStream();
        private readonly BinaryWriter writer;
        internal NativeDependencyTimeline(){writer=new BinaryWriter(bytes);}
        internal void Record(int step)
        {
            int npcs=0,projectiles=0;
            for(int i=0;i<=Main.maxNPCs;i++)if(Known(Main.npc[i]))npcs++;
            for(int i=0;i<=Main.maxProjectiles;i++)if(Known(Main.projectile[i]))projectiles++;
            if(bytes.Length+12L+49L*npcs+44L*projectiles>PredictionWire.MaximumBytes)
                throw new PredictionCapacityException("Dependency timeline exceeds result capacity.");
            writer.Write(step);writer.Write(npcs);
            for(int i=0;i<=Main.maxNPCs;i++)
            {
                NPC n=Main.npc[i];if(!Known(n))continue;
                writer.Write(i);writer.Write(n.generation);writer.Write(n.type);writer.Write(n.netID);
                Motion(n.position,n.velocity);writer.Write(n.life);foreach(float value in n.ai)writer.Write(value);
            }
            writer.Write(projectiles);
            for(int i=0;i<=Main.maxProjectiles;i++)
            {
                Projectile p=Main.projectile[i];if(!Known(p))continue;
                writer.Write(i);writer.Write((uint)p.key);writer.Write(p.type);
                Motion(p.position,p.velocity);writer.Write(p.timeLeft);foreach(float value in p.ai)writer.Write(value);
            }
        }
        internal void WriteTo(BinaryWriter destination)
        {
            writer.Flush();
            if(destination.BaseStream.Length+4+bytes.Length+32>PredictionWire.MaximumBytes)
                throw new PredictionCapacityException("Combined prediction result exceeds capacity.");
            destination.Write((int)bytes.Length);destination.Write(bytes.GetBuffer(),0,(int)bytes.Length);
        }
        private static bool Known(NPC n){return n.active && NativeEntityDirectory.CanAdvance(n);}
        private static bool Known(Projectile p){return p.active && NativeEntityDirectory.CanAdvance(p);}
        private void Motion(Vector2 position,Vector2 velocity)
        {
            if(!Finite(position.X)||!Finite(position.Y)||!Finite(velocity.X)||!Finite(velocity.Y))throw new InvalidDataException("Nonfinite dependency motion.");
            writer.Write(position.X);writer.Write(position.Y);writer.Write(velocity.X);writer.Write(velocity.Y);
        }
        private static bool Finite(float value){return !float.IsNaN(value) && !float.IsInfinity(value);}
        public void Dispose(){writer.Dispose();bytes.Dispose();}
    }
}
