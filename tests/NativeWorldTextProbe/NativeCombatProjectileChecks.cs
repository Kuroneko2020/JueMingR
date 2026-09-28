using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Items;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Faults run inside the patched original AI, after the shared production
    // prefix acquired its scope. No runtime/frame cleanup may mask the result.
    internal static class NativeCombatProjectileChecks
    {
        private static Projectile watched;
        private static object combatUse,toolUse;
        private static readonly InvalidOperationException fault=new InvalidOperationException("isolated shared projectile failure");
        private static bool entered;
        private static int toolEnds;
        private static Exception toolError;
        private static Action<Projectile> inAi;
        private static bool failApply,failPostfix;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var tools=Get(context,"Tools");var input=Get(context,"Input");
            combatUse=Get(combat,"Use");toolUse=Get(tools,"Use");
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,tools,input,5462,0,0);p.inventory[1].SetDefaults(6153);
            NativeCombatCadenceChecks.Save(combat,new CombatOptions(4));
            var audit=new Harmony("JueMingR.Tests.SharedProjectileFailure");
            try
            {
                for(int i=0;i<30 && GetOptional(combatUse,"primary")==null;i++)NativeCombatCadenceChecks.Step(context,false,true,0);
                watched=(Projectile)Get(combatUse,"primary");
                Require(watched!=null && watched.active && watched.owner==p.whoAmI && !(bool)Get(toolUse,"Active") && GetOptional(toolUse,"Intent")==null && (long)Get(toolUse,"Operation")==0,"real combat projectile with legally idle tool owner");
                p.controlUseItem=false;p.controlUseTile=true;
                var ownership=(ItemOperationOwnership)Get(Get(tools,"Items"),"Ownership");
                Require(ownership.IsUseSlot(0),"combat source has real shared use ownership");
                int stack=p.inventory.Sum(item=>item.stack),projectiles=Main.projectile.Count(q=>q.active);bool channel=p.channel,release=p.releaseUseItem,mouse=Main.mouseLeft;
                ulong unknown=(ulong)Get(tools,"unknown");entered=false;toolEnds=0;toolError=null;
                audit.Patch(AccessTools.Method(typeof(Projectile),"AI"),transpiler:new HarmonyMethod(typeof(NativeCombatProjectileChecks),nameof(Inject)));
                audit.Patch(AccessTools.Method(toolUse.GetType(),"EndProjectile"),prefix:new HarmonyMethod(typeof(NativeCombatProjectileChecks),nameof(ToolEndEntered)),finalizer:new HarmonyMethod(typeof(NativeCombatProjectileChecks),nameof(ToolEndFinal)));
                Exception observed=null;try{watched.AI();}catch(Exception e){observed=e;}
                Console.WriteLine("Shared projectile fault: entered="+entered+" original="+ReferenceEquals(observed,fault)+" toolEnds="+toolEnds+" toolError="+toolError+" scopes="+((IList)Get(combatUse,"scopes")).Count+" use="+ownership.HasUse);
                Require(entered && ReferenceEquals(observed,fault),"shared AI finalizer preserves the exact original exception");
                Require(toolEnds==0 && toolError==null,"a combat receipt must never dispatch to the tool finalizer");
                Require(((IList)Get(combatUse,"scopes")).Count==0 && !(bool)Get(combatUse,"Active") && !ownership.HasUse && !ownership.IsProtected(0),"combat scope and real use ownership end before any frame fallback");
                Require(!p.controlUseItem && p.controlUseTile && p.channel==channel && p.releaseUseItem==release && Main.mouseLeft==mouse,"temporary controls return without rolling back native channel/release");
                Require(!(bool)Get(toolUse,"Active") && GetOptional(toolUse,"Intent")==null && (long)Get(toolUse,"Operation")==0 && (ulong)Get(tools,"unknown")==unknown,"combat fault cannot fabricate a tool operation or unknown source");
                Require(p.inventory.Sum(item=>item.stack)==stack && Main.projectile.Count(q=>q.active)==projectiles,"fault does not add an attack or consume an item");
                Console.WriteLine("PASS G11A real shared Projectile.AI finalizer preserves original fault and immediately cleans only combat ownership.");
                CombatCases(context,combat,tools,input,audit);
                ToolCases(context,combat,tools,input);
            }
            finally
            {
                foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);
                watched=null;combatUse=toolUse=null;inAi=null;failApply=failPostfix=false;NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            }
        }
        private static IEnumerable<CodeInstruction> Inject(IEnumerable<CodeInstruction> instructions)
        {
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(NativeCombatProjectileChecks),nameof(InsideAi)));
            foreach(var instruction in instructions)yield return instruction;
        }
        private static void InsideAi(Projectile shot)
        {
            if(!ReferenceEquals(shot,watched))return;
            if(inAi!=null){inAi(shot);return;}
            Require(((IList)Get(combatUse,"scopes")).Count==1 && !(bool)Get(toolUse,"Active"),"fault injection is after actual combat scope acquisition, without an impossible second owner");
            entered=true;throw fault;
        }
        private static void ToolEndEntered(){toolEnds++;}
        private static Exception ToolEndFinal(Exception __exception){toolError=__exception;return __exception;}
        private static Player PrepareCombat(object context,object combat,object tools,object input)
        {
            watched=null;NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,tools,input,5462,0,0);p.inventory[1].SetDefaults(6153);
            NativeCombatCadenceChecks.Step(context,false,false,0);NativeCombatCadenceChecks.Save(combat,new CombatOptions(4));
            for(int i=0;i<30 && GetOptional(combatUse,"primary")==null;i++)NativeCombatCadenceChecks.Step(context,false,true,0);
            watched=(Projectile)Get(combatUse,"primary");p.controlUseItem=false;p.controlUseTile=true;
            return p;
        }
        private static Exception RunAi(Projectile shot){try{shot.AI();return null;}catch(Exception e){return e;}}
        private static int Scopes {get{return ((IList)Get(combatUse,"scopes")).Count;}}
        private static void CombatCases(object context,object combat,object tools,object input,Harmony audit)
        {
            var p=PrepareCombat(context,combat,tools,input);var ownership=(ItemOperationOwnership)Get(Get(tools,"Items"),"Ownership");
            inAi=q=>{Require(Scopes==1 && p.controlUseItem && !p.controlUseTile,"normal AI consumes real combat input");};
            toolEnds=0;Require(RunAi(watched)==null && Scopes==0 && ownership.IsUseSlot(0) && !p.controlUseItem && p.controlUseTile && toolEnds==0,"normal shared combat AI restores controls and retains its action");

            // Other projectiles have no receipt even while combat is active.
            foreach(int owner in new[]{p.whoAmI,p.whoAmI+1})
            {
                var primary=watched;int free=Array.FindIndex(Main.projectile,q=>!q.active);var old=Main.projectile[free];
                var ordinary=new Projectile();ordinary.SetDefaults(1);ordinary.active=true;ordinary.owner=owner;ordinary.whoAmI=free;Main.projectile[free]=ordinary;watched=ordinary;
                try
                {
                    long token=(long)Get(combatUse,"Operation");inAi=q=>{Require(Scopes==0,"untracked/local or nonlocal AI obtains no combat scope");throw fault;};
                    Require(ReferenceEquals(RunAi(ordinary),fault) && (long)Get(combatUse,"Operation")==token && ownership.IsUseSlot(0) && Scopes==0 && !p.controlUseItem && p.controlUseTile && toolEnds==0,"unowned projectile failure cannot clean an active owner");
                }
                finally{Main.projectile[free]=old;watched=primary;}
            }

            // A later postfix can fail after our normal completion. It cannot
            // turn the already consumed receipt into a second cleanup request.
            audit.Patch(AccessTools.Method(typeof(Projectile),"AI"),postfix:new HarmonyMethod(typeof(NativeCombatProjectileChecks),nameof(LatePostfix)){priority=Priority.Last,after=new[]{"JueMingR.Tools"}});
            inAi=q=>{Require(Scopes==1,"normal-before-late-error scope");};failPostfix=true;
            try{Require(ReferenceEquals(RunAi(watched),fault) && Scopes==0 && ownership.IsUseSlot(0) && (bool)Get(combatUse,"Active") && !p.controlUseItem && p.controlUseTile,"Postfix/Finalizer consumes combat receipt once");}
            finally{failPostfix=false;}

            p=PrepareCombat(context,combat,tools,input);int depth=0;
            inAi=q=>
            {
                Require(Scopes==depth+1,"nested shared AI owns a distinct input scope");
                if(depth!=0)return;
                p.controlUseItem=false;p.controlUseTile=true;depth++;q.AI();depth--;
                Require(Scopes==1 && !p.controlUseItem && p.controlUseTile,"inner AI restores the later input before outer failure");throw fault;
            };
            Require(ReferenceEquals(RunAi(watched),fault) && Scopes==0 && !ownership.HasUse && !p.controlUseItem && p.controlUseTile,"outer failure cleans nested scopes without erasing later input");

            p=PrepareCombat(context,combat,tools,input);int bodyEntries=0;inAi=q=>{bodyEntries++;};
            audit.Patch(AccessTools.Method(combatUse.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.CombatInputScope"),"Apply"),postfix:new HarmonyMethod(typeof(NativeCombatProjectileChecks),nameof(AfterApply)));
            failApply=true;
            try{Require(ReferenceEquals(RunAi(watched),fault) && bodyEntries==0 && Scopes==0 && !ownership.HasUse && !(bool)Get(combatUse,"Active") && !p.controlUseItem && p.controlUseTile,"prefix half-failure publishes cleanup before borrowed input returns");}
            finally{failApply=false;}

            // Supplemental delayed callback checks use receipts from the actual
            // shared prefix. The main fault test above always runs real AI.
            p=PrepareCombat(context,combat,tools,input);var oldShot=watched;object receipt=BeginReceipt(tools,oldShot);
            Call(combatUse,"Stop");p=PrepareCombat(context,combat,tools,input);long successor=(long)Get(combatUse,"Operation");
            var callback=Hook(tools,"ProjectileFinal");var arguments=new[]{(object)oldShot,receipt,fault};
            Require(ReferenceEquals(callback.Invoke(null,arguments),fault),"late finalizer retains fault");callback.Invoke(null,arguments);
            Require((long)Get(combatUse,"Operation")==successor && ownership.IsUseSlot(0) && Scopes==0 && !p.controlUseItem && p.controlUseTile,"old and duplicate receipts cannot end a new combat operation");
            watched=null;inAi=null;NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            Console.WriteLine("PASS shared AI normal/unowned/nested/half-prefix failure, late Postfix and retired/duplicate receipt boundaries.");
        }
        private static MethodInfo Hook(object tools,string name){return AccessTools.Method(tools.GetType().Assembly.GetType("JueMingR.TerrariaHost.Tools.ToolHooks"),name);}
        private static object BeginReceipt(object tools,Projectile shot){var args=new object[]{shot,null};Hook(tools,"ProjectileBefore").Invoke(null,args);return args[1];}
        private static void AfterApply(){if(failApply)throw fault;}
        private static void LatePostfix(Projectile __instance){if(failPostfix && ReferenceEquals(__instance,watched))throw fault;}
        private static Player PrepareDrill(object context,object tools,object input)
        {
            watched=null;var p=NativeToolExecutionChecks.Reset(context,tools,input,Terraria.ID.ItemID.AdamantiteDrill,0,0);
            for(int x=41;x<=45;x++)NativeToolsChecks.Tile(x,40,6);
            NativeToolsChecks.SetMode(tools,2,1);Require((bool)Call(Get(tools,"Mining"),"Select",p,41,40,6,false),"real drill mining region selected");
            for(int i=0;i<80;i++)
            {
                NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);NativeQuickItemChecks.BeginWorldStep();p.Update(0);
                var drill=GetOptional(toolUse,"drill") as Projectile;
                if(drill!=null && (bool)Call(toolUse,"OwnsProjectile",drill)){watched=drill;return p;}
                Call(context,"UpdateRuntime");
            }
            throw new InvalidOperationException("real automatic drill projectile was not acquired");
        }
        private static void ToolCases(object context,object combat,object tools,object input)
        {
            var world=Main.ActiveWorldFileData;
            try
            {
                var p=PrepareDrill(context,tools,input);var ownership=(ItemOperationOwnership)Get(Get(tools,"Items"),"Ownership");
                long token=(long)Get(toolUse,"Operation");int notices=0;bool noticeUnknown=false;
                var intent=Get(toolUse,"Intent");var completed=GetOptional(intent,"Completed") as Action<bool,bool>;
                Set(intent,"Completed",(Action<bool,bool>)((started,unknown)=>{notices++;noticeUnknown=unknown;completed?.Invoke(started,unknown);}));
                Main.mouseX=211;Main.mouseY=219;Player.tileTargetX=34;Player.tileTargetY=35;
                inAi=q=>{Require((bool)Get(toolUse,"borrowed") && Scopes==0 && Main.mouseX!=211,"actual drill AI consumes the tool aim scope");};
                toolEnds=0;Require(RunAi(watched)==null && toolEnds==1 && Main.mouseX==211 && Main.mouseY==219 && Player.tileTargetX==34 && Player.tileTargetY==35 && ownership.IsUseSlot(0) && notices==0,"normal drill AI restores exact aim without completing ongoing mining");
                failPostfix=true;try{Require(ReferenceEquals(RunAi(watched),fault) && toolEnds==2 && notices==0 && !(bool)Get(toolUse,"cancelled") && (ulong)Get(tools,"unknown")==0,"tool Postfix/Finalizer must not dispatch twice");}finally{failPostfix=false;}

                object receipt=BeginReceipt(tools,watched);int ownX=Main.mouseX;
                foreach(long invalid in new[]{0L,-1L,token+1})Call(toolUse,"EndProjectile",invalid,fault);
                Require((bool)Get(toolUse,"borrowed") && Main.mouseX==ownX && !(bool)Get(toolUse,"cancelled") && notices==0 && (ulong)Get(tools,"unknown")==0,"zero/invalid/mismatched tokens cannot restore or cancel a real tool operation");
                var afterArgs=new[]{(object)watched,receipt};Hook(tools,"ProjectileAfter").Invoke(null,afterArgs);
                Hook(tools,"ProjectileFinal").Invoke(null,new[]{(object)watched,afterArgs[1],fault});
                Require(Main.mouseX==211 && notices==0 && !(bool)Get(toolUse,"cancelled"),"consumed tool receipt is idempotent");

                Require(!p.selectedItemState.CanChangeSelectedItemImmediately,"native drill animation still requires legal selection return");
                int stacks=p.inventory.Sum(item=>item.stack),shots=Main.projectile.Count(q=>q.active);
                inAi=q=>{Require((bool)Get(toolUse,"borrowed") && Scopes==0,"tool fault belongs only to its actual aim borrower");throw fault;};
                Require(ReferenceEquals(RunAi(watched),fault) && Main.mouseX==211 && Main.mouseY==219 && Player.tileTargetX==34 && Player.tileTargetY==35 && !(bool)Get(toolUse,"borrowed"),"tool exception preserves original fault and returns its temporary aim");
                Require((bool)Get(toolUse,"Active") && (bool)Get(toolUse,"cancelled") && ownership.IsUseSlot(0) && ownership.IsProtected(0) && (ulong)Get(tools,"unknown")==1 && notices==1 && noticeUnknown,"tool exception retains exact unknown source and pending legal return responsibility");
                Require(p.inventory.Sum(item=>item.stack)==stacks && Main.projectile.Count(q=>q.active)==shots,"tool failure creates no extra use or consumption");
                Call(toolUse,"EndProjectile",token,fault);Require(notices==1,"duplicate error cannot notify twice");
                watched=null;inAi=null;for(int i=0;i<100;i++)NativeToolsChecks.Frame(context,input);
                Require(!(bool)Get(toolUse,"Active") && !ownership.HasUse && ownership.IsProtected(0) && p.selectedItem==0 && notices==1,"native legal return retires use but retains real unknown-source protection");

                Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(System.IO.Path.Combine(Terraria.Program.SavePath,"projectile-new-world.wld"),false){UniqueId=Guid.NewGuid()};Call(context,"UpdateRuntime");
                p=PrepareDrill(context,tools,input);long next=(long)Get(toolUse,"Operation");Require(next!=token,"real successor has new tool token");
                Main.mouseX=211;var successorReceipt=BeginReceipt(tools,watched);ownX=Main.mouseX;
                Call(toolUse,"EndProjectile",token,fault);
                Require((long)Get(toolUse,"Operation")==next && (bool)Get(toolUse,"borrowed") && Main.mouseX==ownX && ownership.IsUseSlot(0) && !(bool)Get(toolUse,"cancelled") && (ulong)Get(tools,"unknown")==0,"retired tool callback cannot restore or cancel successor");
                Hook(tools,"ProjectileAfter").Invoke(null,new[]{(object)watched,successorReceipt});
                Console.WriteLine("PASS real drill AI normal/error, exact source protection, deferred legal return, invalid/duplicate/late callbacks and unchanged consumption.");
            }
            finally{watched=null;inAi=null;NativeToolsChecks.SetMode(tools,2,0);Main.ActiveWorldFileData=world;Call(context,"UpdateRuntime");}
        }
    }
}
