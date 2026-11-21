using System;
using System.Collections.Generic;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private sealed class PersonalItemView : IDisposable
        {
            private readonly List<KeyValuePair<int,WorldItem>> previous;
            private readonly Actor actor;
            internal PersonalItemView(Actor actor)
            {
                this.actor=actor;previous=new List<KeyValuePair<int,WorldItem>>(actor.PersonalItems.Count);
                foreach(var pair in actor.PersonalItems)
                {previous.Add(new KeyValuePair<int,WorldItem>(pair.Key,Main.item[pair.Key]));Main.item[pair.Key]=pair.Value;}
            }
            public void Dispose(){foreach(var pair in previous)Main.item[pair.Key]=pair.Value;PrunePersonalItems(actor);}
        }
        internal IDisposable EnterPersonalItems(Actor actor)
        {
            // Sparse per-connection 90 instances, not a per-player item world.
            // Keep the view active through PickupItem -> SyncItem -> native
            // SendData serialization, including nested scopes and exceptions.
            return actor.PersonalItems.Count==0?null:new PersonalItemView(actor);
        }
        private void DecodePersonalItem(Actor actor,byte[] packet)
        {
            if(packet.Length<3)throw new System.IO.InvalidDataException("个人物品数据帧不完整。");
            int index=BitConverter.ToInt16(packet,1);if(index<0 || index>=Main.maxItems)throw new System.IO.InvalidDataException("个人物品槽位无效。");
            WorldItem item;if(!actor.PersonalItems.TryGetValue(index,out item))actor.PersonalItems.Add(index,new WorldItem{whoAmI=index});
            using(CompanionHooks.EnterWithPersonalItems(actor))
            {
                Buffer.BlockCopy(packet,0,decoder.readBuffer,0,packet.Length);decoder.ResetReader();var old=Decoding;Decoding=actor;
                try{int type;decoder.GetData(0,packet.Length,out type);actor.PersonalItems[index]=Main.item[index];}
                finally{Decoding=old;}
            }
            PrunePersonalItems(actor);
        }
        private static void PrunePersonalItems(Actor actor)
        {
            List<int> expired=null;
            foreach(var pair in actor.PersonalItems)if(!pair.Value.active){if(expired==null)expired=new List<int>();expired.Add(pair.Key);}
            if(expired!=null)foreach(int key in expired)actor.PersonalItems.Remove(key);
        }
        internal void AdvancePersonalItems()
        {
            if(Main.netMode!=1 || !HasActors)return;
            foreach(var actor in actors.Values)
            {
                if(!actor.Ready || actor.Closing || actor.PersonalItems.Count==0 || actor.PersonalItemsStep==Main.GameUpdateCount)continue;
                actor.PersonalItemsStep=Main.GameUpdateCount;
                using(CompanionHooks.EnterWithPersonalItems(actor))
                    foreach(var pair in actor.PersonalItems)pair.Value.UpdateItem(pair.Key);
                PrunePersonalItems(actor);
            }
        }
    }
}
