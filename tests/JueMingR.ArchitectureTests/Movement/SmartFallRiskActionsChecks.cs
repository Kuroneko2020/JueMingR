using System;
using System.Collections.Generic;
using JueMingR.Features.Movement;

namespace JueMingR.ArchitectureTests
{
    internal static class SmartFallRiskActionsChecks
    {
        internal static void Check(List<string> failures)
        {
            try
            {
                Require(!SmartFallDecision.Risk(1600, 0, 1, 0, 0, 10, false, true, false, 1), "Native immunity precedes candidates.");
                Require(!SmartFallDecision.Risk(1600, 0, 1, 0, 0, 10, false, false, true, 1), "A natural fall baseline reset is already protection.");
                Require(SmartFallDecision.Risk(1600, 0, 1, 0, 0, 10, false, false, false, 1), "Actual ordinary long descent is dangerous.");
                Require(SmartFallDecision.Risk(1600, 200, -1, 0, 0, 10, false, false, false, 1), "Reverse gravity uses signed native fall distance.");
                Require(SmartFallDecision.Risk(64, 0, 1, 0, 0, 10, true, true, false, 0), "Stoned damage does not borrow accessory immunity or mount multiplier.");
                var c = new[] {
                    new SmartFallCandidate { Family=SmartFallFamily.DoubleJump,Ready=true,Delay=0,Interference=10 },
                    new SmartFallCandidate { Family=SmartFallFamily.Grapple,Ready=true,Delay=5,Interference=40 }
                };
                Require(SmartFallDecision.Choose(c, 2, 5.5f)==0 && !SmartFallDecision.Due(c[0],5.5f), "Expiring slow backup cannot force a ready primary early.");
                c[0].Ready=false;
                Require(SmartFallDecision.Choose(c,2,5.5f)==1 && SmartFallDecision.Due(c[1],5.5f), "An unready higher ranked plan does not block a timely backup.");
                Require(SmartFallDecision.Choose(c,2,5)==-1, "Attachment/consumer on the collision boundary is too late.");
                c[0].Ready=true;
                Require(SmartFallDecision.Choose(c,2,.5f)==0 && SmartFallDecision.Due(c[0],.5f), "Current-stage primary starts in its own final window.");
                Require(SmartFallDecision.Surface(10,20,1,false,161,181,1)==321 && SmartFallDecision.Surface(10,20,2,false,161,173,1)==323, "Both floor slopes use the full collision-box leading edge.");
                Require(SmartFallDecision.Surface(10,20,3,false,161,181,-1)==335 && SmartFallDecision.Surface(10,20,4,false,161,173,-1)==333, "Both ceiling slopes retain reverse-gravity width.");
                Require(SmartFallDecision.Surface(10,20,0,true,161,181,1)==328 && SmartFallDecision.Surface(10,20,0,true,161,181,-1)==336, "Half brick has an eight-pixel floor and full-height underside.");
            }
            catch(Exception e) { failures.Add("Smart fall risk/actions: "+e); }
        }
        private static void Require(bool ok,string message) { if(!ok)throw new InvalidOperationException(message); }
    }
}
