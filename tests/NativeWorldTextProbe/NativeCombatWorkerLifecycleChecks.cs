using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatWorkerLifecycleChecks
    {
        internal static void Run(Assembly host,string layout,string output)
        {
            Terraria.Localization.LanguageManager.Instance.SetLanguage("en-US");Lang.InitializeLegacyLocalization();
            NativeCombatWorkerChecks.Scene(false);
            Terraria.ID.ContentSamples.Initialize();
            Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();
            Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
            BoundaryCases(host);
            using(var child=NativeCombatWorkerChecks.Start(layout))
            {
                var errors=child.StandardError.ReadToEndAsync();
                try
                {
                    Reuse(host,child,output);
                    Transform(host,child,output);
                }
                finally
                {
                    NativeCombatWorkerChecks.Exit(child,"lifecycle helper exits");
                    Require(errors.Wait(5000),"Lifecycle stderr closes.");
                    File.WriteAllText(Path.Combine(output,"lifecycle-worker.log"),errors.Result);Console.WriteLine(errors.Result);
                }
            }
            Console.WriteLine("PASS native same-type slot reuse terminates the old target; same-instance LostGirl transform continues.");
        }
        private static void BoundaryCases(Assembly host)
        {
            const BindingFlags flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            NativeCombatWorkerChecks.Scene(false);NPC original=Main.npc[0];byte generation=original.generation;
            // The real allocator creates a new object even if its old slot is
            // active; it does not retire the old reference's active flag.
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",flags).Invoke(null,new object[]{0,(byte)0});
            Require(original.active && !ReferenceEquals(original,Main.npc[0]),"Original replacement leaves old object active.");
            var end=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionSandbox",true).GetMethod("TargetEnd",flags);
            Require((byte)end.Invoke(null,new object[]{Main.npc[0],original,generation,(byte)0})==2,"Active replacement terminates by reference identity.");
            typeof(NPC).GetProperty("generation").GetSetMethod(true).Invoke(original,new object[]{(byte)(generation+1)});
            Require((byte)end.Invoke(null,new object[]{original,original,generation,(byte)0})==3,"Defensive generation change cannot keep the old birth alive.");
            Require((byte)end.Invoke(null,new object[]{original,original,generation,(byte)1})==1,"Earlier natural termination never reconnects after identity changes.");
            Console.WriteLine("PASS native active replacement plus defensive generation and terminal-latch boundaries.");
        }
        private static void Reuse(Assembly host,System.Diagnostics.Process child,string output)
        {
            NativeCombatWorkerChecks.Scene(false);NPC.ClearAll();Projectile.ClearAll();
            Main.maxTilesX=400;Main.maxTilesY=160;Main.rightWorld=6400;Main.bottomWorld=2560;
            Main.tile=new Tile[400,160];
            for(int x=0;x<400;x++)for(int y=0;y<160;y++)
            {var tile=new Tile();if(y>=65){tile.active(true);tile.type=1;}Main.tile[x,y]=tile;}
            var source=new EntitySource_DebugCommand();
            int parent=NPC.NewNPC(source,1000,700,94,Start:5,Target:0);
            Require(parent==5,"Native Corruptor parent slot.");
            // Advance only the original AI to a real pre-birth phase. The
            // source fixture remains unpatched and no timer is fabricated.
            for(int step=0;step<177;step++)Main.npc[parent].AI();
            Require(Main.npc[parent].localAI[0]==177 && Main.npc[parent].active,"Native Corruptor reaches its pre-birth phase.");
            int selected=NPC.NewNPC(source,5400,800,112);
            Require(selected==0 && Main.npc[selected].target==255,"Native remote VileSpit uses default target.");
            NPC captured=Main.npc[selected];byte generation=captured.generation;
            var scene=NativeCombatWorkerChecks.AcquireFrozen(host,child,new[]{selected,parent},new int[0],selected);
            NativeCombatWorkerChecks.Compare(scene.Future,selected,output,"same-type-npc-slot-reuse",nativeStep:(step,old)=>
            {
                Require(ReferenceEquals(old,captured) && old.type==112 && old.netID==112 && old.generation==generation,"Oracle retains the originally selected identity.");
                if(step>=1)Require(!old.active,"Original target naturally despawns on first update.");
                if(step==3 || step==4)
                {
                    NPC current=Main.npc[selected];
                    Require(!ReferenceEquals(current,old) && current.active && current.type==112 && current.netID==112 && current.generation!=generation,"Native birth reuses the exact slot and type with a new instance.");
                    Require(current.ai[0]==step-3,"New low slot first advances on the following native tick.");
                }
            });
        }
        private static void Transform(Assembly host,System.Diagnostics.Process child,string output)
        {
            NativeCombatWorkerChecks.Scene(false);NPC.ClearAll();Projectile.ClearAll();
            int selected=NPC.NewNPC(new EntitySource_DebugCommand(),700,1040,195,Target:0);
            NPC original=Main.npc[selected];byte generation=original.generation;
            for(int step=0;step<20;step++)original.UpdateNPC(selected);
            Require(original.type==195 && original.ai[0]==20,"Native LostGirl reaches pre-transform phase.");
            var scene=NativeCombatWorkerChecks.AcquireFrozen(host,child,new[]{selected},new int[0],selected);
            NativeCombatWorkerChecks.Compare(scene.Future,selected,output,"same-instance-npc-transform",nativeStep:(step,old)=>
            {
                Require(ReferenceEquals(Main.npc[selected],original) && old.generation==generation && old.active,"Transform retains live birth identity.");
                Require(old.type==(step==0?195:196) && old.netID==old.type,"Original transformation actually changes type and netID.");
            });
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
