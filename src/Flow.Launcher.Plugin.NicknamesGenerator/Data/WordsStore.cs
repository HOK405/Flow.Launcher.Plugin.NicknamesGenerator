using Flow.Launcher.Plugin.NicknamesGenerator.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Flow.Launcher.Plugin.UniqueNamesGenerator.Data;

public sealed class WordsStore
{
    public IReadOnlyList<string> Adjectives { get; }
    public IReadOnlyList<string> Nouns { get; }

    private WordsStore(IReadOnlyList<string> adjectives, IReadOnlyList<string> nouns)
    {
        Adjectives = adjectives;
        Nouns = nouns;
    }

    public static WordsStore LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Words file not found.", path);

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<WordData>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (data == null)
            throw new InvalidOperationException("Words file is not a valid JSON document.");

        static List<string> Normalize(IEnumerable<string> src) =>
            src.Select(s => (s ?? "").Trim())
               .Where(s => s.Length > 0)
               .Distinct(StringComparer.OrdinalIgnoreCase)
               .ToList();

        var adjectives = Normalize(data.Adjectives);
        var nouns = Normalize(data.Nouns);

        if (adjectives.Count == 0)
            throw new InvalidOperationException("Words file contains no adjectives.");
        if (nouns.Count == 0)
            throw new InvalidOperationException("Words file contains no nouns.");

        return new WordsStore(adjectives, nouns);
    }
}