using System.Collections.Generic;

namespace Flow.Launcher.Plugin.NicknamesGenerator.Data;

public sealed class WordData
{
    public List<string> Adjectives { get; set; } = new();

    public List<string> Nouns { get; set; } = new();
}