using System;
using System.IO;
using System.Reflection;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // The authenticated private image already contains every tile callsite
    // guard. This owner controls its lifecycle, sticky failure and used cells.
    // No runtime-wide Harmony patch scan or per-method native detour is needed.
    internal sealed class NativeTileBoundary : IDisposable
    {
        private static FieldInfo enabled,missing,x,y;
        private static Func<Tile[,],int,int,Tile> guardedRead;
        private static NativeTerrainUsage usage;
        internal static NativeTerrainUsage Usage {get{return usage;}}
        internal static bool Missing {get{return missing!=null && (bool)missing.GetValue(null);}}
        internal static int MissingX {get{return x==null?-1:(int)x.GetValue(null);}}
        internal static int MissingY {get{return y==null?-1:(int)y.GetValue(null);}}
        internal int Methods {get{return 1298;}}
        internal int Accesses {get{return 13750;}}
        internal NativeTileBoundary()
        {
            Type guard=typeof(Main).Assembly.GetType(NativeTileImage.GuardName,true);
            const BindingFlags flags=BindingFlags.Public|BindingFlags.Static;
            enabled=guard.GetField("Enabled",flags);missing=guard.GetField("Missing",flags);
            x=guard.GetField("MissingX",flags);y=guard.GetField("MissingY",flags);
            var observed=guard.GetField("Observed",flags);
            if(enabled==null || missing==null || x==null || y==null || enabled.FieldType!=typeof(bool) || missing.FieldType!=typeof(bool) || x.FieldType!=typeof(int) || y.FieldType!=typeof(int))
                throw new InvalidDataException("Private tile guard shape.");
            if(observed==null || observed.FieldType!=typeof(Action<int,int>))throw new InvalidDataException("Private tile observation shape.");
            observed.SetValue(null,new Action<int,int>((column,row)=>{usage.Add(column,row);NativePredictionPurpose.Tile(column,row);}));
            VerifyIntrinsics(guard);End();
            guardedRead=(Func<Tile[,],int,int,Tile>)Delegate.CreateDelegate(typeof(Func<Tile[,],int,int,Tile>),guard.GetMethod("Get",BindingFlags.NonPublic|BindingFlags.Static));
        }
        private delegate ref Tile AddressAccess(Tile[,] tiles,int x,int y);
        private static void VerifyIntrinsics(Type guard)
        {
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
            var get=(Func<Tile[,],int,int,Tile>)Delegate.CreateDelegate(typeof(Func<Tile[,],int,int,Tile>),guard.GetMethod("Get",flags));
            var set=(Action<Tile[,],int,int,Tile>)Delegate.CreateDelegate(typeof(Action<Tile[,],int,int,Tile>),guard.GetMethod("Set",flags));
            var address=(AddressAccess)Delegate.CreateDelegate(typeof(AddressAccess),guard.GetMethod("Address",flags));
            // This is exclusively the worker's private world. Verify the JIT's
            // multidimensional intrinsic/ref semantics once before accepting
            // any snapshot; restore even when a runtime rejects the image.
            Tile[,] previous=Main.tile;
            try
            {
                var alias=new Tile[2,2];Main.tile=alias;var first=new Tile();alias[1,1]=first;Begin();
                if(!ReferenceEquals(get(alias,1,1),first))throw new InvalidDataException("Guard Get changed identity.");
                if(!Usage.Contains(0,1,1) || Usage.Contains(0,0,1))throw new InvalidDataException("Guard omitted a real air tile read.");
                var second=new Tile();set(alias,1,1,second);if(!ReferenceEquals(alias[1,1],second))throw new InvalidDataException("Guard Set changed alias.");
                ref Tile location=ref address(alias,1,1);location=first;if(!ReferenceEquals(alias[1,1],first))throw new InvalidDataException("Guard Address lost reference.");
                bool refused=false;try{get(alias,0,1);}catch(InvalidDataException){refused=true;}
                if(!refused || !Missing || MissingX!=0 || MissingY!=1)throw new InvalidDataException("Guard missing state is not sticky.");
                Begin();bool outside=false;try{get(alias,-1,0);}catch(IndexOutOfRangeException){outside=true;}
                if(!outside || Missing)throw new InvalidDataException("Guard changed native outside-world behavior.");
                if(get(new Tile[1,1],0,0)!=null)throw new InvalidDataException("Guard altered an unrelated array.");
            }
            finally{End();Main.tile=previous;}
        }
        internal static void Begin(){usage=new NativeTerrainUsage();missing.SetValue(null,false);x.SetValue(null,-1);y.SetValue(null,-1);enabled.SetValue(null,true);}
        internal static void End(){if(enabled!=null)enabled.SetValue(null,false);}
        // Host adapters are outside the rewritten game image. Their explicit
        // terrain reads use the same guard, so a missing page is not an observed
        // native null tile. The fallback is for standalone original test worlds.
        internal static Tile Read(int column,int row){return guardedRead==null?Main.tile[column,row]:guardedRead(Main.tile,column,row);}
        public void Dispose(){End();}
    }
}
