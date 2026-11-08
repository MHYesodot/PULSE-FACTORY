using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace PulseFactory.Localization
{
    public sealed class LocalizationCatalog
    {
        private readonly Dictionary<string, string> entries;
        public string Locale { get; }
        public bool RightToLeft { get; }
        public IEnumerable<string> Keys => entries.Keys;

        public LocalizationCatalog(string json)
        {
            LocaleData data;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                data = (LocaleData)new DataContractJsonSerializer(typeof(LocaleData)).ReadObject(stream);
            if (data == null || (data.locale != "en" && data.locale != "he") || data.entries == null
                || data.rtl != (data.locale == "he"))
                throw new SerializationException("Invalid localization resource.");
            entries = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in data.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key) || string.IsNullOrWhiteSpace(entry.value)
                    || entries.ContainsKey(entry.key))
                    throw new SerializationException("Invalid or duplicate localization entry.");
                entries.Add(entry.key, entry.value);
            }
            Locale = data.locale; RightToLeft = data.rtl;
        }

        public void ValidateKeys(LocalizationCatalog other)
        {
            if (!new HashSet<string>(Keys).SetEquals(other.Keys))
                throw new SerializationException("Localization keys are not synchronized.");
            foreach (var key in Keys)
            {
                var ownArguments = Regex.Matches(entries[key], @"\{\d+\}").Cast<Match>().Select(m => m.Value).OrderBy(s => s);
                var otherArguments = Regex.Matches(other.entries[key], @"\{\d+\}").Cast<Match>().Select(m => m.Value).OrderBy(s => s);
                if (!ownArguments.SequenceEqual(otherArguments))
                    throw new SerializationException("Localization placeholders differ: " + key);
            }
        }

        public string Text(string key, params object[] arguments)
        {
            if (!entries.TryGetValue(key, out var value)) throw new KeyNotFoundException("Missing translation: " + key);
            return string.Format(CultureInfo.InvariantCulture, value, arguments);
        }

        public string Display(string key, params object[] arguments)
        {
            var text = Text(key, arguments);
            return RightToLeft ? HebrewVisualOrder(text) : text;
        }

        public string DisplayWrapped(string key, Func<string, bool> fits, params object[] arguments)
        {
            var lines = new List<string>();
            foreach (var paragraph in Text(key, arguments).Split('\n'))
            {
                string line = "";
                foreach (var word in paragraph.Split(' '))
                {
                    string candidate = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && !fits(candidate))
                    {
                        lines.Add(line);
                        line = word;
                    }
                    else line = candidate;
                }
                lines.Add(line);
            }
            // Wrap logical text first so RTL paragraphs retain their top-to-bottom reading order.
            return string.Join("\n", lines.Select(line => RightToLeft ? HebrewVisualOrder(line) : line));
        }

        // IMGUI lacks bidi layout. This handles the supplied unpointed Hebrew with isolated ASCII runs.
        // It is intentionally not a general Unicode bidi implementation.
        public static string HebrewVisualOrder(string text)
        {
            return string.Join("\n", text.Split('\n').Select(line =>
            {
                var elements = new List<string>();
                var iterator = StringInfo.GetTextElementEnumerator(line);
                while (iterator.MoveNext()) elements.Add(iterator.GetTextElement());
                elements.Reverse();
                return Regex.Replace(string.Concat(elements), @"[A-Za-z0-9]+(?:[./:-][A-Za-z0-9]+)*",
                    match => new string(match.Value.Reverse().ToArray()));
            }));
        }

        [DataContract]
        private sealed class LocaleData
        {
            [DataMember(IsRequired = true)] public string locale;
            [DataMember(IsRequired = true)] public bool rtl;
            [DataMember(IsRequired = true)] public Entry[] entries;
        }
        [DataContract]
        private sealed class Entry
        {
            [DataMember(IsRequired = true)] public string key;
            [DataMember(IsRequired = true)] public string value;
        }
    }
}
