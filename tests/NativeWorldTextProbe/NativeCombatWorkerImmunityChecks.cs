using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatWorkerImmunityChecks
    {
        internal static void Run(Assembly host,string layout,string output)
        {
            CombatFont(output);
            Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            using(var child=NativeCombatWorkerChecks.Start(layout))
            {
                var errors=child.StandardError.ReadToEndAsync();
                try
                {
                    foreach(uint cooldown in new[]{1002U,1500U,0U})
                    {
                        NativeCombatWorkerChecks.Scene(false);Projectile.ClearAll();Array.Clear(Projectile.perIDStaticNPCImmunity,0,Projectile.perIDStaticNPCImmunity.Length);
                        for(int i=0;i<Main.combatText.Length;i++)Main.combatText[i]=new CombatText();
                        var target=Main.npc[0];target.life=target.lifeMax=1000;target.position=new Vector2(800,300);
                        int slot=Projectile.NewProjectile(new EntitySource_DebugCommand(),target.Center,Vector2.Zero,119,20,0,Main.myPlayer);
                        var projectile=Main.projectile[slot];projectile.timeLeft=4;
                        Require(projectile.usesIDStaticNPCImmunity && projectile.immunityIdentity==119,"Original existing IceSickle uses type immunity.");
                        Projectile.perIDStaticNPCImmunity[119,0]=cooldown;
                        var scene=NativeCombatWorkerChecks.AcquireFrozen(host,child,new[]{0},new[]{slot},0);
                        if(cooldown==1002)RefuseMissingColumn(host,child,scene.Snapshot,scene.Future);
                        NativeCombatWorkerChecks.Compare(scene.Future,0,output,"static-immunity-"+cooldown);
                        Require(cooldown==1500?target.life==1000:target.life<1000,"Original existing projectile actually tests blocked/expired immunity.");
                    }
                }
                finally
                {
                    NativeCombatWorkerChecks.Exit(child,"immunity helper exits");Require(errors.Wait(5000),"Immunity stderr closes.");
                    File.WriteAllText(Path.Combine(output,"immunity-worker.log"),errors.Result);Console.WriteLine(errors.Result);
                }
            }
            Console.WriteLine("PASS original static projectile immunity: before/at expiry, blocked hit and same-worker reset.");
        }
        private static void RefuseMissingColumn(Assembly host,System.Diagnostics.Process child,byte[] complete,byte[] expected)
        {
            byte[] page;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeImmunitySnapshot",true).GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{writer,new[]{0}});writer.Flush();page=stream.ToArray();}
            int found=-1;
            for(int i=0;i<=complete.Length-page.Length;i++)
            {
                int j=0;while(j<page.Length && complete[i+j]==page[j])j++;
                if(j!=page.Length)continue;Require(found==-1,"Unique immunity value page in frozen fixture.");found=i;
            }
            Require(found>=0,"Locate independent immunity page.");
            foreach(var invalid in new[]{Tuple.Create(0,-1),Tuple.Create(0,201),Tuple.Create(4,-1),Tuple.Create(4,200)})
            {
                byte[] bad=(byte[])complete.Clone();Buffer.BlockCopy(BitConverter.GetBytes(invalid.Item2),0,bad,found+invalid.Item1,4);RefuseIdentity(child,bad);
            }
            byte[] badGeneration=(byte[])complete.Clone();badGeneration[found+8]^=1;RefuseIdentity(child,badGeneration);
            var missing=new byte[complete.Length-page.Length+4];Buffer.BlockCopy(complete,0,missing,0,found);Buffer.BlockCopy(complete,found+page.Length,missing,found+4,complete.Length-found-page.Length);
            using(var reader=new BinaryReader(new MemoryStream(NativeCombatWorkerChecks.Exchange(child,missing),false)))
            {
                Require(reader.ReadInt32()==-NativeCombatWorkerChecks.ExpectedProtocol,"Missing immunity column refuses entire future.");reader.ReadString();string reason=reader.ReadString();reader.ReadInt32();reader.ReadInt32();
                Require(reader.ReadInt32()==1 && reader.ReadInt32()==0 && reason.Contains("immunity column"),"Missing immunity identifies NPC dependency rather than using zero.");
            }
            byte[] restored=NativeCombatWorkerChecks.Exchange(child,complete);
            Require(BitConverter.ToInt32(restored,0)==NativeCombatWorkerChecks.ExpectedProtocol,"A fresh complete request restores immunity after refusal.");
            Require(restored.Take(restored.Length-32).SequenceEqual(expected.Take(expected.Length-32)),"Complete restored future equals the original frozen result, excluding measured times.");
            Console.WriteLine("PASS omitted immunity column refuses and complete same-worker replay recovers.");
        }
        private static void RefuseIdentity(System.Diagnostics.Process child,byte[] bad)
        {
            using(var reader=new BinaryReader(new MemoryStream(NativeCombatWorkerChecks.Exchange(child,bad),false)))
            {Require(reader.ReadInt32()==-NativeCombatWorkerChecks.ExpectedProtocol,"Malformed immunity identity refuses.");reader.ReadString();Require(reader.ReadString().Contains("Immunity column"),"Column count/slot/generation boundary rejects exact cause.");}
        }
        internal static void CombatFont(string output)
        {
            // Original CombatText still allocates a slot and consumes RNG.
            // Supply actual ReLogic CPU glyph metrics, with no texture/device,
            // asset loading. The required source is an empty fixture directory.
            var glyphs=new System.Collections.Generic.List<Rectangle>();var characters=new System.Collections.Generic.List<char>();var kerning=new System.Collections.Generic.List<Vector3>();
            for(char c=' ';c<='~';c++){glyphs.Add(new Rectangle(0,0,0,16));characters.Add(c);kerning.Add(Vector3.Zero);}
            var font=new ReLogic.Graphics.DynamicSpriteFont(0,20,'?');Type pageType=typeof(ReLogic.Graphics.DynamicSpriteFont).Assembly.GetType("ReLogic.Graphics.FontPage",true);
            object page=Activator.CreateInstance(pageType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{null,glyphs,new System.Collections.Generic.List<Rectangle>(glyphs),characters,kerning},null);
            Array pages=Array.CreateInstance(pageType,1);pages.SetValue(page,0);typeof(ReLogic.Graphics.DynamicSpriteFont).GetMethod("SetPages",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(font,new object[]{pages});
            var asset=(ReLogic.Content.Asset<ReLogic.Graphics.DynamicSpriteFont>)Activator.CreateInstance(typeof(ReLogic.Content.Asset<ReLogic.Graphics.DynamicSpriteFont>),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{"immunity-cpu-font"},null);
            string source=Path.Combine(output,"empty-font-source");Directory.CreateDirectory(source);
            asset.GetType().GetMethod("SubmitLoadedContent",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(asset,new object[]{font,new ReLogic.Content.Sources.FileSystemContentSource(source)});
            Terraria.GameContent.FontAssets.CombatText[0]=Terraria.GameContent.FontAssets.CombatText[1]=asset;
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
