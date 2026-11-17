using System;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionFeedbackChecks
    {
        // A bounded component check of message ownership, not a network-ready
        // handshake claim. The caller supplies an actually joined native actor.
        internal static void Run(object host, Guid joined)
        {
            var body=(Player)Call(host,"GetBody",joined);
            var actor=Call(host,"Owned",body);
            Require(actor!=null && (bool)Get(actor,"Ready"),"feedback fixture uses a real ready activation");
            Require((string)Get(host,"Feedback")!="正在加入人物。","synchronous ready Join leaves no joining progress");
            string original=(string)Get(host,"Feedback");
            try
            {
                Set(host,"joiningFeedback",actor);Set(host,"Feedback","正在加入人物。");
                // Only reference ownership is exercised on this other identity;
                // it is not attached, connected, updated or made authoritative.
                var unrelated=Activator.CreateInstance(actor.GetType(),true);
                Call(host,"RetireJoinFeedback",unrelated);
                Require((string)Get(host,"Feedback")=="正在加入人物。" && ReferenceEquals(Get(host,"joiningFeedback"),actor),"another activation cannot retire this joining progress");
                Call(host,"RetireJoinFeedback",actor);
                Require((string)Get(host,"Feedback")=="" && GetOptional(host,"joiningFeedback")==null,"exact completed activation retires its own progress");
                Set(host,"joiningFeedback",actor);Set(host,"Feedback","保存失败哨兵");
                Call(host,"RetireJoinFeedback",actor);
                Require((string)Get(host,"Feedback")=="保存失败哨兵","completion does not erase a later failure or operation");
            }
            finally {Set(host,"joiningFeedback",null);Set(host,"Feedback",original);}
            Console.WriteLine("PASS companion joining feedback: ready sync, exact activation, unrelated identity and newer-error preservation.");
        }
    }
}
