using System;

namespace JueMingR.Features.Fishing
{
    public static class PlayerNameRules
    {
        public static string Normalize(string value)
        {
            string result=(value??string.Empty).Replace("\r",string.Empty).Replace("\n",string.Empty).Replace('\t',' ').Trim();
            if(result.Length==0)throw new ArgumentException("名字不能为空。");
            if(result.Length>20)throw new ArgumentException("名字不能超过20个字符。");
            // UTF-16 is the native limit; reject broken pairs rather than letting
            // the save encoder silently replace part of the player's name.
            for(int i=0;i<result.Length;i++)
                if(char.IsSurrogate(result[i]) && (!char.IsHighSurrogate(result[i]) || ++i>=result.Length || !char.IsLowSurrogate(result[i])))
                    throw new ArgumentException("名字包含不完整字符。");
            return result;
        }
        public static string Increment(string value)
        {
            string name=Normalize(value);int start=name.Length;
            while(start>0 && name[start-1]>='0' && name[start-1]<='9')start--;
            if(start==name.Length)return Normalize(name+"1");
            char[] digits=name.Substring(start).ToCharArray();int index=digits.Length-1;
            while(index>=0 && digits[index]=='9'){digits[index]='0';index--;}
            if(index>=0)digits[index]++;
            return Normalize(name.Substring(0,start)+(index<0?"1":string.Empty)+new string(digits));
        }
    }
}
