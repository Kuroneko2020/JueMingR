using System;
using System.Collections.Generic;
using System.Linq;

namespace JueMingR.Features.Recovery
{
    // Immutable file data only. A domain codec persists only its own fields;
    // native objects, running attempts and errors never enter these documents.
    public sealed class RecoveryOptions
    {
        public int LifeMode { get; }
        public int LastLifeMode { get; }
        public bool Mana { get; }
        public bool Buffs { get; }
        public bool FollowAdd { get; }
        public bool FollowRemove { get; }
        public bool Nurse { get; }
        public bool Furniture { get; }
        public bool Tax { get; }
        public IReadOnlyList<int> NoLife { get; }
        public IReadOnlyList<int> NoMana { get; }
        public IReadOnlyList<int> AllowedBuffs { get; }
        private readonly HashSet<int> life, mana, buffs;
        public RecoveryOptions(int lifeMode=0,bool mana=false,bool buffs=false,bool followAdd=false,bool followRemove=false,
            bool nurse=false,bool furniture=false,bool tax=false,IEnumerable<int> noLife=null,IEnumerable<int> noMana=null,IEnumerable<int> allowedBuffs=null,int lastLifeMode=1)
        {
            if(lifeMode<0 || lifeMode>2)throw new ArgumentOutOfRangeException(nameof(lifeMode));
            if(lastLifeMode<1 || lastLifeMode>2)throw new ArgumentOutOfRangeException(nameof(lastLifeMode));
            // Remember the selected non-off mode in the same immutable candidate.
            // Closing preserves it; a failed save cannot publish a new memory.
            LastLifeMode=lifeMode==0?lastLifeMode:lifeMode;
            LifeMode=lifeMode;Mana=mana;Buffs=buffs;FollowAdd=followAdd;FollowRemove=followRemove;Nurse=nurse;Furniture=furniture;Tax=tax;
            life=Types(noLife);this.mana=Types(noMana);this.buffs=Types(allowedBuffs);
            NoLife=Array.AsReadOnly(life.OrderBy(x=>x).ToArray());NoMana=Array.AsReadOnly(this.mana.OrderBy(x=>x).ToArray());AllowedBuffs=Array.AsReadOnly(this.buffs.OrderBy(x=>x).ToArray());
        }
        private static HashSet<int> Types(IEnumerable<int> source)
        { var result=new HashSet<int>();if(source!=null)foreach(int value in source)if(value<=0 || value>100000 || !result.Add(value) || result.Count>8192)throw new ArgumentException("Invalid item types.");return result; }
        public bool LifeAllowed(int type){return !life.Contains(type);}
        public bool ManaAllowed(int type){return !mana.Contains(type);}
        public bool BuffAllowed(int type){return buffs.Contains(type);}
        public RecoveryOptions Change(int feature,int value)
        { return new RecoveryOptions(feature==0?value:LifeMode,feature==1?value!=0:Mana,feature==4?value!=0:Buffs,feature==6?value!=0:FollowAdd,
            feature==7?value!=0:FollowRemove,feature==2?value!=0:Nurse,feature==3?value!=0:Furniture,feature==5?value!=0:Tax,NoLife,NoMana,AllowedBuffs,LastLifeMode); }
        public RecoveryOptions ChangeType(int list,int type,bool include)
        {
            var types=new HashSet<int>(list==0?NoLife:list==1?NoMana:AllowedBuffs);
            if(include)types.Add(type);else types.Remove(type);
            return new RecoveryOptions(LifeMode,Mana,Buffs,FollowAdd,FollowRemove,Nurse,Furniture,Tax,list==0?(IEnumerable<int>)types:NoLife,list==1?(IEnumerable<int>)types:NoMana,list==2?(IEnumerable<int>)types:AllowedBuffs,LastLifeMode);
        }
        public RecoveryOptions ClearBuffs(){return new RecoveryOptions(LifeMode,Mana,Buffs,FollowAdd,FollowRemove,Nurse,Furniture,Tax,NoLife,NoMana,lastLifeMode:LastLifeMode);}
    }
}
