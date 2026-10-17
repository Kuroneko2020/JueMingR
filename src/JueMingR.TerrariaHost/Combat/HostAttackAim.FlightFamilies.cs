using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        // These finite observed families share only capture/lease plumbing.
        // Their own helpers still own native motors, births and receive order.
        // No observed family can register a new cursor consumer here.
        private enum FiniteFlightFamily { None, Magnet, Pulse, Clinger, Jousting, WhipStar, SpearShard, Yoyo, Cloud, Spirit, Seedler, Rainbow, FreeFlight, ImpactBurst, Weather, Guitar, Nail, Solar, Influx, Arcanum, Medusa, SuperStar, Monk, Grass, BubbleFlail, Piranha, LightsBane, SharpTears, NorthPole, BeeHand, MeleeHitChild, Ai29, WaterAi29, Blizzard, Ray }
        private static bool HasOwnMeleeHitFamily(Item item)
        {return item.type==1123 || HostMeleeHitChildAttack.Weapon(item.type);}
        private static FiniteFlightFamily FiniteFlightFamilyFor(Item item)
        {
            if(HostAi29SpellAttack.Handles(item.shoot))return item.shoot==731?FiniteFlightFamily.WaterAi29:FiniteFlightFamily.Ai29;
            if(HostBlizzardAttack.Handles(item.shoot))return FiniteFlightFamily.Blizzard;
            if(HostRaySpellAttack.Handles(item.shoot))return FiniteFlightFamily.Ray;
            if(item.type==1123)return FiniteFlightFamily.BeeHand;
            if(HostMeleeHitChildAttack.Weapon(item.type))return FiniteFlightFamily.MeleeHitChild;
            if(HostMagnetSphereAttack.Handles(item.shoot))return FiniteFlightFamily.Magnet;
            if(HostPlacedPulseAttack.Handles(item.shoot))return FiniteFlightFamily.Pulse;
            if(HostClingerWallAttack.Handles(item.shoot))return FiniteFlightFamily.Clinger;
            if(HostJoustingLanceAttack.Handles(item.shoot))return FiniteFlightFamily.Jousting;
            if(HostWhipStarAttack.Handles(item.shoot))return FiniteFlightFamily.WhipStar;
            if(HostNorthPoleAttack.Handles(item.shoot))return FiniteFlightFamily.NorthPole;
            if(HostSpearShardAttack.Handles(item.shoot))return FiniteFlightFamily.SpearShard;
            if(HostYoyoFamilyAttack.Parent(item.shoot))return FiniteFlightFamily.Yoyo;
            if(HostCloudAttack.Handles(item.shoot))return FiniteFlightFamily.Cloud;
            if(HostSpiritFlameAttack.Handles(item.shoot))return FiniteFlightFamily.Spirit;
            if(HostSeedlerAttack.Handles(item.shoot))return FiniteFlightFamily.Seedler;
            if(HostRainbowAttack.Handles(item.shoot))return FiniteFlightFamily.Rainbow;
            if(HostFreeFlightAttack.Handles(item.shoot))return FiniteFlightFamily.FreeFlight;
            if(HostImpactBurstAttack.Handles(item.shoot))return FiniteFlightFamily.ImpactBurst;
            if(HostWeatherPainAttack.Handles(item.shoot))return FiniteFlightFamily.Weather;
            if(HostGuitarCurveAttack.Handles(item.shoot))return FiniteFlightFamily.Guitar;
            if(HostNailAttack.Handles(item.shoot))return FiniteFlightFamily.Nail;
            if(HostSolarEruptionAttack.Handles(item.shoot))return FiniteFlightFamily.Solar;
            if(HostInfluxAttack.Handles(item.shoot))return FiniteFlightFamily.Influx;
            if(HostNebulaArcanumAttack.Handles(item.shoot))return FiniteFlightFamily.Arcanum;
            if(HostMedusaScanAttack.Handles(item.shoot))return FiniteFlightFamily.Medusa;
            if(HostSuperStarAttack.Handles(item.shoot))return FiniteFlightFamily.SuperStar;
            if(HostMonkStaffAttack.Handles(item.shoot) || item.type==3858)return FiniteFlightFamily.Monk;
            if(HostGrassCurveAttack.Family(item.shoot))return FiniteFlightFamily.Grass;
            if(HostBubbleFlailFamilyAttack.Handles(item.shoot))return FiniteFlightFamily.BubbleFlail;
            if(HostPiranhaAttack.Family(item.shoot))return FiniteFlightFamily.Piranha;
            if(HostLightsBaneAttack.Family(item.shoot))return FiniteFlightFamily.LightsBane;
            if(HostSharpTearsAttack.Handles(item.shoot))return FiniteFlightFamily.SharpTears;
            return FiniteFlightFamily.None;
        }
        private static bool FiniteFlightMember(FiniteFlightFamily family,Projectile shot)
        {
            switch(family)
            {
                case FiniteFlightFamily.Ai29:return shot.type==521 || shot.type==522 || shot.type==620;
                case FiniteFlightFamily.WaterAi29:return shot.type==731;
                case FiniteFlightFamily.Blizzard:return HostBlizzardAttack.Handles(shot.type);
                case FiniteFlightFamily.Ray:return HostRaySpellAttack.Handles(shot.type);
                case FiniteFlightFamily.Magnet:return HostMagnetSphereAttack.Family(shot.type);
                case FiniteFlightFamily.BeeHand:return HostBeeAttack.Handles(shot.type);
                case FiniteFlightFamily.MeleeHitChild:return HostMeleeHitChildAttack.Family(shot.type);
                case FiniteFlightFamily.Pulse:return HostPlacedPulseAttack.Handles(shot.type);
                case FiniteFlightFamily.Clinger:return HostClingerWallAttack.Handles(shot.type);
                case FiniteFlightFamily.Jousting:return HostJoustingLanceAttack.Handles(shot.type);
                case FiniteFlightFamily.WhipStar:return HostWhipStarAttack.Family(shot.type);
                case FiniteFlightFamily.NorthPole:return HostNorthPoleAttack.Family(shot.type);
                case FiniteFlightFamily.SpearShard:return HostSpearShardAttack.Family(shot.type);
                case FiniteFlightFamily.Yoyo:return HostYoyoFamilyAttack.Child(shot.type) || HostYoyoFamilyAttack.Parent(shot.type) && shot.ai[0]<0;
                case FiniteFlightFamily.Cloud:return HostCloudAttack.Family(shot.type);
                case FiniteFlightFamily.Spirit:return HostSpiritFlameAttack.Family(shot.type);
                case FiniteFlightFamily.Seedler:return HostSeedlerAttack.Family(shot.type);
                case FiniteFlightFamily.Rainbow:return HostRainbowAttack.Family(shot.type);
                case FiniteFlightFamily.FreeFlight:return HostFreeFlightAttack.Family(shot.type);
                case FiniteFlightFamily.ImpactBurst:return HostImpactBurstAttack.Family(shot.type);
                case FiniteFlightFamily.Weather:return HostWeatherPainAttack.Handles(shot.type);
                case FiniteFlightFamily.Guitar:return HostGuitarCurveAttack.Handles(shot.type);
                case FiniteFlightFamily.Nail:return HostNailAttack.Family(shot.type);
                case FiniteFlightFamily.Solar:return HostSolarEruptionAttack.Family(shot.type);
                case FiniteFlightFamily.Influx:return HostInfluxAttack.Family(shot.type);
                case FiniteFlightFamily.Arcanum:return HostNebulaArcanumAttack.Family(shot.type);
                case FiniteFlightFamily.Medusa:return HostMedusaScanAttack.Handles(shot.type);
                case FiniteFlightFamily.SuperStar:return HostSuperStarAttack.Family(shot.type);
                case FiniteFlightFamily.Monk:return HostMonkStaffAttack.Family(shot.type);
                case FiniteFlightFamily.Grass:return HostGrassCurveAttack.Family(shot.type);
                case FiniteFlightFamily.BubbleFlail:return HostBubbleFlailFamilyAttack.Family(shot.type);
                case FiniteFlightFamily.Piranha:return HostPiranhaAttack.Family(shot.type);
                case FiniteFlightFamily.LightsBane:return HostLightsBaneAttack.Family(shot.type);
                case FiniteFlightFamily.SharpTears:return HostSharpTearsAttack.Handles(shot.type);
                default:return false;
            }
        }
        private static AttackContact FiniteFlightContact(FiniteFlightFamily family,Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            switch(family)
            {
                case FiniteFlightFamily.Ai29:case FiniteFlightFamily.WaterAi29:return HostAi29SpellAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Blizzard:return HostBlizzardAttack.KnownFamily(player,sources,path,clock,terrain,root,HostAttackPlayerFrame.Capture(player,path));
                case FiniteFlightFamily.Ray:return HostRaySpellAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Magnet:return HostMagnetSphereAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.BeeHand:return HostBeeAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.MeleeHitChild:return HostMeleeHitChildAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Pulse:return HostPlacedPulseAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Clinger:return HostClingerWallAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Jousting:return HostJoustingLanceAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.WhipStar:return HostWhipStarAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.NorthPole:return HostNorthPoleAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.SpearShard:return HostSpearShardAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Yoyo:return HostYoyoFamilyAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Cloud:return HostCloudAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Spirit:return HostSpiritFlameAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Seedler:return HostSeedlerAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Rainbow:return HostRainbowAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.FreeFlight:return HostFreeFlightAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.ImpactBurst:return HostImpactBurstAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Weather:return HostWeatherPainAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Guitar:return HostGuitarCurveAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Nail:return HostNailAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Solar:return HostSolarEruptionAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Influx:return HostInfluxAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Arcanum:return HostNebulaArcanumAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Medusa:return HostMedusaScanAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.SuperStar:return HostSuperStarAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Monk:return HostMonkStaffAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Grass:return HostGrassCurveAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.BubbleFlail:return HostBubbleFlailFamilyAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.Piranha:return HostPiranhaAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.LightsBane:return HostLightsBaneAttack.KnownFamily(player,sources,path,clock,terrain,root);
                case FiniteFlightFamily.SharpTears:return HostSharpTearsAttack.KnownFamily(player,sources,path,clock,terrain,root);
                default:return null;
            }
        }
        private FlightPreview ReadFiniteFlightFamily(FiniteFlightFamily family,Player player,NpcTrajectory timeline,HostAttackClock clock,out bool live)
        {
            var sources=new List<Projectile>();
            foreach(var shot in Main.projectile)
                if(shot!=null && shot.active && shot.owner==player.whoAmI && shot.damage>0 && !shot.hostile && FiniteFlightMember(family,shot))sources.Add(shot);
            live=sources.Count>0;if(!live)return null;
            var members=sources.ToArray();var primary=members[0];
            var root=new HostAttackObstacles(player,primary,timeline,0,clock.BeforeNpc,clock.NextWorld);
            var contact=FiniteFlightContact(family,player,members,timeline,clock,flightPreviewTerrain,root);
            var pose=(family==FiniteFlightFamily.SpearShard || family==FiniteFlightFamily.NorthPole)?new HostMeleeAttack.SpearFlightState(player,members):null;
            return contact==null?null:new FlightPreview(player,primary,contact,arc:root,spear:pose);
        }
    }
}
