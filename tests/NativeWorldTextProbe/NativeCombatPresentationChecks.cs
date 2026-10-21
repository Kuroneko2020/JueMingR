using System;
using System.Collections;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeCombatPresentationChecks
    {
        // DisplayPath, rather than the producer's last cache publication, owns
        // permission to consume an older completed prefix on this update.
        // Slice that actual immutable prefix at its honest age for the native
        // future comparisons; retain its original capture identity and clock.
        // This reads no new Source work and never publishes to its cache.
        internal static NpcTrajectory CurrentPath(object host)
        {
            var path=GetOptional(host,"DisplayPath") as NpcTrajectory;
            if(path==null)return null;
            long now=(long)Main.GameUpdateCount,age=now-path.SampleTick;
            int maximumAge=(int)host.GetType().GetField("DisplayMaximumAge",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).GetRawConstantValue();
            var selection=Get(host,"Selection");var key=(NpcIdentity)Get(selection,"Target");
            if(!(bool)Get(selection,"HasTarget") || !path.Identity.Equals(key) || path.Identity.Session!=(long)Get(host,"Session") || path.Identity.Slot<0 || path.Identity.Slot>=Main.maxNPCs)
                throw new InvalidOperationException("The current display must belong to its selected session and complete native identity.");
            var n=Main.npc[path.Identity.Slot];
            if(!ReferenceEquals(path.Identity.Token,n) || !n.active || n.life<=0 || n.generation!=path.Identity.Generation || n.type!=path.Identity.Type || n.netID!=path.Identity.NetId)
                throw new InvalidOperationException("A display lease cannot retain a retired, replaced or transformed native instance.");
            if(path.CaptureTick>path.SampleTick || age<0 || age>maximumAge || age>=path.Count-1 || (long)Get(host,"displayLeaseTick")!=now)
                throw new InvalidOperationException("The current display must retain its original finite age and this update's owner lease.");
            var point=path[(int)age];
            if(point.Bounds.Width!=n.width || point.Bounds.Height!=n.height || Vector2.DistanceSquared(n.position,new Vector2(point.Bounds.X,point.Bounds.Y))>4 || Vector2.DistanceSquared(n.velocity,new Vector2(point.Vx,point.Vy))>.01f || point.Phase!=n.ai[0] || point.Wet!=n.wet || point.NoTileCollide!=n.noTileCollide)
                throw new InvalidOperationException("A leased display point must still match the actual native position, velocity, phase and collision facts at its honest age.");
            NpcTrajectory current;
            if(!path.TryWindow(now,path.Count-(int)age-1,path.Version,out current) || current.SampleTick!=now || current.CaptureTick!=path.CaptureTick)
                throw new InvalidOperationException("The display consumer must use only the completed remaining prefix without rewriting its capture.");
            return current;
        }
        // A retained display may outlive Source's latest private calculation.
        // Freeze its own cell lease for terrain-change counterexamples, never
        // attach the mutable Source cell set to a different displayed prefix.
        internal static IDictionary CurrentCells(object host)
        {
            if(GetOptional(host,"DisplayPath")==null)throw new InvalidOperationException("Terrain evidence requires its actual current display owner.");
            var cells=new Hashtable();
            foreach(object pair in (Array)Get(Get(host,"displayTerrain"),"values"))cells.Add(Get(pair,"Key"),Get(pair,"Value"));
            return cells;
        }
        internal static void Project(object world)
        {
            var zoom=Main.GameViewMatrix.ZoomMatrix;var inverse=Matrix.Invert(zoom);
            Set(world,"zoom",zoom);Set(world,"inverse",inverse);Call(world,"ProjectPresentation");
            Call(Get(world,"Marker"),"Project",zoom,inverse);
        }
    }
}
