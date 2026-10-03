using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace NativeWorldTextProbe
{
    // A narrow original-code oracle for the proposed query/advance split.
    // No product acceptance or native state is patched. The deliberately
    // incomplete background model is evidence against that model, not a
    // substitute for the actual NPC future or a permitted product fallback.
    internal static class NativeCombatQueryBoundaryChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context,string output)
        {
            var actual=Future(false);var frozen=Future(true);
            var rows=new List<string>{"step,actual_x,actual_y,frozen_x,frozen_y,background_x,background_y,distance,actual_vx,actual_vy,frozen_vx,frozen_vy"};
            int entered=-1;float largest=0;
            for(int i=0;i<actual.Count;i++)
            {
                var a=actual[i];var b=frozen[i];
                if(a.Distance<=100 && entered<0)entered=i;
                largest=Math.Max(largest,Vector2.Distance(a.Target,b.Target));
                rows.Add(string.Join(",",i,F(a.Target.X),F(a.Target.Y),F(b.Target.X),F(b.Target.Y),F(a.Background.X),F(a.Background.Y),F(a.Distance),F(a.Velocity.X),F(a.Velocity.Y),F(b.Velocity.X),F(b.Velocity.Y)));
            }
            File.WriteAllLines(Path.Combine(output,"query-future-oracle.csv"),rows);
            Console.WriteLine("QUERY-ORACLE initial-distance="+F(actual[0].Distance)+" initial-background-velocity=0 first-natural-entry="+entered+" largest-selected-route-difference="+F(largest));
            Require(actual[0].Distance>100,"The background starts outside the original spatial gate.");
            Require(entered>0,"Pre-existing original AI must naturally enter the original 100px interaction gate.");
            // Both long runs are useful observations, but different background
            // updates may consume RNG. Isolate the geometric causal branch in
            // one original AI call with identical target input and RNG instead
            // of attributing every long-run difference to the spatial gate.
            Vector2 inside=Interaction(actual[entered].Target,actual[entered].Background);
            Vector2 outside=Interaction(actual[entered].Target,actual[0].Background);
            float impulse=Vector2.Distance(inside,outside);
            Console.WriteLine("QUERY-INTERACTION identical-target-and-rng velocity-difference="+F(impulse));
            Require(impulse>.2f,"The naturally entering actor changes the selected original AI's movement at its real spatial gate.");

            // Roundtrip only the small directory, then explicitly mark slot 1
            // known. This probes known-data role versus update permission;
            // it does not restore a full NPC page or its actor context.
            Type directory=context.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEntityDirectory",true);
            directory.GetMethod("Reset",Flags).Invoke(null,null);
            byte[] observed;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {directory.GetMethod("Write",Flags).Invoke(null,new object[]{writer});writer.Flush();observed=stream.ToArray();}
            try
            {
                using(var reader=new BinaryReader(new MemoryStream(observed,false)))directory.GetMethod("Read",Flags).Invoke(null,new object[]{reader});
                directory.GetMethod("KnowNpc",Flags).Invoke(null,new object[]{1});
                bool permitted=(bool)directory.GetMethod("CanAdvance",Flags,null,new[]{typeof(NPC)},null).Invoke(null,new object[]{Main.npc[1]});
                Console.WriteLine("QUERY-RED directory-roundtrip-known-slot=1 can-advance="+permitted);
                Require(!permitted,"Explicit KnowNpc after directory roundtrip must not grant NPC advancement permission.");
            }
            finally{directory.GetMethod("Reset",Flags).Invoke(null,null);}
        }
        private struct Sample {internal Vector2 Target,Background,Velocity;internal float Distance;}
        private static List<Sample> Future(bool freeze)
        {
            NativeCombatLiveContextChecks.FlightWorld();
            foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;
            Main.dayTime=false;Main.time=1000;Main.gameMenu=false;Main.gamePaused=false;
            Main.LocalPlayer.position=new Vector2(1300,1800);Main.LocalPlayer.velocity=Vector2.Zero;
            Main.LocalPlayer.controlLeft=Main.LocalPlayer.controlRight=Main.LocalPlayer.controlUp=Main.LocalPlayer.controlDown=Main.LocalPlayer.controlJump=false;
            typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdateNPCs",new UnifiedRandom(879)}});
            Main.rand=new UnifiedRandom(123);
            NPC target=Main.npc[0],background=Main.npc[1];
            target.SetDefaults(NPCID.Butterfly);target.active=true;target.whoAmI=0;target.position=new Vector2(1500,1800);target.velocity=Vector2.Zero;target.timeLeft=750;
            background.SetDefaults(NPCID.DemonEye);background.active=true;background.whoAmI=1;background.position=new Vector2(1790,1800);background.velocity=Vector2.Zero;background.target=Main.myPlayer;background.timeLeft=750;
            var samples=new List<Sample>();
            for(int i=0;i<=120;i++)
            {
                samples.Add(new Sample{Target=target.position,Background=background.position,Velocity=target.velocity,Distance=background.Hitbox.Distance(target.Center)});
                if(i==120)break;
                typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,Main.GameUpdateCount+1);
                target.UpdateNPC(0);if(!freeze)background.UpdateNPC(1);
                Require(target.active && background.active,"The oracle keeps the same two live original instances.");
            }
            return samples;
        }
        private static Vector2 Interaction(Vector2 targetPosition,Vector2 backgroundPosition)
        {
            NPC target=Main.npc[0],background=Main.npc[1];
            target.SetDefaults(NPCID.Butterfly);target.active=true;target.whoAmI=0;target.position=targetPosition;target.velocity=Vector2.Zero;target.localAI[1]=0;
            background.SetDefaults(NPCID.DemonEye);background.active=true;background.whoAmI=1;background.position=backgroundPosition;Main.rand=new UnifiedRandom(981);
            typeof(NPC).GetMethod("AI_065_Butterflies",Flags).Invoke(target,null);
            return target.velocity;
        }
        private static string F(float value){return value.ToString("R",CultureInfo.InvariantCulture);}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
