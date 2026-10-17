using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Static receiver geometry isolates navigation from NPC motor accuracy.
    // Birth, every AI99 step, TileCollision and Damage are original; the
    // production input scope alone may change the requested control point.
    internal static class NativeCombatYoyoGapChecks
    {
        internal static void Run(object context)
        {
            foreach(string shape in new[]{"horizontal","vertical","corner","platform","slope"})
                foreach(float offset in new[]{0f,7.25f,15f})Run(context,shape,offset);
        }
        private static void Run(object context,string shape,float offset)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);
            player.position=new Vector2(700+offset,646);player.ResetEffects();player.channel=player.controlUseItem=true;player.yoyoGlove=player.magicString=false;player.counterWeight=0;
            Main.screenPosition=new Vector2(550,350);
            var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.aiStyle=-1;target.noGravity=true;target.velocity=target.netOffset=Vector2.Zero;target.position=new Vector2(shape=="vertical"?776:shape=="corner"?808:832,shape=="corner"?552:shape=="vertical"?538:646);target.life=target.lifeMax=10000;target.knockBackResist=0;target.shimmerTransparency=0;
            Array.Clear(target.immune,0,target.immune.Length);Array.Clear(target.buffType,0,target.buffType.Length);Array.Clear(target.buffTime,0,target.buffTime.Length);
            if(shape!="vertical")
            {
                for(int x=48;x<=49;x++)for(int y=5;y<=42;y++){NativeToolsChecks.Tile(x,y,1);if(y==40)Main.tile[x,y].active(false);}
                if(shape=="platform"){Main.tileSolid[19]=Main.tileSolidTop[19]=true;for(int x=48;x<=49;x++)NativeToolsChecks.Tile(x,40,19);}
                if(shape=="slope"){NativeToolsChecks.Tile(47,39,1);Main.tile[47,39].slope(2);NativeToolsChecks.Tile(47,41,1);Main.tile[47,41].halfBrick(true);}
            }
            if(shape=="vertical" || shape=="corner")
            {
                int gap=shape=="vertical"?47:50;
                for(int x=5;x<Main.maxTilesX-5;x++){NativeToolsChecks.Tile(x,38,1);if(x==gap)Main.tile[x,38].active(false);}
            }
            NativeToolExecutionChecks.Sample(context,input,target.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,target,0,true);
            var control=Get(attack,"Control");Require((bool)Get(control,"openingUsable"),"one-tile geometry is found from sub-tile start: "+shape+"/"+offset);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,new object[]{0,player.HeldItem,player.GetWeaponDamage(player.HeldItem),true});
            var shot=Main.projectile.Single(p=>p.active && p.type==541);int key=(int)shot.key;var entries=(System.Collections.IDictionary)Get(control,"shots");
            long first=-1,expected=-1;bool entered=false;
            for(int tick=0;tick<110 && first<0 && shot.active;tick++)
            {
                NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,target.Center,true);Call(combat,"Sample");target.UpdateNPC(2);NativeCombatAimChecks.Prepare(host,attack,target,0,true);
                var entry=entries[key];var contact=(AttackContact)GetOptional(entry,"Contact");expected=contact==null?-1:contact.Timeline.SampleTick+contact.Tick;
                if(tick%10==0)Console.WriteLine("NARROW "+shape+" offset="+offset+" tick="+tick+" center="+shot.Center+" velocity="+shot.velocity+" point="+Get(entry,"Point")+" plan="+expected);
                int life=target.life;shot.Update(shot.whoAmI);
                if(shape=="vertical")entered|=shot.position.Y+shot.height<=608;
                else entered|=shot.position.X>=800 && (shape!="corner" || shot.position.Y+shot.height<=608);
                if(target.life<life)first=Main.GameUpdateCount;
            }
            Console.WriteLine("NARROW RESULT "+shape+" offset="+offset+" entered="+entered+" first="+first+" expected="+expected+" center="+shot.Center);
            Require(entered && first>0 && first==expected,"whole native ball traverses the one-tile route and agrees with current contact time: "+shape+"/"+offset);
        }
    }
}
