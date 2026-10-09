using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using JueMingR.Features.Combat;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatEventChecks
    {
        private static Projectile watched;
        private static int watchedType;
        private static readonly List<Rectangle> nativeBoxes=new List<Rectangle>();
        private static readonly List<Rectangle> lightningTargets=new List<Rectangle>();
        private static readonly List<bool> lightningHits=new List<bool>();
        private static void Observe(Projectile __instance,Rectangle __result)
        {
            if(!ReferenceEquals(__instance,watched) && !(watchedType>0 && __instance.type==watchedType))return;nativeBoxes.Add(__result);
            if(__instance.aiStyle==203)
            {
                // Execute the pure native predicate while its natural Kill
                // owns the temporary point set. The search domain is fixed
                // independently of the observer and survives native cleanup.
                for(int x=320;x<=1520;x+=31)for(int y=32;y<=1550;y+=31)
                {var target=new Rectangle(x,y,20,42);lightningTargets.Add(target);lightningHits.Add(__instance.Colliding(__result,target));}
            }
        }
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true));var geometry=Get(host,"Geometry");
            foreach(var npc in Main.npc)npc.active=false;Main.player[1]=new Player{whoAmI=1,active=true,position=new Vector2(400,400)};
            // Kill legitimately emits native gore. Supply the same allocated
            // slots as game initialization; do not stub or skip the Kill body.
            for(int i=0;i<Main.gore.Length;i++)if(Main.gore[i]==null)Main.gore[i]=new Gore();
            var recorder=new Harmony("JueMingR.Test.NativeTerminationOracle");var getter=AccessTools.Method(typeof(Projectile),"Damage_GetHitbox");recorder.Patch(getter,postfix:new HarmonyMethod(typeof(NativeCombatEventChecks),nameof(Observe)));
            try
            {
                foreach(int type in new[]{711,644,483,424,425,426,1037,1049,1078,283,286,41,514})
                {
                    Rectangle[] oracle=null;
                    for(int owner=0;owner<=1;owner++)
                    {
                        Call(geometry,"Clear");nativeBoxes.Clear();var p=Main.projectile[98];p.SetDefaults(type);p.active=true;p.whoAmI=98;p.owner=owner;p.damage=10;p.position=new Vector2(600.75f,600.75f);p.velocity=new Vector2(-2,1);p.oldVelocity=new Vector2(3,4);p.timeLeft=200;
                        if(type==514)p.ai[0]=1;if(type==644)p.localAI[0]=1;
                        watched=p;p.Kill();watched=null;
                        if(owner==0){oracle=nativeBoxes.ToArray();Require(oracle.Length==(type==283?3:1),"native owner termination actually executes each expected Damage: "+type);}
                        var events=(Array)Get(geometry,"Events");Require((int)Get(geometry,"EventCount")==oracle.Length,"owner/remote event multiplicity for "+type+" owner="+owner);
                        for(int i=0;i<oracle.Length;i++)
                        {
                            var sample=events.GetValue(i);Require((int)Get(sample,"Count")==1,"terminal region has no stale flight trail: "+type);
                            var shape=((Array)Get(sample,"Shapes")).GetValue(0);var a=(Vector2)Get(shape,"A");var b=(Vector2)Get(shape,"B");var actual=new Rectangle((int)a.X,(int)a.Y,(int)(b.X-a.X),(int)(b.Y-a.Y));
                            Require(actual==oracle[i],"remote terminal rectangle versus actual native getter type="+type+" owner="+owner+" index="+i+" expected="+oracle[i]+" actual="+actual);
                            Require((int)Get(shape,"Category")==((type==1049 || type==1078)?3:0),"trap/hostile termination keeps gameplay category: "+type);
                        }
                        int count=(int)Get(geometry,"EventCount");p.Kill();Require((int)Get(geometry,"EventCount")==count,"inactive repeat Kill creates no event: "+type);
                    }
                    Console.WriteLine("EVENT native owner/remote termination type="+type+" regions="+oracle.Length);
                }
                CatBlade(geometry);
                Medusa(geometry);
                Volcano(geometry);
                Lightning(geometry);
                Call(geometry,"Clear");var torch=Main.projectile[98];torch.SetDefaults(949);torch.active=true;torch.whoAmI=98;torch.owner=Main.myPlayer;torch.damage=40;torch.position=new Vector2(900,900);torch.ai[0]=6;torch.ai[1]=100;torch.velocity=new Vector2(8,0);
                Main.LocalPlayer.unlockedBiomeTorches=false;Main.LocalPlayer.immune=true;Rectangle fireBox=torch.Hitbox;torch.AI();torch.HandleMovement(Vector2.Zero);torch.Damage();
                var fire=((Array)Get(geometry,"Events")).GetValue(0);Require((int)Get(geometry,"EventCount")==1 && NativeCombatCoverageChecks.Hit(fire,fireBox) && !NativeCombatCoverageChecks.Hit(fire,torch.Hitbox),"natural SelfHurt event keeps pre-movement region through harmless Damage");
                Main.LocalPlayer.unlockedBiomeTorches=true;torch.AI();torch.Damage();Require((int)Get(geometry,"EventCount")==1,"unlocked flame creates no new hurt event");Main.LocalPlayer.unlockedBiomeTorches=false;Main.LocalPlayer.immune=false;
                foreach(int owner in new[]{Main.myPlayer,1})
                {
                    Call(geometry,"Clear");torch.SetDefaults(949);torch.active=true;torch.whoAmI=98;torch.owner=owner;torch.damage=40;torch.Center=new Vector2(900,900);torch.ai[0]=6;torch.ai[1]=100;Main.LocalPlayer.unlockedBiomeTorches=owner==Main.myPlayer;torch.AI();torch.Damage();
                    var harmless=((Array)Get(geometry,"Attacks")).GetValue(98);Require((harmless==null || (int)Get(harmless,"Count")==0) && (int)Get(geometry,"EventCount")==0,"949 ordinary Damage cannot fabricate hurt for remote owner or unlocked player");
                }
                Main.LocalPlayer.unlockedBiomeTorches=false;
                Console.WriteLine("EVENT natural SelfHurt flame: no victim, immune player and exit gate verified.");
            }
            finally{watched=null;watchedType=0;recorder.Unpatch(getter,HarmonyPatchType.All,recorder.Id);Main.player[1].active=false;}
        }
        private static void Volcano(object geometry)
        {
            Call(geometry,"Clear");foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;
            var player=Main.LocalPlayer;int slot=player.selectedItem;var saved=player.inventory[slot];var visual=player.lastVisualizedSelectedItem;
            try
            {
                var sword=new Item();sword.SetDefaults(121);player.inventory[slot]=sword;player.lastVisualizedSelectedItem=sword.Clone();player.direction=1;player.gravDir=1;player.position=new Vector2(640,640);player.attackCD=0;player.itemAnimationMax=player.itemAnimation=sword.useAnimation;player.itemTime=0;player.controlUseItem=false;player.channel=false;
                player.ItemCheck();var sample=((Array)Get(geometry,"Attacks")).GetValue(Main.maxProjectiles+player.whoAmI);
                Require((bool)Get(player,"_spawnVolcanoExplosion") && sample!=null && (int)Get(sample,"Count")==1 && (int)Get(((Array)Get(sample,"Shapes")).GetValue(0),"Kind")==7,"natural first Volcano swing retains capsule-only pending phase");
                Vector2 a,d;player.GetPointOnSwungItemPath(70,70,0,player.GetAdjustedItemScale(sword),out a,out d);
                var target=Main.npc[0];target.SetDefaults(1);target.active=true;target.whoAmI=0;target.life=target.lifeMax=10000;target.Center=a;Array.Clear(target.immune,0,target.immune.Length);player.attackCD=0;Call(player,"ResetMeleeHitCooldowns");player.itemAnimation=player.itemAnimationMax;player.ItemCheck();
                Require(!(bool)Get(player,"_spawnVolcanoExplosion") && target.life<10000,"real Volcano hit consumes pending explosion in native ItemCheck");
                bool spawned=false;foreach(var p in Main.projectile)spawned|=p.active && p.type==978;Require(spawned && (int)Get(sample,"Count")==2,"natural hit creates Volcano child and exposes rectangle plus capsule phase");
                target.active=false;player.ItemCheck();Require(!(bool)Get(player,"_spawnVolcanoExplosion") && (int)Get(sample,"Count")==2,"later frame of same swing retains consumed phase without fabricated new explosion");
                Console.WriteLine("EVENT natural Volcano ItemCheck: pending capsule, first hit/child, consumed rectangle union and following swing frame.");
            }
            finally{player.inventory[slot]=saved;player.lastVisualizedSelectedItem=visual;player.itemAnimation=player.itemTime=0;Main.npc[0].active=false;}
        }
        private static void Medusa(object geometry)
        {
            Call(geometry,"Clear");nativeBoxes.Clear();foreach(var p in Main.projectile)p.active=false;foreach(var n in Main.npc)n.active=false;
            var player=Main.LocalPlayer;bool channel=player.channel;int selected=player.selectedItem;var item=player.inventory[selected];
            try
            {
                player.channel=true;player.inventory[selected]=new Item();player.inventory[selected].SetDefaults(3269);player.statMana=200;player.dead=false;
                var npc=Main.npc[0];npc.SetDefaults(1);npc.active=true;npc.whoAmI=0;npc.Center=new Vector2(740,640);npc.life=npc.lifeMax=10000;Array.Clear(npc.immune,0,npc.immune.Length);
                var p=Main.projectile[98];p.SetDefaults(535);p.active=true;p.whoAmI=98;p.owner=Main.myPlayer;p.damage=30;p.Center=new Vector2(640,640);p.ai[0]=0;
                watchedType=536;p.AI();watchedType=0;
                Require(nativeBoxes.Count==1 && nativeBoxes[0]==new Rectangle(735,635,10,10),"Medusa naturally damages one target inside temporary child region");
                Require((int)Get(geometry,"EventCount")==1,"Medusa records damage child and excludes four decorative rays");
                var sample=((Array)Get(geometry,"Events")).GetValue(0);
                Require(NativeCombatCoverageChecks.Hit(sample,nativeBoxes[0]) && !NativeCombatCoverageChecks.Hit(sample,new Rectangle(635,635,10,10)),"Medusa snapshot remains at target after child returns to parent center");
            }
            finally{watchedType=0;player.channel=channel;player.inventory[selected]=item;Main.npc[0].active=false;}
        }
        private static void Lightning(object geometry)
        {
            foreach(int type in new[]{1091,1117,1122})foreach(bool water in new[]{false,true})
            {
                Call(geometry,"Clear");nativeBoxes.Clear();lightningTargets.Clear();lightningHits.Clear();
                if(water)for(int x=50;x<100;x++)for(int y=60;y<100;y++)Main.tile[x,y].liquid=255;
                try
                {
                    var p=Main.projectile[98];p.SetDefaults(type);p.active=true;p.whoAmI=98;p.owner=type==1091?255:Main.myPlayer;p.damage=30;p.Center=new Vector2(960,1280);p.ai[2]=123;p.customHitbox=null;
                    watched=p;p.Kill();watched=null;
                    Require(nativeBoxes.Count==1 && p.customHitbox==null && !p.active,"natural lightning Kill executes and clears temporary native point set");
                    Require((int)Get(geometry,"EventCount")==1,"lightning Kill retains one geometry event after source cleanup");var sample=((Array)Get(geometry,"Events")).GetValue(0);
                    int hits=0;for(int i=0;i<lightningHits.Count;i++){if(lightningHits[i])hits++;Require(NativeCombatCoverageChecks.Hit(sample,lightningTargets[i],p)==lightningHits[i],"natural lightning type="+type+" wet="+water+" target="+lightningTargets[i]);}
                    Require(hits>0 && (int)Get(sample,"PointCount")>0,"independent lightning domain has real hits and copied discrete points");
                    Console.WriteLine("EVENT natural lightning type="+type+" liquid="+water+" hits="+hits+" points="+Get(sample,"PointCount"));
                }
                finally{watched=null;if(water)for(int x=50;x<100;x++)for(int y=60;y<100;y++)Main.tile[x,y].liquid=0;}
            }
        }
        private static void CatBlade(object geometry)
        {
            for(int y=39;y<=42;y++){Main.tile[50,y].active(true);Main.tile[50,y].type=Terraria.ID.TileID.Stone;}
            try
            {
                foreach(int bounce in new[]{0,4})
                {
                    Rectangle expected=default(Rectangle);
                    for(int owner=0;owner<2;owner++)
                    {
                        Call(geometry,"Clear");nativeBoxes.Clear();var p=Main.projectile[98];p.SetDefaults(502);p.active=true;p.whoAmI=98;p.owner=owner;p.damage=10;p.position=new Vector2(784,640);p.velocity=p.oldVelocity=new Vector2(4,0);p.ai[0]=bounce;p.wet=false;
                        watched=p;AccessTools.Method(typeof(Projectile),"HandleMovement").Invoke(p,new object[]{Vector2.Zero});watched=null;
                        Require(p.ai[0]==bounce+1,"cat blade fixture really collides at bounce "+bounce);
                        if(owner==0){Require(nativeBoxes.Count==1,"natural cat blade issues one temporary Damage");expected=nativeBoxes[0];}
                        Require((int)Get(geometry,"EventCount")==1,"cat blade local/remote impact retained: owner="+owner+" bounce="+bounce);
                        var sample=((Array)Get(geometry,"Events")).GetValue(0);var shape=((Array)Get(sample,"Shapes")).GetValue(0);var a=(Vector2)Get(shape,"A");var b=(Vector2)Get(shape,"B");
                        Require(new Rectangle((int)a.X,(int)a.Y,(int)(b.X-a.X),(int)(b.Y-a.Y))==expected,"cat blade remote event matches actual local native damage box");
                        if(p.active)p.Damage();Require((int)Get(geometry,"EventCount")==1,"later ordinary cat blade callback cannot erase or duplicate impact: owner="+owner+" bounce="+bounce);
                    }
                }
            }
            finally{for(int y=39;y<=42;y++)Main.tile[50,y].active(false);}
        }
    }
}
