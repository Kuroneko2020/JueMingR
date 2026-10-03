using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;

namespace NativeWorldTextProbe
{
    // Executes native methods in the authenticated private image. No mock
    // query answer or replacement worker participates in these permissions.
    internal static class NativeCombatEligibilityChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Type directory,eligibility;
        internal static void Run(Assembly host)
        {
            directory=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEntityDirectory",true);
            eligibility=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeNpcEligibility",true);
            try
            {
                foreach(bool ignore in new[]{false,true})foreach(bool invulnerable in new[]{false,true})foreach(bool chaseable in new[]{false,true})
                {
                    NPC n=Scene();n.dontTakeDamage=invulnerable;n.chaseable=chaseable;n.immortal=invulnerable;
                    Observe();Require(!n.CanBeChasedBy(null,ignore),"Friendly negative query matches all native qualification arguments.");
                    Call(directory,"KnowNpc",3);Require(!n.CanBeChasedBy(null,ignore),"The fully captured original method agrees with the leaf negative.");
                }
                foreach(int state in new[]{0,1,3,4,8,16,17})
                {var n=Scene();n.ai[0]=state;Observe();Require(!n.CanBeChasedBy(),"Safe town state retains negative query.");}
                foreach(int state in new[]{10,12,13,14,15})
                {var n=Scene();n.ai[0]=state;Observe();Refused(()=>n.CanBeChasedBy(),1,3,"An observed attack phase requires its actual page.");}
                foreach(int type in new[]{377,446,22,54})
                {var n=Scene();n.SetDefaults(type);n.whoAmI=3;n.active=true;n.friendly=true;Observe();Refused(()=>n.CanBeChasedBy(),1,3,"Temporary friendly and attacking towns do not inherit stable query permission.");}
                foreach(string kind in new[]{"friendly","hostile","native-friendly-fire"})
                {
                    var n=Scene();var p=Main.projectile[0];p.SetDefaults(1);p.whoAmI=0;p.active=true;p.owner=0;p.friendly=true;p.damage=10;
                    if(kind=="hostile")p.hostile=true;if(kind=="native-friendly-fire")p.type=318;
                    Observe();Call(directory,"KnowProjectile",0);
                    Action damage=()=>typeof(Projectile).GetMethod("Damage_PVE",Flags).Invoke(p,new object[]{new Rectangle(500,500,20,20),1f});
                    if(kind=="friendly"){damage();Require(Missing()==0,"Pure friendly PVE qualification does not demand town AI or temporary position writes.");}
                    else Refused(damage,1,3,"Real damage qualification exceptions retain missing-page permission.");
                }
                {var n=Scene();Observe();Refused(()=>{n.Size=new Vector2(10,20);},1,3,"Query permission never grants a native write.");}
                {var n=Scene();Observe();Refused(()=>{var position=n.Center;},1,3,"Query permission never grants a native managed address.");}
                {Scene();Observe();Refused(()=>typeof(NPC).GetMethod("GetAvailableNPCSlot",Flags).Invoke(null,new object[]{1,3}),1,3,"Actual allocation scans must upgrade an observed population premise.");}
                {Scene();Observe();Main.npc[3]=new NPC{whoAmI=3,active=true,type=1,lifeMax=100,chaseable=true};Require(Main.npc[3].CanBeChasedBy(),"A newborn object cannot inherit another object's negative answer.");}
                Console.WriteLine("PASS private native eligibility / arguments / safe social and attack phases / temporary friendly / real damage exceptions / write address allocation / newborn reference");
            }
            finally{Call(directory,"Reset");}
        }
        private static NPC Scene()
        {
            Call(directory,"Reset");for(int i=0;i<Main.npc.Length;i++)Main.npc[i]=new NPC{whoAmI=i};for(int i=0;i<Main.projectile.Length;i++)Main.projectile[i]=new Projectile{whoAmI=i};
            var n=Main.npc[3];n.SetDefaults(678);n.whoAmI=3;n.active=true;n.position=new Vector2(500,500);return n;
        }
        private static void Observe()
        {
            byte[] values;using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes)){Call(directory,"Write",writer);Call(eligibility,"Write",writer);writer.Flush();values=bytes.ToArray();}
            Call(directory,"Reset");using(var reader=new BinaryReader(new MemoryStream(values))){Call(directory,"Read",reader);Call(eligibility,"Read",reader);}Call(directory,"Begin");
        }
        private static void Refused(Action action,int kind,int slot,string reason)
        {
            bool refused=false;try{action();}catch(Exception e){while(e is TargetInvocationException)e=e.InnerException;refused=e is InvalidDataException;}
            Require(refused && Missing()==kind && (int)directory.GetProperty("MissingSlot",Flags).GetValue(null)==slot,reason);
        }
        private static int Missing()=>(int)directory.GetProperty("MissingKind",Flags).GetValue(null);
        private static object Call(Type type,string name,params object[] args)=>type.GetMethod(name,Flags).Invoke(null,args);
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
