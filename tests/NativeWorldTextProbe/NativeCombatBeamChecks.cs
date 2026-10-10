using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBeamChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var visual=new Harmony("JueMingR.Tests.BeamRippleOutlet");var ai=typeof(Projectile).GetMethod("AI",Flags);
            visual.Patch(ai,transpiler:new HarmonyMethod(typeof(NativeCombatBeamChecks).GetMethod(nameof(RippleOutlet),BindingFlags.NonPublic|BindingFlags.Static)));
            try{RunCore(context);}finally{visual.Unpatch(ai,HarmonyPatchType.All,visual.Id);}
        }
        // A CPU component has no scene filter/render target. Remove exactly
        // the WaterDistortion QUEUE outlet expression, not native beam AI,
        // LaserScan, movement, collision, Damage, RNG or network authority.
        private static IEnumerable<CodeInstruction> RippleOutlet(IEnumerable<CodeInstruction> instructions)
        {
            var code=instructions.Select(i=>new CodeInstruction(i)).ToList();int matches=0;
            for(int i=1;i<code.Count;i++)if(code[i].opcode==OpCodes.Ldstr && Equals(code[i].operand,"WaterDistortion"))
            {
                int start=i-1;var field=code[start].operand as FieldInfo;
                Require(code[start].opcode==OpCodes.Ldsfld && field?.DeclaringType==typeof(Terraria.Graphics.Effects.Filters) && field.Name=="Scene","exact native WaterDistortion expression start");
                int end=i;for(;end<code.Count;end++){var call=code[end].operand as MethodInfo;if(call?.DeclaringType==typeof(Terraria.GameContent.Shaders.WaterShaderData) && call.Name=="QueueRipple")break;}
                Require(end<code.Count && end-start<100,"exact bounded native Ripple Queue outlet");
                for(int j=start;j<=end;j++){code[j].opcode=OpCodes.Nop;code[j].operand=null;}matches++;
            }
            Require(matches==1,"one cosmetic ripple outlet, no broad native AI skip");Console.WriteLine("BEAM cosmetic isolation: exact WaterDistortion QueueRipple expressions="+matches+"; original AI/LaserScan/Damage retained.");return code;
        }
        private static void RunCore(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var tools=Get(context,"Tools");var input=Get(context,"Input");
            foreach(int weapon in new[]{3541,2882})foreach(bool wall in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,weapon,0,0);p.position=new Vector2(700,646);p.channel=p.controlUseItem=true;p.statMana=p.statManaMax2=10000;Main.screenPosition=new Vector2(600,500);
                if(wall)for(int y=0;y<Main.maxTilesY;y++){Main.tile[60,y].active(true);Main.tile[60,y].type=Terraria.ID.TileID.Stone;}
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var parent=Main.projectile.Single(q=>q.active && q.owner==0);int first=-1;float length=0;
                for(int stage=1;stage<=185;stage++)
                {
                    // This fixture freezes the NPC body while its ordinary
                    // owner immunity clock advances with the native AI phase.
                    if(n.immune[0]>0)n.immune[0]--;
                    NativeCombatAimChecks.Prepare(host,attack,n,0);parent.AI();var beams=Main.projectile.Where(q=>q.active && q.owner==0 && (q.type==632 || q.type==461)).ToArray();
                    if(weapon==2882 && stage<180)Require(beams.Length==0,"charge does not emit its main beam before native 180 stage");
                    if(weapon==2882 && stage==180)Require(beams.Length==1 && ((Terraria.DataStructures.ProjectileKey)beams[0].ai[1]).Equals(parent.key),"continuous charge automatically births beam at native threshold: charge="+parent.ai[0]+" active="+parent.active+" channel="+p.channel+" mana="+p.statMana+" rate="+p.GetSlowMagicUseRate()+" key="+parent.key+" childKeys="+string.Join(",",beams.Select(b=>b.ai[1])));
                    foreach(var beam in beams)
                    {
                        Require(((Terraria.DataStructures.ProjectileKey)beam.ai[1]).Equals(parent.key),"child uses complete bit-reinterpreted parent ProjectileKey, not numeric float/int or slot");
                        int life=n.life;try{beam.Update(beam.whoAmI);}catch(Exception error){Console.WriteLine("BEAM failure stage="+stage+" type="+beam.type+" scale="+beam.scale+" length="+beam.localAI[1]+" WaterFilter="+(Terraria.Graphics.Effects.Filters.Scene["WaterDistortion"]!=null)+" exception="+error.GetType().Name);throw;}if(n.life<life && first<0)first=stage;length=Math.Max(length,beam.localAI[1]);
                        if(weapon==3541 && stage==30)Require(!beam.friendly,"prism stage 30 still cannot damage");
                        if(weapon==3541 && stage==120)Require(beam.friendly && beam.scale<1.4f,"prism phase120 retains partial bundle");
                        if(weapon==3541 && stage==180)Require(beam.friendly && beam.scale==1.4f && beam.damage==parent.damage*3,"prism full charge changes actual width and damage");
                    }
                }
                Require(wall?first<0:first>0,"actual natural beam Damage distinguishes identical scene wall: "+weapon+" wall="+wall+" first="+first+" length="+length);
                Console.WriteLine("PASS natural beam charge/Damage/terrain: weapon="+weapon+" wall="+wall+" firstDamageStage="+first+" maxLength="+length);
                NativeCombatAimChecks.Prepare(host,attack,n,0);var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");
                if(!wall && contact==null){var beam=Main.projectile.First(q=>q.active && (q.type==632 || q.type==461));Vector2 delta=n.Center-beam.Center,unit=beam.velocity.SafeNormalize(Vector2.UnitY);float perpendicular=Math.Abs(delta.X*unit.Y-delta.Y*unit.X);Console.WriteLine("BEAM contact difference parent="+parent.Center+" arm="+p.GetArmPosition()+" mounted="+p.RotatedRelativePoint(p.MountedCenter)+" child="+beam.Center+" unit="+unit+" width="+(22*beam.scale)+" distance="+perpendicular+" length="+beam.localAI[1]+" target="+n.Center+" immune="+n.immune[0]);}
                Require(wall?contact==null:contact!=null,"beam red requires reachable finite staged line contact: weapon="+weapon+" wall="+wall);
                if(contact!=null)
                {
                    int expected=contact.Tick,actual=-1;
                    for(int step=1;step<=24 && actual<0;step++)
                    {
                        NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");
                        if(n.immune[0]>0)n.immune[0]--;NativeCombatAimChecks.Prepare(host,attack,n,0);parent.AI();int life=n.life;
                        foreach(var beam in Main.projectile.Where(q=>q.active && q.owner==0 && (q.type==632 || q.type==461)).ToArray())beam.Update(beam.whoAmI);
                        if(n.life<life)actual=step;
                    }
                    Require(actual==expected,"finite beam Contact matches next real damage window: weapon="+weapon+" expected="+expected+" actual="+actual);
                    Console.WriteLine("PASS beam next contact window: weapon="+weapon+" first="+actual+" impact="+contact.ImpactX+","+contact.ImpactY+" sample="+contact.Timeline.SampleTick);
                }
                parent.Kill();foreach(var beam in Main.projectile.Where(q=>q.active && (q.type==632 || q.type==461))) {beam.Update(beam.whoAmI);Require(!beam.active,"lost native parent key retires beam");}
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
    }
}
