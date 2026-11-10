using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostCombatExperiments
    {
        private static HostCombatExperiments current;
        private readonly Harmony[] hooks=new Harmony[3];
        private readonly List<MethodBase>[] installed={new List<MethodBase>(),new List<MethodBase>(),new List<MethodBase>()};
        internal void Install()
        {
            if(current!=null && !ReferenceEquals(current,this))throw new InvalidOperationException("Live combat experiments already installed.");
            current=this;
            for(int field=0;field<3;field++)
            {
                if(hooks[field]!=null)continue;
                hooks[field]=new Harmony("JueMingR.CombatExperiments."+field);
                try
                {
                    if(field==0)Patch(field,typeof(Projectile),"AI",null,nameof(AfterNativeAi));
                    else if(field==1)InstallTiming();
                    else InstallWall();
                    ready[field]=true;
                }
                catch(Exception error){errors[field]=error;ready[field]=false;RemoveHooks(field);}
            }
        }
        private void Patch(int field,Type type,string name,string prefix=null,string postfix=null,string finalizer=null,string transpiler=null,Type[] signature=null)
        {
            var target=signature==null?AccessTools.DeclaredMethod(type,name):AccessTools.DeclaredMethod(type,name,signature);
            if(target==null || target.GetMethodBody()==null)throw new MissingMethodException(type.FullName,name);
            installed[field].Add(target);
            hooks[field].Patch(target,prefix:prefix==null?null:new HarmonyMethod(GetType(),prefix),postfix:postfix==null?null:new HarmonyMethod(GetType(),postfix),
                transpiler:transpiler==null?null:new HarmonyMethod(GetType(),transpiler),finalizer:finalizer==null?null:new HarmonyMethod(GetType(),finalizer));
        }
        private void RemoveHooks(int field)
        {if(hooks[field]!=null)foreach(var method in installed[field])hooks[field].Unpatch(method,HarmonyPatchType.All,hooks[field].Id);installed[field].Clear();}
        private void DisposeHooks(){for(int i=0;i<3;i++){ready[i]=false;RemoveHooks(i);}if(ReferenceEquals(current,this))current=null;}
        private static void AfterNativeAi(Projectile __instance){current?.Guide(__instance);}
        private void InstallWall()
        {
            // These caller bodies retain every range/identity/attack/geometry
            // predicate. Only their audited terrain queries receive a bypass.
            Patch(2,typeof(Projectile),"HandleMovement",transpiler:nameof(WallTranspiler));
            Patch(2,typeof(Projectile),"UpdatePosition",transpiler:nameof(WallTranspiler));
            foreach(string name in new[]{"AI","AI_001","AI_003_Boomerang","AI_015_Flails","AI_015_Flails_Old","AI_016_Bombs","AI_026","AI_047_MagnetSphere_TryAttacking","AI_053_HandleSentryNPCTargeting","AI_062","AI_099_1_Counterweights","AI_099_2_Yoyos","AI_100_Medusa","AI_123_FloatingTurrets","AI_130_FlameBurstTower_FindTarget","AI_134_Ballista_FindTarget","AI_147_Celeb2Rocket","AI_196_Petal","AI_206_CustomEliminationCheck_Forbidden","AI_019_Spears","AI_191_TrueNightsEdge","AI_208_GlacierFangShot","AI_137_LightningAura_CanHit","Kill","CanHitWithMeleeWeapon","CanHitWithOwnBody","Colliding","FindTargetWithinRange","Damage_PVE_Inner"})
                Patch(2,typeof(Projectile),name,transpiler:nameof(WallTranspiler));
            Patch(2,typeof(Projectile),"FindTargetWithLineOfSight",transpiler:nameof(WallTranspiler),signature:new[]{typeof(Entity),typeof(float),typeof(ICollection<int>)});
            // Immediate real ItemCheck births/damage precede projectile-world.
            // Its scope grants phase only; every wrapper still checks array ref.
            Patch(2,typeof(Player),"ItemCheck",nameof(BeforeLiveItem),null,nameof(EndLiveItem));
        }
        private static void BeforeLiveItem(Player __instance,out WorldScope __state)
        {
            __state=new WorldScope{Prior=worldOwner,Depth=worldDepth};var self=current;
            if(self!=null && self.ThroughWallEffective && LocalPlayer(__instance)){worldOwner=self;worldDepth++;}
        }
        private static Exception EndLiveItem(Exception __exception,WorldScope __state){EndWorld(__state);return __exception;}
        private static readonly Type[] lineSignature={typeof(Vector2),typeof(int),typeof(int),typeof(Vector2),typeof(int),typeof(int)};
        // Fixed 1.4.5.8 call-site counts detect ABI/body drift. In the large AI
        // body and AI062, follow-to-owner and environment probes are excluded.
        private static readonly Dictionary<string,int> queryCounts=new Dictionary<string,int>
        {
            {"AI",27},{"AI_001",6},{"AI_003_Boomerang",1},{"AI_015_Flails",1},{"AI_015_Flails_Old",1},{"AI_016_Bombs",1},{"AI_026",5},
            {"AI_047_MagnetSphere_TryAttacking",1},{"AI_053_HandleSentryNPCTargeting",4},{"AI_062",7},{"AI_099_1_Counterweights",1},{"AI_099_2_Yoyos",1},
            {"AI_100_Medusa",2},{"AI_123_FloatingTurrets",2},{"AI_130_FlameBurstTower_FindTarget",2},{"AI_134_Ballista_FindTarget",2},{"AI_147_Celeb2Rocket",1},
            {"AI_196_Petal",1},{"AI_206_CustomEliminationCheck_Forbidden",1},{"CanHitWithMeleeWeapon",4},{"CanHitWithOwnBody",5},{"Colliding",6},
            {"AI_019_Spears",1},{"AI_191_TrueNightsEdge",2},{"AI_208_GlacierFangShot",1},{"AI_137_LightningAura_CanHit",5},{"Kill",1},
            {"FindTargetWithinRange",1},{"FindTargetWithLineOfSight",1},{"Damage_PVE_Inner",4},{"HandleMovement",1},{"UpdatePosition",0}
        };
        private static bool AttackQuery(string name,int ordinal)
        {
            if(name=="AI")return ordinal<=13 && ordinal!=3 || ordinal==22 || ordinal==25 || ordinal==26;
            if(name=="AI_062")return ordinal<6;
            return true;
        }
        private static IEnumerable<CodeInstruction> WallTranspiler(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        {
            string name=__originalMethod.Name;int query=0,lasers=0,tiles=0,solid=0;
            var result=new List<CodeInstruction>();
            foreach(var original in instructions)
            {
                var instruction=new CodeInstruction(original);var method=instruction.operand as MethodInfo;
                if(instruction.opcode==OpCodes.Ldfld && Equals(instruction.operand,AccessTools.Field(typeof(Projectile),"tileCollide")) && (name=="HandleMovement" || name=="UpdatePosition"))
                {instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(HostCombatExperiments),nameof(NativeTileCollide));tiles++;}
                else if(method!=null && (method.DeclaringType==typeof(Collision) || method.DeclaringType==typeof(WorldGen)))
                {
                    string replacement=null;
                    if(name=="AI_137_LightningAura_CanHit" && method.DeclaringType==typeof(WorldGen) && method.Name=="SolidTile")
                    {replacement=nameof(NativeAttackSolidTile);solid++;}
                    else if(name=="AI_191_TrueNightsEdge" && method.DeclaringType==typeof(Collision) && method.Name=="SolidTiles")
                    {replacement=nameof(NativeAttackSolidTiles);solid++;}
                    else if(method.Name=="CanHit" || method.Name=="CanHitLine")
                    {
                        bool selected=AttackQuery(name,query++);
                        if(selected)replacement=method.GetParameters().Length==2?nameof(NativeCanHitEntity):method.Name=="CanHit"?nameof(NativeCanHit):nameof(NativeCanHitLine);
                    }
                    else if(method.Name=="LaserScan"){replacement=name=="AI"?nameof(NativeLaserScan):null;lasers++;}
                    else if(method.Name=="TileCollision" && name=="HandleMovement")replacement=nameof(NativeTileCollision);
                    else if(method.Name=="SlopeCollision" && (name=="HandleMovement" || name=="UpdatePosition"))replacement=nameof(NativeSlopeCollision);
                    if(replacement!=null)
                    {
                        // Move labels/exception blocks to the new first operand,
                        // so incoming branches still push the added context.
                        var context=new CodeInstruction(OpCodes.Ldarg_0);context.labels.AddRange(instruction.labels);instruction.labels.Clear();context.blocks.AddRange(instruction.blocks);instruction.blocks.Clear();result.Add(context);
                        instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(HostCombatExperiments),replacement);
                    }
                }
                result.Add(instruction);
            }
            int expected;if(!queryCounts.TryGetValue(name,out expected) || query!=expected || name=="AI" && lasers!=1 || (name=="AI_137_LightningAura_CanHit" || name=="AI_191_TrueNightsEdge") && solid!=1 || (name=="HandleMovement" || name=="UpdatePosition") && tiles==0)
                throw new InvalidOperationException("Fixed .8 terrain caller changed: "+name+" queries="+query+" expected="+expected);
            return result;
        }
        private static bool Bypass(Entity context){var p=context as Projectile;return current!=null && current.Wall(p);}
        // These solid predicates belong only to the audited attack gates,
        // never their environment/following queries or global terrain helpers.
        private static bool NativeAttackSolidTile(int x,int y,bool noDoors,Entity context)
        {return !Bypass(context) && WorldGen.SolidTile(x,y,noDoors);}
        private static bool NativeAttackSolidTiles(Vector2 position,int width,int height,Entity context)
        {return !Bypass(context) && Collision.SolidTiles(position,width,height);}
        private static bool NativeCanHit(Vector2 a,int aw,int ah,Vector2 b,int bw,int bh,Entity context)
        {return Bypass(context) || Collision.CanHit(a,aw,ah,b,bw,bh);}
        private static bool NativeCanHitLine(Vector2 a,int aw,int ah,Vector2 b,int bw,int bh,Entity context)
        {return Bypass(context) || Collision.CanHitLine(a,aw,ah,b,bw,bh);}
        private static bool NativeCanHitEntity(Entity a,Entity b,Entity context)
        {return Bypass(context) || Collision.CanHit(a,b);}
        private static void NativeLaserScan(Vector2 start,Vector2 direction,float width,float maximum,float[] samples,Entity context)
        {
            if(!Bypass(context)){Collision.LaserScan(start,direction,width,maximum,samples);return;}
            for(int i=0;i<samples.Length;i++)samples[i]=maximum;
        }
        // Fixed native walking families: AI026 ground pet/slime, AI066
        // ground spiders, AI067 frog/flinx/pirate/tiger. Flying AI062
        // minions also set tileCollide=true, but do not own floor support.
        private static bool GroundSupport(Projectile p)
        {return p.sentry || p.minion && (p.aiStyle==26 || p.aiStyle==66 || p.aiStyle==67);}
        private static bool NativeTileCollide(Projectile p)
        {return p.tileCollide && (!Bypass(p) || GroundSupport(p));}
        private static Vector2 NativeTileCollision(Vector2 position,Vector2 velocity,int width,int height,bool fallThrough,bool fall2,int gravDir,bool ignoreDoors,bool ignoreAetheriumPlatforms,bool hoik,Entity context)
        {
            if(Bypass(context) && GroundSupport((Projectile)context))
            {
                // Native diagonal collision may cancel its earlier floor clamp
                // when a wall in the same column stops X. Restoring only X then
                // keeps the cancelled Y and lets a walking summon fall below
                // its support. Query vanilla's vertical movement independently;
                // retain its platform/gravity rules and release horizontal walls.
                var vertical=Collision.TileCollision(position,new Vector2(0,velocity.Y),width,height,fallThrough,fall2,gravDir,ignoreDoors,ignoreAetheriumPlatforms,hoik);
                return new Vector2(velocity.X,vertical.Y);
            }
            return Collision.TileCollision(position,velocity,width,height,fallThrough,fall2,gravDir,ignoreDoors,ignoreAetheriumPlatforms,hoik);
        }
        private static Vector4 NativeSlopeCollision(Vector2 position,Vector2 velocity,int width,int height,float gravity,bool fall,bool ignoreAetheriumPlatforms,Entity context)
        {
            var result=Collision.SlopeCollision(position,velocity,width,height,gravity,fall,ignoreAetheriumPlatforms);
            if(Bypass(context) && GroundSupport((Projectile)context)){result.X=position.X;result.Z=velocity.X;}return result;
        }
    }
}
