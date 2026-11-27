using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using JueMingR.Features.ItemBrowser;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    internal static class NativeItemAliases
    {
        // These two immutable name dictionaries come from the running
        // Terraria assembly. Read them only for a real name query and
        // without switching the game's current language or resource packs.
        private static readonly Lazy<Task<LanguageManager[]>> Prepared =
            new Lazy<Task<LanguageManager[]>>(() => Task.Run(ReadDictionaries));

        private static LanguageManager[] ReadDictionaries()
        {
            var assembly = typeof(Terraria.Main).Assembly;
            return new[] { ReadDictionary(assembly, "en-US"), ReadDictionary(assembly, "zh-Hans") };
        }

        private static LanguageManager ReadDictionary(Assembly assembly, string culture)
        {
            try
            {
                using (var stream = assembly.GetManifestResourceStream("Terraria.Localization.Content." + culture + ".Items.json"))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            // The original reader accepts comments in en-US.
                            // A fresh manager avoids touching the shared
                            // language, culture, or gameplay event subscribers.
                            var names = (LanguageManager)Activator.CreateInstance(typeof(LanguageManager), nonPublic: true);
                            names.LoadLanguageFromFileTextJson(reader.ReadToEnd(), canCreateCategories: true);
                            return names;
                        }
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        internal static bool IsPending(string filter)
        {
            return !String.IsNullOrWhiteSpace(filter) && BrowserCatalog.ClassifyQuery(filter) == 0 &&
                !Prepared.Value.IsCompleted;
        }

        internal static bool Matches(string localizationKey, string filter)
        {
            const string prefix = "ItemName.";
            if (localizationKey?.StartsWith(prefix, StringComparison.Ordinal) != true || IsPending(filter)) return false;
            var loading = Prepared.Value;
            if (loading.Status != TaskStatus.RanToCompletion) return false;
            foreach (var dictionary in loading.GetAwaiter().GetResult())
            {
                if (dictionary != null && dictionary.Exists(localizationKey) &&
                    dictionary.GetTextValue(localizationKey).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}
