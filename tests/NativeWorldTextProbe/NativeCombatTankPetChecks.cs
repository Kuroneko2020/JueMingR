using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatTankPetChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(Assembly host)
        {
            var players=(Player[])Main.player.Clone();var shots=(Projectile[])Main.projectile.Clone();var npcs=(NPC[])Main.npc.Clone();int local=Main.myPlayer;
            try
            {
                for(int i=0;i<Main.player.Length;i++)Main.player[i]=new Player{whoAmI=i};
                for(int i=0;i<Main.npc.Length;i++)Main.npc[i]=new NPC();
                Main.myPlayer=0;
                var motionType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePlayerMotion",true);
                var guardianAI=typeof(Projectile).GetMethod("AI_120_StardustGuardian",Flags);
                foreach(string mode in new[]{"ordinary","dead","ghost","outside"})foreach(bool alreadyReset in new[]{false,true})
                {
                    Player[] pair={Player(mode),Player(mode)};Projectile[] guardians={Guardian(pair[0]),Guardian(pair[1])};
                    for(int i=0;i<2;i++)
                    {Main.player[1]=pair[i];Main.projectile[0]=guardians[i];guardianAI.Invoke(guardians[i],null);if(alreadyReset)pair[i].Update(1);}
                    object motion=Activator.CreateInstance(motionType,true);
                    using(var bytes=new MemoryStream())
                    {
                        using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))motionType.GetMethod("Write",Flags).Invoke(null,new object[]{writer,pair[1]});
                        bytes.Position=0;using(var reader=new BinaryReader(bytes))motionType.GetMethod("Read",Flags).Invoke(motion,new object[]{reader,1});
                    }
                    // Real AI reasserts for three phases, then stops. Compare
                    // each phase against Player.Update; do not duplicate its
                    // reset algorithm as the expected-value implementation.
                    for(int tick=0;tick<6;tick++)
                    {
                        Main.player[1]=pair[0];Main.projectile[0]=guardians[0];pair[0].Update(1);
                        Main.player[1]=pair[1];Main.projectile[0]=guardians[1];motionType.GetMethod("Advance",Flags).Invoke(motion,null);
                        Equal(pair,mode+" player phase "+tick);
                        if(tick<3)for(int i=0;i<2;i++){Main.player[1]=pair[i];Main.projectile[0]=guardians[i];guardianAI.Invoke(guardians[i],null);}
                        Equal(pair,mode+" projectile phase "+tick);
                    }
                }
                Console.WriteLine("PASS tank pet native two-phase lifetime / reassert / retire / dead ghost outside / initial reset");
                MinionOrder(motionType);
            }
            finally{Array.Copy(players,Main.player,players.Length);Array.Copy(shots,Main.projectile,shots.Length);Array.Copy(npcs,Main.npc,npcs.Length);Main.myPlayer=local;}
        }
        private static Player Player(string mode)
        {
            var p=new Player{whoAmI=1,active=true,position=new Vector2(400,600),dead=mode=="dead",ghost=mode=="ghost",respawnTimer=600,statLife=400,statLifeMax=400,statLifeMax2=400,numMinions=3,slotsMinions=2.5f};
            if(mode=="outside")p.position=new Vector2(16,16);p.MinionRestTargetPoint=p.Center;return p;
        }
        private static Projectile Guardian(Player p)
        {var g=new Projectile();g.SetDefaults(623);g.whoAmI=0;g.owner=1;g.alpha=0;g.ai[0]=1;g.Center=p.Center;return g;}
        private static void MinionOrder(Type motionType)
        {
            foreach(bool overBudget in new[]{false,true})
            {
                var players=new Player[2][];var projectiles=new Projectile[2][];object motion=null;
                for(int branch=0;branch<2;branch++)
                {
                    players[branch]=new[]{Player("ordinary"),Player("ordinary")};
                    projectiles[branch]=new Projectile[Main.projectile.Length];
                    for(int i=0;i<projectiles[branch].Length;i++)projectiles[branch][i]=new Projectile{whoAmI=i};
                    for(int i=0;i<Main.player.Length;i++)Main.player[i]=new Player{whoAmI=i};
                    for(int i=0;i<2;i++){var p=players[branch][i];p.whoAmI=i;p.position=new Vector2(400+i*200,1000);p.isControlledByFilm=true;p.immune=true;p.immuneTime=100000;Main.player[i]=p;p.Update(i);}
                    foreach(int slot in overBudget?new[]{0,3,4,7}:new[]{0,3,4})
                    {
                        var p=projectiles[branch][slot];p.SetDefaults(slot==0 || slot==7?266:623);p.whoAmI=slot;p.owner=slot==3?1:0;p.active=true;p.alpha=0;p.timeLeft=600;p.position=new Vector2(400+slot*10,950);p.extraUpdates=slot==4?1:0;
                    }
                    if(branch==1)
                    {
                        motion=Activator.CreateInstance(motionType,true);
                        for(int slot=0;slot<2;slot++)using(var bytes=new MemoryStream())
                        {using(var w=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))motionType.GetMethod("Write",Flags).Invoke(null,new object[]{w,players[branch][slot]});bytes.Position=0;using(var r=new BinaryReader(bytes))motionType.GetMethod("Read",Flags).Invoke(motion,new object[]{r,slot});}
                    }
                }
                Main.myPlayer=0;
                for(int tick=0;tick<2;tick++)
                {
                    for(int branch=0;branch<2;branch++)
                    {
                        for(int i=0;i<2;i++)Main.player[i]=players[branch][i];Array.Copy(projectiles[branch],Main.projectile,Main.projectile.Length);
                        if(branch==0)for(int i=0;i<2;i++)Main.player[i].Update(i);else motionType.GetMethod("Advance",Flags).Invoke(motion,null);
                        // Both sides execute the actual projectile method in
                        // native slot order, including extra updates and Kill.
                        for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;Main.projectile[slot].Update(slot);}
                        Main.ProjectileUpdateLoopIndex=-1;
                    }
                    for(int i=0;i<2;i++)if(players[0][i].numMinions!=players[1][i].numMinions || players[0][i].slotsMinions!=players[1][i].slotsMinions)throw new InvalidOperationException("Original minion aggregate differs at tick "+tick);
                    foreach(int slot in new[]{0,3,4,7})if(projectiles[0][slot].active!=projectiles[1][slot].active || projectiles[0][slot].minionPos!=projectiles[1][slot].minionPos)throw new InvalidOperationException("Native minion rank/budget differs at slot "+slot);
                }
                if(overBudget && projectiles[0][7].active)throw new InvalidOperationException("Original later minion must exercise over-budget Kill.");
            }
            Console.WriteLine("PASS original minion ordered contributors / other owner / extra updates / exact budget and later Kill / two phases");
        }
        private static void Equal(Player[] pair,string stage)
        {if(pair[0].tankPet!=pair[1].tankPet || pair[0].tankPetReset!=pair[1].tankPetReset || pair[0].numMinions!=pair[1].numMinions || pair[0].slotsMinions!=pair[1].slotsMinions)throw new InvalidOperationException("Tank pet lifetime differs from original: "+stage+" oracle="+pair[0].tankPet+"/"+pair[0].tankPetReset+" count="+pair[0].numMinions+"/"+pair[0].slotsMinions+" predicted="+pair[1].tankPet+"/"+pair[1].tankPetReset+" count="+pair[1].numMinions+"/"+pair[1].slotsMinions);}
    }
}
