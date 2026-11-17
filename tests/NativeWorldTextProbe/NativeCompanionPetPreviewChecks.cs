using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionPetPreviewChecks
    {
        [ThreadStatic] private static Projectile failingDummy;
        private const string Failure="private pet preview draw failure sentinel";
        private static void FailAfterPrepare(Projectile proj)
        {if(ReferenceEquals(proj,failingDummy))throw new InvalidOperationException(Failure);}
        private static void DrawPet(object preview,Matrix matrix)
        {Call(Get(preview,"Pet"),"Draw",Get(preview,"Display"),Get(preview,"showcaseFeet"),Get(preview,"showcaseScale"),matrix);}
        private static void AssertUnion(object window,object preview,bool own,ProbeGraphics graphics,Matrix matrix)
        {
            // Draw the actual complete body + pet on a transparent full canvas,
            // without the window's frame clip: clipped pixels cannot pass fit.
            var pixels=graphics.Pixels(()=>{DrawPet(preview,matrix);Call(preview,"DrawBodyAt",Get(window,"portraits"),Get(preview,"showcaseFeet"),Get(preview,"showcaseScale"),matrix);},matrix,Main.screenWidth,Main.screenHeight);
            int minX=Main.screenWidth,minY=Main.screenHeight,maxX=0,maxY=0,count=0;
            for(int y=0;y<Main.screenHeight;y++)for(int x=0;x<Main.screenWidth;x++)if(pixels[y*Main.screenWidth+x].A>20){count++;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
            var frame=Get(window,own?"ownerShowcase":"companionShowcase");float scale=matrix.M11;
            float left=(float)Get(frame,"X")*scale,top=(float)Get(frame,"Y")*scale,right=(float)Get(frame,"Right")*scale,bottom=(float)Get(frame,"Bottom")*scale;
            float dx=(minX+maxX)/2f-(left+right)/2,dy=(minY+maxY)/2f-(top+bottom)/2;
            Console.WriteLine("SHOWCASE-UNION owner="+own+" pet="+(GetOptional(Get(preview,"Pet"),"Dummy")!=null)+" pixels="+count+" center-delta="+dx+","+dy+" bounds="+minX+","+minY+".."+maxX+","+maxY+" scale="+Get(preview,"showcaseScale"));
            Require(count>60&&Math.Abs(dx)<=3&&Math.Abs(dy)<=3&&minX>=left+6*scale-1&&maxX<=right-6*scale+1&&minY>=top+6*scale-1&&maxY<=bottom-6*scale+1,"unclipped actual body/pet pixel union is centered in the fixed frame with visible inner margin");
        }
        private static void ExceptionBoundary(object window,object preview,bool own,ProbeGraphics graphics,Matrix matrix)
        {
            var pet=Get(preview,"Pet");var display=(Player)Get(preview,"Display");
            var position=display.position;var random=Main.rand;var entity=Main.CurrentDrawnEntity;
            int shader=Main.CurrentDrawnEntityShader,mount=Mount.currentShader;var light=Lighting.GetColor(20,20);
            Require(light!=Color.White,"exception probe uses real non-menu lighting so leaked white-light scope is detectable");
            var guard=new Harmony("JueMingR.Tests.CompanionPetPreviewFailure");bool threw=false;
            try
            {
                failingDummy=(Projectile)Get(pet,"Dummy");
                guard.Patch(AccessTools.Method(typeof(Main),"PrepareDrawnProjectileDrawing"),postfix:new HarmonyMethod(typeof(NativeCompanionPetPreviewChecks),nameof(FailAfterPrepare)));
                Set(preview,"showcaseRevision",-1);
                graphics.Render(()=> {
                    try {Call(preview,"DrawShowcase",Get(window,"portraits"),Get(window,own?"ownerShowcase":"companionShowcase"),matrix,Get(Get(window,"renderer"),"Generation"));}
                    catch(TargetInvocationException e) when(e.InnerException is InvalidOperationException&&e.InnerException.Message==Failure) {threw=true;}
                },matrix);
                Require(threw&&ReferenceEquals(Main.rand,random)&&display.position==position&&ReferenceEquals(Main.CurrentDrawnEntity,entity)&&Main.CurrentDrawnEntityShader==shader&&Mount.currentShader==mount&&Lighting.GetColor(20,20)==light,"real native draw exception propagates after exact dummy setup and restores RNG/display/entity/shader/mount/lighting scope");
            }
            finally {failingDummy=null;guard.UnpatchAll(guard.Id);}
            // Render's successful End above, then a fresh native Draw, prove the
            // interrupted private SpriteBatch was replaced with its caller pass.
            var hooks=preview.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.CompanionPetPreviewHooks",true);
            Require(hooks.GetField("measuring",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)==null,"failed native measurement returns its exact body/pet capture scope");
            graphics.Render(()=>Call(window,"Draw"),matrix);
            Console.WriteLine("PASS exact native pet draw exception remains visible and restores caller SpriteBatch/matrix and every borrowed draw scope.");
        }
        internal static void Run(object window,ProbeGraphics graphics,Player owner,Player ai,string output,Matrix? projection=null)
        {
            Matrix matrix=projection??Main.UIScaleMatrix;
            graphics.LoadItemTextures(new int[]{ItemID.Carrot,ItemID.ZephyrFish});
            graphics.LoadTexture("Projectile","Images/Projectile_111",111);graphics.LoadTexture("Projectile","Images/Projectile_380",380);
            var ownerItem=owner.miscEquips[0];var aiItem=ai.miscEquips[0];bool ownerHidden=owner.hideMisc[0],aiHidden=ai.hideMisc[0];
            var players=Main.player.ToArray();var projectiles=Main.projectile.ToArray();var mouse=Main.mouseItem;var random=Main.rand;
            var ownerPosition=owner.position;var aiPosition=ai.position;int afk=owner.afkCounter,timer=owner.miscCounter;
            int dust=Main.dust.Count(d=>d!=null&&d.active),gore=Main.gore.Count(g=>g!=null&&g.active);
            var entity=Main.CurrentDrawnEntity;int shader=Main.CurrentDrawnEntityShader,mount=Mount.currentShader;
            var screen=new Vector2(Main.screenWidth,Main.screenHeight);int layouts=(int)Get(window,"LayoutBuilds");
            try
            {
                owner.miscEquips[0]=new Item();owner.miscEquips[0].SetDefaults(ItemID.Carrot);
                ai.miscEquips[0]=new Item();ai.miscEquips[0].SetDefaults(ItemID.ZephyrFish);owner.hideMisc[0]=ai.hideMisc[0]=false;
                Main.rand=new UnifiedRandom(9519);var expected=new UnifiedRandom(9519);
                Call(window,"Prepare",matrix,screen);
                graphics.Image(Path.Combine(output,"companion-native-equipped-pets.png"),()=>Call(window,"Draw"),matrix,Main.screenWidth,Main.screenHeight);
                foreach(bool own in new[]{true,false})
                {
                    var preview=Get(window,own?"ownerPreview":"companionPreview");var display=(Player)Get(preview,"Display");var pet=Get(preview,"Pet");var dummy=(Projectile)Get(pet,"Dummy");
                    Require(dummy.owner==255&&dummy.isAPreviewDummy&&dummy.type==(own?111:380)&&!Main.projectile.Contains(dummy)&&!ReferenceEquals(display,own?owner:ai),"equipped pet owns an original detached preview dummy and detached visual owner");
                    var frame=Get(window,own?"ownerShowcase":"companionShowcase");var resources=Get(window,own?"ownerHero":"companionHero");
                    Require((float)Get(resources,"Right")+7<=(float)Get(frame,"X"),"pet showcase has a real gap from resource text/icons/bars");
                    var pixels=graphics.Pixels(()=>DrawPet(preview,matrix),matrix,Main.screenWidth,Main.screenHeight);
                    int minX=Main.screenWidth,minY=Main.screenHeight,maxX=0,maxY=0,count=0;
                    for(int y=0;y<Main.screenHeight;y++)for(int x=0;x<Main.screenWidth;x++)if(pixels[y*Main.screenWidth+x].A>20){count++;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
                    float scale=matrix.M11;
                    Require(count>25&&minX>=(float)Get(frame,"X")*scale-1&&maxX<=(float)Get(frame,"Right")*scale+1&&minY>=(float)Get(frame,"Y")*scale-1&&maxY<=(float)Get(frame,"Bottom")*scale+1,"actual native pet pixels fit the dedicated dark showcase without covering resources/name/tabs");
                    Console.WriteLine("PET-PIXELS owner="+own+" type="+dummy.type+" pixels="+count+" bounds="+minX+","+minY+".."+maxX+","+maxY);
                    AssertUnion(window,preview,own,graphics,matrix);if(own){ExceptionBoundary(window,preview,own,graphics,matrix);AssertUnion(window,preview,own,graphics,matrix);}
                    int rebuilds=(int)Get(pet,"Rebuilds"),measurements=(int)Get(preview,"ShowcaseMeasurements");for(int i=0;i<4;i++)graphics.Render(()=>Call(window,"Draw"),matrix);
                    Require((int)Get(pet,"Rebuilds")==rebuilds&&(int)Get(preview,"ShowcaseMeasurements")==measurements,"stable pet presentation reuses the native dummy and performs no layer measurement/readback");
                }
                Require((int)Get(window,"LayoutBuilds")==layouts,"equipping/hiding a preview pet does not rebuild static controls or retire valid input");
                Require(Main.rand.Next()==expected.Next()&&Main.player.SequenceEqual(players)&&Main.projectile.SequenceEqual(projectiles)&&ReferenceEquals(Main.mouseItem,mouse),"native pet drawing preserves world RNG/player/projectile arrays and mouse item");
                Require(owner.position==ownerPosition&&ai.position==aiPosition&&owner.afkCounter==afk&&owner.miscCounter==timer&&Main.dust.Count(d=>d!=null&&d.active)==dust&&Main.gore.Count(g=>g!=null&&g.active)==gore,"pet previews do not advance real bodies, timers or world particles");
                Require(ReferenceEquals(Main.CurrentDrawnEntity,entity)&&Main.CurrentDrawnEntityShader==shader&&Mount.currentShader==mount,"native pet scope restores shared draw entity/shader and mount state");
                var ownerPreview=Get(window,"ownerPreview");var aiPreview=Get(window,"companionPreview");var aiDummy=Get(Get(aiPreview,"Pet"),"Dummy");
                var equippedOwner=owner.miscEquips[0];owner.hideMisc[0]=true;Call(window,"Prepare",matrix,screen);
                graphics.Image(Path.Combine(output,"companion-native-equipped-pet-hidden.png"),()=>Call(window,"Draw"),matrix,Main.screenWidth,Main.screenHeight);
                var hidden=graphics.Pixels(()=>DrawPet(ownerPreview,matrix),matrix,Main.screenWidth,Main.screenHeight);
                Require(GetOptional(Get(ownerPreview,"Pet"),"Dummy")==null&&hidden.All(c=>c.A==0)&&ReferenceEquals(Get(Get(aiPreview,"Pet"),"Dummy"),aiDummy),"native hideMisc clears this owner's dummy and actual pet pixels while the other dummy stays unchanged");
                AssertUnion(window,ownerPreview,true,graphics,matrix);
                owner.hideMisc[0]=false;Call(window,"Prepare",matrix,screen);
                graphics.Image(Path.Combine(output,"companion-native-equipped-pet-restored.png"),()=>Call(window,"Draw"),matrix,Main.screenWidth,Main.screenHeight);
                var restored=graphics.Pixels(()=>DrawPet(ownerPreview,matrix),matrix,Main.screenWidth,Main.screenHeight);
                Require(restored.Count(c=>c.A>20)>25&&((Projectile)Get(Get(ownerPreview,"Pet"),"Dummy")).type==111&&ReferenceEquals(owner.miscEquips[0],equippedOwner)&&ReferenceEquals(Get(Get(aiPreview,"Pet"),"Dummy"),aiDummy),"unhiding restores the same equipped pet source without replacing or rebuilding the other character's dummy");
                Require((int)Get(window,"LayoutBuilds")==layouts,"hide/show changes only local display and preserves static controls");
            }
            finally
            {
                owner.miscEquips[0]=ownerItem;ai.miscEquips[0]=aiItem;owner.hideMisc[0]=ownerHidden;ai.hideMisc[0]=aiHidden;Main.rand=random;
                Call(window,"Prepare",matrix,screen);graphics.Render(()=>Call(window,"Draw"),matrix);
            }
            Console.WriteLine("PASS original equipped bunny/flying-fish preview dummies, native animations/draw, dark frame bounds, visibility, static work and real-owner/world isolation; no pet Spawn/AI/Update.");
        }
        internal static void Showcase(object context,ProbeGraphics graphics,string output)
        {
            // Same finite native startup/graphics/window consumers as the full
            // visual leaf; this focused entry has no creator/Journey/input run.
            NativeCombatAimChecks.Initialize();
            Main.spawnTileX=60;Main.spawnTileY=58;Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
            for(int x=2;x<118;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=TileID.Stone;}
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player),"Spawn"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            NativeCompanionCoreLifecycleChecks.PrepareHuman();graphics.LoadCompanionAssets();
            var shell=Get(context,"Shell");var host=Get(context,"Companions");var window=Get(shell,"CompanionUi");var archive=Get(host,"profiles");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(archive,"Ready");});
            Require((bool)Call(host,"Create","居中队友"),"focused showcase uses a real saved AI archive");
            NativeQuickItemChecks.Until(()=>{Call(host,"Update",(ulong)Main.GameUpdateCount);return ((IEnumerable)Get(host,"Profiles")).Cast<object>().Any();});
            var id=(Guid)Get(((IEnumerable)Get(host,"Profiles")).Cast<object>().Last(),"Id");
            NativeQuickItemChecks.Until(()=>{Call(host,"Update",(ulong)Main.GameUpdateCount);return (bool)Call(archive,"IsSaved",id);});
            var owner=Main.LocalPlayer;var ai=(Player)Call(host,"GetBody",id);
            graphics.LoadItemTextures(owner.inventory.Concat(ai.inventory).Where(i=>i!=null&&!i.IsAir).Select(i=>i.type).Distinct());
            var renderer=Get(shell,"renderer");string name=owner.name,style=Main.ResourceSetsManager.ActiveSetKeyName;
            int[] stats={owner.statLife,owner.statLifeMax,owner.statLifeMax2,owner.statMana,owner.statManaMax,owner.statManaMax2};
            try
            {
                owner.name="验证主人";owner.statLife=463;owner.statLifeMax=500;owner.statLifeMax2=600;owner.statMana=371;owner.statManaMax=200;owner.statManaMax2=400;
                Main.screenWidth=1280;Main.screenHeight=800;Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1;
                Call(renderer,"RefreshResources");Call(renderer,"Prepare",Get(shell,"State"),1280f,800f,1f);
                Call(Main.ResourceSetsManager,"SetActive","HorizontalBarsWithFullText");Call(window,"Toggle",Get(Get(Get(shell,"State"),"Layout"),"Viewport"));Set(window,"selected",id);Set(window,"page",0);
                Call(window,"Prepare",Matrix.Identity,new Vector2(1280,800));
                graphics.Image(Path.Combine(output,"companion-showcase-empty.png"),()=>Call(window,"Draw"),Matrix.Identity,1280,800);
                AssertUnion(window,Get(window,"ownerPreview"),true,graphics,Matrix.Identity);AssertUnion(window,Get(window,"companionPreview"),false,graphics,Matrix.Identity);
                Run(window,graphics,owner,ai,output);
                Main.screenWidth=840;Main.screenHeight=640;Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();var matrix=Matrix.CreateScale(1.5f);
                Call(window,"Prepare",matrix,new Vector2(840,640));
                foreach(bool own in new[]{true,false}) {var frame=Get(window,own?"ownerShowcase":"companionShowcase");var view=Get(window,own?"ownerView":"companionView");Require((float)Get(frame,"Right")<=(float)Get(view,"Right")-11.9f,"focused narrow showcase keeps its right inner margin");}
                graphics.Image(Path.Combine(output,"companion-showcase-narrow-empty.png"),()=>Call(window,"Draw"),matrix,840,640);
                AssertUnion(window,Get(window,"ownerPreview"),true,graphics,matrix);AssertUnion(window,Get(window,"companionPreview"),false,graphics,matrix);
                string narrow=Path.Combine(output,"narrow");Directory.CreateDirectory(narrow);Run(window,graphics,owner,ai,narrow,matrix);
                var preview=Get(window,"ownerPreview");var pet=Get(preview,"Pet");int draws=(int)Get(pet,"Draws"),measures=(int)Get(preview,"ShowcaseMeasurements");
                Call(window,"Close");for(int i=0;i<4;i++){Call(window,"Prepare",matrix,new Vector2(840,640));graphics.Render(()=>Call(window,"Draw"),matrix);}
                Require(GetOptional(pet,"Dummy")==null&&GetOptional(preview,"showcaseAssets")==null&&(int)Get(pet,"Draws")==draws&&(int)Get(preview,"ShowcaseMeasurements")==measures,"closed showcase clears owned dummies/asset lease and performs zero drawing or layer measurement");
            }
            finally {Call(window,"Close");owner.name=name;owner.statLife=stats[0];owner.statLifeMax=stats[1];owner.statLifeMax2=stats[2];owner.statMana=stats[3];owner.statManaMax=stats[4];owner.statManaMax2=stats[5];Call(Main.ResourceSetsManager,"SetActive",style);}
            Console.WriteLine("PASS fixed showcase actual unclipped unions: no pet/Bunny/Zephyr, default+narrow centering/fit, stable/closed zero measurement and exception isolation; no creation/Journey/multiplayer rerun.");
        }
    }
}
