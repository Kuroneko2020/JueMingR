using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeYoyoCausalChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        private static bool checkFacing;private static int expectedFacing;
        private static string label,stage;private static uint origin;private static readonly List<string> trace=new List<string>();private static readonly List<int> births=new List<int>(),splits=new List<int>();private static readonly HashSet<int> known=new HashSet<int>();
        private static void Dir(Player __instance,int __0){if(__instance.whoAmI==Main.myPlayer)trace.Add(label+","+(Main.GameUpdateCount-origin)+","+stage+",ChangeDir,"+__0+","+__instance.direction);}
        private static void ItemBefore(Player __instance){if(__instance.whoAmI==Main.myPlayer){stage="ItemCheck";if(checkFacing)Require(__instance.direction==expectedFacing,"Actual ItemCheck attack direction cannot be overwritten by moving keys.");trace.Add(label+","+(Main.GameUpdateCount-origin)+",ItemCheck,entry,"+__instance.direction+","+__instance.itemAnimation+","+__instance.itemTime+","+__instance.releaseUseItem);}}
        private static void AiBefore(Projectile __instance){if(__instance.aiStyle==99 && __instance.owner==Main.myPlayer){stage="AI99";trace.Add(label+","+(Main.GameUpdateCount-origin)+",AI99,entry,"+__instance.key+","+__instance.ai[0]+","+__instance.Center.X+","+Main.LocalPlayer.Center.X+","+Main.LocalPlayer.direction+","+Main.LocalPlayer.channel);}}
        private static void AiAfter(Projectile __instance){if(__instance.aiStyle==99 && __instance.owner==Main.myPlayer)trace.Add(label+","+(Main.GameUpdateCount-origin)+",AI99,exit,"+__instance.key+","+__instance.ai[0]+","+__instance.active+","+Main.LocalPlayer.direction+","+Main.LocalPlayer.itemTime);stage="outer";}
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var tools=Get(context,"Tools");var input=Get(context,"Input");var recorder=new Harmony("JueMingR.Tests.YoyoCause");
            recorder.Patch(typeof(Player).GetMethod("ChangeDir",Flags),prefix:new HarmonyMethod(typeof(NativeYoyoCausalChecks),nameof(Dir)));
            recorder.Patch(typeof(Player).GetMethod("ItemCheck",Flags),prefix:new HarmonyMethod(typeof(NativeYoyoCausalChecks),nameof(ItemBefore)){priority=Priority.Last});
            recorder.Patch(typeof(Projectile).GetMethod("AI",Flags),prefix:new HarmonyMethod(typeof(NativeYoyoCausalChecks),nameof(AiBefore)){priority=Priority.Last},postfix:new HarmonyMethod(typeof(NativeYoyoCausalChecks),nameof(AiAfter)));
            var output=Environment.GetEnvironmentVariable("JUEMINGR_YOYO_EVIDENCE");Directory.CreateDirectory(output);trace.Clear();trace.Add("case,tick,consumer,event,values");
            try
            {
                // Real BordersMovement reserves640 pixels at each edge. Keep
                // every220-action case inside a wide symmetric original world.
                Main.maxTilesX=384;Main.rightWorld=6144;Main.tile=new Tile[384,120];
                for(int x=0;x<384;x++)for(int y=0;y<120;y++){Main.tile[x,y]=new Tile();if(y==43){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}}
                int executed=0;
                foreach(int move in Environment.GetEnvironmentVariable("JUEMINGR_YOYO_MOVE_ONLY")=="-1"?new[]{-1}:new[]{0,1,-1})foreach(int mirror in new[]{1,-1})foreach(bool facing in new[]{false,true})
                {
                    int[] original=null;foreach(bool managed in new[]{false,true})
                    {
                        checkFacing=false;NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,3262,0,0);p.position=new Vector2(3000,646);foreach(var item in p.armor)item.TurnToAir();p.armor[3].SetDefaults(ItemID.MagicString);
                        NativeCombatCadenceChecks.Save(combat,new CombatOptions((managed?16:0)|(facing?32:0)));origin=Main.GameUpdateCount;births.Clear();splits.Clear();known.Clear();label=(managed?"R":"original")+"-move"+move+"-mirror"+mirror+"-face"+facing;
                        checkFacing=managed && facing;expectedFacing=mirror;float startX=p.position.X;
                        bool previousOriginal=false;int release=0;int deadTick=-1,readyTick=-1;
                        for(int t=0;t<220;t++)
                        {
                            bool live=Main.projectile.Any(s=>s.active && s.owner==p.whoAmI && s.type==p.HeldItem.shoot && s.ai[0]!=-2);
                            bool ready=p.itemAnimation<=0 && p.itemTime<=0 && p.reuseDelay<=0 && !p.delayUseItem;
                            bool left=managed || !live && ready || t<release;
                            Main.screenPosition=p.Center-new Vector2(400,300);var point=p.Center+new Vector2(mirror*180,-30);
                            NativeCombatCadenceChecks.Step(context,left,false,0,move==0?new Keys[0]:new[]{move>0?Keys.D:Keys.A},point);
                            if(move!=0 && t>20)Require(p.velocity.X*move>0 && p.position.X>700 && p.position.X<Main.rightWorld-700,"Native continuous movement stays inside both real borders.");
                            if(t==0 || t==219)Console.WriteLine("YOYO MOVEMENT "+label+" tick="+t+" controls="+p.controlLeft+"/"+p.controlRight+" velocity="+p.velocity+" position="+p.position+" startX="+startX);
                            foreach(var shot in Main.projectile.Where(s=>s.active && s.owner==p.whoAmI && s.aiStyle==99))if(known.Add((int)shot.key))
                            {if(shot.ai[0]==-2){splits.Add(t);trace.Add(label+","+t+",world,split,"+shot.key);}else{births.Add(t);release=t+1;trace.Add(label+","+t+",world,birth,"+shot.key);}}
                            bool current=Main.projectile.Any(s=>s.active && s.owner==p.whoAmI && s.type==p.HeldItem.shoot && s.ai[0]!=-2);
                            if(previousOriginal && !current){deadTick=t;trace.Add(label+","+t+",world,return-ended");}previousOriginal=current;
                            if(!current && p.itemAnimation<=0 && p.itemTime<=0 && p.reuseDelay<=0 && !p.delayUseItem){readyTick=t;trace.Add(label+","+t+",world,Ready");}
                        }
                        Require(move==0 || (p.position.X-startX)*move>5,"Declared forward/reverse movement must actually move the native player.");
                        executed++;checkFacing=false;Require(births.Count>=3 && splits.Count>=3,"Full native original/split/return/rebirth chain executes "+label);
                        File.WriteAllText(Path.Combine(output,label+"-summary.txt"),"births="+string.Join(",",births)+" splits="+string.Join(",",splits)+" return="+deadTick+" ready="+readyTick);
                        Console.WriteLine("YOYO "+label+" births="+string.Join(",",births)+" splits="+string.Join(",",splits));
                        if(!managed)original=births.ToArray();else Require(births[1]<=original[1],"R removes additional wait at actual second emission: "+label+" original="+original[1]+" R="+births[1]);
                    }
                }
                Console.WriteLine("PASS YOYO CAUSAL cases="+executed+"; actual movement/emission/split/return/Ready and real ItemCheck/AI99 ChangeDir consumers.");
            }
            finally{checkFacing=false;File.WriteAllLines(Path.Combine(output,"event-chain.csv"),trace.Select(row=>string.Join(",",row.Split(new[]{','},5).Select(value=>"\""+value.Replace("\"","\"\"")+"\""))));foreach(var method in recorder.GetPatchedMethods().ToArray())recorder.Unpatch(method,HarmonyPatchType.All,recorder.Id);NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
    }
}
