using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    internal enum PalmHitState { None, Found, Unconfirmed, Overlap }
    internal struct PalmHit
    { internal int X,Y; internal Tile Tile; internal Rectangle Bounds; }

    // Request-only three-cell trunk lookup and a bounded visible crown receipt.
    // Crown hit means the actual transformed texture quad, NOT opaque pixels.
    // Native biome discovery is observed at its caller; we never execute its
    // unbounded downward scan or infer a nearest tree from the cursor.
    internal sealed class NativePalmGeometry:IDisposable
    {
        private const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static;
        private static NativePalmGeometry current;
        private static readonly FieldInfo Renderer=typeof(TileDrawing).GetField("_natureRenderer",F), Entries=typeof(NextNatureRenderer).GetField("_entries",F);
        private readonly Func<bool> demand;
        private struct Crown
        {
            internal int X,Y,FrameX,FrameY;internal Tile Tile;
            internal Vector2 Position,Origin;internal Rectangle Source,Bounds;internal float Rotation,Scale;
            internal bool Contains(Vector2 point)
            {
                Vector2 local=Vector2.Transform(point-Position,Matrix.CreateRotationZ(-Rotation))/Scale+Origin;
                return local.X>=0 && local.Y>=0 && local.X<Source.Width && local.Y<Source.Height;
            }
            internal bool Matches()
            {return NativeDisplayContents.Loaded(X,Y) && ReferenceEquals(Main.tile[X,Y],Tile) && Tile.active() && Tile.type==323 && Tile.frameX==FrameX && Tile.frameY==FrameY;}
        }
        private Crown[] building=new Crown[128],committed=new Crown[128];
        private int buildingCount,committedCount;
        private bool recording,outerComplete,overflow,available;
        private object buildingTiles,buildingWorld,buildingConnection,committedTiles,committedWorld,committedConnection;
        private Vector2 unscaled;
        private Vector2 buildingScreen,committedScreen;
        private Matrix buildingZoom,committedZoom;
        private float buildingGravity,committedGravity;
        internal NativePalmGeometry(Func<bool> demand){this.demand=demand;}
        internal void Install(Harmony harmony,List<MethodInfo> patches)
        {
            if(current!=null)throw new InvalidOperationException("palm-owner-already-installed");current=this;
            var trees=typeof(TileDrawing).GetMethod("DrawTrees",F);var outer=typeof(TileDrawing).GetMethod("PostDrawTiles",new[]{typeof(bool)});
            if(trees==null || outer==null)throw new MissingMethodException("palm-native-producer");
            patches.Add(trees);harmony.Patch(trees,transpiler:Hook(nameof(TreesTranspiler)));
            patches.Add(outer);harmony.Patch(outer,prefix:Hook(nameof(BeginOuter)),postfix:Hook(nameof(EndOuter)),finalizer:Hook(nameof(OuterFinally)));
        }
        private static HarmonyMethod Hook(string name){return new HarmonyMethod(typeof(NativePalmGeometry).GetMethod(name,F));}
        private static void BeginOuter(bool __0,out bool __state)
        {
            __state=!__0 && current!=null;
            if(!__state)return;var owner=current;
            if(!owner.demand()){owner.Invalidate();owner.recording=false;return;}
            owner.recording=true;owner.outerComplete=false;owner.overflow=false;owner.buildingCount=0;
            owner.buildingTiles=Main.tile;owner.buildingWorld=Main.ActiveWorldFileData;owner.buildingConnection=Netplay.Connection;owner.unscaled=Main.Camera.UnscaledPosition;
            owner.buildingScreen=PlayerInput.OriginalScreenSize;owner.buildingZoom=Main.GameViewMatrix.ZoomMatrix;owner.buildingGravity=Main.LocalPlayer.gravDir;
        }
        private static void EndOuter(bool __runOriginal,bool __state)
        {if(!__state || current==null)return;current.outerComplete=current.recording && __runOriginal;current.recording=false;if(!current.outerComplete)current.Invalidate();}
        private static Exception OuterFinally(Exception __exception,bool __state)
        {if(__state && __exception!=null && current!=null)current.Invalidate();return __exception;}
        internal void CommitVisibleFrame(bool complete)
        {
            // DrawTrees only queues nature sprites. PostDrawTiles(false) must
            // reach DrawAfterAllObjects, then the same UI pass must complete.
            // An interrupted/skipped producer never publishes a partial table.
            if(!complete || !outerComplete || overflow || !ReferenceEquals(buildingTiles,Main.tile) || !ReferenceEquals(buildingWorld,Main.ActiveWorldFileData) || !ReferenceEquals(buildingConnection,Netplay.Connection) || buildingScreen!=PlayerInput.OriginalScreenSize || buildingZoom!=Main.GameViewMatrix.ZoomMatrix || buildingGravity!=Main.LocalPlayer.gravDir)
            {Invalidate();return;}
            var old=committed;committed=building;building=old;committedCount=buildingCount;
            committedTiles=buildingTiles;committedWorld=buildingWorld;committedConnection=buildingConnection;available=true;outerComplete=false;
            committedScreen=buildingScreen;committedZoom=buildingZoom;committedGravity=buildingGravity;
        }
        internal void Invalidate(){available=outerComplete=recording=false;}
        private static bool CrownFrame(Tile tile){return NativeNaturalNames.PalmFrame(tile) && tile.frameX>=88 && tile.frameX<=132;}
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RecordCrown(int before,TileDrawing drawing,int x,int y,Texture2D texture,Vector2 position,Rectangle source,Color color,float rotation,Vector2 origin,float scale,SpriteEffects effects)
        {
            var owner=current;if(owner==null || !owner.recording)return;
            try
            {
                if(before<0 || QueueCount(drawing)!=before+1 || !NativeDisplayContents.Loaded(x,y) || !CrownFrame(Main.tile[x,y]) || texture==null || source.Width<=0 || source.Height<=0 || scale<=0 || float.IsNaN(scale) || float.IsInfinity(scale) || float.IsNaN(rotation) || float.IsInfinity(rotation)) {owner.overflow=true;return;}
                if(owner.buildingCount==owner.building.Length){owner.overflow=true;return;}
                Tile tile=Main.tile[x,y];var crown=new Crown{X=x,Y=y,Tile=tile,FrameX=tile.frameX,FrameY=tile.frameY,Position=position+owner.unscaled,Source=source,Origin=origin,Rotation=rotation,Scale=scale};
                Vector2 min=new Vector2(float.MaxValue),max=new Vector2(float.MinValue);Matrix rotate=Matrix.CreateRotationZ(rotation);
                Include(Vector2.Zero,ref min,ref max,crown,rotate);Include(new Vector2(source.Width,0),ref min,ref max,crown,rotate);Include(new Vector2(0,source.Height),ref min,ref max,crown,rotate);Include(new Vector2(source.Width,source.Height),ref min,ref max,crown,rotate);
                if(float.IsNaN(min.X) || float.IsNaN(min.Y) || float.IsInfinity(min.X) || float.IsInfinity(min.Y)){owner.overflow=true;return;}
                int left=(int)Math.Floor(min.X),top=(int)Math.Floor(min.Y);crown.Bounds=new Rectangle(left,top,(int)Math.Ceiling(max.X)-left,(int)Math.Ceiling(max.Y)-top);
                owner.building[owner.buildingCount++]=crown;
            }
            catch{owner.overflow=true;}
        }
        private static void Include(Vector2 point,ref Vector2 min,ref Vector2 max,Crown crown,Matrix rotate)
        {Vector2 vertex=Vector2.Transform((point-crown.Origin)*crown.Scale,rotate)+crown.Position;min=Vector2.Min(min,vertex);max=Vector2.Max(max,vertex);}
        private static int QueueCount(TileDrawing drawing)
        {
            if(current==null || !current.recording)return -1;
            try{var renderer=Renderer.GetValue(drawing) as NextNatureRenderer;return renderer==null?-1:((System.Collections.ICollection)Entries.GetValue(renderer)).Count;}
            catch{current.overflow=true;return -1;}
        }
        private static IEnumerable<CodeInstruction> TreesTranspiler(IEnumerable<CodeInstruction> instructions,ILGenerator generator)
        {
            var code=new List<CodeInstruction>(instructions);var result=new List<CodeInstruction>();
            var biome=typeof(TileDrawing).GetMethod("GetPalmTreeBiome",F);var draw=typeof(TileDrawing).GetMethod("DrawNature",F);
            if(biome==null || draw==null)throw new MissingMethodException("palm-callsite-methods");
            var x=generator.DeclareLocal(typeof(int));var y=generator.DeclareLocal(typeof(int));var args=new List<LocalBuilder>{generator.DeclareLocal(typeof(TileDrawing))};
            var queued=generator.DeclareLocal(typeof(int));
            foreach(var parameter in draw.GetParameters())args.Add(generator.DeclareLocal(parameter.ParameterType));
            int biomes=0,draws=0;
            foreach(var instruction in code)
            {
                if(instruction.Calls(biome))
                {
                    biomes++;var first=new CodeInstruction(OpCodes.Stloc,y);first.labels.AddRange(instruction.labels);instruction.labels.Clear();result.Add(first);result.Add(new CodeInstruction(OpCodes.Stloc,x));result.Add(new CodeInstruction(OpCodes.Ldloc,x));result.Add(new CodeInstruction(OpCodes.Ldloc,y));
                }
                if(instruction.Calls(draw) && ++draws==4)
                {
                    // Preserve the exact original drawing call, even if its
                    // forwarding wrapper is inlined. Observe actual parameters
                    // at this locked caller after the original normal return.
                    for(int i=args.Count-1;i>=0;i--){var store=new CodeInstruction(OpCodes.Stloc,args[i]);if(i==args.Count-1){store.labels.AddRange(instruction.labels);instruction.labels.Clear();}result.Add(store);}
                    result.Add(new CodeInstruction(OpCodes.Ldloc,args[0]));result.Add(new CodeInstruction(OpCodes.Call,typeof(NativePalmGeometry).GetMethod(nameof(QueueCount),F)));result.Add(new CodeInstruction(OpCodes.Stloc,queued));
                    foreach(var argument in args)result.Add(new CodeInstruction(OpCodes.Ldloc,argument));result.Add(instruction);
                    result.Add(new CodeInstruction(OpCodes.Ldloc,queued));result.Add(new CodeInstruction(OpCodes.Ldloc,args[0]));
                    result.Add(new CodeInstruction(OpCodes.Ldloc,x));result.Add(new CodeInstruction(OpCodes.Ldloc,y));
                    for(int i=1;i<=8;i++)result.Add(new CodeInstruction(OpCodes.Ldloc,args[i]));
                    result.Add(new CodeInstruction(OpCodes.Call,typeof(NativePalmGeometry).GetMethod(nameof(RecordCrown),F)));continue;
                }
                result.Add(instruction);
            }
            if(biomes!=1 || draws!=4)throw new InvalidOperationException("palm-callsite-shape");return result;
        }
        private bool ContextMatches(){return available && ReferenceEquals(committedTiles,Main.tile) && ReferenceEquals(committedWorld,Main.ActiveWorldFileData) && ReferenceEquals(committedConnection,Netplay.Connection) && committedScreen==PlayerInput.OriginalScreenSize && committedZoom==Main.GameViewMatrix.ZoomMatrix && committedGravity==Main.LocalPlayer.gravDir;}
        private bool HasCrown(int x,int y)
        {if(!ContextMatches())return false;for(int i=0;i<committedCount;i++)if(committed[i].X==x && committed[i].Y==y && committed[i].Matches())return true;return false;}
        private static void Add(ref int count,ref PalmHit hit,int x,int y,Rectangle bounds)
        {if(count>0 && hit.X==x && hit.Y==y)return;count++;hit=new PalmHit{X=x,Y=y,Tile=Main.tile[x,y],Bounds=bounds};}
        internal static PalmHitState Resolve(Vector2 point,out PalmHit hit)
        {
            hit=default(PalmHit);int count=0,x=(int)Math.Floor(point.X/16),y=(int)Math.Floor(point.Y/16);bool unknown=false;var owner=current;
            for(int sx=x-1;sx<=x+1;sx++)
            {
                if(sx<0 || sx>=Main.maxTilesX || y<0 || y>=Main.maxTilesY)continue;
                if(!NativeDisplayContents.Loaded(sx,y))continue;
                Tile tile=Main.tile[sx,y];if(tile==null || !tile.active() || !NativeNaturalNames.PalmFrame(tile) || CrownFrame(tile))continue;
                Rectangle? native=WorldGen.GetTileVisualHitbox(sx,y);if(native.HasValue && native.Value.Contains(point.ToPoint()))Add(ref count,ref hit,sx,y,native.Value);
            }
            if(owner!=null && owner.ContextMatches())for(int i=0;i<owner.committedCount;i++)
            {Crown crown=owner.committed[i];if(!crown.Contains(point))continue;if(!crown.Matches()){unknown=true;continue;}Add(ref count,ref hit,crown.X,crown.Y,crown.Bounds);}
            // This fixed local guard only REJECTS missing crown evidence; it
            // never selects a tree or expands its hitbox. Known valid receipts
            // provide exact quad negatives. No unreceived tile is read.
            for(int sx=x-5;sx<=x+5;sx++)for(int sy=y-1;sy<=y+7;sy++)
            {
                if(sx<0 || sy<0 || sx>=Main.maxTilesX || sy>=Main.maxTilesY)continue;
                // An unknown neighbouring section alone is not evidence of a
                // palm. Preserve unrelated received air/wall/wire results;
                // an actual visible crown receipt with a now-unreceived source
                // is rejected separately by Crown.Matches above.
                if(!NativeDisplayContents.Loaded(sx,sy))continue;
                Tile tile=Main.tile[sx,sy];if(tile!=null && tile.active() && CrownFrame(tile) && (owner==null || !owner.HasCrown(sx,sy)))unknown=true;
            }
            return count>1?PalmHitState.Overlap:unknown?PalmHitState.Unconfirmed:count==1?PalmHitState.Found:PalmHitState.None;
        }
        public void Dispose(){Invalidate();if(ReferenceEquals(current,this))current=null;}
    }
}
