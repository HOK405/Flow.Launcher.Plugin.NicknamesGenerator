using Flow.Launcher.Plugin.NicknamesGenerator.Configuration.Enums;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.NicknamesGenerator.Data;

public sealed class GeneratedNamePlan
{
    public List<string> Parts { get; set; } = new();
    public CaseMode CaseMode { get; set; }
    public string Separator { get; set; } = "";

    public bool UseNumbers { get; set; }
    public int DigitsLen { get; set; }
    public string LeftDigits { get; set; } = "";
    public string RightDigits { get; set; } = "";
    public NumberPosition NumberPosition { get; set; }
}