using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeYoyoBoundaryHitChecks
    {
        private static readonly HashSet<int> pairedKeys=new HashSet<int>();
        private static void PairBefore(out HashSet<int> __state){__state=new HashSet<int>(Main.projectile.Where(q=>q.active).Select(q=>(int)q.key));}
        private static void PairAfter(Player __instance,HashSet<int> __state){foreach(var q in Main.projectile.Where(q=>q.active && q.owner==__instance.whoAmI && !q.counterweight && q.aiStyle==99 && q.ai[0]!=-2 && !__state.Contains((int)q.key)))pairedKeys.Add((int)q.key);}
        private static readonly List<string> hits=new List<string>();private static string label;private static uint origin;
        private static void Before(NPC __1,out int __state){__state=__1.life;}
        private static void After(Projectile __instance,NPC __1,int __state){if(__1.life<__state)hits.Add(label+","+(Main.GameUpdateCount-origin)+","+__instance.type+","+__instance.ai[0]+","+(__state-__1.life));}
        internal static void Run(object context,string output)
        {
            var combat=Get(context,"Combat");var tools=Get(context,"Tools");var input=Get(context,"Input");var use=Get(combat,"Use");var audit=new Harmony("JueMingR.Tests.YoyoAcceptedHit");hits.Clear();hits.Add("case,tick,type,ai0,damage");
            audit.Patch(AccessTools.Method(typeof(Projectile),"Damage_PVE_Inner"),prefix:new HarmonyMethod(typeof(NativeYoyoBoundaryHitChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeYoyoBoundaryHitChecks),nameof(After)));
            audit.Patch(AccessTools.Method(typeof(Player),"Counterweight"),prefix:new HarmonyMethod(typeof(NativeYoyoBoundaryHitChecks),nameof(PairBefore)),postfix:new HarmonyMethod(typeof(NativeYoyoBoundaryHitChecks),nameof(PairAfter)));
            // Original FindFrame accepts a not-loaded asset and returns. Keep
            // that real graphics-off path without changing dedServ/netMode.
            Terraria.GameContent.TextureAssets.Npc[NPCID.BlueSlime]=(ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>)Activator.CreateInstance(typeof(ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{"yoyo-receiver-unloaded"},null);
            try
            {
                foreach(bool glove in new[]{false,true})foreach(bool managed in new[]{false,true})
                {
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,3262,0,0);p.position=new Vector2(950,646);foreach(var item in p.armor)item.TurnToAir();p.armor[3].SetDefaults(ItemID.MagicString);if(glove){p.armor[4].SetDefaults(ItemID.YoYoGlove);p.armor[5].SetDefaults(ItemID.RedCounterweight);}
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions(managed?16:0));
                    // Explicit stationary receiver; no native timers/immunity or
                    // projectile state is altered after initialization. Its real
                    // NPC.Update advances immunity, and Projectile.Update owns hits.
                    var victim=Main.npc[0];victim.SetDefaults(NPCID.BlueSlime);victim.whoAmI=0;victim.active=true;victim.position=p.Center+new Vector2(16,-12);victim.life=victim.lifeMax=100000;victim.knockBackResist=0;victim.aiStyle=-1;victim.noGravity=victim.noTileCollide=true;
                    pairedKeys.Clear();origin=Main.GameUpdateCount;label=(managed?"R":"original")+"-glove-counterweight-"+glove;int hitStart=hits.Count,births=0,splits=0,paired=0,counter=0;var known=new HashSet<int>();int release=0;
                    for(int t=0;t<80;t++)
                    {
                        bool live=Main.projectile.Any(q=>q.active && q.owner==p.whoAmI && q.type==p.HeldItem.shoot && q.ai[0]!=-2);bool ready=p.itemAnimation<=0 && p.itemTime<=0 && p.reuseDelay<=0 && !p.delayUseItem;
                        NativeCombatCadenceChecks.Step(context,managed || !live && ready || t<release,false,0,point:p.Center+new Vector2(180,-30));victim.UpdateNPC(0);
                        foreach(var q in Main.projectile.Where(q=>q.active && q.owner==p.whoAmI && q.aiStyle==99))if(known.Add((int)q.key)){if(q.ai[0]==-2)splits++;else if(q.type==p.HeldItem.shoot){births++;release=t+1;}else counter++;}
                        if(managed){var primary=GetOptional(use,"primary") as Projectile;var second=GetOptional(use,"paired") as Projectile;if(primary!=null && (bool)Call(use,"Valid",primary,(int)Get(use,"primaryKey")))Require(primary.type==p.HeldItem.shoot && primary.ai[0]!=-2,"R valid primary never captures a native split or counterweight.");if(second!=null && (bool)Call(use,"Valid",second,(int)Get(use,"pairedKey"))){paired++;Require(second.ai[0]!=-2 && second.type==p.HeldItem.shoot,"R paired original excludes magic-string children.");}}
                    }
                    Require(hits.Count>hitStart,"Natural original Projectile.Update must yield actual accepted hits "+label);
                    Console.WriteLine("YOYO PAIR PRECONDITION "+label+" nativeGlove="+p.yoyoGlove+" nativeCounter="+p.counterWeight+" pairedKeys="+string.Join(";",pairedKeys)+" births="+births+" split="+splits+" counterBirths="+counter);
                    Require(splits>1 && births>1 && (!glove || pairedKeys.Count>0),"Natural paired/split/counterweight chain actually executes "+label);
                    Console.WriteLine("YOYO HITS "+label+" accepted="+(hits.Count-hitStart)+" mainEmissions="+(births-pairedKeys.Count)+" totalSameTypeBirths="+births+" splits="+splits+" actualCounterweightPaired="+pairedKeys.Count+" pairedKeys="+string.Join(";",pairedKeys)+" RpairedFieldObservations="+paired+" counterweights="+counter+" ticks="+string.Join(";",hits.Skip(hitStart)));
                }
                foreach(string boundary in new[]{"release","off","replacement","focus","ui","session"})
                {
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,3262,0,0);foreach(var item in p.armor)item.TurnToAir();p.armor[3].SetDefaults(ItemID.MagicString);NativeCombatCadenceChecks.Save(combat,new CombatOptions(16));NativeCombatCadenceChecks.Step(context,true,false,0);
                    var original=GetOptional(use,"primary") as Projectile;Require(original!=null && original.active,"Boundary begins with a real native tracked original.");
                    long operation=(long)Get(use,"Operation");uint decision=(uint)Get(use,"decisionTick");bool press=(bool)Get(use,"press");int itemTime=p.itemTime,animation=p.itemAnimation,reuse=p.reuseDelay;float ai=original.ai[0];
                    for(int i=0;i<4;i++){Call(use,"Sync",p);Require((long)Get(use,"Operation")==operation && (uint)Get(use,"decisionTick")==decision && (bool)Get(use,"press")==press,"Same-tick reentry keeps the admitted yoyo decision and original identity.");Call(use,"FinishFrame");}
                    Require(p.itemTime==itemTime && p.itemAnimation==animation && p.reuseDelay==reuse && original.ai[0]==ai,"Reentry does not write native timers or projectile phase.");
                    if(boundary=="release")NativeCombatCadenceChecks.Step(context,false,false,0);
                    else if(boundary=="off")NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    else if(boundary=="replacement"){var replacement=new Item();replacement.SetDefaults(3262);p.inventory[0]=replacement;Call(use,"Sync",p);}
                    else if(boundary=="focus"){Set(input,"foregroundWindow",(Func<IntPtr>)(()=>IntPtr.Zero));NativeToolExecutionChecks.Sample(context,input,p.Center,true);Call(combat,"Sample");Call(use,"Sync",p);Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));}
                    else if(boundary=="ui"){Main.playerInventory=true;p.mouseInterface=true;Call(use,"Sync",p);p.mouseInterface=false;Main.playerInventory=false;}
                    else Call(combat,"OnSessionEnded");
                    Require(!(bool)Get(use,"Active"),"Real yoyo boundary retires R intent: "+boundary);
                    if(boundary!="release")Require(original.active && original.ai[0]==ai && p.itemTime==itemTime && p.itemAnimation==animation && p.reuseDelay==reuse,"Cancellation leaves native original/timers untouched: "+boundary);
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions());Console.WriteLine("YOYO BOUNDARY "+boundary+" sameTickReentries=4 intentRetired=true");
                }
                Console.WriteLine("PASS YOYO actual accepted-hit4 receiver cases plus primary/paired/split identity and six cancellation boundaries with same-tick reentry.");
            }
            finally{File.WriteAllLines(Path.Combine(output,"accepted-hits.csv"),hits);foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
    }
}
