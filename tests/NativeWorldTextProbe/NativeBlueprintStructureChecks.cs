using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static bool structureWatching;
        private static int structureChanges;
        private static void InitializeStructureNativeMetadata()
        {
            // Native Main initializes Minecart before a ready Blueprint profile
            // can capture its immutable36-slot mirror table. RunConstruction
            // calls this before its ordinary preparation first creates one.
            typeof(NativeLegacyPlacementIdentityChecks).GetMethod("PrepareTrackMetadata",F).Invoke(null,null);
        }
        private static void ConstructionStructures(object context,object host,object tools,object input,Action<string,Action> check)
        {
            var audit=new Harmony("JueMingR.Tests.BlueprintNativeStructures");
            audit.Patch(typeof(Player).GetMethod("ItemCheck_UseMiningTools_TryPoundingTile",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(StructureBeforePound)),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(StructureAfterPound)));
            try
            {
                foreach(int type in new[]{TileID.Traps,TileID.WirePipe,TileID.ProjectilePressurePad})
                {int current=type;check("special original hammer type="+current,()=>StructureSingle(context,host,tools,input,current));}
                check("original dual-slot track topology and zero-material restoration",()=>StructureTracks(context,host,tools,input));
                check("original display-doll pose capture and restoration",()=>StructureDoll(context,host,tools,input));
                check("ordinary hammer exclusions retained",()=>OrdinaryHammerEligibilityContract(context,host,tools,input));
            }
            finally{structureWatching=false;audit.UnpatchAll(audit.Id);}
        }
        private static long StructureStateAt(int x,int y)
        {
            Tile tile=Main.tile[x,y];
            if(tile.type==TileID.DisplayDoll)
            {
                int left=x-tile.frameX%36/18,top=y-tile.frameY/18;TileEntity entity;
                if(!TileEntity.ByPosition.TryGetValue(new Point16(left,top),out entity) || !(entity is TEDisplayDoll))return -1;
                return ((long)tile.type<<32)|(byte)typeof(TEDisplayDoll).GetField("_pose",F).GetValue(entity);
            }
            return ((long)tile.type<<32)|((long)(ushort)tile.frameY<<16)|(ushort)tile.frameX;
        }
        private static void StructureBeforePound(int __3,int __4,out long __state)
        {__state=structureWatching && __3<50?StructureStateAt(__3,__4):Int64.MinValue;}
        private static void StructureAfterPound(bool __runOriginal,int __3,int __4,long __state)
        {if(__runOriginal && __state!=Int64.MinValue && StructureStateAt(__3,__4)!=__state)structureChanges++;}
        private static void StructureSamplePound(object context,object input,Player p,int x,int y)
        {
            // SOURCE ONLY: native UseMiningTools returns before its rearm
            // when the cursor is outside item range. Aim the real neutral
            // input at this source, not the unrelated common Step cursor.
            Action neutral=()=>NativeCombatCadenceChecks.Step(context,false,false,0,null,new Vector2(x*16+8,y*16+8));
            for(int frame=0;frame<80 && (p.itemTime>0 || p.itemAnimation>0 || p.reuseDelay>0);frame++)neutral();
            neutral();Require(p.poundRelease,"Original in-range neutral hammer step has rearmed poundRelease.");
            object[] args={p.inventory[0],p.hitTile.HitObject(x,y,1),true,x,y};
            typeof(Player).GetMethod("ItemCheck_UseMiningTools_TryPoundingTile",F).Invoke(p,args);
        }
        private static Item StructureMaterial(int type)
        {return ContentSamples.ItemsByType.Values.Where(i=>i.createTile==type && i.placeStyle==0).OrderBy(i=>i.type).First();}
        private static void StructurePadSupport(int x,int y)
        {foreach(Point point in new[]{new Point(x-1,y),new Point(x+1,y),new Point(x,y-1),new Point(x,y+1)})NativeToolsChecks.Tile(point.X,point.Y,TileID.Stone);}
        private static void StructureSingle(object context,object host,object tools,object input,int type)
        {
            Player p=Reset(context,host,tools,input);p.position.X=58*16;p.inventory[0].SetDefaults(ItemID.WoodenHammer);
            if(type==TileID.ProjectilePressurePad)StructurePadSupport(60,42);
            Require(WorldGen.PlaceTile(60,42,type,true,true,p.whoAmI,0),"Independent original source body, type="+type);
            long initial=StructureStateAt(60,42);StructureSamplePound(context,input,p,60,42);
            Require(StructureStateAt(60,42)!=initial,"Original dedicated source hammer changes its actual structural field, type="+type);
            var library=(LibraryState)Get(host,"Library");
            var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,library,()=>Step(context,input),60,42,1,1,"原生特殊锤击"+type);
            var saved=OpenCapturedAppearance(library,entry);CaptureCell wanted=saved.Cells.Single();
            Require(wanted.FrameX==Main.tile[60,42].frameX && wanted.FrameY==Main.tile[60,42].frameY,"Actual capture/save/reopen preserves native structural fields.");
            p.position=new Vector2(640,646);p.inventory[0].TurnToAir();p.inventory[12].SetDefaults(ItemID.WoodenHammer);
            Item material=StructureMaterial(type);p.inventory[13].SetDefaults(material.type);p.inventory[13].stack=7;
            if(type==TileID.ProjectilePressurePad)StructurePadSupport(44,42);
            Require(WorldGen.PlaceTile(44,42,type,true,true,p.whoAmI,0),"Existing destination body is original default placement, not desired state.");
            Require(Main.tile[44,42].frameX!=wanted.FrameX || Main.tile[44,42].frameY!=wanted.FrameY,"Independent source target differs from destination before production runs.");
            Place(context,host,input,entry,44,42);
            if(type==TileID.Traps)StructureMirrorTwice(context,host,input,44,42);
            structureChanges=0;structureWatching=true;
            try{SetAutomatic(host,input,true);Until(context,input,()=>Scene(host).Length==0);}
            finally{structureWatching=false;SetAutomatic(host,input,false);}
            Require(Main.tile[44,42].frameX==wanted.FrameX && Main.tile[44,42].frameY==wanted.FrameY && structureChanges>0 && p.inventory[13].stack==7 && p.inventory[12].stack==1,
                "Production uses an actual dedicated hammer transition, reaches the exact captured state, and does not consume a second body: "+type);
            Console.WriteLine("BLUEPRINT STRUCTURE type="+type+" source=original-pounding capture/save/reopen=true actual-native-changes="+structureChanges+" body-debit=0 final-frame="+wanted.FrameX+","+wanted.FrameY+" retired=true");
        }
        private static void StructureMirrorTwice(object context,object host,object input,int x,int y)
        {
            for(int pass=0;pass<2;pass++)
                StructureMirrorCommit(context,host,input,x,y,pass==0);
        }
        private static void StructureMirrorCommit(object context,object host,object input,int x,int y,bool mirrored)
        {
                NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"MirrorProjection");
                Require((bool)Get(host,"ProjectionEditing"),"Actual mirror action enters its owned world gesture.");
                Call(host,"AcceptWorldPointer",true,false,false,x,y,Main.maxTilesX,Main.maxTilesY);
                Call(host,"AcceptWorldPointer",true,true,false,x,y,Main.maxTilesX,Main.maxTilesY);
                Until(context,input,()=>!(bool)Get(host,"ProjectionEditing"));
                Require((bool)Get(Scene(host).Single(),"MirroredHorizontally")==mirrored,"The actual mirror commit updates the same instance's persisted view.");
        }
        private static void StructureTrackBody(int x,int y,Player p)
        {
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                Require(WorldGen.PlaceTile(x+dx,y+dy,TileID.MinecartTrack,true,true,p.whoAmI,0),"Original finite 3x3 track topology source/default body.");
            for(int pass=0;pass<2;pass++)for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)Minecart.FrameTrack(x+dx,y+dy,false,true);
        }
        private static void StructureTracks(object context,object host,object tools,object input)
        {
            Player p=Reset(context,host,tools,input);p.position.X=58*16;p.inventory[0].SetDefaults(ItemID.WoodenHammer);
            StructureTrackBody(60,42,p);var seen=new HashSet<long>();long initial=StructureStateAt(60,42);
            StructureSamplePound(context,input,p,60,42);
            while(Main.tile[60,42].frameY<0 && seen.Add(StructureStateAt(60,42)))StructureSamplePound(context,input,p,60,42);
            Require(Main.tile[60,42].frameY>=0 && StructureStateAt(60,42)!=initial,"Original local topology and actual hammer cycle produce a changed genuine dual-slot track.");
            var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>Step(context,input),59,41,3,3,"原生双槽轨道");
            var saved=OpenCapturedAppearance((LibraryState)Get(host,"Library"),entry);
            int nativeStates=saved.Cells.Sum(c=>StructureTrackCycleStates(59+c.X,41+c.Y));
            p.position=new Vector2(640,646);p.inventory[0].TurnToAir();p.inventory[12].SetDefaults(ItemID.WoodenHammer);p.inventory[13].SetDefaults(StructureMaterial(TileID.MinecartTrack).type);p.inventory[13].stack=9;
            // Expected reflection comes from the original36 physical tables:
            // reverse the sampled8-pixel rail heights and swap actual endpoint
            // geometry, independently of the product's mirror resolver.
            var expected=saved.Cells.Select(c=>{c.X=2-c.X;c.FrameX=StructureMirroredTrack(c.FrameX);c.FrameY=StructureMirroredTrack(c.FrameY);return c;}).ToArray();
            // Mirror before adding the native default destination. Eight
            // unpounded neighbors otherwise already Match, and the accepted
            // partial-completion gate correctly rejects a later mirror.
            Require(saved.Cells.All(c=>!Main.tile[43+c.X,41+c.Y].active() || Main.tile[43+c.X,41+c.Y].type!=TileID.MinecartTrack),"The mirror starts before any destination rail has been built.");
            Place(context,host,input,entry,43,41);StructureMirrorCommit(context,host,input,43,41,true);
            StructureTrackBody(44,42,p);
            Require(expected.Any(c=>Main.tile[43+c.X,41+c.Y].frameX!=c.FrameX || Main.tile[43+c.X,41+c.Y].frameY!=c.FrameY),"Original destination topology needs actual mirrored track postprocessing.");
            int frameBudget=checked((nativeStates+expected.Length)*(Math.Max(p.inventory[12].useAnimation,p.inventory[12].useTime)+6));
            int frames=0;structureChanges=0;structureWatching=true;
            try
            {
                SetAutomatic(host,input,true);
                // Nine real native cycles take more than a common8-second
                // worker wait. Bound actual Updates by the source's original
                // reachable states and the held hammer's natural animation,
                // with one settlement/selection allowance per target.
                for(;frames<frameBudget && Scene(host).Length!=0;frames++)Step(context,input);
                if(Scene(host).Length!=0)throw new TimeoutException("Finite original track-state/timer budget exhausted: frames="+frames+" nativeStates="+nativeStates);
            }
            finally{structureWatching=false;SetAutomatic(host,input,false);}
            Require(expected.All(c=>Main.tile[43+c.X,41+c.Y].frameX==c.FrameX && Main.tile[43+c.X,41+c.Y].frameY==c.FrameY) && structureChanges>0 && p.inventory[13].stack==9,
                "Actual dedicated native track hammers reach both independently mirrored slots throughout the finite shared neighborhood without another rail material.");
        }
        private static int StructureTrackCycleStates(int x,int y)
        {
            var options=(int[][])AccessTools.Field(typeof(Minecart),"_trackSwitchOptions").GetValue(null);
            var kinds=(int[])AccessTools.Field(typeof(Minecart),"_trackType").GetValue(null);
            int mask=(int)AccessTools.Method(typeof(Minecart),"GetNearbyTilesSetLookupIndex").Invoke(null,new object[]{x,y});
            int front=Main.tile[x,y].frameX,kind=front>=0?kinds[front]:0,n=options[mask]?.Count(frame=>kinds[frame]==kind)??0;
            int bound=checked(n*(n+1)+2);var states=new HashSet<long>();
            // SOURCE ONLY. FrameTrack changes just this native source tile;
            // neighbor eligibility reads bodies, not their slot values. The
            // saved target is already immutable. Never sample the destination.
            for(int i=0;i<bound;i++)
            {if(!states.Add(StructureStateAt(x,y)))return states.Count;Minecart.FrameTrack(x,y,true,true);}
            throw new InvalidOperationException("Original rail cycle exceeded its finite front/back option count.");
        }
        private static short StructureMirroredTrack(short frame)
        {
            if(frame<0)return frame;
            var left=(int[])AccessTools.Field(typeof(Minecart),"_leftSideConnection").GetValue(null);
            var right=(int[])AccessTools.Field(typeof(Minecart),"_rightSideConnection").GetValue(null);
            var types=(int[])AccessTools.Field(typeof(Minecart),"_trackType").GetValue(null);
            var heights=(int[][])AccessTools.Field(typeof(Minecart),"_tileHeight").GetValue(null);
            var boost=(bool[])AccessTools.Field(typeof(Minecart),"_boostLeft").GetValue(null);
            var candidates=Enumerable.Range(0,36).Where(i=>types[i]==types[frame] && left[i]==right[frame] && right[i]==left[frame] &&
                heights[i].SequenceEqual(heights[frame].Reverse()) && (types[i]!=2 || boost[i]!=boost[frame])).ToArray();
            Require(candidates.Length==1,"The sampled original rail's physical reflection has one exact native frame: "+frame);
            return (short)candidates[0];
        }
        private static void StructureDoll(object context,object host,object tools,object input)
        {
            Player p=Reset(context,host,tools,input);
            using(new PayloadEntityFixture())
            {
                p.position.X=58*16;p.inventory[0].SetDefaults(ItemID.WoodenHammer);
                Require(WorldGen.PlaceObject(60,42,TileID.DisplayDoll,true,0,direction:1),"Independent original display-doll source body.");
                Require(TEDisplayDoll.Hook_AfterPlacement(60,42)>=0,"Original source post-placement hook creates its real pose owner.");
                StructureSamplePound(context,input,p,60,40);
                long wantedPose=StructureStateAt(60,40);
                Require((wantedPose&255)>0 && (wantedPose&255)<=3,"Empty source doll reaches a real original pose without copied equipment.");
                var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>Step(context,input),60,40,2,3,"原生人体模型姿态");
                var saved=OpenCapturedAppearance((LibraryState)Get(host,"Library"),entry);
                Require(saved.Cells.Count(c=>c.Payload.PoseState==BlueprintPayloadState.Known)==1 && saved.Cells.Single(c=>c.Payload.PoseState==BlueprintPayloadState.Known).Payload.Pose==(byte)wantedPose,"Saved complete object carries the independently observed pose at one anchor only.");
                p.position=new Vector2(640,646);p.inventory[0].TurnToAir();p.inventory[12].SetDefaults(ItemID.WoodenHammer);p.inventory[13].SetDefaults(StructureMaterial(TileID.DisplayDoll).type);p.inventory[13].stack=7;
                Require(WorldGen.PlaceObject(44,42,TileID.DisplayDoll,true,0,direction:1) && TEDisplayDoll.Hook_AfterPlacement(44,42)>=0,"Original destination doll has its own default pose owner.");
                Require(StructureStateAt(44,40)!=wantedPose,"Destination has not been prewritten to the wanted pose.");
                Place(context,host,input,entry,44,40);structureChanges=0;structureWatching=true;
                try{SetAutomatic(host,input,true);Until(context,input,()=>Scene(host).Length==0);}
                finally{structureWatching=false;SetAutomatic(host,input,false);}
                Require(StructureStateAt(44,40)==wantedPose && structureChanges>0 && p.inventory[13].stack==7,"Actual original doll pose consumer restores saved state with no second body or copied equipment.");
            }
        }
    }
}
