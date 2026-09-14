using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using JueMingR.Features.Guidance;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeGuidanceCostChecks
    {
        internal static void Run(object context, object host)
        {
            var old=Main.npc; var player=Main.LocalPlayer; Item equipment=player.armor[3];
            var update=(Action)Delegate.CreateDelegate(typeof(Action),context,context.GetType().GetMethod("UpdateRuntime",NativeGuidanceChecks.Flags));
            object world=Get(host,"World"),npcs=Get(context,"nativeNpcs"),reader=Get(Get(host,"source"),"Equipment");
            var prepare=(Action)Delegate.CreateDelegate(typeof(Action),world,world.GetType().GetMethod("Prepare",NativeGuidanceChecks.Flags));
            var warning=(EquipmentWarning)Get(host,"Equipment");
            var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",BindingFlags.Public|BindingFlags.Static);
            var allocated=method==null?null:(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);
            try
            {
                Main.npc=new NPC[Main.maxNPCs]; for(int i=0;i<Main.maxNPCs;i++)Main.npc[i]=NativeGuidanceChecks.Npc(i,46,0,600+i);
                Main.npc[1]=NativeGuidanceChecks.Npc(1,45,4,1200); Main.npc[2]=NativeGuidanceChecks.Npc(2,368,0,2400);
                Main.npc[3]=NativeGuidanceChecks.Npc(3,4,0,200); player.accCritterGuide=true;player.hideInfo[11]=false;
                player.armor[3]=NativeGuidanceEquipmentChecks.Accessory(Terraria.ID.ItemID.Toolbelt);
                foreach(string scenario in new[]{"closed","no-ability-no-danger","stable-200-active","changing-200-active"})
                {
                    foreach(GuidanceKind kind in Enum.GetValues(typeof(GuidanceKind)))Call(host,"SetEnabled",kind,scenario!="closed"&&(kind!=GuidanceKind.Merchant||scenario!="no-ability-no-danger"));
                    player.accCritterGuide=scenario!="no-ability-no-danger";Main.npc[3].boss=scenario=="stable-200-active"||scenario=="changing-200-active";
                    for(int i=0;i<30;i++){update();prepare();}
                    int basic=(int)Get(npcs,"BasicReads"),slots=(int)Get(reader,"EffectiveSlotReads"),shows=warning.Notifications,layouts=(int)Get(Get(world,"MerchantText"),"Layouts");
                    long bytes=allocated==null?0:allocated();long start=Stopwatch.GetTimestamp();
                    for(int i=0;i<120;i++)
                    {
                        if(scenario=="changing-200-active"){player.armor[3].type=i%2==0?407:5010;Main.npc[2].position.X+=1;}
                        update();prepare();
                    }
                    double elapsed=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
                    long difference=allocated==null?-1:allocated()-bytes;
                    int native=(int)Get(npcs,"BasicReads")-basic,effective=(int)Get(reader,"EffectiveSlotReads")-slots,shown=warning.Notifications-shows,laid=(int)Get(Get(world,"MerchantText"),"Layouts")-layouts;
                    Require(native==(scenario=="closed"?0:24000),"bounded actual 200-slot source demand: "+scenario);
                    if(scenario=="closed"||scenario=="no-ability-no-danger")Require(effective==0&&shown==0&&laid==0,"early gates exclude equipment and presentation work");
                    if(scenario=="stable-200-active")Require(shown==0&&laid==0,"stable actual Host does not recreate notifications/text");
                    if(scenario=="changing-200-active")Require(shown==119||shown==120,"every actual changed issue set is observed");
                    Console.WriteLine("Guidance cost "+scenario+": warm updates=120; active NPC=200; basic="+native+"; effective-slot-reads="+effective+"; Show="+shown+"; merchant-layout="+laid+"; total-ms="+elapsed.ToString("F3",CultureInfo.InvariantCulture)+"; current-thread-bytes="+(difference<0?"NA":difference.ToString(CultureInfo.InvariantCulture))+" (CPU prepare only, not FPS)");
                }
            }
            finally{Main.npc=old;player.armor[3]=equipment;player.accCritterGuide=true;foreach(GuidanceKind kind in Enum.GetValues(typeof(GuidanceKind)))Call(host,"SetEnabled",kind,true);update();}
        }
    }
}
