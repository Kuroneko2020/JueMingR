namespace JueMingR.Features.Fishing
{
    public static class FishFilter
    {
        // Unknown observation and absent live sonar always keep. Candidate
        // previews may explicitly supply known facts; they never authorize use.
        public static bool Keep(FishingOptions options,FishKey? candidate,string localizedName,bool sonar,bool crate,bool quest)
        {
            if(options==null || options.FilterMode==0 || !sonar || !candidate.HasValue)return true;
            FishKey key=candidate.Value;FishList list=options.List(options.FilterMode,options.Match);
            bool match=options.Match==0?list.Contains(key):list.Matches(localizedName);
            int special=crate?options.Crates:quest?options.Quests:key.Kind==FishKind.Npc?options.Npcs:0;
            // Explicit special rules override either list, including an empty
            // active list. Follow (0) alone delegates to ordinary matching.
            if(special==1)return true;
            if(special==2)return false;
            return options.FilterMode==1?match:!match;
        }
    }
}
