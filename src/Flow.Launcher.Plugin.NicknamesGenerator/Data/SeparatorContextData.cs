namespace Flow.Launcher.Plugin.NicknamesGenerator.Data;

public sealed class SeparatorContextData
{
    public GeneratedNamePlan Plan { get; set; } = new();
    public string Ending { get; set; } = "";
}