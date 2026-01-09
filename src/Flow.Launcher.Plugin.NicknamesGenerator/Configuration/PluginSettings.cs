using Flow.Launcher.Plugin.NicknamesGenerator.Configuration.Enums;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.NicknamesGenerator.Configuration;

public class PluginSettings
{
    public bool UseCustomEndings { get; set; } = false;
    public List<string> CustomEndings { get; set; } = new();

    public PartsMode PartsMode { get; set; } = PartsMode.Range;
    public int FixedParts { get; set; } = 2;
    public int MinParts { get; set; } = 1;
    public int MaxParts { get; set; } = 2;

    public bool AllowDoubleNounTail { get; set; } = true;
    public int DoubleNounTailChancePercent { get; set; } = 50;

    public ChoiceMode CaseSelectionMode { get; set; } = ChoiceMode.Random;
    public CaseMode FixedCaseMode { get; set; } = CaseMode.PascalCase;
    public bool RandomCasePascal { get; set; } = true;
    public bool RandomCaseCamel { get; set; } = true;
    public bool RandomCaseLower { get; set; } = true;

    public ChoiceMode SeparatorSelectionMode { get; set; } = ChoiceMode.Random;
    public SeparatorMode FixedSeparatorMode { get; set; } = SeparatorMode.None;
    public bool RandomSepNone { get; set; } = true;
    public bool RandomSepUnderscore { get; set; } = true;
    public bool RandomSepDot { get; set; } = true;
    public bool RandomSepDash { get; set; } = true;

    public bool UseNumbers { get; set; } = true;
    public int NumberDigitsMin { get; set; } = 3;
    public int NumberDigitsMax { get; set; } = 4;
    public NumberPosition NumberPosition { get; set; } = NumberPosition.Suffix;

    public bool ShowBatchAction { get; set; } = false;
    public int DefaultBatchCount { get; set; } = 20;
    public bool EnsureUniqueInBatch { get; set; } = true;
    public OutputFormat OutputFormat { get; set; } = OutputFormat.NewLines;

    public bool ShowReloadAction { get; set; } = false;
    public string WordsFileName { get; set; } = "words.json";

    public EnterActionMode EnterActionMode { get; set; } = EnterActionMode.CopyAndPaste;
}