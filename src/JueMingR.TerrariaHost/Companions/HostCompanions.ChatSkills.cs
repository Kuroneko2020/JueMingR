using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JueMingR.Platform.Llm;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private const string GameSkillTerrariaVersion = "1.4.6.1";
        private const string GameSkillRevision = "1";
        private const int MaxGameSkillSectionLength = 620;

        private sealed class GameSkill
        {
            internal GameSkill(string key, string title, string summary, params string[] aliases)
            {
                Key = key; Title = title; Summary = summary; Aliases = aliases;
                Sections = new Lazy<string[]>(() => LoadGameSkillSections(key));
            }

            internal readonly string Key, Title, Summary;
            internal readonly string[] Aliases;
            internal readonly Lazy<string[]> Sections;

            internal bool Matches(string query) => Match(Key, query) || Match(Title, query) ||
                Aliases.Any(alias => Match(alias, query));

            private static bool Match(string value, string query) =>
                value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                query.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // The short index is versioned alongside the resources. No world entry,
        // connection switch or tool declaration loads the body or calls a model.
        private static readonly GameSkill[] GameSkills =
        {
            new GameSkill("mining", "挖矿与方块", "镐力、实得数量、放置和墙壁。", "挖矿", "矿点", "采矿", "镐", "方块", "拆墙", "铜矿"),
            new GameSkill("shimmer", "微光转化", "验证当前池水、持物及转化，再确认结果。", "微光", "微光池", "以太", "转化", "分解"),
            new GameSkill("fishing", "钓鱼", "核对钓位、水、鱼饵和真实收线。", "钓鱼", "钓位", "任务鱼", "渔夫", "松露虫"),
            new GameSkill("combat", "清场与守卫", "区分有限清场、持续防守和原版受击资格。", "战斗", "清怪", "清场", "杀怪", "防守", "守住", "友好生物"),
            new GameSkill("landmarks", "地标和材料采集", "按存档查地标、覆盖范围和附近材料。", "地标", "记下", "上次", "采集", "材料", "神庙", "地牢", "矿点", "标记")
        };

        private static readonly LlmToolDefinition[] GameSkillTools =
        {
            ChatTool("find_game_skills", "按主题找内置玩法指南，例如挖矿、微光、钓鱼、清怪或地标。只返回短索引。",
                "\"query\":{\"type\":\"string\",\"maxLength\":64}"),
            ChatTool("read_game_skill", "按索引的 key 读取一节攻略。用现场工具复核实际条件。",
                "\"key\":{\"type\":\"string\",\"maxLength\":24},\"section\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":8}",
                ",\"required\":[\"key\"]")
        };

        partial void AddSkillChatTools(string playerText, bool continueSkill, List<LlmToolDefinition> tools)
        {
            // Retained tool groups remain replayable after a follow-up such as “继续”.
            if (continueSkill || ChatMentions(playerText, "玩法", "攻略", "指南", "技巧", "技能", "怎么做", "怎么玩") ||
                GameSkills.Any(skill => skill.Matches(playerText))) tools.AddRange(GameSkillTools);
        }

        private bool TryDispatchSkillChatTool(ChatCommand command)
        {
            var args = command.Call.Arguments;
            switch (command.Call.Name)
            {
                case "find_game_skills":
                    if (!ChatFields(args, "query") || !ChatString(args, "query", 64, out string query))
                        RejectChatTool(command, "请输入不超过 64 个字符的玩法主题。");
                    else
                        CompleteChatQuery(command, FindGameSkills(query));
                    return true;
                case "read_game_skill":
                    if (!ChatFields(args, "key", "section") || !ChatString(args, "key", 24, out string key) ||
                        !ReadChatInt(args, "section", 1, 1, 8, out int section) || String.IsNullOrEmpty(key))
                        RejectChatTool(command, "请使用玩法索引中的 key 和有效小节编号。");
                    else
                    {
                        string body = ReadGameSkill(key, section);
                        if (body == null) RejectChatTool(command, "没有这个玩法指南或小节。可先查询玩法索引。");
                        else CompleteChatQuery(command, body);
                    }
                    return true;
                default:
                    return false;
            }
        }

        private static string FindGameSkills(string query)
        {
            query = query?.Trim() ?? "";
            var rows = GameSkills.Where(skill => query.Length == 0 || skill.Matches(query))
                .Select(skill => skill.Key + " · " + skill.Title + " · " + skill.Summary).ToArray();
            return rows.Length == 0 ? "没有匹配的内置指南。可留空查询已有主题。" :
                SkillVersion + "\n" + String.Join("\n", rows) + "\n用 read_game_skill 指定 key 取正文。";
        }

        private static string ReadGameSkill(string key, int section)
        {
            var skill = GameSkills.FirstOrDefault(entry => StringComparer.Ordinal.Equals(entry.Key, key));
            if (skill == null) return null;
            var pages = skill.Sections.Value;
            if (section < 1 || section > pages.Length) return null;
            return skill.Title + " · " + SkillVersion + " · " + section + "/" + pages.Length + "\n" + pages[section - 1] +
                (section == pages.Length ? "" : "\n下一节：read_game_skill key=" + skill.Key + " section=" + (section + 1));
        }

        private static string SkillVersion => "Terraria " + GameSkillTerrariaVersion + " · 指南第 " + GameSkillRevision + " 版";

        private static string[] LoadGameSkillSections(string key)
        {
            // Keys come from the built-in index, never from a player path or file name.
            using (var resource = typeof(HostCompanions).Assembly.GetManifestResourceStream("JueMingR.GameSkills." + key + ".md"))
            {
                if (resource == null || resource.Length > MaxGameSkillSectionLength * 3 * 8)
                    return Array.Empty<string>();
                using (var reader = new StreamReader(resource, Encoding.UTF8))
                {
                    var pages = reader.ReadToEnd().Replace("\r\n", "\n").Trim()
                        .Split(new[] { "\n## " }, StringSplitOptions.None)
                        .Select((page, index) => index == 0 ? page.Trim() : "## " + page.Trim()).ToArray();
                    return pages.Length <= 8 && pages.All(page => page.StartsWith("## ", StringComparison.Ordinal) &&
                        page.Length <= MaxGameSkillSectionLength) ? pages : Array.Empty<string>();
                }
            }
        }
    }
}
