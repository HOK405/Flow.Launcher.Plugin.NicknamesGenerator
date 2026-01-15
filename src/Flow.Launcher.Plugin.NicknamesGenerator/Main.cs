using Flow.Launcher.Plugin.NicknamesGenerator.Configuration;
using Flow.Launcher.Plugin.NicknamesGenerator.Configuration.Enums;
using Flow.Launcher.Plugin.NicknamesGenerator.Core;
using Flow.Launcher.Plugin.NicknamesGenerator.Data;
using Flow.Launcher.Plugin.UniqueNamesGenerator;
using Flow.Launcher.Plugin.UniqueNamesGenerator.Data;
using Flow.Launcher.Plugin.UniqueNamesGenerator.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.NicknamesGenerator;

public class Main : IPlugin, ISettingProvider, IContextMenu
{
    private const int PasteInitialDelayMs = 220;

    private const int MaxBatchCount = 500;

    private const int EndingsMaxCountShown = 50;

    private PluginInitContext _context = null!;

    private PluginSettings _settings = new();

    private WordsStore? _words;

    private string? _wordsError;

    private string _iconPath = "icon.png";

    public void Init(PluginInitContext context)
    {
        _context = context;
        _settings = _context.API.LoadSettingJsonStorage<PluginSettings>();
        _iconPath = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "icon.png");
        ReloadWords();
    }

    public Control CreateSettingPanel() => new SettingsView(_context, _settings, ReloadWords);

    public List<Result> Query(Query query)
    {
        var args = (query.Search ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (_settings.ShowReloadAction && args.Length == 1 && args[0].Equals("reload", StringComparison.OrdinalIgnoreCase))
        {
            return new List<Result>
            {
                new Result
                {
                    Title = "Reload dictionary", SubTitle = "Reload words.json from the plugin folder", IcoPath = _iconPath, Score = 1000, Action = _ =>
                    {
                        ReloadWords();
                        return true;
                    }
                }
            };
        }
        if (_words == null)
        {
            return new List<Result>
            {
                new Result
                {
                    Title = "Dictionary not loaded", SubTitle = _wordsError ?? "words.json could not be loaded. Make sure it exists in the plugin folder and is valid JSON.", IcoPath = _iconPath, Score = 1000, Action = _ => true
                }
            };
        }

        int? requestedCount = null;
        int? overrideParts = null;
        if (args.Length >= 1 && int.TryParse(args[0], out var c)) requestedCount = c;

        if (args.Length >= 2 && int.TryParse(args[1], out var p)) overrideParts = p;

        var results = new List<Result>();

        var plan = NickNameGenerator.GeneratePlan(_settings, _words, overrideParts);
        var baseName = NickNameGenerator.BuildFromPlan(plan);

        results.Add(new Result
        {
            Title = baseName,
            SubTitle = BuildEnterSubtitle(),
            IcoPath = _iconPath,
            Score = 1000,
            ContextData = new SeparatorContextData { Plan = plan, Ending = "" },
            Action = _ =>
            {
                ExecuteEnterAction(baseName);
                return true;
            }
        });
        if (_settings.UseCustomEndings && _settings.CustomEndings != null && _settings.CustomEndings.Count > 0)
        {
            var endings = _settings.CustomEndings.Select(x => (x ?? "").Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(EndingsMaxCountShown).ToList();
            int score = 990;
            foreach (var e in endings)
            {
                var ending = e;
                var full = NickNameGenerator.BuildFromPlan(plan, ending: ending);

                results.Add(new Result
                {
                    Title = full,
                    SubTitle = BuildEnterSubtitle() + " � " + ending,
                    IcoPath = _iconPath,
                    Score = score,
                    ContextData = new SeparatorContextData { Plan = plan, Ending = ending },
                    Action = _ =>
                    {
                        ExecuteEnterAction(full);
                        return true;
                    }
                });

                score = Math.Max(900, score - 2);
            }
        }
        if (_settings.ShowBatchAction)
        {
            int batchCount = requestedCount.HasValue ? Math.Clamp(requestedCount.Value, 1, MaxBatchCount) : Math.Clamp(_settings.DefaultBatchCount, 1, MaxBatchCount);
            if (batchCount > 1)
            {
                results.Add(new Result
                {
                    Title = _settings.EnterActionMode == EnterActionMode.CopyAndPaste ? "Copy and paste {batchCount} usernames" : "Copy {batchCount} usernames",
                    SubTitle = overrideParts.HasValue ? "Parts override: {Math.Clamp(overrideParts.Value, 1, MaxOverrideParts)}" : "Use plugin settings for parts",
                    IcoPath = _iconPath,
                    Score = 500,
                    Action = _ =>
                    {
                        var list = NickNameGenerator.GenerateBatch(_settings, _words, batchCount, overrideParts);
                        var text = _settings.OutputFormat == OutputFormat.CommaSpace ? string.Join(", ", list) : string.Join(Environment.NewLine, list);
                        ExecuteEnterAction(text);
                        return true;
                    }
                });
            }
        }
        if (_settings.ShowReloadAction)
        {
            results.Add(new Result
            {
                Title = "Reload dictionary",
                SubTitle = "Reload words.json from the plugin folder",
                IcoPath = _iconPath,
                Score = 100,
                Action = _ =>
                {
                    ReloadWords();
                    return true;
                }
            });
        }
        return results;
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult?.ContextData is not SeparatorContextData data)
            return new List<Result>();

        var plan = data.Plan;
        var ending = (data.Ending ?? "").Trim();

        var all = new (string Label, string Sep)[]
        {
            ("None", ""),
            ("_", "_"),
            (".", "."),
            ("-", "-"),
        };

        var list = new List<Result>();
        int score = 1000;

        foreach (var (label, sep) in all)
        {
            if (string.Equals(sep, plan.Separator ?? "", StringComparison.Ordinal))
                continue;

            var title = NickNameGenerator.BuildFromPlan(plan, separatorOverride: sep, ending: ending);

            list.Add(new Result
            {
                Title = title,
                SubTitle = string.IsNullOrEmpty(ending)
                    ? $"{BuildEnterSubtitle()} � Separator: {label}"
                    : $"{BuildEnterSubtitle()} � Separator: {label} � Ending: {ending}",
                IcoPath = _iconPath,
                Score = score--,
                Action = _ =>
                {
                    ExecuteEnterAction(title);
                    return true;
                }
            });
        }

        return list;
    }

    private string BuildEnterSubtitle()
    {
        return _settings.EnterActionMode == EnterActionMode.CopyAndPaste ? "Copy and Paste" : "Copy";
    }
    private void ExecuteEnterAction(string text)
    {
        if (_settings.EnterActionMode != EnterActionMode.CopyAndPaste)
        {
            _context.API.CopyToClipboard(text);
            return;
        }
        var blocked = PasteHelper.GetForegroundWindowHandle();
        _context.API.CopyToClipboard(text);
        _context.API.HideMainWindow();
        PasteHelper.PasteFromClipboard(PasteInitialDelayMs, blocked);
    }
    private void ReloadWords()
    {
        try
        {
            var dir = _context.CurrentPluginMetadata.PluginDirectory;
            var file = (_settings.WordsFileName ?? "words.json").Trim();
            if (file.Length == 0) file = "words.json";
            var path = Path.Combine(dir, file);
            _words = WordsStore.LoadFromFile(path);
            _wordsError = null;
        }
        catch (Exception ex)
        {
            _words = null;
            _wordsError = ex.Message;
        }
    }
}