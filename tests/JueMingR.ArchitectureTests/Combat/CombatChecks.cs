using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Platform.Items;

namespace JueMingR.ArchitectureTests
{
    internal static class CombatChecks
    {
        internal static void Check(List<string> failures)
        {
            try
            {
                CombatDomainChecks.Run();
                CombatObservationChecks.Run();
                NpcTrajectoryWindowChecks.Run();
                RollingNpcPredictionChecks.Run();
                BasicNpcMotionChecks.Run();
                var assembly=typeof(JueMingR.Features.Tools.ToolOptions).Assembly;
                Type options=assembly.GetType("JueMingR.Features.Combat.CombatOptions");
                Require(options!=null,"Combat preferences are absent");
                object initial=Activator.CreateInstance(options,new object[]{0,12});
                Require((int)options.GetProperty("EnabledMask").GetValue(initial)==0,"all eight abilities default off");
                Require((int)options.GetProperty("SwitchInterval").GetValue(initial)==12,"default switch interval is twelve simulation ticks");
                foreach(int interval in new[]{0,12,30})
                {
                    object value=Activator.CreateInstance(options,new object[]{255,interval});
                    for(int i=0;i<8;i++)Require((bool)options.GetMethod("Enabled").Invoke(value,new object[]{i}),"each independent preference is retained");
                    object changed=options.GetMethod("Toggle").Invoke(value,new object[]{3});
                    Require(!(bool)options.GetMethod("Enabled").Invoke(changed,new object[]{3}) && (bool)options.GetMethod("Enabled").Invoke(value,new object[]{3}),"toggle is immutable and specific");
                }
                foreach(var pair in new[]{new[]{256,12},new[]{-1,12},new[]{0,-1},new[]{0,31}})
                {
                    bool rejected=false;
                    try{Activator.CreateInstance(options,new object[]{pair[0],pair[1]});}
                    catch(TargetInvocationException e){rejected=e.InnerException is ArgumentOutOfRangeException;}
                    Require(rejected,"invalid preference values cannot silently normalize");
                }
                // Cursor use is an input resource, never a fictitious inventory
                // source. Existing quick-item callers remain restricted to 0..49.
                var owner=new ItemOperationOwnership();owner.SetSession(1);
                MethodInfo cursor=typeof(ItemOperationOwnership).GetMethod("TryBeginCursorUse");
                Require(cursor!=null,"cursor input lease is absent");
                long a=owner.NewUseToken(),b=owner.NewUseToken();
                Require(!owner.TryBeginUse(1,58,a),"ordinary item lease still rejects cursor slot");
                Require((bool)cursor.Invoke(owner,new object[]{1L,a}),"cursor receives a distinct use lease");
                Require(!owner.TryBeginUse(1,1,b) && !owner.IsUseSlot(1) && !owner.IsUseSlot(58),"cursor excludes other use without inventing source protection");
                owner.EndUse(1,b);Require(!owner.TryBeginUse(1,1,b),"stale finalizer cannot release cursor owner");
                owner.EndUse(1,a);Require(owner.TryBeginUse(1,1,b),"matching cursor release admits next owner");
                Require(!(bool)cursor.Invoke(owner,new object[]{1L,a}),"inventory use excludes cursor owner");
                owner.SetSession(2);Require((bool)cursor.Invoke(owner,new object[]{2L,owner.NewUseToken()}),"session change retires input lease");
                Require(assembly.GetType("JueMingR.Features.Combat.CombatSettings")!=null,"combat reliable settings owner is absent");
            }
            catch(Exception e){failures.Add("Combat: "+e);}
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
