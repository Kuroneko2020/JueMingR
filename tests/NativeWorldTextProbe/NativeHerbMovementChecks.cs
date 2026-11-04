using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Tools;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Both arms supply mapped movement and let the real Player.Update own
    // collision, speed, selection and timers. Only the initial fixture is set
    // directly; no measured step teleports, matures or removes a plant.
    internal static class NativeHerbMovementChecks
    {
        private const int First=38,Last=90,Row=41,Limit=700;
        private static readonly Vector2 Away=new Vector2(480,540);
        private static object observedHost;
        private static bool observeCancellation;
        private static int observedStep;
        private static string firstCancellation;
        private sealed class Effect
        {
            internal int X,Step,Type,FrameX;
            internal long Input,Operation;
            internal uint Simulation;
            internal bool Active;
            public override string ToString(){return X+"@"+Step+"/"+Simulation+"/"+Input+":"+(Active?Type.ToString():"air")+"/"+FrameX+"#"+Operation;}
        }
        private sealed class Result
        {
            internal readonly List<Effect> Effects=new List<Effect>();
            internal readonly List<string> Uses=new List<string>();
            internal float Travel,MaxSpeed;
            internal int Steps;
            internal string Cancellation;
        }
        internal static void Run(object context,object host,object input)
        {
            var p=Main.LocalPlayer;
            var armor=(Item[])p.armor.Clone();var buffs=(int[])p.buffType.Clone();var times=(int[])p.buffTime.Clone();
            bool smart=Main.SmartCursorWanted_Mouse,smartPad=Main.SmartCursorWanted_GamePad;
            var failures=new List<string>();Exception originalFailure=null;
            var observer=new Harmony("JueMingR.Tests.HerbMovementCancellation");observedHost=host;
            observer.Patch(Get(host,"Use").GetType().GetMethod("Cancel",BindingFlags.Instance|BindingFlags.NonPublic),prefix:new HarmonyMethod(typeof(NativeHerbMovementChecks),nameof(Cancelled)));
            try
            {
                foreach(int tool in new[]{ItemID.StaffofRegrowth,ItemID.AcornAxe})foreach(bool boots in new[]{false,true})
                {
                    var reference=Traverse(context,host,input,tool,0,boots,false);
                    Print("reference tool="+tool+" boots="+boots,reference);
                    // A reference that cannot finish does not establish a new
                    // promise about an arbitrarily fast or impossible pass.
                    Require(reference.Effects.Count==Last-First+1,"native continuous movement reference finishes the entire reachable pass tool="+tool+" boots="+boots+" actual="+reference.Effects.Count);
                    foreach(int slot in new[]{0,12})
                    {
                        var automatic=Traverse(context,host,input,tool,slot,boots,true);
                        string label="tool="+tool+" source="+slot+" boots="+boots;
                        Print("automatic "+label,automatic);
                        if(automatic.Effects.Count!=reference.Effects.Count){failures.Add(label+" misses "+(reference.Effects.Count-automatic.Effects.Count));continue;}
                        // Compare cumulative actual progress after the first
                        // effect. Discovery entry remains separately reported;
                        // it cannot excuse repeated gaps inside a moving pass.
                        for(int i=1;i<reference.Effects.Count;i++)
                            if(automatic.Effects[i].Step-automatic.Effects[0].Step>reference.Effects[i].Step-reference.Effects[0].Step+1)
                            {failures.Add(label+" loses native progress at effect "+(i+1));break;}
                    }
                }
                if(failures.Count>0)Console.WriteLine("HERB_MOVE comparison failures: "+string.Join("; ",failures));
                Backup(context,host,input,false);Backup(context,host,input,true);
                foreach(string boundary in new[]{"complete","manual","source","off","death","session"})PendingBoundary(context,host,input,boundary);
                Require(failures.Count==0,"moving herb cadence: "+string.Join("; ",failures));
                Console.WriteLine("PASS herb moving comparison: mapped continuous walking/Hermes boots, both native regrowth tools, held/carried sources, actual effects; real water-blocked harvest and seed handoff. Synthetic Player.Update evidence only; owner experience/FPS/multiplayer unexecuted.");
            }
            catch(Exception error){originalFailure=error;Console.WriteLine("FAIL herb movement original: "+error);throw;}
            finally
            {
                observeCancellation=false;observedHost=null;
                foreach(var method in observer.GetPatchedMethods().ToArray())observer.Unpatch(method,HarmonyPatchType.All,observer.Id);
                try{NativeToolsChecks.SetMode(host,1,0);Call(Get(host,"Use"),"Retire");}
                catch(Exception cleanup){Console.WriteLine("FAIL herb movement cleanup: "+cleanup);if(originalFailure==null)throw;}
                finally
                {
                    for(int i=0;i<armor.Length;i++)p.armor[i]=armor[i];
                    Array.Copy(buffs,p.buffType,buffs.Length);Array.Copy(times,p.buffTime,times.Length);
                    Main.SmartCursorWanted_Mouse=smart;Main.SmartCursorWanted_GamePad=smartPad;
                    NativeToolExecutionChecks.Sample(context,input,Away,false);
                }
            }
        }
        private static void Cancelled(object __instance)
        {
            if(!observeCancellation || firstCancellation!=null || !(bool)Get(__instance,"Active") || (bool)Get(__instance,"cancelled"))return;
            object intent=Get(__instance,"Intent");var valid=GetOptional(intent,"Valid") as Func<bool>;var target=(Vector2)Get(intent,"Target");var tile=Main.tile[(int)(target.X/16),(int)(target.Y/16)];
            firstCancellation="step="+observedStep+" lease="+Get(__instance,"Operation")+" source="+Call(__instance,"SourceIdentity")+" identity="+Call(__instance,"Identity")+
                " admitted="+Call(observedHost,"Admit",Main.LocalPlayer,false)+" targetValid="+(valid?.Invoke()??false)+" field="+Get(Get(Get(observedHost,"Herbs"),"Field"),"Count")+
                " target="+(int)(target.X/16)+","+(int)(target.Y/16)+":"+(tile.active()?tile.type.ToString():"air")+" animation="+Main.LocalPlayer.itemAnimation+
                " path="+string.Join("<",new StackTrace(false).GetFrames().Take(9).Select(frame=>frame.GetMethod().DeclaringType?.Name+"."+frame.GetMethod().Name));
        }
        private static Player Prepare(object context,object host,object input,int tool,int slot,bool boots)
        {
            var p=NativeToolExecutionChecks.Reset(context,host,input,tool,slot,slot==0?0:1);
            for(int i=0;i<p.armor.Length;i++)p.armor[i]=new Item();
            Array.Clear(p.buffType,0,p.buffType.Length);Array.Clear(p.buffTime,0,p.buffTime.Length);
            Require(!p.mount.Active,"moving herb fixture starts without an unrelated mount");
            if(boots)p.armor[3].SetDefaults(ItemID.HermesBoots);
            Main.SmartCursorWanted_Mouse=Main.SmartCursorWanted_GamePad=false;
            return p;
        }
        private static void Plant(int x,bool water=false,int row=Row)
        {
            Require(WorldGen.PlaceTile(x,row+1,78,mute:true,forced:true,plr:0),"moving herb fixture has actual supported flower pots");
            NativeToolsChecks.Tile(x,row,84);Main.tile[x,row].frameX=0;
            if(water)Main.tile[x,row].liquid=255;
        }
        private static Result Traverse(object context,object host,object input,int tool,int slot,bool boots,bool automatic)
        {
            // Vanilla clamps the world boundary at leftWorld+640. Start at
            // that legal coordinate, so no clamping masquerades as movement.
            var p=Prepare(context,host,input,tool,slot,boots);p.position=new Vector2(640,646);
            for(int x=First;x<=Last;x++)Plant(x);
            if(automatic)NativeToolsChecks.SetMode(host,1,1);
            object use=Get(host,"Use");var result=new Result();var done=new bool[Last-First+1];
            float start=p.position.X,end=(Last+9)*16;long operation=-1,notedLease=-1;string kind=null;bool cancelled=false;
            observeCancellation=automatic;firstCancellation=null;
            for(int step=0;step<Limit && p.Center.X<end;step++)
            {
                int target=-1;float distance=float.MaxValue;
                if(!automatic)for(int x=First;x<=Last;x++)
                {
                    var t=Main.tile[x,Row];if(done[x-First] || !t.active() || t.type!=84 || !p.IsInTileInteractionRange(x,Row,TileReachCheckSettings.Simple,p.HeldItem.tileBoost+p.blockRange))continue;
                    float d=Vector2.DistanceSquared(p.Center,new Vector2(x*16+8,Row*16+8));
                    if(d<distance){distance=d;target=x;}
                }
                Vector2 aim=target<0?Away:new Vector2(target*16+8,Row*16+8);
                NativeToolExecutionChecks.Sample(context,input,aim,!automatic,1);
                Require(Terraria.GameInput.PlayerInput.Triggers.Current.Right,"moving herb control reaches the real mapped Right input");
                float previousX=p.position.X;observedStep=step;
                NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");result.Steps=step+1;
                Require(p.position.X>=previousX && p.position.X-previousX<16,"each measured position advances only by the native walking step");
                result.MaxSpeed=Math.Max(result.MaxSpeed,Math.Abs(p.velocity.X));
                long current=(long)Get(use,"Operation");object intent=GetOptional(use,"Intent");string currentKind=intent==null?"none":Get(intent,"Kind").ToString();bool stopped=(bool)Get(use,"cancelled");
                if(current!=operation || currentKind!=kind || stopped!=cancelled)
                {result.Uses.Add(step+"/"+Get(input,"Frame")+":"+currentKind+"#"+current+" cancel="+stopped+" animation="+p.itemAnimation+" time="+p.itemTime+" x="+p.position.X);operation=current;kind=currentKind;cancelled=stopped;}
                for(int x=First;x<=Last;x++)if(!done[x-First])
                {
                    var t=Main.tile[x,Row];if(t.active() && t.type==84)continue;
                    done[x-First]=true;result.Effects.Add(new Effect{X=x,Step=step,Simulation=Main.GameUpdateCount,Input=(long)Get(input,"Frame"),Operation=current,Active=t.active(),Type=t.type,FrameX=t.frameX});
                    Require(t.active() && t.type==82 && t.frameX==0,"dry native harvest really regrows the same style without a seed operation");
                }
                if(stopped && current>0 && current!=notedLease && Enumerable.Range(First,Last-First+1).Any(x=>Main.tile[x,Row].active() && Main.tile[x,Row].type==84 && p.IsInTileInteractionRange(x,Row,TileReachCheckSettings.Simple,p.inventory[slot].tileBoost+p.blockRange)))
                {result.Uses.Add("readyWhileCancelled="+step+"#"+current+" animation="+p.itemAnimation+" x="+p.position.X);notedLease=current;}
            }
            result.Cancellation=firstCancellation;observeCancellation=false;
            result.Travel=p.position.X-start;
            Require(p.Center.X>=end && result.Travel>(Last-First)*16,"mapped native movement actually traverses the long field");
            Require(result.MaxSpeed>(boots?5f:2f),"walking and actual equipment produce the declared native movement condition");
            Require(((ReplantQueue)Get(Get(host,"Herbs"),"Pending")).Count==0,"dry native movement does not manufacture backup competition");
            Require(!result.Uses.Any(x=>x.Contains(":Seed#")),"free regrowth never borrows a seed use");
            Require(!(bool)Get(use,"Active"),"a finished moving pass releases its finite native animation lease");
            return result;
        }
        private static void Print(string label,Result result)
        {
            int maximum=0;for(int i=1;i<result.Effects.Count;i++)maximum=Math.Max(maximum,result.Effects[i].Step-result.Effects[i-1].Step);
            Console.WriteLine("HERB_MOVE "+label+" effects="+result.Effects.Count+" first="+(result.Effects.Count==0?-1:result.Effects[0].Step)+" steps="+result.Steps+" travel="+result.Travel+" maxSpeed="+result.MaxSpeed+" longestEffectGap="+maximum+" effects[x@step/simulation/input:tile/frame#lease]="+string.Join(",",result.Effects));
            Console.WriteLine("HERB_MOVE uses "+label+" "+string.Join(";",result.Uses));
            if(result.Cancellation!=null)Console.WriteLine("HERB_MOVE firstCancellation "+label+" "+result.Cancellation);
        }
        private static void Backup(object context,object host,object input,bool supplied)
        {
            var p=Prepare(context,host,input,ItemID.StaffofRegrowth,12,false);
            var plots=new List<Point>();
            // A legal multi-tier field keeps real mature work available at
            // the tool's native contention boundary. Platforms above the
            // player's feet do not relocate or obstruct the stationary player.
            foreach(int row in new[]{35,38,41,44,47})for(int x=35;x<=47;x++)if(x!=40)
            {NativeToolsChecks.Tile(x,row+2,row==38?19:1);Plant(x,x==41 && row==Row,row);plots.Add(new Point(x,row));}
            NativeToolsChecks.SetMode(host,1,1);object herbs=Get(host,"Herbs"),use=Get(host,"Use");
            var pending=(ReplantQueue)Get(herbs,"Pending");int queued=-1,seeded=-1,continued=-1,ready=-1,seedUse=-1;ulong created=0;bool changed=false;
            var events=new List<string>();long previous=-1;var completed=new bool[plots.Count];int remainingAtSeed=-1;
            for(int step=0;step<180;step++)
            {
                NativeToolExecutionChecks.Sample(context,input,Away,false);NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");
                // Move actual drops after the native action and before the
                // next pickup phase, keeping the missing-seed control honest.
                foreach(var item in Main.item.Where(x=>x.active))item.position=new Vector2(1600,1600);
                long operation=(long)Get(use,"Operation");object intent=GetOptional(use,"Intent");string kind=intent==null?"none":Get(intent,"Kind").ToString();
                if(operation!=previous){events.Add(step+":"+kind+"#"+operation+" animation="+p.itemAnimation+" time="+p.itemTime);previous=operation;}
                if(queued<0 && pending.Count>0)
                {
                    Require(!Main.tile[41,Row].active() && pending.Count==1,"backup is registered by an actual water-blocked native Harvest token");
                    Require(kind=="Harvest","real harvest owns the failed immediate replant");queued=step;created=pending[0].Created;
                    Require(plots.Any(point=>Main.tile[point.X,point.Y].type==84),"real backup competes with remaining mature harvest work");
                    Main.tile[41,Row].liquid=0;
                    if(supplied){p.inventory[17].SetDefaults(ItemID.DaybloomSeeds);p.inventory[17].stack=2;}
                }
                if(queued>=0)
                {
                    if((bool)Call(herbs,"SeedReady",p) && ready<0)ready=step;
                    if(kind=="Seed" && seedUse<0)seedUse=step;
                    if(Main.tile[41,Row].active() && Main.tile[41,Row].type==82 && seeded<0)
                    {seeded=step;remainingAtSeed=plots.Count(point=>Main.tile[point.X,point.Y].type==84);}
                    if(pending.Count>0)Require(pending[0].Created==created,"new harvest does not renew the original backup age");
                    for(int i=0;i<plots.Count;i++)if(!completed[i])
                    {
                        var point=plots[i];var tile=Main.tile[point.X,point.Y];if(tile.active() && tile.type==84)continue;
                        completed[i]=true;if(point.X==41 && point.Y==Row)continue;
                        changed=true;if(seeded>=0 && step>seeded && continued<0)continued=step;
                    }
                }
            }
            Console.WriteLine("HERB_BACKUP supplied="+supplied+" queued="+queued+" ready="+ready+" seedUse="+seedUse+" seeded="+seeded+" remainingAtSeed="+remainingAtSeed+" continued="+continued+" seeds="+p.inventory.Where(x=>x.type==ItemID.DaybloomSeeds).Sum(x=>x.stack)+" events="+string.Join(";",events));
            Require(queued>=0 && changed,"actual failed replant and subsequent real harvest progress are both observed");
            if(supplied)
            {
                Require(ready>=queued && seedUse>=queued && seeded>=seedUse,"a genuinely executable backup gets a real native seed turn");
                Require(p.inventory[17].type==ItemID.DaybloomSeeds && p.inventory[17].stack==1,"real fallback consumes exactly one supplied seed");
                Require(pending.Count==0,"successful native replant retires the actual pending responsibility");
                Require(remainingAtSeed>0 && continued>seeded,"seed handoff occurs with real mature work remaining and returns to actual harvest progress");
            }
            else Require(ready<0 && seedUse<0 && seeded<0 && pending.Count==1,"missing seed remains a finite wait and cannot manufacture contention or progress");
        }
        private static void PendingBoundary(object context,object host,object input,string boundary)
        {
            var p=Prepare(context,host,input,ItemID.StaffofRegrowth,12,false);Plant(42);
            NativeToolsChecks.SetMode(host,1,1);object use=Get(host,"Use"),ownership=Get(Get(host,"Items"),"Ownership");bool pending=false;Exception originalFailure=null;
            Action<bool> step=held=>{NativeToolExecutionChecks.Sample(context,input,Away,held);NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");};
            try
            {
                for(int i=0;i<p.inventory[12].useAnimation+4;i++)
                {
                    step(false);
                    if(Main.tile[42,Row].type==82 && (bool)Get(use,"Active") && !(bool)Get(use,"cancelled") && !(bool)Get(use,"nativeReady") && p.itemAnimation>0)
                    {pending=true;break;}
                }
                Require(pending && !(bool)Get(use,"ActionValid"),"actual free harvest enters a neutral, non-consuming finite wait before "+boundary);
                long token=(long)Get(use,"Operation");int animation=p.itemAnimation,time=p.itemTime;
                NativeToolExecutionChecks.Outer(context,input,3);
                Require((long)Get(use,"Operation")==token && !(bool)Get(use,"cancelled") && p.itemAnimation==animation && p.itemTime==time,"empty outer callbacks retain the exact pending lease without advancing native timing");
                if(boundary=="complete")
                {
                    for(int i=0;i<animation+2;i++)step(false);
                    Require(!(bool)Get(use,"Active") && p.selectedItem==1 && !(bool)Call(ownership,"IsUseSlot",12),"an empty field returns the original selection at the natural animation boundary");
                }
                else
                {
                    if(boundary=="manual")step(true);
                    else
                    {
                        if(boundary=="source")p.inventory[12]=new Item();
                        else if(boundary=="off")NativeToolsChecks.SetMode(host,1,0);
                        else if(boundary=="death")p.dead=true;
                        else Main.gameMenu=true;
                        NativeToolExecutionChecks.Outer(context,input,1);
                    }
                    Require(!(bool)Get(use,"Active") || (bool)Get(use,"cancelled"),"the finite pending lease immediately loses automation at "+boundary);
                    Require(!(bool)Get(use,"ActionValid"),"a revoked pending target cannot regain consumption at "+boundary);
                    if(boundary=="manual")Require(!(bool)Call(ownership,"IsUseSlot",12),"genuine physical input immediately owns the source during pending");
                }
                Require(Main.tile[42,Row].active() && Main.tile[42,Row].type==82,"pending/retirement never reharvests the free regrowth seedling");
                Console.WriteLine("HERB_PENDING boundary="+boundary+" token="+token+" nativeRemaining="+animation+" active="+Get(use,"Active")+" actionValid="+Get(use,"ActionValid"));
            }
            catch(Exception error){originalFailure=error;Console.WriteLine("FAIL herb pending original "+boundary+": "+error);throw;}
            finally
            {
                p.dead=false;Main.gameMenu=false;
                // Controls requires the actual Runtime player. A session exit
                // cleared it; restore this fixture's session before requesting
                // a setting change, rather than timing out on an absent player.
                try
                {
                    NativeToolExecutionChecks.Sample(context,input,Away,false);Call(context,"UpdateRuntime");
                    NativeToolsChecks.SetMode(host,1,0);Call(use,"Retire");
                }
                catch(Exception cleanup){Console.WriteLine("FAIL herb pending cleanup "+boundary+": "+cleanup);if(originalFailure==null)throw;}
            }
        }
    }
}
