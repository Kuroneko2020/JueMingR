using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private static void ExceptionBoundary(object preview,object pose,ProbeGraphics graphics)
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
                graphics.Render(()=> {
                    try {Call(preview,"DrawPet",pose,Main.UIScaleMatrix);}
                    catch(TargetInvocationException e) when(e.InnerException is InvalidOperationException&&e.InnerException.Message==Failure) {threw=true;}
                },Main.UIScaleMatrix);
                Require(threw&&ReferenceEquals(Main.rand,random)&&display.position==position&&ReferenceEquals(Main.CurrentDrawnEntity,entity)&&Main.CurrentDrawnEntityShader==shader&&Mount.currentShader==mount&&Lighting.GetColor(20,20)==light,"real native draw exception propagates after exact dummy setup and restores RNG/display/entity/shader/mount/lighting scope");
            }
            finally {failingDummy=null;guard.UnpatchAll(guard.Id);}
            // Render's successful End above, then a fresh native Draw, prove the
            // interrupted private SpriteBatch was replaced with its caller pass.
            graphics.Render(()=>Call(preview,"DrawPet",pose,Main.UIScaleMatrix),Main.UIScaleMatrix);
            Console.WriteLine("PASS exact native pet draw exception remains visible and restores caller SpriteBatch/matrix and every borrowed draw scope.");
        }
        internal static void Run(object window,ProbeGraphics graphics,Player owner,Player ai,string output)
        {
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
                Call(window,"Prepare",Main.UIScaleMatrix,screen);
                graphics.Image(Path.Combine(output,"companion-native-equipped-pets.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);
                foreach(bool own in new[]{true,false})
                {
                    var preview=Get(window,own?"ownerPreview":"companionPreview");var display=(Player)Get(preview,"Display");var pet=Get(preview,"Pet");var dummy=(Projectile)Get(pet,"Dummy");
                    Require(dummy.owner==255&&dummy.isAPreviewDummy&&dummy.type==(own?111:380)&&!Main.projectile.Contains(dummy)&&!ReferenceEquals(display,own?owner:ai),"equipped pet owns an original detached preview dummy and detached visual owner");
                    var frame=Get(window,own?"ownerShowcase":"companionShowcase");var resources=Get(window,own?"ownerHero":"companionHero");
                    Require((float)Get(resources,"Right")+7<=(float)Get(frame,"X"),"pet showcase has a real gap from resource text/icons/bars");
                    var pose=Call(Get(window,own?"ownerPortrait":"companionPortrait"),"Offset",-14f,0f);
                    var pixels=graphics.Pixels(()=>Call(preview,"DrawPet",pose,Main.UIScaleMatrix),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);
                    int minX=Main.screenWidth,minY=Main.screenHeight,maxX=0,maxY=0,count=0;
                    for(int y=0;y<Main.screenHeight;y++)for(int x=0;x<Main.screenWidth;x++)if(pixels[y*Main.screenWidth+x].A>20){count++;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
                    float scale=Main.UIScaleMatrix.M11;
                    Require(count>25&&minX>=(float)Get(frame,"X")*scale-1&&maxX<=(float)Get(frame,"Right")*scale+1&&minY>=(float)Get(frame,"Y")*scale-1&&maxY<=(float)Get(frame,"Bottom")*scale+1,"actual native pet pixels fit the dedicated dark showcase without covering resources/name/tabs");
                    Console.WriteLine("PET-PIXELS owner="+own+" type="+dummy.type+" pixels="+count+" bounds="+minX+","+minY+".."+maxX+","+maxY);
                    if(own)ExceptionBoundary(preview,pose,graphics);
                    int rebuilds=(int)Get(pet,"Rebuilds");for(int i=0;i<4;i++)graphics.Render(()=>Call(window,"Draw"),Main.UIScaleMatrix);
                    Require((int)Get(pet,"Rebuilds")==rebuilds,"stable pet presentation reuses the same native dummy");
                }
                Require((int)Get(window,"LayoutBuilds")==layouts,"equipping/hiding a preview pet does not rebuild static controls or retire valid input");
                Require(Main.rand.Next()==expected.Next()&&Main.player.SequenceEqual(players)&&Main.projectile.SequenceEqual(projectiles)&&ReferenceEquals(Main.mouseItem,mouse),"native pet drawing preserves world RNG/player/projectile arrays and mouse item");
                Require(owner.position==ownerPosition&&ai.position==aiPosition&&owner.afkCounter==afk&&owner.miscCounter==timer&&Main.dust.Count(d=>d!=null&&d.active)==dust&&Main.gore.Count(g=>g!=null&&g.active)==gore,"pet previews do not advance real bodies, timers or world particles");
                Require(ReferenceEquals(Main.CurrentDrawnEntity,entity)&&Main.CurrentDrawnEntityShader==shader&&Mount.currentShader==mount,"native pet scope restores shared draw entity/shader and mount state");
                var ownerPreview=Get(window,"ownerPreview");var aiPreview=Get(window,"companionPreview");var aiDummy=Get(Get(aiPreview,"Pet"),"Dummy");var ownerPose=Call(Get(window,"ownerPortrait"),"Offset",-14f,0f);
                var equippedOwner=owner.miscEquips[0];owner.hideMisc[0]=true;Call(window,"Prepare",Main.UIScaleMatrix,screen);
                graphics.Image(Path.Combine(output,"companion-native-equipped-pet-hidden.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);
                var hidden=graphics.Pixels(()=>Call(ownerPreview,"DrawPet",ownerPose,Main.UIScaleMatrix),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);
                Require(GetOptional(Get(ownerPreview,"Pet"),"Dummy")==null&&hidden.All(c=>c.A==0)&&ReferenceEquals(Get(Get(aiPreview,"Pet"),"Dummy"),aiDummy),"native hideMisc clears this owner's dummy and actual pet pixels while the other dummy stays unchanged");
                owner.hideMisc[0]=false;Call(window,"Prepare",Main.UIScaleMatrix,screen);
                graphics.Image(Path.Combine(output,"companion-native-equipped-pet-restored.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);
                var restored=graphics.Pixels(()=>Call(ownerPreview,"DrawPet",ownerPose,Main.UIScaleMatrix),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);
                Require(restored.Count(c=>c.A>20)>25&&((Projectile)Get(Get(ownerPreview,"Pet"),"Dummy")).type==111&&ReferenceEquals(owner.miscEquips[0],equippedOwner)&&ReferenceEquals(Get(Get(aiPreview,"Pet"),"Dummy"),aiDummy),"unhiding restores the same equipped pet source without replacing or rebuilding the other character's dummy");
                Require((int)Get(window,"LayoutBuilds")==layouts,"hide/show changes only local display and preserves static controls");
            }
            finally
            {
                owner.miscEquips[0]=ownerItem;ai.miscEquips[0]=aiItem;owner.hideMisc[0]=ownerHidden;ai.hideMisc[0]=aiHidden;Main.rand=random;
                Call(window,"Prepare",Main.UIScaleMatrix,screen);graphics.Render(()=>Call(window,"Draw"),Main.UIScaleMatrix);
            }
            Console.WriteLine("PASS original equipped bunny/flying-fish preview dummies, native animations/draw, dark frame bounds, visibility, static work and real-owner/world isolation; no pet Spawn/AI/Update.");
        }
    }
}
