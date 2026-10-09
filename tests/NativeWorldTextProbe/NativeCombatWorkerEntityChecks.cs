using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatWorkerEntityChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(Assembly host,string layout,string output)
        {
            NativeCombatWorkerChecks.Scene(false);
            DirectoryRoundTrip(host);
            Main.npc[0].SetDefaults(36);Main.npc[0].whoAmI=0;Main.npc[0].active=true;
            Main.npc[0].ai[0]=-1;Main.npc[0].ai[1]=5;Main.npc[0].target=0;Main.npc[0].timeLeft=750;
            Main.npc[5].SetDefaults(35);Main.npc[5].whoAmI=5;Main.npc[5].active=true;
            Main.npc[5].position=new Vector2(700,400);Main.npc[5].ai[0]=1;Main.npc[5].target=0;Main.npc[5].timeLeft=750;
            var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic);
            using(var child=NativeCombatWorkerChecks.Start(layout))
            {
                var errors=child.StandardError.ReadToEndAsync();
                try
                {
                byte[] partial=(byte[])capture.Invoke(null,new object[]{new[]{0},0,1000L,120});
                using(var reader=new BinaryReader(new MemoryStream(NativeCombatWorkerChecks.Exchange(child,partial))))
                {
                    Require(reader.ReadInt32()==-NativeCombatWorkerChecks.ExpectedProtocol,"Missing dependency is a refused request.");
                    reader.ReadString();string reason=reader.ReadString();reader.ReadInt32();reader.ReadInt32();
                    Require(reader.BaseStream.Length-reader.BaseStream.Position>=12,"Structured entity dependency missing, not an absent parent: "+reason);
                    int kind=reader.ReadInt32(),slot=reader.ReadInt32(),field=reader.ReadInt32();
                    Require(kind==1 && slot==5 && field!=0,"Original hand read identifies the uncaptured head field; actual="+kind+"/"+slot+"/"+field.ToString("X8")+" reason="+reason);
                    Require(typeof(Main).Module.ResolveField(field).DeclaringType==typeof(NPC) || typeof(Main).Module.ResolveField(field).DeclaringType==typeof(Entity),"Missing field token belongs to original NPC or Entity.");
                }
                byte[] complete=(byte[])capture.Invoke(null,new object[]{new[]{0,5},0,1000L,120});
                object npcSchema=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetField("Npcs",Flags).GetValue(null);
                int originalType=Main.npc[0].type;
                RefuseConflict(child,SpoofPage(complete,npcSchema,Main.npc[0],()=>Main.npc[0].type=49,()=>Main.npc[0].type=originalType),"NPC page conflicts");
                byte generation=Main.npc[0].generation;var setGeneration=typeof(NPC).GetProperty("generation").GetSetMethod(true);
                RefuseConflict(child,SpoofPage(complete,npcSchema,Main.npc[0],()=>setGeneration.Invoke(Main.npc[0],new object[]{(byte)(generation+1)}),()=>setGeneration.Invoke(Main.npc[0],new object[]{generation})),"NPC page conflicts");
                byte[] future=NativeCombatWorkerChecks.Exchange(child,complete);
                NativeCombatWorkerChecks.Compare(future,0,output,"hand-lower-slot-than-parent");
                NativeCombatWorkerChecks.Scene(false);
                var source=new Terraria.DataStructures.EntitySource_DebugCommand();
                int a=Projectile.NewProjectile(source,new Vector2(400,400),Vector2.Zero,1,0,0),b=Projectile.NewProjectile(source,new Vector2(500,400),Vector2.Zero,1,0,0);
                Require(a<b && b<Main.maxProjectiles,"Distinct native projectile identities.");
                var captureScene=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureScene",Flags);
                byte[] projectileScene=(byte[])captureScene.Invoke(null,new object[]{new[]{0},new[]{a},0,1000L,120});
                object projectileSchema=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEntitySnapshot",true).GetField("Projectiles",Flags).GetValue(null);
                var originalKey=Main.projectile[a].key;
                RefuseConflict(child,SpoofPage(projectileScene,projectileSchema,Main.projectile[a],()=>Main.projectile[a].key=Main.projectile[b].key,()=>Main.projectile[a].key=originalKey),"Projectile page conflicts");
                int projectileType=Main.projectile[a].type;
                RefuseConflict(child,SpoofPage(projectileScene,projectileSchema,Main.projectile[a],()=>Main.projectile[a].type=2,()=>Main.projectile[a].type=projectileType),"Projectile page conflicts");
                Require(BitConverter.ToInt32(NativeCombatWorkerChecks.Exchange(child,complete),0)==NativeCombatWorkerChecks.ExpectedProtocol,"Identity failures do not poison the next request.");
                SeatedTown(host,child,output);
                Require(BitConverter.ToInt32(NativeCombatWorkerChecks.Exchange(child,complete),0)==NativeCombatWorkerChecks.ExpectedProtocol,"Seated NPC state cannot leak into the next request.");
                }
                finally
                {NativeCombatWorkerChecks.Exit(child,"entity helper exits");File.WriteAllText(Path.Combine(output,"entity-worker.log"),errors.Result);Console.WriteLine(errors.Result);}
            }
            Console.WriteLine("PASS unknown entity field, native directory read/write/ref and inactive-key checks; conflicting NPC type/generation and projectile key/type refused; same-worker recovery.");
        }
        private static void SeatedTown(Assembly host,System.Diagnostics.Process child,string output)
        {
            NativeCombatWorkerChecks.Scene(false);NPC.ClearAll();Projectile.ClearAll();
            Require(Main.player[0].talkNPC==-1,"Town fixture is not talking to the player.");
            // The real parent has this Main-initialized collection. It is not
            // part of the wire, so a missing private collection remains visible.
            Main.sittingManager=new Terraria.DataStructures.AnchoredEntitiesCollection();
            bool dedicated=Main.dedServ;
            try{Main.dedServ=true;System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Terraria.GameContent.TownNPCProfiles).TypeHandle);}finally{Main.dedServ=dedicated;}
            Require(WorldGen.PlaceTile(44,64,15,mute:true,forced:true,plr:0,style:0),"Original chair placement.");
            int slot=NPC.NewNPC(new Terraria.DataStructures.EntitySource_DebugCommand(),44*16+8,65*16,Terraria.ID.NPCID.Guide,Target:0);
            Require(slot>=0 && slot<Main.maxNPCs,"Original Guide birth.");var guide=Main.npc[slot];
            typeof(NPC).GetMethod("AI_007_TryForcingSitting",Flags).Invoke(guide,new object[]{44,65});
            Require(guide.ai[0]==5 && guide.ai[1]>=900 && guide.velocity==Vector2.Zero,"Original seated Guide state.");
            var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("Capture",Flags);
            byte[] request=(byte[])capture.Invoke(null,new object[]{new[]{slot},slot,1000L,120});
            byte[] future=NativeCombatWorkerChecks.Exchange(child,request);
            NativeCombatWorkerChecks.Compare(future,slot,output,"native-seated-guide",playerUpdate:()=>Main.sittingManager.ClearNPCAnchors(),expectPlayerMotion:false);
            Require(guide.ai[0]==5,"Town oracle remains seated across the full window.");
        }
        internal static void DirectoryRoundTrip(Assembly host)
        {
            Type directory=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEntityDirectory",true);
            var index=(int[,])typeof(Projectile).GetField("keyToIndex",Flags).GetValue(null);var saved=(int[,])index.Clone();
            var objects=(Projectile[])Main.projectile.Clone();
            try
            {
                // A reused slot leaves an old index pointing at a different
                // key. In particular slot zero's noncanonical key is absent,
                // even though clearing the native index would resurrect it.
                var absent=new Terraria.DataStructures.ProjectileKey(1,5,1);
                var replacement=new Terraria.DataStructures.ProjectileKey(2,6,3);
                var older=new Terraria.DataStructures.ProjectileKey(3,7,4);
                var canonical=new Terraria.DataStructures.ProjectileKey(3,7,5);
                var sentinel=new Terraria.DataStructures.ProjectileKey(4,8,6);
                Main.projectile[0]=new Projectile{key=absent,whoAmI=0};
                Main.projectile[1]=new Projectile{key=replacement,whoAmI=1};
                Main.projectile[2]=new Projectile{key=older,whoAmI=2};
                Main.projectile[3]=new Projectile{key=canonical,whoAmI=3};
                Main.projectile[1000]=new Projectile{key=sentinel,whoAmI=1000};
                index[1,5]=1;index[2,6]=1;index[3,7]=3;index[4,8]=1000;
                Projectile found;
                Require(!Projectile.TryLookup(absent,out found) && !Projectile.TryLookup(older,out found),"Original stale keys are absent.");
                byte[] bytes;using(var output=new MemoryStream())using(var writer=new BinaryWriter(output))
                {directory.GetMethod("Write",Flags).Invoke(null,new object[]{writer});writer.Flush();bytes=output.ToArray();}
                directory.GetMethod("Reset",Flags).Invoke(null,null);
                using(var reader=new BinaryReader(new MemoryStream(bytes,false)))directory.GetMethod("Read",Flags).Invoke(null,new object[]{reader});
                Require(!Projectile.TryLookup(absent,out found),"Directory must not resurrect a noncanonical slot-zero key.");
                Require(!Projectile.TryLookup(older,out found),"Directory preserves generation absence.");
                Require(Projectile.TryLookup(canonical,out found) && ReferenceEquals(found,Main.projectile[3]),"Directory preserves canonical generation.");
                Require(Projectile.TryLookup(sentinel,out found) && ReferenceEquals(found,Main.projectile[1000]),"Directory preserves the native overflow sentinel key.");
                // Independently parse schema pages to locate index cells. A
                // different generation still names the same native cell.
                var offsets=new int[1001];
                object npcSchema=directory.GetField("Npcs",Flags).GetValue(null),projectileSchema=directory.GetField("Projectiles",Flags).GetValue(null);
                using(var reader=new BinaryReader(new MemoryStream(bytes,false)))
                {
                    Require(reader.ReadString()==(string)npcSchema.GetType().GetField("Schema",Flags).GetValue(npcSchema),"NPC directory schema is declared once.");
                    for(int i=0;i<201;i++)npcSchema.GetType().GetMethod("ReadFields",Flags).Invoke(npcSchema,new object[]{reader,new NPC()});
                    Require(reader.ReadString()==(string)projectileSchema.GetType().GetField("Schema",Flags).GetValue(projectileSchema),"Projectile directory schema is declared once.");
                    for(int i=0;i<1001;i++){projectileSchema.GetType().GetMethod("ReadFields",Flags).Invoke(projectileSchema,new object[]{reader,new Projectile()});offsets[i]=(int)reader.BaseStream.Position;reader.ReadInt32();}
                    Require(reader.BaseStream.Position==bytes.Length,"Exact directory record shape including sentinel slots.");
                    Require(bytes.Length<40000,"All directory identities remain bounded without 1200 duplicate schema headers.");
                }
                byte[] conflict=(byte[])bytes.Clone();Buffer.BlockCopy(BitConverter.GetBytes(2),0,conflict,offsets[3],4);
                RefuseDirectory(directory,conflict,"Conflicting projectile directory cell");
                foreach(int invalid in new[]{-1,1001})
                {byte[] bad=(byte[])bytes.Clone();Buffer.BlockCopy(BitConverter.GetBytes(invalid),0,bad,offsets[0],4);RefuseDirectory(directory,bad,"Projectile directory lookup slot");}
            }
            finally
            {directory.GetMethod("Reset",Flags).Invoke(null,null);Array.Copy(objects,Main.projectile,objects.Length);Array.Copy(saved,index,saved.Length);}
            Console.WriteLine("PASS native sparse-key directory roundtrip: stale slot zero, shared cell generations and sentinel.");
        }
        private static void RefuseDirectory(Type directory,byte[] bytes,string reason)
        {
            directory.GetMethod("Reset",Flags).Invoke(null,null);bool rejected=false;
            try{using(var reader=new BinaryReader(new MemoryStream(bytes,false)))directory.GetMethod("Read",Flags).Invoke(null,new object[]{reader});}
            catch(TargetInvocationException e){rejected=e.InnerException is InvalidDataException && e.InnerException.Message.Contains(reason);}
            Require(rejected,"Malformed directory rejected: "+reason);
        }
        private static byte[] SpoofPage(byte[] request,object schema,object actor,Action change,Action restore)
        {
            byte[] before=Page(schema,actor),after;
            try{change();after=Page(schema,actor);}finally{restore();}
            Require(before.Length==after.Length,"Conflict changes only a fixed-width value page.");
            int location=-1;
            for(int start=0;start<=request.Length-before.Length;start++)
            {
                if(request[start]!=before[0])continue;int i=1;while(i<before.Length && request[start+i]==before[i])i++;
                if(i==before.Length){Require(location==-1,"One independently serialized full actor page in frame.");location=start;}
            }
            Require(location>=0,"Original full actor page located without guessing protocol offsets.");
            byte[] corrupted=(byte[])request.Clone();Buffer.BlockCopy(after,0,corrupted,location,after.Length);return corrupted;
        }
        private static byte[] Page(object schema,object actor)
        {using(var buffer=new MemoryStream())using(var writer=new BinaryWriter(buffer)){schema.GetType().GetMethod("Write",Flags).Invoke(schema,new[]{(object)writer,actor});writer.Flush();return buffer.ToArray();}}
        private static void RefuseConflict(System.Diagnostics.Process child,byte[] frame,string expected)
        {
            using(var reader=new BinaryReader(new MemoryStream(NativeCombatWorkerChecks.Exchange(child,frame))))
            {Require(reader.ReadInt32()==-NativeCombatWorkerChecks.ExpectedProtocol,"Conflicting page cannot succeed.");reader.ReadString();string reason=reader.ReadString();Require(reason.Contains(expected),"Specific directory consistency rejection: "+reason);}
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
