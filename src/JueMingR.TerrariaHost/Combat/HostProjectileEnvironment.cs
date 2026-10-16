using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // A finite uniform-liquid continuation. Unsupported transitions, shimmer,
    // native water transformations and changing wind gates retire Contact;
    // absence of a solution never changes the actual projectile/environment.
    internal sealed partial class HostProjectileEnvironment
    {
        internal struct State
        {
            private readonly Player windPlayer;private readonly bool playerWind,windRelevant,wind,goodWorld,noTraps,tenthAnniversary;private readonly float speed,strength;private readonly double surface;
            internal State(bool capture,bool windRelevant=true):this(capture,windRelevant,null){}
            internal State(bool capture,bool windRelevant,Player player){windPlayer=player;playerWind=PlayerWind(player);this.windRelevant=windRelevant;wind=Main.windPhysics;goodWorld=Main.getGoodWorld;noTraps=Main.noTrapsWorld;tenthAnniversary=Main.tenthAnniversaryWorld;speed=Main.windSpeedCurrent;strength=Main.windPhysicsStrength;surface=Main.worldSurface;}
            // Projectile wind reads speed/strength only behind its real gate.
            // Hand owners also capture the independent player wind-push gate;
            // an unqualified family owner retains its conservative dependency.
            private static bool PlayerWind(Player player){return player!=null && player.windPushed && player.CanBePushedByWind();}
            internal bool Current {get{return (windPlayer==null || playerWind==PlayerWind(windPlayer)) && (!windRelevant || wind==Main.windPhysics && (windPlayer!=null && !playerWind && !wind || speed==Main.windSpeedCurrent && strength==Main.windPhysicsStrength)) && goodWorld==Main.getGoodWorld && noTraps==Main.noTrapsWorld && tenthAnniversary==Main.tenthAnniversaryWorld && surface==Main.worldSurface;}}
        }
        // The locked original ShouldUseWindPhysics first gates on Main.windPhysics.
        // A dependency lease must also survive that gate toggling, so inspect
        // its exact type override/style rule without mutating the live gate.
        private static bool PotentialWind(Projectile body)
        {
            if(body==null)return true;
            bool? immunity=ProjectileID.Sets.WindPhysicsImmunity[body.type];
            if(immunity.HasValue)return !immunity.Value;
            switch(body.aiStyle)
            {case 1:case 2:case 8:case 10:case 14:case 16:case 17:case 21:case 24:case 28:case 29:case 32:case 33:case 34:case 35:case 49:case 72:case 93:case 96:case 106:return true;}
            return false;
        }
        internal static bool HandWindRelevant(Player player,Item item)
        {
            if(item==null || player!=null && player.windPushed && player.CanBePushedByWind())return true;
            if(item.shoot<=0 && HostSwingAttack.Handles(item))return false;
            // Other mechanisms can choose ammo, transforms or autonomous
            // births. Keep their broad lease until those dependencies are
            // audited. Spear/ordinary-whip modeled members are explicit.
            if(!HostMeleeAttack.KnownSpearParent(item.shoot) && !HostMeleeAttack.KnownWhipParent(item.shoot))return true;
            Projectile body;if(!ContentSamples.ProjectilesByType.TryGetValue(item.shoot,out body) || PotentialWind(body))return true;
            int child=HostMeleeAttack.DerivedSpearChild(item.shoot);
            return child!=0 && (!ContentSamples.ProjectilesByType.TryGetValue(child,out body) || PotentialWind(body));
        }
        private readonly Projectile sample;
        private readonly PredictionTerrain terrain;
        private readonly bool windRelevant,wind;
        private readonly float windSpeed,windStrength;
        private readonly double surface;
        internal readonly float Scale,WindX;
        private HostProjectileEnvironment(Projectile sample,PredictionTerrain terrain,float scale,float windX)
        {
            this.sample=sample;this.terrain=terrain;Scale=scale;WindX=windX;windRelevant=PotentialWind(sample);wind=Main.windPhysics;windSpeed=Main.windSpeedCurrent;windStrength=Main.windPhysicsStrength;surface=Main.worldSurface;
        }
        internal bool Unchanged {get{return (!windRelevant || wind==Main.windPhysics && (!wind || windSpeed==Main.windSpeedCurrent && windStrength==Main.windPhysicsStrength)) && surface==Main.worldSurface;}}
        internal static AttackContact Solve(Projectile sample,PredictionTerrain terrain,Vector2 origin,AttackMotion motion,NpcTrajectory timeline,int age,int delay,int firstTick,Func<int,bool> receives,out HostProjectileEnvironment environment,Func<int,int,float,float,bool> damagePassage=null,Func<int,int,float,float,bool> beforeMotion=null)
        {
            environment=null;float scale;if(!Factor(sample,terrain,origin.X,origin.Y,out scale))return null;
            // At most two uniform gate cases. Each actual candidate replay
            // checks the native gate before applying its chosen impulse; the
            // zero-velocity basis used for inversion is never a game query.
            float impulse=sample.ShouldUseWindPhysics()?Main.windSpeedCurrent*Main.windPhysicsStrength:0;
            for(int mode=0;mode<(impulse==0?1:2);mode++)
            {var candidate=new HostProjectileEnvironment(sample,terrain,scale,mode==0?0:impulse);var contact=AttackIntercept.Solve(origin.X,origin.Y,motion.WithEnvironment(scale,candidate.WindX),timeline,age,delay,candidate.Passage,firstTick,receives,damagePassage,beforeMotion);if(contact!=null){environment=candidate;return contact;}}
            return null;
        }
        private static bool Factor(Projectile sample,PredictionTerrain terrain,float x,float y,out float scale)
        {
            scale=1;bool wet;byte kind;if(!terrain.ProjectileWet(x,y,sample.width,sample.height,out wet,out kind) || wet && kind==3 || sample.shimmerWet)return false;
            if(wet && sample.type==1114)return false; // This crystal variant dies in water in AI_024.
            if(!wet || sample.ignoreWater || sample.type==242 || sample.type==302 || sample.type==638)return true;
            // These native AI/type changes require another phase model. Lava
            // and tileCollide=false also have distinct movement/exit rules.
            // FireArrow's wet TransformType(1) changes only type, with the
            // same AI001 motor and collision body. Uniform water/honey thus
            // keeps exact movement even though the hit no longer applies fire.
            if(kind==1 || sample.type==34 || !sample.tileCollide)return false;
            scale=kind==2?.25f:.5f;return true;
        }
        internal static bool Impulse(Projectile sample,PredictionTerrain terrain,float x,float y,float vx,out float impulse)
        {
            impulse=0;
            if(!sample.ShouldUseWindPhysics() || y>=Main.worldSurface*16 || Math.Abs(vx)>=16)return true;
            float delta=Main.windSpeedCurrent*Main.windPhysicsStrength;
            if(delta==0 || !(vx>0 && Main.windSpeedCurrent<0 || vx<0 && Main.windSpeedCurrent>0 || Math.Abs(vx)<Math.Abs(delta)*180))return true;
            PredictionTile tile;PredictionStop stop;if(!terrain.Tile((int)x/16,(int)y/16,out tile,out stop))return false;
            if(tile.Wall==0)impulse=delta;return true;
        }
        internal bool Passage(float x,float y,float nx,float ny,float width,float height)
        {
            float scale,impulse,vx=(nx-x)/Scale,vy=(ny-y)/Scale;
            if(!Unchanged || !Factor(sample,terrain,x,y,out scale) || scale!=Scale || !Impulse(sample,terrain,x,y,vx-WindX,out impulse) || impulse!=WindX)return false;
            // Damage geometry never relaxes the native movement body. The
            // shared native phase includes segmented dry motion and slopes;
            // an inverse straight path is usable only when that phase leaves
            // both its stored velocity and final displacement unchanged.
            if(!sample.tileCollide)return true;
            Vector2 half=new Vector2(sample.width,sample.height)*.5f,originalPosition=new Vector2(x,y)-half,originalVelocity=new Vector2(vx,vy),position=originalPosition,velocity=originalVelocity,wetVelocity;bool hit;
            if(!terrain.ProjectileTileMotion(sample,ref position,ref velocity,Scale<1,Scale==.25f,out wetVelocity,out hit) || hit)return false;
            // The no-contact fact lives in native top-left coordinates. Do
            // not reconstruct a center with a separate float addition order
            // to impose a second passage gate after native hit=false.
            return position==originalPosition && velocity==originalVelocity;
        }
        internal static bool Dry(Projectile shot,PredictionTerrain terrain,float x,float y,float vx)
        {
            float scale,impulse;return !(shot.type==34 && shot.wet && !shot.lavaWet) && Factor(shot,terrain,x,y,out scale) && scale==1 && Impulse(shot,terrain,x,y,vx,out impulse) && impulse==0;
        }
    }
}
