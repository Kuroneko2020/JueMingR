using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Items;
using Terraria.Utilities;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatWorkerTagChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(Assembly host)
        {
            NativeCombatWorkerChecks.Scene(false);
            var context=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeActorContext",true);
            var source=Main.player[0];source.maxTagEffects=2;source.tagEffectDuration=1;
            var target=Main.npc[0];target.whoAmI=0;
            source.TagEffectStack.TryApplyTagToNPC(4672,target);
            byte[] contextBytes=Write(context,"WritePlayer",source);
            // Actor context carries nested values; primitive position is a
            // separate production page. Keep this tag-only fixture inside the
            // native world so BordersMovement does not test an unrelated edge.
            var copy=new Player{active=true,whoAmI=0,position=source.position,maxTagEffects=2,tagEffectDuration=1};Read(context,"ReadPlayer",contextBytes,copy);
            Require(copy.TagEffectStack.IsNPCTagged(4672,0),"RED: restoring existing tag state must not produce a new empty stack.");
            var projectile=new Projectile();projectile.SetDefaults(266);projectile.owner=0;
            string expected=Damage(source,projectile,target),actual=Damage(copy,projectile,target);
            Require(expected==actual && actual.StartsWith("4|4|True|"),"Original tag damage and RNG consumption agree after restore.");
            var tag=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTagSnapshot",true);
            Require(ReferenceEquals(typeof(TagEffectStack).GetField("_owner",Flags).GetValue(copy.TagEffectStack),copy),"Tag stack private owner preserved.");
            var originalStates=States(source);var states=States(copy);
            Require(!ReferenceEquals(states,originalStates) && !ReferenceEquals(states[0],originalStates[0]) && ReferenceEquals(typeof(TagEffectState).GetField("_owner",Flags).GetValue(states[0]),copy),"Tag state private owner and independent storage.");
            int[] times=Times(originalStates[0],"TimeLeftOnNPC"),procs=Times(originalStates[0],"ProcTimeLeftOnNPC");times[0]=1;times[1]=2;procs[0]=2;
            Read(tag,"Read",Write(tag,"Write",source),copy);
            source.TagEffectStack.Update();Main.player[0]=copy;AdvancePlayer(host,copy);
            Require(!copy.TagEffectStack.IsNPCTagged(4672,0) && copy.TagEffectStack.IsNPCTagged(4672,1) && copy.TagEffectStack.CanProcOnNPC(4672,0),"Player phase decrements preexisting tag/proc once before NPC/projectile phases.");
            Require(Write(tag,"Write",source).SequenceEqual(Write(tag,"Write",copy)),"Native expiration state equals continuation player phase.");
            source.TagEffectStack.Update();AdvancePlayer(host,copy);
            Require(!copy.TagEffectStack.IsNPCTagged(4672,1) && !copy.TagEffectStack.CanProcOnNPC(4672,0),"Tag/proc expire at the original second update.");
            int[] copiedTimes=Times(States(copy)[0],"TimeLeftOnNPC");copiedTimes[0]=2;
            copy.dead=true;AdvancePlayer(host,copy);Require(copiedTimes[0]==1,"Active dead player still decrements tag before native early return.");
            copy.dead=false;copy.ghost=true;AdvancePlayer(host,copy);Require(copiedTimes[0]==0,"Active ghost player still decrements tag.");
            Main.player[0]=new Player();Main.player[1]=copy;copy.whoAmI=1;copy.position=source.position;copiedTimes[0]=2;
            AdvancePlayer(host,copy);Require(copiedTimes[0]==1 && !copy.outOfRange,"Observed remote ghost decrements tags.");
            copy.position=new Microsoft.Xna.Framework.Vector2(-160,-160);AdvancePlayer(host,copy);
            Require(copiedTimes[0]==1 && copy.outOfRange,"Remote player outside native update area does not decrement tags.");
            copy.position=source.position;int x=(int)(copy.position.X+copy.width/2)/16,y=(int)(copy.position.Y+copy.height/2)/16;Tile tile=Main.tile[x,y];
            try{Main.tile[x,y]=null;AdvancePlayer(host,copy);Require(copiedTimes[0]==1 && copy.outOfRange,"Observed native null tile is an early return.");}finally{Main.tile[x,y]=tile;}
            Main.player[1]=new Player();copy.whoAmI=0;copy.ghost=false;Main.player[0]=copy;

            source.TagEffectStack=new TagEffectStack(source);source.TagEffectStack.TryApplyTagToNPC(4672,target);source.TagEffectStack.TryApplyTagToNPC(4912,target);source.TagEffectStack.TryEnableProcOnNPC(4912,target);
            byte[] bytes=Write(tag,"Write",source);Read(tag,"Read",bytes,copy);
            Require(States(copy)[0].Type==4912 && States(copy)[1].Type==4672,"Tag priority order retained.");
            Require(Proc(source,projectile,target)==Proc(copy,projectile,target),"Existing Firecracker creates the same original explosion and consumes proc exactly once.");
            source.maxTagEffects=copy.maxTagEffects=1;source.TagEffectStack.Update();copy.TagEffectStack.Update();
            Require(Write(tag,"Write",source).SequenceEqual(Write(tag,"Write",copy)),"Native capacity reduction retains historical inactive entries and order.");
            foreach(int count in new[]{-1,6})Refuse(tag,BitConverter.GetBytes(count),copy);
            byte[] bad=(byte[])bytes.Clone();bad[4]=0;Refuse(tag,bad,copy);
            bad=(byte[])bytes.Clone();Buffer.BlockCopy(BitConverter.GetBytes((int)Terraria.ID.ItemID.Count),0,bad,5,4);Refuse(tag,bad,copy);
            bad=(byte[])bytes.Clone();Buffer.BlockCopy(BitConverter.GetBytes(0),0,bad,5,4);Refuse(tag,bad,copy);
            bad=(byte[])bytes.Clone();Buffer.BlockCopy(BitConverter.GetBytes(4912),0,bad,10+Main.maxNPCs*8,4);Refuse(tag,bad,copy);
            Console.WriteLine("PASS original existing tag damage/RNG, owner/order/storage, player-phase expiry, single Firecracker proc and bounded malformed state.");
        }
        internal static void RemoteTerrain(Assembly host,string layout,string output)
        {
            NativeCombatWorkerChecks.Scene(false);
            var remote=Main.player[1];remote.active=true;remote.whoAmI=1;remote.ghost=true;remote.maxTagEffects=1;remote.tagEffectDuration=1;
            remote.position=new Microsoft.Xna.Framework.Vector2(110*16-remote.width/2,110*16-remote.height/2);
            remote.TagEffectStack.TryApplyTagToNPC(4672,Main.npc[0]);
            var wire=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true);
            byte[] partial=(byte[])wire.GetMethod("CaptureRegion",Flags).Invoke(null,new object[]{new[]{0},0,1000L,120,1L,0,0,95,95,false});
            using(var child=NativeCombatWorkerChecks.Start(layout))
            {
                var errors=child.StandardError.ReadToEndAsync();
                try
                {
                    using(var reader=new BinaryReader(new MemoryStream(NativeCombatWorkerChecks.Exchange(child,partial),false)))
                    {
                        Require(reader.ReadInt32()==-NativeCombatWorkerChecks.ExpectedProtocol,"Worker must refuse unknown remote player terrain.");reader.ReadString();reader.ReadString();
                        Require(reader.ReadInt32()==110 && reader.ReadInt32()==110,"Unknown remote center is not observed null or an outOfRange success.");
                    }
                    var scene=NativeCombatWorkerChecks.AcquireFrozen(host,child,new[]{0},new int[0],0);
                    // An active ghost takes the original tag tick but does not
                    // move. All future NPC/dependency states remain independently
                    // evaluated after the complete worker result is frozen.
                    NativeCombatWorkerChecks.Compare(scene.Future,0,output,"remote-tag-terrain",playerUpdate:()=>remote.TagEffectStack.Update(),expectPlayerMotion:false);
                }
                finally
                {
                    NativeCombatWorkerChecks.Exit(child,"remote tag terrain helper exits");Require(errors.Wait(5000),"Remote tag stderr closes.");
                    File.WriteAllText(Path.Combine(output,"remote-tag-worker.log"),errors.Result);
                }
            }
            Console.WriteLine("PASS actual helper remote-tag unknown terrain refusal, fresh complete recapture and 120-step native future.");
        }
        private static string Damage(Player player,Projectile projectile,NPC target)
        {
            Main.rand=new UnifiedRandom(818);var changes=TagDamageChanges.None;player.TagEffectStack.ModifyHit(projectile,target,ref changes);
            return changes.AddedBaseDamage+"|"+changes.HighestAddedBaseDamage+"|"+changes.AddProjectileTagDamage+"|"+changes.TotalDamageMultiplier+"|"+changes.Crit+"|"+NativeCombatWorkerChecks.RandomStamp();
        }
        private static string Proc(Player player,Projectile projectile,NPC target)
        {
            Projectile.ClearAll();Main.rand=new UnifiedRandom(919);player.TagEffectStack.OnHit(projectile,target,10);
            var born=Main.projectile.Where(p=>p.active).ToArray();Require(born.Length==1 && born[0].type==918 && born[0].damage==27 && born[0].localNPCImmunity[target.whoAmI]==-1,"Original Firecracker proc actually executed.");
            player.TagEffectStack.OnHit(projectile,target,10);Require(Main.projectile.Count(p=>p.active)==1 && !player.TagEffectStack.CanProcOnNPC(4912,target.whoAmI),"Consumed proc cannot execute twice.");
            return born[0].type+"|"+born[0].position+"|"+born[0].damage+"|"+born[0].timeLeft+"|"+NativeCombatWorkerChecks.RandomStamp();
        }
        private static void AdvancePlayer(Assembly host,Player player)
        {
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePlayerMotion",true);object motion=Activator.CreateInstance(type,true);
            byte[] bytes=Write(type,"Write",player);using(var reader=new BinaryReader(new MemoryStream(bytes,false)))type.GetMethod("Read",Flags).Invoke(motion,new object[]{reader,player.whoAmI});
            type.GetMethod("Begin",Flags).Invoke(motion,null);type.GetMethod("Advance",Flags).Invoke(motion,null);
        }
        private static TagEffectState[] States(Player player){return (TagEffectState[])typeof(TagEffectStack).GetField("_effectStates",Flags).GetValue(player.TagEffectStack);}
        private static int[] Times(TagEffectState state,string name){return (int[])typeof(TagEffectState).GetField(name,Flags).GetValue(state);}
        private static byte[] Write(Type type,string method,Player player)
        {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){type.GetMethod(method,Flags).Invoke(null,new object[]{writer,player});writer.Flush();return stream.ToArray();}}
        private static void Read(Type type,string method,byte[] bytes,Player player)
        {using(var reader=new BinaryReader(new MemoryStream(bytes,false)))type.GetMethod(method,Flags).Invoke(null,new object[]{reader,player});}
        private static void Refuse(Type type,byte[] bytes,Player player)
        {bool refused=false;try{Read(type,"Read",bytes,player);}catch(TargetInvocationException e){refused=e.InnerException is InvalidDataException;}Require(refused,"Malformed tag state rejected.");}
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
