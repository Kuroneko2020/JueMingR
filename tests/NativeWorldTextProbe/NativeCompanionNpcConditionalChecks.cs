using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionNpcStateChecks
    {
        internal static void Conditional(object host,Player first,Player second,Action<int> publish)
        {
            var human=Main.LocalPlayer;var players=new[]{human,first,second};var positions=players.Select(p=>p.position).ToArray();var original=Main.npc[Slot];int mode=Main.netMode;
            try
            {
                human.position=new Vector2(1000,918);first.position=new Vector2(1050,918);second.position=new Vector2(1250,918);
                foreach(int type in new[]{NPCID.Vulture,399})
                {
                    Call(host,"ClearWorldSources");var source=Spawn(type,(byte)(type==NPCID.Vulture?220:221));source.position.Y=type==NPCID.Vulture?960-source.height:880;source.TargetClosest();
                    Main.netMode=0;try{source.UpdateNPC(Slot);if(type==399)for(int action=0;source.ai[0]!=2 && action<61;action++){NativeQuickItemChecks.BeginWorldStep();source.UpdateNPC(Slot);}}finally{Main.netMode=1;}
                    Require(type==NPCID.Vulture?source.ai[0]==1 && !source.noGravity:source.ai[0]==2 && !source.noTileCollide,"actual native conditional phase ENTRY publishes the previous action's physical flag, type"+type);publish(Slot);var observed=((NPC[])Get(host,"observedNpcs"))[Slot];long stamp=(long)Call(host,"NpcSampleTick",observed),revision=(long)Call(host,"NpcObservationRevision",observed);
                    Require(!(bool)Call(host,"ObservedNpcCombatReady",observed) && observed.noGravity==source.noGravity && observed.noTileCollide==source.noTileCollide,"conditional raw phase cannot overwrite its current sample with a future action's flag");
                    NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvanceNpcObservation");Require((bool)Call(host,"ObservedNpcCombatReady",observed) && (type==NPCID.Vulture?observed.noGravity:observed.noTileCollide) && (long)Call(host,"NpcSampleTick",observed)==stamp && (long)Call(host,"NpcObservationRevision",observed)==revision && !(bool)Call(host,"FreshNpcObservation",observed),"real qualified shared action establishes the new flags without rebasing the raw sample");
                    for(int packet=0;packet<3;packet++){source.UpdateNPC(Slot);publish(Slot);Require((bool)Call(host,"ObservedNpcCombatReady",observed),"continuous same accepted phase preserves established local inputs and remains available");}
                    Console.WriteLine("NPC CONDITIONAL type="+type+" naturalEntry=True currentSampleRetained=True actualActionEstablishes=True rawTick/RevisionUnchanged=True samePhase23x3NoStarvation=True");
                }
            }
            finally{Main.netMode=mode;Main.npc[Slot]=original;Call(host,"ClearWorldSources");for(int i=0;i<players.Length;i++)players[i].position=positions[i];}
        }
    }
}
