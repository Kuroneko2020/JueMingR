using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Only loaded-state and dimensions cross this boundary. Placeholder assets
    // never own a Texture2D, resource loader, device, or pending load operation.
    // The fixture can copy the complete small table; the product sampler must
    // capture required keys/cache revisions, not rescan it on every Update.
    internal static class NativeAssetSnapshot
    {
        private struct Value { internal bool Present,Loaded; internal int Width,Height; }
        private static readonly Dictionary<Asset<Texture2D>,int> keys=new Dictionary<Asset<Texture2D>,int>();
        private static readonly Asset<Texture2D>[] placeholders=new Asset<Texture2D>[TextureAssets.Npc.Length+TextureAssets.Gore.Length];
        private static readonly Value[] values=new Value[placeholders.Length];
        private static readonly bool[] known=new bool[placeholders.Length];
        internal static string Failure { get; private set; }
        internal static int MissingKey {get;private set;}=-1;
        internal static void ResetFailure(){Failure=null;MissingKey=-1;}
        private static readonly Type AssetType=typeof(Asset<Texture2D>);
        private static readonly MethodInfo IsLoaded=AssetType.GetProperty("IsLoaded").GetGetMethod();
        private static readonly MethodInfo AssetValue=AssetType.GetProperty("Value").GetGetMethod();
        private static readonly MethodInfo WidthMethod=typeof(Terraria.Utils).GetMethod("Width",new[]{AssetType});
        private static readonly MethodInfo HeightMethod=typeof(Terraria.Utils).GetMethod("Height",new[]{AssetType});
        private static readonly MethodInfo SourceMethod=typeof(SpriteFrame).GetMethod("GetSourceRectangle",new[]{typeof(Texture2D)});

        internal static void WriteFixture(BinaryWriter writer)
        {
            writer.Write(values.Length);
            for(int i=0;i<values.Length;i++)
            {
                var asset=i<TextureAssets.Npc.Length?TextureAssets.Npc[i]:TextureAssets.Gore[i-TextureAssets.Npc.Length];
                writer.Write(i);writer.Write(asset!=null);
                bool loaded=asset!=null && asset.IsLoaded;writer.Write(loaded);
                writer.Write(loaded?asset.Value.Width:0);writer.Write(loaded?asset.Value.Height:0);
            }
        }
        internal static void ClearWorld()
        {
            Array.Clear(known,0,known.Length);Array.Clear(values,0,values.Length);Failure=null;MissingKey=-1;
            Array.Copy(placeholders,0,TextureAssets.Npc,0,TextureAssets.Npc.Length);
            Array.Copy(placeholders,TextureAssets.Npc.Length,TextureAssets.Gore,0,TextureAssets.Gore.Length);
        }
        internal static void WriteKeys(BinaryWriter writer,int[] selected)
        {
            writer.Write(selected.Length);int prior=-1;
            foreach(int key in selected)
            {
                if(key<=prior || key>=values.Length)throw new InvalidDataException("Production asset page order.");prior=key;
                var asset=key<TextureAssets.Npc.Length?TextureAssets.Npc[key]:TextureAssets.Gore[key-TextureAssets.Npc.Length];bool loaded=asset!=null && asset.IsLoaded;
                writer.Write(key);writer.Write(asset!=null);writer.Write(loaded);writer.Write(loaded?asset.Value.Width:0);writer.Write(loaded?asset.Value.Height:0);
            }
        }
        internal static void Read(BinaryReader reader)
        {
            ResetFailure();Array.Clear(known,0,known.Length);
            if(placeholders[0]==null)
            {
                var ctor=AssetType.GetConstructor(BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(string)},null);
                for(int i=0;i<placeholders.Length;i++)
                {var asset=(Asset<Texture2D>)ctor.Invoke(new object[]{"prediction-metadata-"+i});placeholders[i]=asset;keys.Add(asset,i);}
            }
            // Unknown is not unloaded. Unknown slots keep an identity token so
            // any native metadata read fails explicitly instead of inventing 0.
            Array.Copy(placeholders,0,TextureAssets.Npc,0,TextureAssets.Npc.Length);
            Array.Copy(placeholders,TextureAssets.Npc.Length,TextureAssets.Gore,0,TextureAssets.Gore.Length);
            int count=reader.ReadInt32();if(count<0 || count>values.Length)throw new InvalidDataException("Asset metadata count.");
            for(int i=0;i<count;i++)
            {
                int key=reader.ReadInt32();if(key<0 || key>=values.Length || known[key])throw new InvalidDataException("Asset metadata key.");
                var value=new Value{Present=reader.ReadBoolean(),Loaded=reader.ReadBoolean(),Width=reader.ReadInt32(),Height=reader.ReadInt32()};
                if(value.Loaded && (!value.Present || value.Width<1 || value.Height<1 || value.Width>32768 || value.Height>32768) || !value.Loaded && (value.Width!=0 || value.Height!=0))throw new InvalidDataException("Asset metadata dimensions.");
                known[key]=true;values[key]=value;
                if(!value.Present){if(key<TextureAssets.Npc.Length)TextureAssets.Npc[key]=null;else TextureAssets.Gore[key-TextureAssets.Npc.Length]=null;}
            }
        }
        internal static bool UsesMetadata(IEnumerable<CodeInstruction> code)
        {return code.Any(i=>Equals(i.operand,IsLoaded)||Equals(i.operand,WidthMethod)||Equals(i.operand,HeightMethod)||Equals(i.operand,SourceMethod));}
        internal static void Bind(Type guard)
        {
            guard.GetField("Metadata").SetValue(null,(Func<object,int,int>)((asset,kind)=>kind==0?(Loaded((Asset<Texture2D>)asset)?1:0):kind==1?Width((Asset<Texture2D>)asset):kind==2?Height((Asset<Texture2D>)asset):throw new InvalidDataException("Metadata operation.")));
            guard.GetField("Rectangle").SetValue(null,(Func<object,object,object>)((frame,asset)=>{var value=(SpriteFrame)frame;return SourceRectangle(ref value,(Asset<Texture2D>)asset);}));
        }
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> source)
        {
            var code=source.ToList();
            for(int i=0;i<code.Count;i++)
            {
                string replacement=null;
                if(Equals(code[i].operand,IsLoaded))replacement="Loaded";
                else if(Equals(code[i].operand,WidthMethod))replacement="Width";
                else if(Equals(code[i].operand,HeightMethod))replacement="Height";
                else if(Equals(code[i].operand,SourceMethod))replacement="SourceRectangle";
                else if(Equals(code[i].operand,AssetValue))
                {
                    // These native Gore expressions consume Value only as the
                    // immediate SpriteFrame argument. Keep the asset token on
                    // the stack and replace both ends, preserving the ref frame.
                    if(i+1>=code.Count || !Equals(code[i+1].operand,SourceMethod))throw new InvalidOperationException("Unclosed native asset value access.");
                    code[i].opcode=OpCodes.Nop;code[i].operand=null;
                }
                if(replacement!=null){code[i].opcode=OpCodes.Call;code[i].operand=typeof(NativeAssetSnapshot).GetMethod(replacement,BindingFlags.Static|BindingFlags.NonPublic);}
                yield return code[i];
            }
        }
        private static Value Get(Asset<Texture2D> asset)
        {
            if(asset==null)throw new NullReferenceException("Native asset was absent at capture.");
            int key;if(!keys.TryGetValue(asset,out key) || !known[key])
            {MissingKey=keys.TryGetValue(asset,out key)?key:-1;Failure="Unobserved texture metadata: "+(MissingKey>=0?key.ToString():"foreign");throw new InvalidDataException(Failure);}
            return values[key];
        }
        private static bool Loaded(Asset<Texture2D> asset){return Get(asset).Loaded;}
        private static int Width(Asset<Texture2D> asset){var value=Get(asset);return value.Loaded?value.Width:0;}
        private static int Height(Asset<Texture2D> asset){var value=Get(asset);return value.Loaded?value.Height:0;}
        private static Rectangle SourceRectangle(ref SpriteFrame frame,Asset<Texture2D> asset)
        {
            var value=Get(asset);if(!value.Loaded)throw new InvalidDataException("Native source rectangle requested an unloaded asset.");
            int width=value.Width/frame.ColumnCount,height=value.Height/frame.RowCount;
            return new Rectangle(frame.CurrentColumn*width,frame.CurrentRow*height,width-(frame.ColumnCount!=1?frame.PaddingX:0),height-(frame.RowCount!=1?frame.PaddingY:0));
        }
    }
}
