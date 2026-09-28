using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal struct CombatShape
    {
        internal Vector2 A,B;internal float Width;internal int Category;internal bool Line,Approximate;
    }
    internal sealed class CombatShapeSample
    {
        internal readonly CombatShape[] Shapes=new CombatShape[160];
        internal int Count,Owner,Identity,Type,Sequence;internal uint Tick;internal long Session;internal bool Overflow;
        internal object Token;
        internal void Rectangle(Rectangle box,int category,bool approximate=false)
        {Add(new CombatShape{A=new Vector2(box.X,box.Y),B=new Vector2(box.Right,box.Bottom),Category=category,Approximate=approximate});}
        internal void Line(Vector2 start,Vector2 end,float width,int category,bool approximate=false)
        {Add(new CombatShape{A=start,B=end,Width=width,Line=true,Category=category,Approximate=approximate});}
        private void Add(CombatShape value){if(Count==Shapes.Length){Overflow=true;return;}Shapes[Count++]=value;}
        internal void Reset(uint tick,long session){Count=0;Overflow=false;Tick=tick;Session=session;Sequence++;}
    }
    // Samples are current native damage windows, not speculative hit tests.
    // Per-projectile replacement keeps extraUpdates distinct: only the last
    // natural substep is current. Kill-time explosions use a bounded event lane.
    internal sealed class CombatGeometry
    {
        internal readonly CombatShapeSample[] Attacks=new CombatShapeSample[Main.maxProjectiles+Main.maxPlayers];
        internal readonly CombatShapeSample[] Events=new CombatShapeSample[128];
        internal readonly CombatShapeSample[] Npcs=new CombatShapeSample[Main.maxNPCs];
        private readonly List<Vector2> whipNow=new List<Vector2>(64),whipBefore=new List<Vector2>(64);
        private int eventCount;private uint eventTick;private bool eventOverflow;
        internal int EventCount {get{return eventCount;}}
        internal bool EventOverflow {get{return eventTick==Main.GameUpdateCount && eventOverflow;}}
        internal long Session;
        internal bool Failed;
#if DEBUG
        internal int ProjectileSamples,NpcSamples,MeleeSamples;
#endif
        private static readonly HashSet<int> Special=new HashSet<int>{76,77,78,85,121,122,123,124,125,126,294,301,452,454,455,461,464,466,537,554,580,597,598,607,611,614,623,632,636,642,661,684,686,687,697,698,699,707,711,756,758,802,842,871,872,877,878,879,919,923,927,932,933,938,939,940,941,942,943,944,945,961,963,965,973,974,985,1041,1093,1100,1106,1112,1115,1116,1117,1118,1124,1125,1127};
        internal void Clear(){Array.Clear(Attacks,0,Attacks.Length);Array.Clear(Events,0,Events.Length);Array.Clear(Npcs,0,Npcs.Length);eventCount=0;eventOverflow=false;Failed=false;}
        internal void BeginNpcs(){for(int i=0;i<Npcs.Length;i++)if(Npcs[i]!=null)Npcs[i].Count=0;}
        internal void BeginDamage(Projectile shot)
        {
            if(shot.whoAmI<0 || shot.whoAmI>=Main.maxProjectiles)return;
            if(Attacks[shot.whoAmI]!=null)Attacks[shot.whoAmI].Count=0;
            // The remote sentry's owner-only Damage is not called locally.
            // Its naturally advanced phase still gives the same current burst
            // area. Deduplicate the owner's nested and regular Damage callbacks.
            if(shot.type==644 && shot.localAI[1]==30 && shot.damage>0 && Ally(shot.owner))
            {
                StartEvents();for(int i=0;i<eventCount;i++)if(ReferenceEquals(Events[i].Token,shot) && Events[i].Identity==(int)shot.key)return;
                var sample=Event();if(sample==null)return;Stamp(sample,shot);var center=shot.Center;sample.Rectangle(new Rectangle((int)(center.X-30),(int)(center.Y-30),60,60),0);
            }
        }
        private void StartEvents(){if(eventTick!=Main.GameUpdateCount){eventCount=0;eventOverflow=false;eventTick=Main.GameUpdateCount;}}
        private CombatShapeSample Event(){StartEvents();if(eventCount==Events.Length){eventOverflow=true;return null;}return Sample(Events,eventCount++);}
        private static void Stamp(CombatShapeSample sample,Projectile shot){sample.Owner=shot.owner;sample.Identity=(int)shot.key;sample.Type=shot.type;sample.Token=shot;}
        private CombatShapeSample Sample(CombatShapeSample[] array,int slot)
        {var sample=array[slot]??(array[slot]=new CombatShapeSample());sample.Reset(Main.GameUpdateCount,Session);return sample;}
        private static bool Ally(int owner)
        {return owner>=0 && owner<Main.maxPlayers && Main.player[owner]!=null && Main.player[owner].active && Main.LocalPlayer!=null && (owner==Main.myPlayer || !Main.LocalPlayer.InOpposingTeam(Main.player[owner]));}
        internal void Projectile(Projectile shot,Rectangle hitbox,bool killing)
        {
            int type=shot.type;
            if(type==644)return; // Its temporary owner/remote phase is captured above.
            // Colliding has target-independent harmless phases after the
            // general Damage gate. A dashed outline must not turn these
            // telegraphs/control states into an apparent live damage region.
            if(type==1115 && shot.ai[1]!=1 || type==454 && shot.ai[0]>=0 && shot.ai[0]<60 && shot.ai[1]!=-1 || type==1124 && shot.ai[0]<15 || type==965 && shot.alpha>64 || type==452 && shot.ai[0]<2 || (type==756 || type==961 || type==1041 || type==1125) && shot.ai[0]<0)return;
            int category=shot.friendly && !shot.npcProj && !shot.trap && Ally(shot.owner)?0:shot.hostile?3:-1;
            if(category<0 || shot.damage<=0 || shot.whoAmI<0 || shot.whoAmI>=Main.maxProjectiles)return;
            CombatShapeSample sample;
            // Rainbow-crystal sentry damage exists only inside its AI call;
            // the following harmless normal substep must not erase that event.
            if(killing || !shot.active)
            {sample=Event();if(sample==null)return;}
            else sample=Sample(Attacks,shot.whoAmI);
            Stamp(sample,shot);
#if DEBUG
            ProjectileSamples++;
#endif
            // These pure Colliding adjustments happen after Damage_GetHitbox.
            // The destructive getter's observed rectangle alone is too small
            // for these explosions; do not call it again to reconstruct them.
            if(type==301 || type==294 || type==77 || type==76 || type==78 || type==1116 || type==554 || type==1115)
            {int inflate=type==301?22:type==1116?5:type==554?24:type==1115?(int)Utils.Remap(shot.ai[0],0,60,0,22):10;hitbox.Inflate(inflate,inflate);sample.Rectangle(hitbox,category);return;}
            if(type<ProjectileID.Sets.IsAWhip.Length && ProjectileID.Sets.IsAWhip[type])
            {
                if(shot.owner<0 || shot.owner>=Main.maxPlayers)return;
                whipNow.Clear();whipBefore.Clear();
                Terraria.Projectile.FillWhipControlPoints(shot,whipNow,Main.player[shot.owner],true,0);
                Terraria.Projectile.FillWhipControlPoints(shot,whipBefore,Main.player[shot.owner],true,-1);
                for(int i=0;i<Math.Min(whipNow.Count,whipBefore.Count);i++)
                {var a=hitbox;var b=hitbox;a.X=(int)whipNow[i].X-a.Width/2;a.Y=(int)whipNow[i].Y-a.Height/2;b.X=(int)whipBefore[i].X-b.Width/2;b.Y=(int)whipBefore[i].Y-b.Height/2;sample.Rectangle(Rectangle.Union(a,b),category);}
                return;
            }
            if(type==872)
            {for(int i=0;i<Math.Min(50,shot.oldPos.Length);i+=2)if(shot.oldPos[i]!=Vector2.Zero){var b=shot.Hitbox;b.X=(int)shot.oldPos[i].X;b.Y=(int)shot.oldPos[i].Y;sample.Rectangle(b,category);}return;}
            if(type==632 || type==461 || type==642 || type==455 || type==537)
            {sample.Rectangle(hitbox,category);if(type!=455 || shot.localAI[0]>=20)sample.Line(shot.Center,shot.Center+shot.velocity*shot.localAI[1],(type==642?30:type==455?36:22)*shot.scale,category);return;}
            if(shot.aiStyle==19)
            {
                sample.Rectangle(hitbox,category);Rectangle extension;
                if(shot.owner>=0 && shot.owner<Main.maxPlayers && shot.AI_019_Spears_GetExtensionHitbox(Main.player[shot.owner],out extension))
                {
                    var to=new Vector2(extension.Center.X,extension.Center.Y);float length=Vector2.Distance(to,shot.Center),spacing=Math.Max(12,Math.Max(extension.Width,extension.Height));
                    for(float offset=spacing;offset<length && sample.Count<158;offset+=spacing){var p=Vector2.Lerp(shot.Center,to,offset/length);sample.Rectangle(new Rectangle((int)p.X-extension.Width/2,(int)p.Y-extension.Height/2,extension.Width,extension.Height),category);}
                    sample.Rectangle(extension,category);
                }
                return;
            }
            if(type==877 || type==878 || type==879)
            {float angle=shot.rotation-(float)Math.PI/4-(float)Math.PI/2-(shot.spriteDirection==1?(float)Math.PI:(float)Math.PI/2);sample.Line(shot.Center,shot.Center+new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle))*95,23*shot.scale,category);return;}
            if(type==699)
            {sample.Rectangle(hitbox,category);float angle=shot.rotation-(float)Math.PI/4*Math.Sign(shot.velocity.X)+(shot.spriteDirection==-1?(float)Math.PI:0);sample.Line(shot.Center,shot.Center+new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle))*-95,23*shot.scale,category);return;}
            if(type==466 || type==580 || type==686 || type==711 && shot.penetrate!=-1)
            {sample.Rectangle(hitbox,category);for(int i=0;i<shot.oldPos.Length && shot.oldPos[i]!=Vector2.Zero;i++){var b=hitbox;b.X=(int)shot.oldPos[i].X;b.Y=(int)shot.oldPos[i].Y;sample.Rectangle(b,category);}return;}
            if(type==464 && shot.ai[1]!=1)
            {
                sample.Rectangle(hitbox,category);float baseAngle=(float)Math.Atan2(shot.velocity.Y,shot.velocity.X)-(float)Math.PI/2;
                float radius=720*(shot.ai[0]%45/45);
                for(int i=0;i<6;i++){float angle=baseAngle+i*(float)Math.PI/3;var p=shot.Center+new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle))*radius;sample.Rectangle(new Rectangle((int)p.X-15,(int)p.Y-15,30,30),category);}return;
            }
            if(shot.aiStyle==15 && shot.ai[0]==0 && shot.owner>=0 && shot.owner<Main.maxPlayers)
            {
                var player=Main.player[shot.owner];var c=player.MountedCenter;
                for(int i=0;i<32;i++){double a=i*Math.PI/16,b=(i+1)*Math.PI/16;float ay=(float)Math.Sin(a),by=(float)Math.Sin(b);sample.Line(c+new Vector2((float)Math.Cos(a)*55,ay*55*(player.gravDir>0 && ay>0?.4f:.8f)),c+new Vector2((float)Math.Cos(b)*55,by*55*(player.gravDir>0 && by>0?.4f:.8f)),1,category,true);}return;
            }
            // Special native Colliding branches not yet represented stay dashed
            // and labelled approximate. Ordinary rectangles keep exact size;
            // scale is never blindly applied a second time.
            sample.Rectangle(hitbox,category,Special.Contains(type) || shot.aiStyle==137 || shot.aiStyle==190 || shot.aiStyle==203);
        }
        internal void Melee(Player player,Item item,Rectangle box,int damage)
        {
            if(damage<=0 || !Ally(player.whoAmI))return;
            var sample=Sample(Attacks,Main.maxProjectiles+player.whoAmI);sample.Owner=player.whoAmI;sample.Type=item.type;sample.Token=player;
#if DEBUG
            MeleeSamples++;
#endif
            sample.Rectangle(box,0,item.type==121);
            if(item.type==121){Vector2 a,b,d;player.GetPointOnSwungItemPath(70,70,0,player.GetAdjustedItemScale(item),out a,out d);player.GetPointOnSwungItemPath(70,70,.9f,player.GetAdjustedItemScale(item),out b,out d);sample.Line(a,b,32,0,true);}
        }
        internal void Npc(NPC n)
        {
            bool receive=CombatSelection.Receives(n,true),harm=!n.friendly && n.damage>0 && n.type!=400 && !(n.type==636 && (n.ai[0]==0 || n.ai[0]==10));
            if(!receive && !harm)return;
            var sample=Sample(Npcs,n.whoAmI);sample.Type=n.type;sample.Identity=n.generation;sample.Token=n;
#if DEBUG
            NpcSamples++;
#endif
            var body=new Rectangle((int)(n.position.X+n.netOffset.X),(int)(n.position.Y+n.netOffset.Y),n.width,n.height);
            if(n.type==414)body.Inflate(8,8);
            bool attack=CombatSelection.ProjectileLike(n);
            if(receive)sample.Rectangle(body,attack?4:1);
            if(!harm)return;
            var danger=new Rectangle((int)(n.position.X+n.netOffset.X),(int)(n.position.Y+n.netOffset.Y),n.width,n.height);
            int type=n.type,d=n.direction;var center=new Vector2(danger.X+n.width*.5f,danger.Y+n.height*.5f);
            if(type==668)danger.Height=Math.Max(0,danger.Height-80);
            if(n.ai[2]>5 && (type>=430 && type<=436 || type==591 || type==494 || type==495))
            {int extra=type==494 || type==495?18:34;danger.Width+=extra;if(n.spriteDirection<0)danger.X-=extra;}
            AddDanger(sample,danger,body,receive,attack);
            Rectangle append=Rectangle.Empty;
            if(type==460 || type==466 || type>=552 && type<=554 && n.ai[0]>0 && n.ai[0]<24)
            {int width=type>=552 && type<=554?34:30,height=type==466?8:14;append=new Rectangle((int)center.X-(d<0?width:0),danger.Y+n.height-(type==466?32:20),width,height);}
            else if(type==417 && n.ai[0]==6 && n.ai[3]>0 && n.ai[3]<4)append=new Rectangle((int)center.X-50,(int)center.Y-50,100,100);
            else if(type==576 || type==577)
            {
                int frame=n.frame.Y,w=0,h=0,a=0,b=0;
                if(frame==15){w=120;h=30;b=24;}else if(frame==16){w=120;h=60;a=10;}else if(frame==17){w=100;h=90;a=50;}else if(frame==18){w=100;h=50;a=90;b=10;}
                if(w>0)append=new Rectangle((int)center.X-a*d-(d<0?w:0),(int)center.Y-h+b,w,h);
            }
            else if(type==668 && n.frame.Y==15 && n.ai[0]!=4)append=new Rectangle((int)center.X+42*d-(d<0?64:0),(int)center.Y-100,64,180);
            if(append.Width>0)AddDanger(sample,append,body,receive,attack);
        }
        private static void AddDanger(CombatShapeSample sample,Rectangle danger,Rectangle body,bool receive,bool projectile)
        {
            int category=projectile?3:2;
            if(!receive){sample.Rectangle(danger,category);return;}
            // Class C is only the harmful part outside the attackable body.
            // Keep the union's gaps; a surrounding bounding box invents damage.
            var intersection=Rectangle.Intersect(danger,body);if(intersection.Width<=0 || intersection.Height<=0){sample.Rectangle(danger,category);return;}
            if(danger.Top<intersection.Top)sample.Rectangle(new Rectangle(danger.Left,danger.Top,danger.Width,intersection.Top-danger.Top),category);
            if(danger.Bottom>intersection.Bottom)sample.Rectangle(new Rectangle(danger.Left,intersection.Bottom,danger.Width,danger.Bottom-intersection.Bottom),category);
            if(danger.Left<intersection.Left)sample.Rectangle(new Rectangle(danger.Left,intersection.Top,intersection.Left-danger.Left,intersection.Height),category);
            if(danger.Right>intersection.Right)sample.Rectangle(new Rectangle(intersection.Right,intersection.Top,danger.Right-intersection.Right,intersection.Height),category);
        }
    }
}
