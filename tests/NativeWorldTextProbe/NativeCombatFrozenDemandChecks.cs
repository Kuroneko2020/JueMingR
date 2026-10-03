using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;
using JueMingR.Features.Combat;

namespace NativeWorldTextProbe
{
    // One frozen original instant, not a production page-hint policy. Every
    // refusal restarts from the same full values and keyed RNG; only pages
    // explicitly requested by the original worker are added by AcquireFrozen.
    internal static class NativeCombatFrozenDemandChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        internal static void Run(object context,object native,string output)
        {
            NativeCombatObservationChecks.Save(NativeCombatAttackMechanismChecks.Get(context,"CombatObservation"),new ObservationOptions());
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.hardMode=true;
            var player=Main.LocalPlayer;player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;
            player.controlLeft=player.controlRight=player.controlJump=false;player.dead=false;
            player.statLife=player.statLifeMax=player.statLifeMax2=100000;player.immune=true;player.immuneTime=100000;
            player.wet=player.lavaWet=player.honeyWet=player.shimmerWet=false;player.fallStart=player.fallStart2=150;
            Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);Array.Clear(player.hurtCooldowns,0,player.hurtCooldowns.Length);
            for(int i=0;i<10;i++)player.armor[i].TurnToAir();
            typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,1000U);
            typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)},{"UpdateProjectiles",new UnifiedRandom(171)}});
            int selected=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2400,110,Start:16,Target:0);Main.npc[selected].life=Main.npc[selected].lifeMax=100000;
            for(int i=0;i<33;i++)if(i!=selected){var n=new NPC();n.SetDefaults(678);n.whoAmI=i;n.active=true;n.position=new Vector2(3000+(i<selected?i:i-1)*30,2400-n.height);Main.npc[i]=n;}
            var assembly=native.GetType().Assembly;int attempt=0;
            var rows=new List<string>{"attempt,kind,slot,field,requestBytes,responseBytes,error"};
            using(var child=NativeCombatWorkerChecks.Start(Path.GetDirectoryName(assembly.Location)))
            {
                var errors=child.StandardError.ReadToEndAsync();
                try
                {
                    var frozen=NativeCombatWorkerChecks.AcquireFrozen(assembly,request=>
                    {
                        Require(attempt<=40,"One bounded frozen dependency closure.");
                        byte[] response=NativeCombatWorkerChecks.Exchange(child,request);
                        string label="frozen-"+attempt;
                        File.WriteAllBytes(Path.Combine(output,label+"-request.bin"),request);File.WriteAllBytes(Path.Combine(output,label+"-core.bin"),response);
                        using(var reader=new BinaryReader(new MemoryStream(response)))
                        {
                            int protocol=reader.ReadInt32();int kind=0,slot=-1,field=0;string error="";
                            if(protocol<0){reader.ReadString();error=reader.ReadString();reader.ReadInt32();reader.ReadInt32();kind=reader.ReadInt32();slot=reader.ReadInt32();field=reader.ReadInt32();}
                            rows.Add(NativeCombatAttackMechanismChecks.Csv(attempt,kind,slot,field.ToString("X8"),request.Length,response.Length,error));
                        }
                        attempt++;return response;
                    },new[]{selected},new int[0],selected,new Rectangle(0,0,Main.maxTilesX,Main.maxTilesY),horizon:180);
                    File.WriteAllBytes(Path.Combine(output,"frozen-complete-request.bin"),frozen.Snapshot);
                    File.WriteAllBytes(Path.Combine(output,"frozen-complete-core.bin"),frozen.Future);
                    byte[] repeat=NativeCombatWorkerChecks.Exchange(child,frozen.Snapshot);File.WriteAllBytes(Path.Combine(output,"frozen-repeat-core.bin"),repeat);
                    // The final 32 bytes are measured clock ticks, not state.
                    Require(frozen.Future.Take(frozen.Future.Length-32).SequenceEqual(repeat.Take(repeat.Length-32)),"Frozen replay preserves every result/dependency byte except timing.");
                    File.WriteAllText(Path.Combine(output,"frozen-pages.txt"),"NPC="+string.Join(",",frozen.Npcs)+"\nProjectile="+string.Join(",",frozen.Projectiles));
                    // This player stands on the floor with no held movement;
                    // the motion-quality bit describes displacement, not whether
                    // the original Player.Update callback was invoked.
                    NativeCombatWorkerChecks.Compare(frozen.Future,selected,output,"frozen-town-demand",nativeStreams:true,expectPlayerMotion:false,
                        playerUpdate:()=>{using(Main.SwapRandom("UpdatePlayers"))Main.LocalPlayer.Update(0);},motionTolerance:.002f,expectedHorizon:180);
                    Console.WriteLine("FROZEN-DEMAND attempts="+attempt+" npc-pages="+frozen.Npcs.Length+" projectile-pages="+frozen.Projectiles.Length+" exact-repeat=true original-updates=180; no live hint-policy change");
                }
                finally
                {
                    NativeCombatWorkerChecks.Exit(child,"frozen demand helper exits");Require(errors.Wait(5000),"Frozen demand stderr closes.");
                    File.WriteAllText(Path.Combine(output,"frozen-worker.log"),errors.Result);File.WriteAllLines(Path.Combine(output,"frozen-demand.csv"),rows);
                }
            }
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
