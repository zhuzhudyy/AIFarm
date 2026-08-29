using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AIFarmNPC.Agent
{
    public sealed class NaturalLanguageFarmParser
    {
        private static readonly Dictionary<string, string> CropAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "小麦", "wheat" }, { "wheat", "wheat" },
                { "玉米", "corn" }, { "corn", "corn" },
                { "胡萝卜", "carrot" }, { "carrot", "carrot" },
                { "番茄", "tomato" }, { "西红柿", "tomato" }, { "tomato", "tomato" },
                { "土豆", "potato" }, { "马铃薯", "potato" }, { "potato", "potato" },
                { "水稻", "rice" }, { "稻米", "rice" }, { "rice", "rice" },
                { "芜菁", "turnip" }, { "大头菜", "turnip" }, { "turnip", "turnip" }
            };

        private static readonly Regex PlotPattern = new Regex(
            @"(?:地块|田地|田|plot)\s*[#号：:]?\s*([a-zA-Z0-9_-]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public ParsedFarmCommand Parse(string input, string defaultPlotId = "plot-1", string defaultCropId = "wheat")
        {
            var text = (input ?? string.Empty).Trim();
            var lower = text.ToLowerInvariant();
            var language = Regex.IsMatch(text, @"[\u3400-\u9fff]") ? "zh" : "en";
            var crop = FindCrop(lower, defaultCropId);
            var plot = FindPlot(text, defaultPlotId);
            var intent = FindIntent(lower);
            return new ParsedFarmCommand(intent, crop, plot, text, language);
        }

        private static string FindCrop(string text, string fallback)
        {
            foreach (var pair in CropAliases)
            {
                if (text.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return pair.Value;
            }

            return string.IsNullOrWhiteSpace(fallback) ? "wheat" : fallback;
        }

        private static string FindPlot(string text, string fallback)
        {
            var match = PlotPattern.Match(text);
            if (!match.Success) return fallback;
            var captured = match.Groups[1].Value;
            return Regex.IsMatch(captured, @"^\d+$") ? "plot-" + captured : captured;
        }

        private static FarmIntent FindIntent(string text)
        {
            if (ContainsAny(text, "播种", "播下", "sow"))
                return FarmIntent.Sow;
            if (ContainsAny(text, "全流程", "全过程", "种田", "种植", "帮我种", "照料", "打理", "一条龙",
                    "种", "full cycle", "whole cycle", "take care", "farm", "grow", "plant"))
                return FarmIntent.FullCycle;
            if (ContainsAny(text, "收获", "采收", "harvest", "collect crop"))
                return FarmIntent.Harvest;
            if (ContainsAny(text, "除草", "拔草", "weed", "remove weeds"))
                return FarmIntent.Weed;
            if (ContainsAny(text, "施肥", "肥料", "fertilize", "fertilise"))
                return FarmIntent.Fertilize;
            if (ContainsAny(text, "浇水", "灌溉", "water", "irrigate"))
                return FarmIntent.Water;
            return FarmIntent.Unknown;
        }

        private static bool ContainsAny(string text, params string[] terms)
        {
            for (var i = 0; i < terms.Length; i++)
            {
                if (text.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
