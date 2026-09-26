using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JueMingR.Features.Recovery;
using JueMingR.Platform.Items;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class RecoverySettingsChecks
    {
        internal static void Check(List<string> failures)
        {
            RecoveryModeChecks.Check(failures);
            var codec=new RecoveryCodec(1);var value=new RecoveryOptions(buffs:true,followRemove:true,allowedBuffs:Enumerable.Range(1,500));
            var read=codec.Decode(codec.Encode(value));if(!read.Buffs || !read.FollowRemove || read.AllowedBuffs.Count!=500 || read.FollowAdd)failures.Add("Recovery settings lost long whitelist or independent follow values.");
            foreach(string json in new[]{"{\"format\":\"JueMingR.RecoveryBuffs\",\"version\":9,\"buffs\":true,\"followAdd\":false,\"followRemove\":false,\"allowedBuffs\":[]}",
                "{\"format\":\"JueMingR.RecoveryBuffs\",\"version\":1,\"buffs\":true,\"followAdd\":false,\"followRemove\":false,\"allowedBuffs\":[2,2]}",
                "{\"format\":\"JueMingR.RecoveryBuffs\",\"version\":1,\"buffs\":true,\"followAdd\":false,\"followRemove\":false,\"allowedBuffs\":[],\"unknown\":true}"})
            {try{codec.Decode(Encoding.UTF8.GetBytes(json));failures.Add("Recovery invalid/future settings accepted.");}catch(PreferenceFormatException){}}
            var owner=new ItemOperationOwnership();owner.SetSession(3);
            if(owner.TryBeginRecovery(3,new ulong[5],1))failures.Add("Empty recovery write set must not acquire ownership.");
            owner.SetSession(4);var slots=new ulong[5];slots[4]=4;
            if(!owner.TryBeginRecovery(4,slots,2) || !owner.IsProtected(4,2) || owner.IsProtected(0,2))failures.Add("Recovery void ownership confused account/slot.");
            owner.EndRecovery(4,slots,2,true);if(!owner.IsProtected(4,2))failures.Add("Unknown recovery lost ownership.");
            slots=new ulong[5];slots[0]=8;if(!owner.TryBeginRecovery(4,slots,3))failures.Add("Independent main source should proceed despite void unknown.");
            owner.EndRecovery(4,slots,3,false);if(owner.IsProtected(0,3) || !owner.IsProtected(4,2))failures.Add("Successful recovery incorrectly cleared another unknown source.");
        }
    }
}
