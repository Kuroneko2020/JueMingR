using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Request-local provenance for the existing private native execution, not
    // permission to simulate more actors. Computation and RNG remain intact.
    // Reads depend on their provider; writes can affect their recipient. The
    // reverse closure from the selected actor includes indirect projectile
    // effects before choosing BOTH terrain and historical proof premises.
    internal static class NativePredictionPurpose
    {
        private const int ProjectileBase=201,Global=1202,Count=1203,Words=19;
        private static readonly ulong[,] needs=new ulong[Count,Words];
        private static readonly NativeTerrainUsage[] terrain=new NativeTerrainUsage[Count];
        private static readonly bool[] relevant=new bool[Count];
        private static bool enabled;
#if JMR_AIM_DIAGNOSTICS
        private static string diagnosticSource;
        internal static int DiagnosticActor=>actor;
        internal static void DiagnosticMissing(string detail){if(AimDiagnostics.Active)try{AimDiagnostics.Event("dependency-missing",(long)Main.GameUpdateCount,"consumer="+actor+";"+detail);}catch(Exception error){AimDiagnostics.Missing("dependency-missing",error);}}
#endif
        private static int actor=Global,selected,queryDepth,temporaryDepth,readOnlyDepth;
        private static NPC candidate;
        private static bool impact;
        private struct CandidateState {internal NPC Actor;internal bool Impact;}
        private static bool vulnerablePlayer;
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;

        internal static void Install(Harmony patches)
        {
            patches.Patch(typeof(Projectile).GetMethod("Damage_PVE",Flags),prefix:Hook(nameof(QueryBefore)),finalizer:Hook(nameof(QueryAfter)));
            patches.Patch(typeof(Projectile).GetMethod("Damage_PVE_Inner",Flags),prefix:Hook(nameof(CandidateBefore)),finalizer:Hook(nameof(CandidateAfter)));
            patches.Patch(typeof(Projectile).GetMethod("Colliding",Flags),postfix:Hook(nameof(CollisionAfter)));
            // Locked Entity leaf getters take a managed address only to read
            // Vector2.X/Y. This changes provenance, never page permissions or
            // the meaning of an address obtained by native mutation code.
            foreach(string name in new[]{"get_Center","get_Hitbox"})
                patches.Patch(typeof(Entity).GetMethod(name,Flags),prefix:Hook(nameof(ReadOnlyBefore)),finalizer:Hook(nameof(ReadOnlyAfter)));
            patches.Patch(typeof(NPC).GetMethod("getRect",Flags),prefix:Hook(nameof(ReadOnlyBefore)),finalizer:Hook(nameof(ReadOnlyAfter)));
            foreach(string name in new[]{"Damage_StartIteratingNPC","Damage_StopIteratingNPC"})
                patches.Patch(typeof(Projectile).GetMethod(name,Flags),prefix:Hook(nameof(TemporaryBefore)),finalizer:Hook(nameof(TemporaryAfter)));
            foreach(var method in typeof(Projectile).GetMethods(Flags))if(method.Name=="NewProjectile")
                patches.Patch(method,prefix:Hook(nameof(BirthBefore)),finalizer:Hook(nameof(BirthAfter)));
            patches.Patch(typeof(Projectile).GetMethod("NewProjectileSetup",Flags),postfix:Hook(nameof(BirthSetupAfter)));
            patches.Patch(typeof(Player).GetMethod("Hurt",Flags),postfix:Hook(nameof(PlayerHurtAfter)));
        }
        private static HarmonyMethod Hook(string name){return new HarmonyMethod(typeof(NativePredictionPurpose).GetMethod(name,Flags));}
        internal static void Begin(int target)
        {
            Array.Clear(needs,0,needs.Length);foreach(var usage in terrain)usage?.Reset();Array.Clear(relevant,0,relevant.Length);
            selected=target;actor=Global;queryDepth=temporaryDepth=readOnlyDepth=0;candidate=null;impact=false;enabled=true;
            vulnerablePlayer=false;foreach(var p in Main.player)if(p.active && !p.dead && !p.ghost && !(p.immune && p.immuneTime>PredictionWire.MaximumHorizon))vulnerablePlayer=true;
        }
        internal static void End(){enabled=false;actor=Global;queryDepth=temporaryDepth=readOnlyDepth=0;candidate=null;impact=false;}
        internal static bool Pause(){bool prior=enabled;enabled=false;return prior;}
        internal static void Resume(bool prior){enabled=prior;}
        internal static int Enter(NPC value){int prior=actor;actor=enabled?value.whoAmI:Global;return prior;}
        internal static int Enter(Projectile value){int prior=actor;actor=enabled?ProjectileBase+value.whoAmI:Global;if(enabled)MarkProjectile(value);return prior;}
        internal static void Leave(int prior){actor=prior;}
        internal static void Leave(int prior,Projectile value){if(enabled)MarkProjectile(value);actor=prior;}
        internal static void Tile(int x,int y)
        {
            if(!enabled)return;
            var used=terrain[actor];if(used==null)terrain[actor]=used=new NativeTerrainUsage();
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.Active && !used.Cells.ContainsKey(x/32*128+y/32))try{AimDiagnostics.Event("dependency-tile-source",(long)Main.GameUpdateCount,"consumer="+actor+";firstX="+x+";firstY="+y+";chunk="+(x/32*128+y/32));}catch(Exception error){AimDiagnostics.Missing("dependency-tile-source",error);}
#endif
            used.Add(x,y);
        }
        // Called only after the ordinary field permission/identity check. No
        // omitted page or unsupported field is promoted by provenance.
        internal static void Access(object value,int mode,bool directoryRead,int field)
        {
            if(mode==1 && readOnlyDepth!=0)mode=0;
            if(!enabled || temporaryDepth!=0 || mode==0 && (directoryRead || value is NPC && queryDepth!=0 && !impact))return;
            int provider;var npc=value as NPC;
            if(npc!=null)provider=npc.whoAmI;
            else{var p=value as Projectile;if(p==null)return;provider=ProjectileBase+p.whoAmI;}
            if(provider<0 || provider>=Global || provider==actor)return;
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.Active && ((mode==0 || mode==1) && (needs[actor,provider/64]&(1UL<<(provider%64)))==0 || mode!=0 && (needs[provider,actor/64]&(1UL<<(actor%64)))==0))try{diagnosticSource="field="+field+";mode="+mode+";directoryRead="+directoryRead;}catch(Exception error){AimDiagnostics.Missing("dependency-source",error);}
#endif
            if(mode==0)Link(actor,provider);
            else{Link(provider,actor);if(mode==1)Link(actor,provider);}
        }
        private static void Link(int consumer,int provider)
        {
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.Active && (needs[consumer,provider/64]&(1UL<<(provider%64)))==0)try{AimDiagnostics.Event("dependency-source",(long)Main.GameUpdateCount,"consumer="+consumer+";provider="+provider+";actor="+actor+";source="+diagnosticSource);}catch(Exception error){AimDiagnostics.Missing("dependency-source",error);}
#endif
            needs[consumer,provider/64]|=1UL<<(provider%64);
        }
        internal static NativeTerrainUsage Complete(int[] npcs,int[] projectiles,out bool[] npcRequired,out bool[] projectileRequired)
        {
            relevant[Global]=true;relevant[selected]=true;
            // A damaging shot is relevant BEFORE a target hit: a background
            // can absorb its finite penetration first. Native friendly shots
            // conservatively remain roots, as do hostile shots that can affect
            // this target/player. Already simulated harmless-to-both shots do
            // not become roots merely because they hurt an unrelated bunny.
            foreach(int slot in projectiles)MarkProjectile(Main.projectile[slot]);
            for(int i=0;i<=Main.maxProjectiles;i++)if(Main.projectile[i].active && NativeEntityDirectory.CanAdvance(Main.projectile[i]))MarkProjectile(Main.projectile[i]);
            var pending=new Queue<int>();for(int i=0;i<Count;i++)if(relevant[i])pending.Enqueue(i);
            while(pending.Count!=0)
            {
                int consumer=pending.Dequeue();
                for(int word=0;word<Words;word++)
                {
                    ulong bits=needs[consumer,word];if(bits==0)continue;
                    for(int bit=0;bit<64 && word*64+bit<Count;bit++)if((bits&(1UL<<bit))!=0 && !relevant[word*64+bit])
                    {int provider=word*64+bit;relevant[provider]=true;pending.Enqueue(provider);}
                }
            }
            npcRequired=new bool[npcs.Length];for(int i=0;i<npcs.Length;i++)npcRequired[i]=relevant[npcs[i]];
            projectileRequired=new bool[projectiles.Length];for(int i=0;i<projectiles.Length;i++)projectileRequired[i]=relevant[ProjectileBase+projectiles[i]];
            var result=new NativeTerrainUsage();for(int i=0;i<Count;i++)if(relevant[i] && terrain[i]!=null)result.Union(terrain[i]);
#if JMR_AIM_DIAGNOSTICS
            if(AimDiagnostics.Active)try{AimDiagnostics.Event("dependency-final-roles",(long)Main.GameUpdateCount,"npcs="+string.Join(",",npcs)+";npcRequired="+string.Join(",",npcRequired)+";projectiles="+string.Join(",",projectiles)+";projectileRequired="+string.Join(",",projectileRequired)+";terrainChunks="+result.Cells.Count);}catch(Exception error){AimDiagnostics.Missing("dependency-final-roles",error);}
#endif
            return result;
        }
        private static void QueryBefore(out bool __state){__state=enabled;if(__state)queryDepth++;}
        private static void QueryAfter(bool __state){if(__state)queryDepth--;}
        private static void ReadOnlyBefore(out bool __state){__state=enabled;if(__state)readOnlyDepth++;}
        private static void ReadOnlyAfter(bool __state){if(__state)readOnlyDepth--;}
        private static void CandidateBefore(NPC targetNPC,out CandidateState __state){__state=new CandidateState{Actor=candidate,Impact=impact};if(enabled){candidate=targetNPC;impact=false;}}
        private static void CandidateAfter(CandidateState __state){candidate=__state.Actor;impact=__state.Impact;}
        private static void CollisionAfter(bool __result)
        {
            if(!enabled || !__result || candidate==null || actor<ProjectileBase || actor>=Global)return;
            impact=true;
#if JMR_AIM_DIAGNOSTICS
            diagnosticSource="projectile collision";
#endif
            Link(actor,candidate.whoAmI);Link(candidate.whoAmI,actor);
        }
        private static void MarkProjectile(Projectile p)
        {if(p.friendly || p.hostile && (Main.npc[selected].friendly || vulnerablePlayer))relevant[ProjectileBase+p.whoAmI]=true;}
        private static void BirthBefore(out int __state){__state=actor;}
        private static void BirthAfter(int __state){actor=__state;}
        private static void BirthSetupAfter(Projectile __result)
        {if(!enabled)return;int producer=actor;actor=ProjectileBase+__result.whoAmI;
#if JMR_AIM_DIAGNOSTICS
            diagnosticSource="projectile birth";
#endif
            Link(actor,producer);}
        private static void PlayerHurtAfter(double __result)
        {if(enabled && __result>0){
#if JMR_AIM_DIAGNOSTICS
            diagnosticSource="player hurt";
#endif
            Link(Global,actor);}}
        // Only the locked original pair's +/-netOffset is bookkeeping. It
        // cannot manufacture a damaging write dependency for every query.
        private static void TemporaryBefore(out bool __state){__state=enabled;if(__state)temporaryDepth++;}
        private static void TemporaryAfter(bool __state){if(__state)temporaryDepth--;}
    }
}
