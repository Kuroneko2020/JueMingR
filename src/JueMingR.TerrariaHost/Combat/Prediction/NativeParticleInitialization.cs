using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.Graphics.Renderers;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Only the three audited CURRENT initialization paths are represented.
    // Host capture reads an already loaded texture; it never requests content,
    // touches a particle pool or constructs a particle. Restored metadata and
    // scratch state belong solely to the serial private Worker, not live Main.
    internal static class NativeParticleInitialization
    {
        private const int Version=1,SporeProjectile=228,MaximumDimension=32768;
        private static bool restored,sporeLoaded;
        private static int sporeFrames;
        private static LittleFlyingCritterParticle fly;
        internal static void Clear(){restored=sporeLoaded=false;sporeFrames=0;}
        internal static void Write(BinaryWriter writer)
        {
            var assets=TextureAssets.Projectile;
            var asset=assets!=null && assets.Length>SporeProjectile?assets[SporeProjectile]:null;
            bool present=asset!=null,loaded=present && asset.IsLoaded;
            int width=loaded?asset.Value.Width:0,height=loaded?asset.Value.Height:0;
            int frames=Main.projFrames!=null && Main.projFrames.Length>SporeProjectile?Main.projFrames[SporeProjectile]:0;
            Validate(present,loaded,width,height,frames);
            writer.Write(Version);writer.Write(present);writer.Write(loaded);writer.Write(width);writer.Write(height);writer.Write(frames);
        }
        internal static void Read(BinaryReader reader)
        {
            Clear();
            if(reader.ReadInt32()!=Version)throw new InvalidDataException("Particle initialization page version.");
            bool present=Boolean(reader),loaded=Boolean(reader);int width=reader.ReadInt32(),height=reader.ReadInt32(),frames=reader.ReadInt32();
            Validate(present,loaded,width,height,frames);
            // Allocate outside original actor callbacks: an allocation failure
            // remains the Worker's real OOM, rather than an exception swallowed
            // by native UpdateNPC. Fetch/Prepare never publish this scratch.
            if(fly==null)fly=new LittleFlyingCritterParticle();
            sporeLoaded=loaded;sporeFrames=frames;restored=true;
        }
        internal static void Consume(ParticleOrchestraType type,ParticleOrchestraSettings settings)
        {
            if(type!=ParticleOrchestraType.SporeCloud && type!=ParticleOrchestraType.PooFly && type!=ParticleOrchestraType.SilverBulletSparkle)return;
            if(!restored)throw new InvalidDataException("Particle initialization lacks a private captured premise.");
            switch(type)
            {
                case ParticleOrchestraType.SporeCloud:
                    if(!sporeLoaded || sporeFrames==0)throw new InvalidDataException("Spore initialization requires observed loaded projectile metadata and frames.");
                    // Native Request/Fetch has no RNG or capacity early return.
                    // SetTypeInfo's current RandomizeFrame draws this ACTUAL
                    // frame count after the already evaluated caller branch.
                    Main.rand.Next(sporeFrames);
                    break;
                case ParticleOrchestraType.PooFly:
                    // In the authenticated .8 image CountParticlesInUse starts
                    // count/index at zero, tests index<count before its loop,
                    // and returns zero for EVERY pool state. Its <=50 branch
                    // therefore has no threshold draw. Request/Fetch has no
                    // RNG; retain actual Prepare, not a fixed number of Nexts.
                    fly.FetchFromPool();fly.Prepare(LittleFlyingCritterParticle.FlyType.RegularFly,settings.PositionInWorld,300);
                    break;
                case ParticleOrchestraType.SilverBulletSparkle:
                    // The three scalar draws precede the original circular
                    // helper; using that helper preserves its real RNG rule.
                    // Their values affect only the deliberately isolated art.
                    Main.rand.NextFloat();Main.rand.NextFloat();Main.rand.NextFloat();Main.rand.NextVector2Circular(4f,4f);
                    break;
            }
        }
        private static bool Boolean(BinaryReader reader)
        {byte value=reader.ReadByte();if(value>1)throw new InvalidDataException("Particle initialization boolean.");return value!=0;}
        private static void Validate(bool present,bool loaded,int width,int height,int frames)
        {
            // Reuse NativeAssetSnapshot.Read's 32768 dimension domain for
            // corrupt-page rejection; frames have no invented upper cap.
            // Unloaded/missing is a valid observation; only an
            // actual Spore invocation needs to reject that unknown resource.
            if(loaded && (!present || width<1 || height<1 || width>MaximumDimension || height>MaximumDimension)
                || !loaded && (width!=0 || height!=0) || frames<0)
                throw new InvalidDataException("Particle initialization metadata.");
        }
    }
}
