using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Same real ItemCheck, native timers and synthetic layout in both arms.
    // The reference supplies a deterministic legal mouse trajectory instead
    // of human aiming; it never clears targets or advances timers itself.
    internal static class NativeToolCadenceChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private sealed class Result {internal readonly List<int> Effects=new List<int>();internal int Frames;}
        internal static void Run(object context)
        {
            object host=Get(context,"Tools"),input=Get(context,"Input");var p=Main.LocalPlayer;
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return ((JueMingR.Features.Tools.ToolSettings[])Get(host,"Settings")).All(s=>s.Loaded);});
            typeof(Main).GetMethod("Initialize_TileAndNPCData1",Flags).Invoke(null,null);
            typeof(Main).GetMethod("Initialize_TileAndNPCData2",Flags).Invoke(null,null);TileObjectData.Initialize();
            for(int i=1;i<Main.player.Length;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            var isolation=new Harmony("JueMingR.Tests.G09CadenceAchievement");
            var achievement=typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleMining",Flags);
            isolation.Patch(achievement,prefix:new HarmonyMethod(typeof(NativeToolCadenceChecks),nameof(SkipAchievement)));
            var failures=new List<string>();
            try
            {
                foreach(int id in new[]{ItemID.StaffofRegrowth,ItemID.AcornAxe})foreach(int slot in new[]{0,12})
                    Compare(context,host,input,id,slot,true,1f,failures);
                foreach(int id in new[]{ItemID.ShroomiteDiggingClaw,ItemID.CopperPickaxe,ItemID.AdamantiteDrill,ItemID.Drax,ItemID.Picksaw})
                    Compare(context,host,input,id,0,false,1f,failures);
                Compare(context,host,input,ItemID.ShroomiteDiggingClaw,0,false,0.75f,failures);
                Fairness(context,host,input);
            }
            finally{isolation.Unpatch(achievement,HarmonyPatchType.All,isolation.Id);for(int i=0;i<3;i++)NativeToolsChecks.SetMode(host,i,0);}
            Require(failures.Count==0,"continuous cadence must preserve the native legal opportunities: "+string.Join("; ",failures));
            Console.WriteLine("PASS G09 cadence: two regrowth tools held/borrowed, Shroomite Claw with native pickSpeed, low-power thirty-cell vein, drills and combined tools against native held-use reference.");
        }
        private static bool SkipAchievement(){return false;}
        private static void Fairness(object context,object host,object input)
        {
            var p=Main.LocalPlayer;var processing=Get(context,"Processing");var use=Get(host,"Use");
            for(int i=0;i<3;i++)NativeToolsChecks.SetMode(host,i,0);for(int i=0;i<60;i++)NativeToolsChecks.Frame(context,input);
            foreach(var item in p.inventory)item.TurnToAir();foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;
            for(int x=30;x<60;x++)for(int y=30;y<50;y++)Main.tile[x,y].ClearEverything();
            p.position=new Vector2(640,640);p.inventory[0].SetDefaults(424);p.inventory[0].stack=50;p.inventory[12].SetDefaults(213);p.selectedItemState.Select(0);p.selectedItemState.Update();
            NativeToolsChecks.Tile(42,42,1);Require(WorldGen.PlaceTile(42,41,78,mute:true,forced:true,plr:0),"fairness actual pot");NativeToolsChecks.Tile(42,40,84);NativeExtractionChecks.Machine(44,40,219);
            NativeToolsChecks.SetMode(host,1,1);for(int i=0;i<4;i++){NativeToolsChecks.Frame(context,input);Main.tile[42,40].type=84;}
            Require((bool)Get(use,"Active") && (bool)Call(use,"ProtectOriginal",p.inventory[0]),"fairness begins with tool borrowing the original sole extraction material");
            Call(processing,"Set",1,true);NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return (bool)Call(processing,"Value",1);});
            int first=-1,harvests=0,afterExtractHarvest=-1;int initial=p.inventory[0].stack;
            try
            {
                for(int f=1;f<=180;f++)
                {
                    NativeToolsChecks.Frame(context,input);
                    if(p.inventory[0].stack<initial && first<0)first=f;
                    if(Main.tile[42,40].type==82){harvests++;if(first>0 && afterExtractHarvest<0)afterExtractHarvest=f;Main.tile[42,40].type=84;}
                    // Synthetic external maturation keeps a real ready target
                    // throughout contention; this arm is not a growth/cadence
                    // benchmark. Every harvest/consume still uses ItemCheck.
                }
                Console.WriteLine("G09 sustained contention: firstExtract="+first+" nextHarvest="+afterExtractHarvest+" harvests="+harvests+" consumes="+(initial-p.inventory[0].stack));
                Require(first>0 && first<=p.inventory[12].useAnimation*2+2,"sole protected original material gets a bounded native handoff");
                Require(afterExtractHarvest>first && afterExtractHarvest-first<=p.inventory[0].useAnimation+2 && initial-p.inventory[0].stack>=2 && harvests>=2,"extraction yields its next native selection turn back to a real ready tool");
            }
            finally{NativeToolsChecks.SetMode(host,1,0);Call(processing,"Set",1,false);NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return (bool)Call(processing,"Controls",1);});for(int i=0;i<60;i++)NativeToolsChecks.Frame(context,input);}
        }
        private static void Compare(object context,object host,object input,int id,int slot,bool herbs,float speed,List<string> failures)
        {
            var reference=RunArm(context,host,input,id,slot,herbs,speed,false,6000);
            var automatic=RunArm(context,host,input,id,slot,herbs,speed,true,Math.Max(200,reference.Frames*4+100));
            string label=(herbs?"herbs":"mining")+" tool="+id+" source="+slot+" pickSpeed="+speed;
            Console.WriteLine("G09 cadence "+label+" native="+string.Join(",",reference.Effects)+" R="+string.Join(",",automatic.Effects)+" frames="+reference.Frames+"/"+automatic.Frames);
            if(reference.Effects.Count==0 || automatic.Effects.Count!=reference.Effects.Count){failures.Add(label+" incomplete "+automatic.Effects.Count+"/"+reference.Effects.Count);return;}
            if(automatic.Effects[0]>reference.Effects[0]+1)failures.Add(label+" delayed first effect");
            // A single native-selection entry update is not an extra gap per
            // target. Compare the progress curve after the first real effect.
            for(int i=1;i<reference.Effects.Count;i++)
                if(automatic.Effects[i]-automatic.Effects[0]>reference.Effects[i]-reference.Effects[0]+1)
                {failures.Add(label+" progress#"+(i+1)+" lost native opportunity");break;}
        }
        private static Result RunArm(object context,object host,object input,int id,int slot,bool herbs,float speed,bool automatic,int limit)
        {
            var p=Main.LocalPlayer;
            for(int i=0;i<3;i++)NativeToolsChecks.SetMode(host,i,0);
            for(int i=0;i<80;i++)NativeToolsChecks.Frame(context,input);
            foreach(var item in p.inventory)item.TurnToAir();foreach(var q in Main.projectile)q.active=false;
            for(int x=30;x<55;x++)for(int y=30;y<55;y++)Main.tile[x,y].ClearEverything();
            // Fixture reset occurs before the measured interval, never while
            // either arm executes. Native hit damage and all timers run intact.
            p.position=new Vector2(640,640);p.velocity=Vector2.Zero;p.direction=1;p.gravDir=1;p.pickSpeed=speed;
            p.itemAnimation=p.itemTime=p.toolTime=0;p.controlUseItem=p.channel=false;p.hitTile=new HitTile();
            p.inventory[slot].SetDefaults(id);p.selectedItemState.Select(automatic?0:slot);p.selectedItemState.Update();
            Main.mouseLeft=false;Main.worldSurface=60;var points=new List<Point>();
            if(herbs)
            {
                foreach(int y in new[]{39,42})foreach(int x in new[]{38,40,42,44})
                {NativeToolsChecks.Tile(x,y+2,1);Require(WorldGen.PlaceTile(x,y+1,78,mute:true,forced:true,plr:0),"cadence real pot");NativeToolsChecks.Tile(x,y,84);Main.tile[x,y].frameX=0;points.Add(new Point(x,y));}
            }
            else
                for(int x=39;x<45;x++)for(int y=38;y<43;y++){NativeToolsChecks.Tile(x,y,6);points.Add(new Point(x,y));}
            if(automatic)
            {
                NativeToolsChecks.SetMode(host,herbs?1:2,1);
                if(!herbs)Require((bool)Call(Get(host,"Mining"),"Select",p,42,40,6,false),"cadence full native region");
            }
            var result=new Result();var completed=new bool[points.Count];int target=-1;
            for(int frame=1;frame<=limit && result.Effects.Count<points.Count;frame++)
            {
                NativeQuickItemChecks.Sample(input,new Keys[0]);Call(Get(context,"Shell"),"ProcessInput");
                if(!automatic)
                {
                    if(target<0 || completed[target])target=Enumerable.Range(0,points.Count).Where(i=>!completed[i]).OrderBy(i=>Vector2.DistanceSquared(p.Center,new Vector2(points[i].X*16+8,points[i].Y*16+8))).First();
                    var point=points[target];Main.mouseX=point.X*16+8;Main.mouseY=point.Y*16+8;Player.tileTargetX=point.X;Player.tileTargetY=point.Y;
                    Main.mouseLeft=true;PlayerInput.Triggers.Current.MouseLeft=true;
                }
                NativeQuickItemChecks.NativeFrame(p);
                foreach(var q in Main.projectile.Where(q=>q.active && q.owner==0 && (q.aiStyle==20 || q.type==445)))q.AI();
                Call(context,"UpdateRuntime");result.Frames=frame;
                for(int i=0;i<points.Count;i++)if(!completed[i])
                {
                    var t=Main.tile[points[i].X,points[i].Y];bool done=herbs?t.active() && t.type==82 && t.frameX==0:!t.active();
                    if(done){completed[i]=true;result.Effects.Add(frame);}
                }
            }
            Require(result.Effects.Count==points.Count || automatic,"native reference must finish the identical reachable layout tool="+id+" completed="+result.Effects.Count);
            if(herbs)Require(!p.inventory.Any(item=>item.type==ItemID.DaybloomSeeds),"continuous free regrowth does not fabricate/consume an inventory seed");
            if(automatic && result.Effects.Count==points.Count)NativeToolsCacheChecks.NaturalCompletion(context,herbs);
            return result;
        }
    }
}
