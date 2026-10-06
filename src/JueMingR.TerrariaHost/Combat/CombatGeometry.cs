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
        // Curves describe filled native domains, not a collection of damaging
        // outline segments. Conditions remain separate from display geometry:
        // several native rules also depend on the victim's size or visibility.
        // Kind: 0 rectangle/line, 1 strict circle, 2 asymmetric ellipse,
        // 3/4 native fast/slow cone, 5 discrete points, 6 inclusive circle,
        // 7 endpoint/corner-distance capsule. Condition: 1 center LOS,
        // 2 rectangle LOS, 3 line LOS+distance, 4 target shrink, 5 aura LOS,
        // 6 connected liquid, 7 projectile-only NPC extension, 8 unknown
        // remote Volcano phase, 9 native endpoint/corner distance semantics,
        // 10 player contact LOS/target immunity, 11 target-center distance,
        // 12 Inferno target-center/buff immunity, 13 selected-victim event.
        internal int Kind,Condition;internal Vector2 Sight;
        internal Rectangle Bounds;internal bool HasBounds;
    }
    internal sealed class CombatShapeSample
    {
        internal CombatShape[] Shapes=new CombatShape[8];
        internal int Count,Owner,Identity,Type,Sequence;internal uint Tick;internal long Session;internal bool Overflow;
        internal object Token;
        internal bool Presented;internal long Presentation;
        internal Vector2[] Points;internal int PointCount;internal Point PointSize;internal Rectangle PointBounds;
        internal void Rectangle(Rectangle box,int category,bool approximate=false)
        {Add(new CombatShape{A=new Vector2(box.X,box.Y),B=new Vector2(box.Right,box.Bottom),Category=category,Approximate=approximate});}
        internal void Line(Vector2 start,Vector2 end,float width,int category,bool approximate=false)
        {Add(new CombatShape{A=start,B=end,Width=width,Line=true,Category=category,Approximate=approximate});}
        internal void Capsule(Vector2 start,Vector2 end,float radius,int category)
        {Add(new CombatShape{A=start,B=end,Width=radius,Kind=7,Category=category,Condition=9});}
        internal void Curve(Vector2 center,float radius,Vector2 parameters,int kind,int category,int condition=0)
        {if(radius>0)Add(new CombatShape{A=center,B=parameters,Width=radius,Kind=kind,Category=category,Condition=condition,Sight=center});}
        internal void Condition(int condition,Vector2 sight){if(Count>0){Shapes[Count-1].Condition=condition;Shapes[Count-1].Sight=sight;}}
        internal void Limit(Rectangle box){if(Count>0){Shapes[Count-1].HasBounds=true;Shapes[Count-1].Bounds=box;}}
        internal void PointSet(Terraria.DataStructures.MultiPointHitbox source,int category)
        {
            // Dedicated storage keeps normal projectiles small. 65536 points
            // covers the native generator across the largest supported world
            // diagonal (2 * distance / 8); it is not the shape-slot budget.
            PointCount=Math.Min(source.Points.Length,65536);Overflow|=PointCount!=source.Points.Length;
            if(Points==null || Points.Length<PointCount)Points=new Vector2[PointCount];
            Array.Copy(source.Points,Points,PointCount);PointSize=source.PointSize;PointBounds=source.BoundingRect;
            Add(new CombatShape{Kind=5,Category=category});
        }
        private void Add(CombatShape value){if(Count==Shapes.Length){if(Count==160){Overflow=true;return;}Array.Resize(ref Shapes,Math.Min(160,Shapes.Length*2));}Shapes[Count++]=value;}
        internal void Reset(uint tick,long session){Count=PointCount=0;Overflow=Presented=false;Presentation=0;Tick=tick;Session=session;Sequence++;}
    }
    // Samples are current native damage windows, not speculative hit tests.
    // Per-projectile replacement keeps extraUpdates distinct: only the last
    // natural substep is current. Kill-time explosions use a bounded event lane.
    internal sealed class CombatGeometry
    {
        internal readonly CombatShapeSample[] Attacks=new CombatShapeSample[Main.maxProjectiles+Main.maxPlayers];
        // A full projectile array can terminate in one update; Venom Bullet
        // alone emits three Damage calls. Four records per slot covers that
        // native burst plus an earlier pulse, without inflating regular slots.
        internal readonly CombatShapeSample[] Events=new CombatShapeSample[Main.maxProjectiles*4];
        internal readonly CombatShapeSample[] Npcs=new CombatShapeSample[Main.maxNPCs];
        internal readonly CombatShapeSample[] Bodies=new CombatShapeSample[Main.maxPlayers];
        private readonly List<Vector2> whipNow=new List<Vector2>(64),whipBefore=new List<Vector2>(64);
        private int eventCount,eventReplacement;private uint eventTick;private bool eventOverflow;
        internal int EventCount {get{return eventCount;}}
        internal bool EventOverflow {get{return eventOverflow;}}
        internal long Session;
        internal bool Failed;
#if DEBUG
        internal int ProjectileSamples,NpcSamples,MeleeSamples;
#endif
        internal void Clear(){Array.Clear(Attacks,0,Attacks.Length);Array.Clear(Events,0,Events.Length);Array.Clear(Npcs,0,Npcs.Length);Array.Clear(Bodies,0,Bodies.Length);eventCount=eventReplacement=0;eventOverflow=false;Failed=false;}
        internal void BeginNpcs(){for(int i=0;i<Npcs.Length;i++)if(Npcs[i]!=null)Npcs[i].Count=0;}
        internal void BeginDamage(Projectile shot)
        {
            if(shot.whoAmI<0 || shot.whoAmI>=Main.maxProjectiles)return;
            if(Attacks[shot.whoAmI]!=null)Attacks[shot.whoAmI].Count=0;
        }
        internal void Crystal(Projectile shot)
        {
            // Only the remote owner needs this adapter. The same native helper
            // is called in both the midlife pulse and the terminal explosion.
            if(shot.damage<=0 || !Ally(shot.owner))return;var sample=Event();if(sample==null)return;
            Stamp(sample,shot);sample.Rectangle(Utils.CenteredRectangle(shot.Center,new Vector2(60)),0);
        }
        internal void SelfHurt(Projectile shot)
        {
            // Torch God's flame bypasses Damage entirely. Other callers also
            // have a natural Damage callback; their geometry is captured there.
            if(shot.type!=949)return;
            Projectile(shot,shot.Hitbox,true);
        }
        internal void RemoteTermination(Projectile shot,Vector2 position,Vector2 size,Vector2 oldVelocity,float scale,int category)
        {
            if(category<0 || shot.damage<=0)return;
            int type=shot.type,side=type==711?140:type==41?64:type==514?112:type==283 || type==286?80:type>=424 && type<=426?(int)(128*scale):type==1037 || type==1049 || type==1078?(int)(192*scale):0;
            if(side==0 && type!=483)return;
            // These exact owner-only native Kill branches do not call Damage
            // on this client. Preserve integer recentering before Resize: the
            // odd intermediate meteor size can move its center by half a pixel.
            if(type==483){position-=new Vector2(50);size+=new Vector2(100,1);}
            else if(type==711){position+=size/2-new Vector2(70);size=new Vector2(140);}
            else
            {
                position+=new Vector2((int)size.X/2,(int)size.Y/2)-new Vector2(side/2);size=new Vector2(side);
                if(type==1037 || type==1049 || type==1078){int final=type==1049?112:64;position+=size/2-new Vector2(final/2);size=new Vector2(final);}
            }
            int count=type==283?3:1;var step=((float)Math.Atan2(oldVelocity.Y,oldVelocity.X)).ToRotationVector2()*60;
            for(int i=0;i<count;i++)
            {var sample=Event();Stamp(sample,shot);sample.Rectangle(new Rectangle((int)position.X,(int)position.Y,(int)size.X,(int)size.Y),category);position+=step;}
        }
        internal void CatImpact(Projectile shot)
        {
            int category=Category(shot);if(category<0 || shot.damage<=0)return;
            int side=40+8*(int)shot.ai[0];var sample=Event();Stamp(sample,shot);
            sample.Rectangle(Utils.CenteredRectangle(shot.Center,new Vector2(side)),category);
        }
        internal void PrepareEvents()
        {
            if(eventTick==Main.GameUpdateCount)return;eventTick=Main.GameUpdateCount;
            for(int i=eventCount-1;i>=0;i--)
            {
                var value=Events[i];
                // An event owns one actual presentation opportunity, not four
                // world steps. Capacity replacement remains bounded and explicit.
                if(!value.Presented && value.Session==Session)continue;
                Events[i]=Events[--eventCount];Events[eventCount]=value;
            }
        }
        internal void PresentedEvents(long presentation)
        {for(int i=0;i<eventCount;i++)if(Events[i].Presentation==presentation)Events[i].Presented=true;eventOverflow=false;}
        private CombatShapeSample Event()
        {
            PrepareEvents();if(eventCount<Events.Length)return Sample(Events,eventCount++);
            // Exceptional overload rotates replacement; it does not reserve
            // the first events forever while suppressing every later attack.
            eventOverflow=true;int replacement=eventReplacement;eventReplacement=(eventReplacement+1)%Events.Length;return Sample(Events,replacement);
        }
        private static void Stamp(CombatShapeSample sample,Projectile shot){sample.Owner=shot.owner;sample.Identity=(int)shot.key;sample.Type=shot.type;sample.Token=shot;}
        private CombatShapeSample Sample(CombatShapeSample[] array,int slot)
        {var sample=array[slot]??(array[slot]=new CombatShapeSample());sample.Reset(Main.GameUpdateCount,Session);return sample;}
        private static bool Ally(int owner)
        {return owner>=0 && owner<Main.maxPlayers && Main.player[owner]!=null && Main.player[owner].active && Main.LocalPlayer!=null && (owner==Main.myPlayer || !Main.LocalPlayer.InOpposingTeam(Main.player[owner]));}
        internal static int Category(Projectile shot)
        {return shot.friendly && !shot.npcProj && !shot.trap && Ally(shot.owner)?0:shot.hostile || shot.type==949?3:-1;}
        internal void Projectile(Projectile shot,Rectangle hitbox,bool killing)
        {
            int type=shot.type;
            // Colliding has target-independent harmless phases after the
            // general Damage gate. A dashed outline must not turn these
            // telegraphs/control states into an apparent live damage region.
            if(type==1115 && shot.ai[1]!=1 || type==454 && shot.ai[0]>=0 && shot.ai[0]<60 && shot.ai[1]!=-1 || type==1124 && shot.ai[0]<15 || type==965 && shot.alpha>64 || type==452 && shot.ai[0]<2 || (type==756 || type==961 || type==1041 || type==1125) && shot.ai[0]<0)return;
            int category=Category(shot);
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
            if(ProjectileCollisionGeometry.TryCapture(shot,hitbox,category,sample))return;
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
                    for(float offset=spacing;offset<length;offset+=spacing){var p=Vector2.Lerp(shot.Center,to,offset/length);sample.Rectangle(Utils.CenteredRectangle(p,new Vector2(extension.Width,extension.Height)),category);}
                    sample.Rectangle(extension,category);
                }
                return;
            }
            if(type==699)
            {sample.Rectangle(hitbox,category);float angle=shot.rotation-(float)Math.PI/4*Math.Sign(shot.velocity.X)+(shot.spriteDirection==-1?(float)Math.PI:0);sample.Line(shot.Center,shot.Center+new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle))*-95,23*shot.scale,category);return;}
            if(type==466 || type==580 || type==686 || type==711 && shot.penetrate!=-1)
            {sample.Rectangle(hitbox,category);for(int i=0;i<shot.oldPos.Length && shot.oldPos[i]!=Vector2.Zero;i++){var b=hitbox;b.X=(int)shot.oldPos[i].X;b.Y=(int)shot.oldPos[i].Y;sample.Rectangle(b,category);}return;}
            if(type==464 && shot.ai[1]!=1)
            {
                // Preserve native rotation order before integer truncation:
                // combining angles shifts a horizontal satellite by one pixel.
                sample.Rectangle(hitbox,category);var ray=new Vector2(0,-720).RotatedBy(shot.velocity.ToRotation())*(shot.ai[0]%45/45);
                for(int i=0;i<6;i++){float angle=i*((float)Math.PI*2)/6;sample.Rectangle(Utils.CenteredRectangle(shot.Center+ray.RotatedBy(angle),new Vector2(30,30)),category);}return;
            }
            // All native special geometry has been dispatched above. A phase
            // gate alone does not make the remaining ordinary box approximate.
            sample.Rectangle(hitbox,category);
        }
        internal void Melee(Player player,Item item,Rectangle box,int damage,bool? volcanoPending=null)
        {
            if(damage<=0 || !Ally(player.whoAmI))return;
            var sample=Sample(Attacks,Main.maxProjectiles+player.whoAmI);sample.Owner=player.whoAmI;sample.Type=item.type;sample.Token=player;
#if DEBUG
            MeleeSamples++;
#endif
            if(item.type!=121 || volcanoPending!=true){sample.Rectangle(box,0);if(item.type==121 && !volcanoPending.HasValue)sample.Condition(8,Vector2.Zero);}
            if(item.type==121){Vector2 a,b,d;player.GetPointOnSwungItemPath(70,70,0,player.GetAdjustedItemScale(item),out a,out d);player.GetPointOnSwungItemPath(70,70,.9f,player.GetAdjustedItemScale(item),out b,out d);sample.Capsule(a,b,16,0);}
        }
        private CombatShapeSample BodySample(Player player)
        {
            if(!Ally(player.whoAmI) || player.dead)return null;var prior=Bodies[player.whoAmI];
            var sample=prior!=null && prior.Tick==Main.GameUpdateCount && prior.Session==Session && ReferenceEquals(prior.Token,player)?prior:Sample(Bodies,player.whoAmI);
            sample.Owner=player.whoAmI;sample.Token=player;return sample;
        }
        internal void Body(Player player,Rectangle box)
        {var sample=BodySample(player);if(sample==null)return;sample.Rectangle(box,0);sample.Condition(10,player.Center);}
        internal void BodyCircle(Player player,float radius,bool transient,int condition)
        {if(!Ally(player.whoAmI) || player.dead)return;var sample=transient?Event():BodySample(player);sample.Owner=player.whoAmI;sample.Token=player;sample.Curve(player.Center,radius,Vector2.Zero,6,0,condition);}
        internal void TargetEvent(Player player,NPC target)
        {
            if(!Ally(player.whoAmI))return;var sample=Event();sample.Owner=player.whoAmI;sample.Token=player;
            // Retaliation and electric-eel tag hits select actual victims. No
            // line between actors or surrounding aura is a damaging region.
            sample.Rectangle(target.Hitbox,0);sample.Condition(13,target.Center);
        }
        internal void Npc(NPC n)
        {
            bool receive=CombatSelection.Receives(n,true),harm=!n.friendly && n.damage>0 && n.type!=400 && !(n.type==636 && (n.ai[0]==0 || n.ai[0]==10));
            if(!receive && !harm)return;
            var sample=Sample(Npcs,n.whoAmI);sample.Type=n.type;sample.Identity=n.generation;sample.Token=n;
#if DEBUG
            NpcSamples++;
#endif
            var body=CombatSelection.ReceiveBounds(n);
            bool attack=CombatSelection.ProjectileLike(n);
            if(receive){sample.Rectangle(body,attack?4:1);if(n.type==414)sample.Condition(7,Vector2.Zero);}
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
