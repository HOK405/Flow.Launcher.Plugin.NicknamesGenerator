using Flow.Launcher.Plugin.NicknamesGenerator.Configuration;
using Flow.Launcher.Plugin.NicknamesGenerator.Configuration.Enums;
using Flow.Launcher.Plugin.UniqueNamesGenerator.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Flow.Launcher.Plugin.NicknamesGenerator.Core;

public static class NickNameGenerator
{
    private const int MinPartsAllowed = 1;
    private const int MaxPartsAllowed = 5;

    public static string Generate(PluginSettings settings, WordsStore words, int? overrideParts)
    {
        var partsCount = GetPartsCount(settings, overrideParts);
        var parts = BuildParts(settings, words, partsCount);

        var sep = PickSeparator(settings);
        var caseMode = PickCaseMode(settings);

        var baseName = ApplyFormatting(parts, caseMode, sep);

        if (!settings.UseNumbers)
            return baseName;

        int digits = PickDigits(settings);
        var left = GenerateDigits(digits);
        var right = GenerateDigits(digits);

        return settings.NumberPosition switch
        {
            NumberPosition.Prefix => left + baseName,
            NumberPosition.Suffix => baseName + right,
            _ => left + baseName + right
        };
    }

    public static List<string> GenerateBatch(PluginSettings settings, WordsStore words, int count, int? overrideParts)
    {
        count = Math.Clamp(count, 1, 500);

        if (!settings.EnsureUniqueInBatch)
        {
            var list = new List<string>(count);
            for (int i = 0; i < count; i++)
                list.Add(Generate(settings, words, overrideParts));
            return list;
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(count);

        int guard = 0;
        int guardMax = Math.Max(2000, count * 60);

        while (result.Count < count && guard < guardMax)
        {
            guard++;
            var v = Generate(settings, words, overrideParts);
            if (set.Add(v))
                result.Add(v);
        }

        return result;
    }

    private static int GetPartsCount(PluginSettings settings, int? overrideParts)
    {
        if (overrideParts.HasValue)
            return Math.Clamp(overrideParts.Value, MinPartsAllowed, MaxPartsAllowed);

        if (settings.PartsMode == PartsMode.Fixed)
            return Math.Clamp(settings.FixedParts, MinPartsAllowed, MaxPartsAllowed);

        int min = Math.Clamp(settings.MinParts, MinPartsAllowed, MaxPartsAllowed);
        int max = Math.Clamp(settings.MaxParts, MinPartsAllowed, MaxPartsAllowed);
        if (min > max) max = min;

        return NextInt(min, max);
    }

    private static List<string> BuildParts(PluginSettings settings, WordsStore words, int count)
    {
        var parts = new List<string>(count);

        if (count == 1)
        {
            parts.Add(PickNoun(words));
            return parts;
        }

        bool forceTwoNounTail = false;
        if (settings.AllowDoubleNounTail && count >= 3)
            forceTwoNounTail = NextInt(1, 100) <= Math.Clamp(settings.DoubleNounTailChancePercent, 0, 100);

        if (count == 2)
        {
            parts.Add(PickAdjective(words));
            parts.Add(PickNoun(words));
            return parts;
        }

        if (forceTwoNounTail)
        {
            for (int i = 0; i < count - 2; i++)
                parts.Add(PickAdjective(words));
            parts.Add(PickNoun(words));
            parts.Add(PickNoun(words));
            return parts;
        }

        if (count == 3)
        {
            parts.Add(PickAdjective(words));
            parts.Add(PickAdjective(words));
            parts.Add(PickNoun(words));
            return parts;
        }

        if (count == 4)
        {
            if (NextInt(1, 100) <= 50)
            {
                parts.Add(PickAdjective(words));
                parts.Add(PickNoun(words));
                parts.Add(PickAdjective(words));
                parts.Add(PickNoun(words));
                return parts;
            }

            parts.Add(PickAdjective(words));
            parts.Add(PickAdjective(words));
            parts.Add(PickAdjective(words));
            parts.Add(PickNoun(words));
            return parts;
        }

        if (NextInt(1, 100) <= 60)
        {
            parts.Add(PickAdjective(words));
            parts.Add(PickNoun(words));
            parts.Add(PickAdjective(words));
            parts.Add(PickAdjective(words));
            parts.Add(PickNoun(words));
            return parts;
        }

        parts.Add(PickAdjective(words));
        parts.Add(PickAdjective(words));
        parts.Add(PickNoun(words));
        parts.Add(PickAdjective(words));
        parts.Add(PickNoun(words));
        return parts;
    }

    private static string ApplyFormatting(List<string> parts, CaseMode mode, string sep)
    {
        var cleaned = parts.Select(CleanToken).Where(x => x.Length > 0).ToList();
        if (cleaned.Count == 0) return "";

        if (mode == CaseMode.LowerCase)
        {
            return string.Join(sep, cleaned.Select(x => x.ToLowerInvariant()));
        }

        if (mode == CaseMode.PascalCase)
        {
            return string.Join(sep, cleaned.Select(ToTitle));
        }

        var first = cleaned[0].ToLowerInvariant();
        var rest = cleaned.Skip(1).Select(ToTitle);
        return string.Join(sep, new[] { first }.Concat(rest));
    }

    private static string PickSeparator(PluginSettings settings)
    {
        if (settings.SeparatorSelectionMode == ChoiceMode.Fixed)
            return settings.FixedSeparatorMode switch
            {
                SeparatorMode.Underscore => "_",
                SeparatorMode.Dot => ".",
                SeparatorMode.Dash => "-",
                _ => ""
            };

        var options = new List<SeparatorMode>();
        if (settings.RandomSepNone) options.Add(SeparatorMode.None);
        if (settings.RandomSepUnderscore) options.Add(SeparatorMode.Underscore);
        if (settings.RandomSepDot) options.Add(SeparatorMode.Dot);
        if (settings.RandomSepDash) options.Add(SeparatorMode.Dash);

        if (options.Count < 2)
        {
            options.Clear();
            options.Add(SeparatorMode.None);
            options.Add(SeparatorMode.Underscore);
        }

        var pick = options[NextInt(0, options.Count - 1)];
        return pick switch
        {
            SeparatorMode.Underscore => "_",
            SeparatorMode.Dot => ".",
            SeparatorMode.Dash => "-",
            _ => ""
        };
    }

    private static CaseMode PickCaseMode(PluginSettings settings)
    {
        if (settings.CaseSelectionMode == ChoiceMode.Fixed)
            return settings.FixedCaseMode;

        var options = new List<CaseMode>();
        if (settings.RandomCasePascal) options.Add(CaseMode.PascalCase);
        if (settings.RandomCaseCamel) options.Add(CaseMode.CamelCase);
        if (settings.RandomCaseLower) options.Add(CaseMode.LowerCase);

        if (options.Count < 2)
        {
            options.Clear();
            options.Add(CaseMode.PascalCase);
            options.Add(CaseMode.CamelCase);
        }

        return options[NextInt(0, options.Count - 1)];
    }

    private static int PickDigits(PluginSettings settings)
    {
        int min = Math.Clamp(settings.NumberDigitsMin, 1, 10);
        int max = Math.Clamp(settings.NumberDigitsMax, 1, 10);
        if (min > max) max = min;
        return NextInt(min, max);
    }

    private static string GenerateDigits(int len)
    {
        len = Math.Clamp(len, 1, 10);
        var chars = new char[len];
        for (int i = 0; i < len; i++)
            chars[i] = (char)('0' + NextInt(0, 9));
        return new string(chars);
    }

    private static string PickAdjective(WordsStore words)
    {
        var list = words.Adjectives ?? new List<string>();
        if (list.Count == 0) return "cool";
        return list[NextInt(0, list.Count - 1)];
    }

    private static string PickNoun(WordsStore words)
    {
        var list = words.Nouns ?? new List<string>();
        if (list.Count == 0) return "user";
        return list[NextInt(0, list.Count - 1)];
    }

    private static string CleanToken(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var trimmed = new string(s.Where(char.IsLetterOrDigit).ToArray());
        return trimmed;
    }

    private static string ToTitle(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var lower = s.ToLowerInvariant();
        if (lower.Length == 1) return lower.ToUpperInvariant();
        return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
    }

    private static int NextInt(int minInclusive, int maxInclusive)
    {
        if (minInclusive > maxInclusive)
            (minInclusive, maxInclusive) = (maxInclusive, minInclusive);

        return RandomNumberGenerator.GetInt32(minInclusive, maxInclusive + 1);
    }
}