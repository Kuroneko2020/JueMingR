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
        internal static void Shape(object host,Player first,Player second,Action<int> publish,bool support=false)
        {
            var human=Main.LocalPlayer;var players=new[]{human,first,second};var positions=players.Select(p=>p.position).ToArray();var old=Main.npc[Slot];
            try
            {
                human.position=new Vector2(1000,918);first.position=new Vector2(1050,918);second.position=new Vector2(1250,918);Call(host,"ClearWorldSources");
                NPC source,observed;if(support){source=Spawn(NPCID.Antlion,140);source.position=new Vector2(1100,960-source.height);source.UpdateNPC(Slot);publish(Slot);observed=((NPC[])Get(host,"observedNpcs"))[Slot];
                Console.WriteLine("NPC SHAPE support source="+source.position+"/"+source.velocity+" flags="+source.noGravity+"/"+source.noTileCollide+" observed="+observed.position+"/"+observed.velocity+" flags="+observed.noGravity+"/"+observed.noTileCollide+" bottom="+source.Bottom);
                Require(source.noGravity && source.noTileCollide && source.velocity.Y==-.2f && observed.noGravity && observed.noTileCollide && observed.position==source.position,"native supported Antlion23 retains its pre-tail support-derived flags at the received959.8 foot");
                NativeQuickItemChecks.BeginWorldStep();source.UpdateNPC(Slot);Call(host,"AdvanceNpcObservation");Require(!source.noGravity && !observed.noGravity && !observed.noTileCollide,"following actual action queries the current support point and revokes both Antlion flags");Console.WriteLine("NPC SHAPE Antlion nativeSupport=True tailFoot=True nextExit=True");return;}

                Call(host,"ClearWorldSources");source=Spawn(378,141);human.Center=source.Center+new Vector2(-20,0);source.UpdateNPC(Slot);Require(source.ai[1]==5 && source.ai[2]==10 && source.width<160 && !source.dontTakeDamage,"native close-range bomb publishes timer10 before its expansion action");publish(Slot);observed=((NPC[])Get(host,"observedNpcs"))[Slot];Require(observed.width==source.width && observed.position==source.position && !observed.dontTakeDamage,"entry23 preserves the actual not-yet-expanded shape and qualification");
                NativeQuickItemChecks.BeginWorldStep();source.UpdateNPC(Slot);Call(host,"AdvanceNpcObservation");Require(source.width==160 && observed.width==160 && source.dontTakeDamage && observed.dontTakeDamage && observed.position==source.position && observed.velocity==source.velocity,"next real action expands/stops/immunizes the shared bomb with exactly the native geometry tail");publish(Slot);var point=observed.position;publish(Slot);Require(observed.position==point && observed.position==source.position && observed.width==160,"already expanded repeated23 never shifts its center twice");Console.WriteLine("NPC SHAPE Bomb timer10->9 nativeExpansion160=True repeat23Idempotent=True");
                int remainingActions=0;while(source.active && remainingActions++<9){NativeQuickItemChecks.BeginWorldStep();source.UpdateNPC(Slot);Call(host,"AdvanceNpcObservation");Require(observed.active==source.active,"accepted timer closes the bomb's native local contact window without waiting for another23");}Require(!observed.active && !source.active,"the expanded160 body is not retained after its accepted timer ends");

                human.position=new Vector2(1000,918);
                foreach(int item in new[]{364,174})
                {
                    Call(host,"ClearWorldSources");source=Spawn(NPCID.BlueSlime,(byte)(item==364?142:143));source.ai[1]=item;source.UpdateNPC(Slot);publish(Slot);observed=((NPC[])Get(host,"observedNpcs"))[Slot];
                    Require(source.life==source.lifeMax && observed.life==source.life && observed.lifeMax==source.lifeMax && observed.width==source.width && observed.height==source.height && observed.scale==source.scale && observed.damage==source.damage && observed.defense==source.defense,"native item-slime persistent form includes the full-health bit's actual maximum as well as current size/combat inputs");
                    var formPoint=observed.position;publish(Slot);Require(observed.position==formPoint && observed.width==source.width && observed.life==source.life,"repeated real item-form23 neither multiplies shape nor full health");source.life--;publish(Slot);Require(observed.life==source.life,"numeric injured health stays wire health and is not multiplied again");Console.WriteLine("NPC SHAPE item="+item+" size="+observed.Size+" life="+observed.life+"/"+observed.lifeMax+" repeat=True");
                }
                Call(host,"ClearWorldSources");source=Spawn(NPCID.KingSlime,144);source.position=new Vector2(1100,900);source.UpdateNPC(Slot);publish(Slot);observed=((NPC[])Get(host,"observedNpcs"))[Slot];Require(NPCID.Sets.SyncAnchor[source.type]!=Vector2.Zero && observed.Size==source.Size && observed.position==source.position && observed.Bottom==source.Bottom,"nonzero native KingSlime wire anchor is decoded with the actual dynamic dimensions");var anchorPoint=observed.position;publish(Slot);Require(observed.position==anchorPoint && observed.Size==source.Size,"same native anchored23 is idempotent");Console.WriteLine("NPC SHAPE KingSlime anchor="+NPCID.Sets.SyncAnchor[source.type]+" size="+observed.Size+" position="+observed.position+" repeat=True");
            }
            finally{Main.npc[Slot]=old;Call(host,"ClearWorldSources");for(int i=0;i<players.Length;i++)players[i].position=positions[i];}
        }
    }
}
