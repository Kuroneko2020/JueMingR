using System;
using System.Collections.Generic;
using System.Reflection;

namespace JueMingR.ArchitectureTests
{
    internal static class RecoveryChecks
    {
        internal static void Check(List<string> failures)
        {
            Type rules = typeof(JueMingR.Features.CoinDeposit.CoinRules).Assembly.GetType("JueMingR.Features.Recovery.RecoveryRules");
            if (rules == null) { failures.Add("Recovery selection contract is not implemented."); return; }
            var score = (Func<int,int,int,int,long>)Delegate.CreateDelegate(typeof(Func<int,int,int,int,long>), rules.GetMethod("LifeScore"));
            var better = (Func<int,int,int,int,int,bool>)Delegate.CreateDelegate(typeof(Func<int,int,int,int,int,bool>), rules.GetMethod("BetterLife"));
            Action<int,int,int,int[],int> check = (mode,d,m,amounts,expected) => {
                int found=0; foreach(int h in amounts) if(score(mode,d,m,h)>=0 && (found==0 || better(mode,d,m,h,found)))found=h;
                if(found!=expected)failures.Add("Recovery mode="+mode+" D="+d+" M="+m+" expected="+expected+" actual="+found);
            };
            foreach(int d in new[]{1,40,50})check(2,d,500,new[]{100},0);
            foreach(int d in new[]{51,100,300})check(2,d,500,new[]{100},100);
            check(2,7,500,new[]{15},0);check(2,8,500,new[]{15},15);
            check(2,100,500,new[]{200},0);check(2,101,500,new[]{200},200);
            check(2,60,500,new[]{50,100},50);
            foreach(int d in new[]{70,75,80})check(2,d,500,new[]{50,100},100);
            check(2,101,500,new[]{100,200},100);check(2,8,500,new[]{15,200},15);
            check(1,70,500,new[]{50,100},50);check(1,75,500,new[]{50,100},100);check(1,1,500,new[]{200},200);
            check(1,0,500,new[]{100},0);check(2,0,500,new[]{100},0);check(2,51,100,new[]{300},300);
            check(0,100,500,new[]{100},0);check(2,Int32.MaxValue,Int32.MaxValue,new[]{Int32.MaxValue},Int32.MaxValue);
            // Equal providers retain the earlier source, so host's main/void,
            // slot/type traversal determines a stable tie without sorting.
            if(better(2,70,500,100,100))failures.Add("Equal provider must not replace the stable first source.");
            Console.WriteLine("Recovery: strict one-way threshold and distinct selection table checked.");
            RecoverySettingsChecks.Check(failures);
        }
    }
}
