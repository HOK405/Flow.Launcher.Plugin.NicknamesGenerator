using Flow.Launcher.Plugin.NicknamesGenerator.Configuration;
using Flow.Launcher.Plugin.NicknamesGenerator.Configuration.Enums;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Flow.Launcher.Plugin.UniqueNamesGenerator;

public partial class SettingsView : UserControl
{
    private const double TwoColumnsMaxWidth = 1140;
    private const int MaxEndingLength = 100;
    private const int EndingsPageSize = 5;
    private const string DefaultExampleEnding = "@example.com";

    private readonly PluginInitContext _context;
    private readonly PluginSettings _settings;
    private readonly Action _reloadAction;

    private readonly ObservableCollection<string> _endings = new();
    private readonly ObservableCollection<EndingRow> _endingsPage = new();

    private int _pageIndex;
    private int? _editIndex;
    private bool _twoColumns;

    public ImageSource EditIcon { get; }
    public ImageSource DeleteIcon { get; }
    public ImageSource ArrowIcon { get; }

    private sealed class EndingRow
    {
        public string No { get; }
        public string Value { get; }
        public int Index { get; }

        public EndingRow(string no, string value, int index)
        {
            No = no;
            Value = value;
            Index = index;
        }
    }

    public SettingsView(PluginInitContext context, PluginSettings settings, Action reloadAction)
    {
        InitializeComponent();

        _context = context;
        _settings = settings;
        _reloadAction = reloadAction;

        DataContext = this;

        var dir = _context.CurrentPluginMetadata.PluginDirectory;

        EditIcon = LoadImageOrFallback(Path.Combine(dir, "assets", "edit.png"));
        DeleteIcon = LoadImageOrFallback(Path.Combine(dir, "assets", "delete.png"));
        ArrowIcon = LoadImageOrFallback(Path.Combine(dir, "assets", "arrow.png"));

        NumberPositionCombo.ItemsSource = new[] { "Front", "End", "Both" };
        OutputFormatCombo.ItemsSource = new[] { "New lines", "Comma + space" };

        EndingsListView.ItemsSource = _endingsPage;

        LoadToUI();
        WireEvents();

        FixPartsRange();
        FixDigitRange();

        UpdateLabels();
        UpdateEnabledStates();
        UpdateFormattingModeLabels();
        RebuildEndingsPage();

        Loaded += (_, __) => ApplyResponsiveLayout();
        SizeChanged += (_, __) => ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        var w = ActualWidth;
        if (double.IsNaN(w) || w <= 0) return;

        var wantTwo = w <= TwoColumnsMaxWidth;
        if (wantTwo == _twoColumns) return;

        _twoColumns = wantTwo;

        if (_twoColumns)
        {
            Col2.Width = new GridLength(0);
            ColCPanel.Visibility = Visibility.Collapsed;

            if (EnterActionGroup.Parent is Panel p1) p1.Children.Remove(EnterActionGroup);
            if (DictionaryGroup.Parent is Panel p2) p2.Children.Remove(DictionaryGroup);
            if (BatchGroup.Parent is Panel p3) p3.Children.Remove(BatchGroup);

            ColBExtra.Children.Clear();
            ColBExtra.Children.Add(EnterActionGroup);
            ColBExtra.Children.Add(DictionaryGroup);
            ColBExtra.Children.Add(BatchGroup);
        }
        else
        {
            Col2.Width = new GridLength(1, GridUnitType.Star);
            ColCPanel.Visibility = Visibility.Visible;

            if (EnterActionGroup.Parent is Panel p1) p1.Children.Remove(EnterActionGroup);
            if (DictionaryGroup.Parent is Panel p2) p2.Children.Remove(DictionaryGroup);
            if (BatchGroup.Parent is Panel p3) p3.Children.Remove(BatchGroup);

            ColCPanel.Children.Clear();
            ColCPanel.Children.Add(EnterActionGroup);
            ColCPanel.Children.Add(DictionaryGroup);
            ColCPanel.Children.Add(BatchGroup);

            ColBExtra.Children.Clear();
        }
    }

    private void WireEvents()
    {
        UseEndingsBox.Click += (_, __) => { UpdateEnabledStates(); Save(); };
        AddEndingBtn.Click += (_, __) => CommitEndingFromInput();

        NewEndingBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitEndingFromInput();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
        };

        PrevPageBtn.Click += (_, __) => { _pageIndex--; RebuildEndingsPage(); };
        NextPageBtn.Click += (_, __) => { _pageIndex++; RebuildEndingsPage(); };

        ReloadBtn.Click += (_, __) =>
        {
            Save();
            _reloadAction();
        };

        WordsFileBox.LostFocus += (_, __) => Save();
        ShowReloadActionBox.Click += (_, __) => Save();

        EnterCopyRadio.Click += (_, __) => Save();
        EnterCopyPasteRadio.Click += (_, __) => Save();

        MinPartsSlider.ValueChanged += (_, __) => { FixPartsRange(); UpdateLabels(); };
        MaxPartsSlider.ValueChanged += (_, __) => { FixPartsRange(); UpdateLabels(); };
        MinPartsSlider.PreviewMouseLeftButtonUp += (_, __) => Save();
        MaxPartsSlider.PreviewMouseLeftButtonUp += (_, __) => Save();

        DoubleNounTailBox.Click += (_, __) => { UpdateEnabledStates(); Save(); };
        DoubleNounChanceSlider.ValueChanged += (_, __) => UpdateLabels();
        DoubleNounChanceSlider.PreviewMouseLeftButtonUp += (_, __) => Save();

        CaseRandPascal.Click += (_, __) => { EnsureAtLeastOneCase(); UpdateFormattingModeLabels(); Save(); };
        CaseRandCamel.Click += (_, __) => { EnsureAtLeastOneCase(); UpdateFormattingModeLabels(); Save(); };
        CaseRandLower.Click += (_, __) => { EnsureAtLeastOneCase(); UpdateFormattingModeLabels(); Save(); };

        SepRandNone.Click += (_, __) => { EnsureAtLeastOneSep(); UpdateFormattingModeLabels(); Save(); };
        SepRandUnderscore.Click += (_, __) => { EnsureAtLeastOneSep(); UpdateFormattingModeLabels(); Save(); };
        SepRandDot.Click += (_, __) => { EnsureAtLeastOneSep(); UpdateFormattingModeLabels(); Save(); };
        SepRandDash.Click += (_, __) => { EnsureAtLeastOneSep(); UpdateFormattingModeLabels(); Save(); };

        UseNumbersBox.Click += (_, __) => { UpdateEnabledStates(); Save(); };

        MinDigitsSlider.ValueChanged += (_, __) => { FixDigitRange(); UpdateLabels(); };
        MaxDigitsSlider.ValueChanged += (_, __) => { FixDigitRange(); UpdateLabels(); };
        MinDigitsSlider.PreviewMouseLeftButtonUp += (_, __) => Save();
        MaxDigitsSlider.PreviewMouseLeftButtonUp += (_, __) => Save();

        NumberPositionCombo.SelectionChanged += (_, __) => Save();

        ShowBatchActionBox.Click += (_, __) => { UpdateEnabledStates(); Save(); };
        BatchCountBox.LostFocus += (_, __) => Save();
        UniqueBatchBox.Click += (_, __) => Save();
        OutputFormatCombo.SelectionChanged += (_, __) => Save();
    }

    public void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var src = e.OriginalSource as DependencyObject;
        if (src != null && FindAncestor<Slider>(src) != null)
            return;

        e.Handled = true;

        if (Parent is not UIElement parent) return;

        var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = this
        };

        parent.RaiseEvent(args);
    }

    public void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        e.Handled = true;
    }

    public void OnEditEndingClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b) return;
        if (b.Tag is not int idx) return;
        if (idx < 0 || idx >= _endings.Count) return;

        _editIndex = idx;
        JumpToIndex(idx);

        NewEndingBox.Text = _endings[idx];
        AddEndingBtn.Content = "Save";
        NewEndingBox.Focus();
        NewEndingBox.SelectAll();
    }

    public void OnRemoveEndingClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b) return;
        if (b.Tag is not int idx) return;
        if (idx < 0 || idx >= _endings.Count) return;

        _endings.RemoveAt(idx);

        if (_editIndex.HasValue)
        {
            if (_editIndex.Value == idx) CancelEdit();
            else if (_editIndex.Value > idx) _editIndex -= 1;
        }

        SaveEndingsOnly();
        ClampPage();
        RebuildEndingsPage();
    }

    private void CommitEndingFromInput()
    {
        var v = (NewEndingBox.Text ?? "").Trim();
        if (v.Length == 0)
        {
            CancelEdit();
            return;
        }

        if (v.Length > MaxEndingLength)
            v = v.Substring(0, MaxEndingLength);

        if (_editIndex.HasValue && _editIndex.Value >= 0 && _editIndex.Value < _endings.Count)
        {
            var editIdx = _editIndex.Value;

            var dup = FindIndexIgnoreCase(v, editIdx);
            if (dup.HasValue)
            {
                JumpToIndex(dup.Value);
                CancelEdit();
                NewEndingBox.Text = "";
                return;
            }

            _endings[editIdx] = v;
            SaveEndingsOnly();
            JumpToIndex(editIdx);
            RebuildEndingsPage();
            CancelEdit();
            NewEndingBox.Text = "";
            return;
        }

        var existing = FindIndexIgnoreCase(v, null);
        if (existing.HasValue)
        {
            JumpToIndex(existing.Value);
            RebuildEndingsPage();
            NewEndingBox.Text = "";
            return;
        }

        _endings.Add(v);
        SaveEndingsOnly();

        var newIdx = _endings.Count - 1;
        JumpToIndex(newIdx);
        RebuildEndingsPage();
        NewEndingBox.Text = "";
    }

    private void SaveEndingsOnly()
    {
        _settings.UseCustomEndings = UseEndingsBox.IsChecked == true;
        _settings.CustomEndings = _endings
            .Select(x => (x ?? "").Trim())
            .Where(x => x.Length > 0)
            .Select(x => x.Length > MaxEndingLength ? x.Substring(0, MaxEndingLength) : x)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _context.API.SaveSettingJsonStorage<PluginSettings>();
    }

    private void CancelEdit()
    {
        _editIndex = null;
        AddEndingBtn.Content = "Add";
    }

    private int? FindIndexIgnoreCase(string value, int? exceptIndex)
    {
        for (int i = 0; i < _endings.Count; i++)
        {
            if (exceptIndex.HasValue && i == exceptIndex.Value) continue;
            if (string.Equals(_endings[i], value, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return null;
    }

    private void JumpToIndex(int idx)
    {
        if (idx < 0) idx = 0;
        _pageIndex = idx / EndingsPageSize;
        ClampPage();
    }

    private int TotalPages()
    {
        if (_endings.Count == 0) return 1;
        var pages = (int)Math.Ceiling(_endings.Count / (double)EndingsPageSize);
        return Math.Max(1, pages);
    }

    private void ClampPage()
    {
        var pages = TotalPages();
        if (_pageIndex < 0) _pageIndex = 0;
        if (_pageIndex > pages - 1) _pageIndex = pages - 1;
    }

    private void RebuildEndingsPage()
    {
        ClampPage();
        _endingsPage.Clear();

        PagerPanel.Visibility = Visibility.Visible;

        if (_endings.Count == 0)
        {
            EndingsListView.Visibility = Visibility.Collapsed;
            NoEndingsText.Visibility = Visibility.Visible;

            PageText.Text = "Page 1 / 1";
            PrevPageBtn.IsEnabled = false;
            NextPageBtn.IsEnabled = false;
            return;
        }

        EndingsListView.Visibility = Visibility.Visible;
        NoEndingsText.Visibility = Visibility.Collapsed;

        var pages = TotalPages();
        PageText.Text = $"Page {_pageIndex + 1} / {pages}";

        PrevPageBtn.IsEnabled = _pageIndex > 0;
        NextPageBtn.IsEnabled = _pageIndex < pages - 1;

        var start = _pageIndex * EndingsPageSize;
        var slice = _endings
            .Select((val, idx) => new { val, idx })
            .Skip(start)
            .Take(EndingsPageSize)
            .ToList();

        foreach (var it in slice)
            _endingsPage.Add(new EndingRow((it.idx + 1).ToString(), it.val, it.idx));
    }

    private void LoadToUI()
    {
        UseEndingsBox.IsChecked = _settings.UseCustomEndings;

        _endings.Clear();
        if (_settings.CustomEndings != null)
        {
            foreach (var e in _settings.CustomEndings
                         .Select(x => (x ?? "").Trim())
                         .Where(x => x.Length > 0)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                _endings.Add(e.Length > MaxEndingLength ? e.Substring(0, MaxEndingLength) : e);
            }
        }

        if (_endings.Count == 0)
        {
            _endings.Add(DefaultExampleEnding);
            _settings.CustomEndings = new() { DefaultExampleEnding };
            _context.API.SaveSettingJsonStorage<PluginSettings>();
        }

        EnterCopyRadio.IsChecked = _settings.EnterActionMode != EnterActionMode.CopyAndPaste;
        EnterCopyPasteRadio.IsChecked = _settings.EnterActionMode == EnterActionMode.CopyAndPaste;

        WordsFileBox.Text = _settings.WordsFileName ?? "words.json";
        ShowReloadActionBox.IsChecked = _settings.ShowReloadAction;

        if (_settings.PartsMode == PartsMode.Fixed)
        {
            var v = ClampInt(_settings.FixedParts, 1, 5);
            MinPartsSlider.Value = v;
            MaxPartsSlider.Value = v;
        }
        else
        {
            MinPartsSlider.Value = ClampInt(_settings.MinParts, 1, 5);
            MaxPartsSlider.Value = ClampInt(_settings.MaxParts, 1, 5);
        }

        DoubleNounTailBox.IsChecked = _settings.AllowDoubleNounTail;
        DoubleNounChanceSlider.Value = ClampInt(_settings.DoubleNounTailChancePercent, 10, 100);

        ApplyCaseFromSettings();
        ApplySeparatorFromSettings();

        EnsureAtLeastOneCase();
        EnsureAtLeastOneSep();

        UseNumbersBox.IsChecked = _settings.UseNumbers;
        MinDigitsSlider.Value = ClampInt(_settings.NumberDigitsMin, 1, 10);
        MaxDigitsSlider.Value = ClampInt(_settings.NumberDigitsMax, 1, 10);

        NumberPositionCombo.SelectedIndex = _settings.NumberPosition switch
        {
            NumberPosition.Prefix => 0,
            NumberPosition.Suffix => 1,
            _ => 2
        };

        ShowBatchActionBox.IsChecked = _settings.ShowBatchAction;
        BatchCountBox.Text = _settings.DefaultBatchCount.ToString();
        UniqueBatchBox.IsChecked = _settings.EnsureUniqueInBatch;
        OutputFormatCombo.SelectedIndex = (int)_settings.OutputFormat;

        _pageIndex = 0;
        _editIndex = null;
        AddEndingBtn.Content = "Add";
    }

    private void ApplyCaseFromSettings()
    {
        if (_settings.CaseSelectionMode == ChoiceMode.Fixed)
        {
            CaseRandPascal.IsChecked = _settings.FixedCaseMode == CaseMode.PascalCase;
            CaseRandCamel.IsChecked = _settings.FixedCaseMode == CaseMode.CamelCase;
            CaseRandLower.IsChecked = _settings.FixedCaseMode == CaseMode.LowerCase;
            return;
        }

        CaseRandPascal.IsChecked = _settings.RandomCasePascal;
        CaseRandCamel.IsChecked = _settings.RandomCaseCamel;
        CaseRandLower.IsChecked = _settings.RandomCaseLower;
    }

    private void ApplySeparatorFromSettings()
    {
        if (_settings.SeparatorSelectionMode == ChoiceMode.Fixed)
        {
            SepRandNone.IsChecked = _settings.FixedSeparatorMode == SeparatorMode.None;
            SepRandUnderscore.IsChecked = _settings.FixedSeparatorMode == SeparatorMode.Underscore;
            SepRandDot.IsChecked = _settings.FixedSeparatorMode == SeparatorMode.Dot;
            SepRandDash.IsChecked = _settings.FixedSeparatorMode == SeparatorMode.Dash;
            return;
        }

        SepRandNone.IsChecked = _settings.RandomSepNone;
        SepRandUnderscore.IsChecked = _settings.RandomSepUnderscore;
        SepRandDot.IsChecked = _settings.RandomSepDot;
        SepRandDash.IsChecked = _settings.RandomSepDash;
    }

    private void UpdateEnabledStates()
    {
        EndingsPanel.IsEnabled = UseEndingsBox.IsChecked == true;
        ChancePanel.IsEnabled = DoubleNounTailBox.IsChecked == true;

        NumbersPanel.Visibility = UseNumbersBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        BatchPanel.Visibility = ShowBatchActionBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EnsureAtLeastOneCase()
    {
        var c = CountTrue(CaseRandPascal.IsChecked, CaseRandCamel.IsChecked, CaseRandLower.IsChecked);
        if (c > 0) return;
        CaseRandPascal.IsChecked = true;
    }

    private void EnsureAtLeastOneSep()
    {
        var c = CountTrue(SepRandNone.IsChecked, SepRandUnderscore.IsChecked, SepRandDot.IsChecked, SepRandDash.IsChecked);
        if (c > 0) return;
        SepRandNone.IsChecked = true;
    }

    private void UpdateFormattingModeLabels()
    {
        var caseCount = CountTrue(CaseRandPascal.IsChecked, CaseRandCamel.IsChecked, CaseRandLower.IsChecked);
        CaseModeText.Text = caseCount <= 1 ? "FIXED" : "RANDOM";

        var sepCount = CountTrue(SepRandNone.IsChecked, SepRandUnderscore.IsChecked, SepRandDot.IsChecked, SepRandDash.IsChecked);
        SepModeText.Text = sepCount <= 1 ? "FIXED" : "RANDOM";
    }

    private void FixPartsRange()
    {
        var min = ClampInt((int)Math.Round(MinPartsSlider.Value), 1, 5);
        var max = ClampInt((int)Math.Round(MaxPartsSlider.Value), 1, 5);

        if (min > max)
        {
            max = min;
            MaxPartsSlider.Value = max;
        }

        if (max < min)
        {
            min = max;
            MinPartsSlider.Value = min;
        }
    }

    private void FixDigitRange()
    {
        var min = ClampInt((int)Math.Round(MinDigitsSlider.Value), 1, 10);
        var max = ClampInt((int)Math.Round(MaxDigitsSlider.Value), 1, 10);

        if (min > max)
        {
            max = min;
            MaxDigitsSlider.Value = max;
        }

        if (max < min)
        {
            min = max;
            MinDigitsSlider.Value = min;
        }
    }

    private void UpdateLabels()
    {
        MinPartsValue.Text = ((int)Math.Round(MinPartsSlider.Value)).ToString();
        MaxPartsValue.Text = ((int)Math.Round(MaxPartsSlider.Value)).ToString();

        MinDigitsValue.Text = ((int)Math.Round(MinDigitsSlider.Value)).ToString();
        MaxDigitsValue.Text = ((int)Math.Round(MaxDigitsSlider.Value)).ToString();

        DoubleNounChanceValue.Text = ((int)Math.Round(DoubleNounChanceSlider.Value)).ToString();
    }

    private void Save()
    {
        SaveEndingsOnly();

        _settings.EnterActionMode = EnterCopyPasteRadio.IsChecked == true ? EnterActionMode.CopyAndPaste : EnterActionMode.CopyOnly;

        _settings.WordsFileName = (WordsFileBox.Text ?? "words.json").Trim();
        if (_settings.WordsFileName.Length == 0) _settings.WordsFileName = "words.json";
        _settings.ShowReloadAction = ShowReloadActionBox.IsChecked == true;

        FixPartsRange();
        var partsMin = ClampInt((int)Math.Round(MinPartsSlider.Value), 1, 5);
        var partsMax = ClampInt((int)Math.Round(MaxPartsSlider.Value), 1, 5);

        _settings.MinParts = partsMin;
        _settings.MaxParts = partsMax;

        if (partsMin == partsMax)
        {
            _settings.PartsMode = PartsMode.Fixed;
            _settings.FixedParts = partsMin;
        }
        else
        {
            _settings.PartsMode = PartsMode.Range;
            _settings.FixedParts = partsMin;
        }

        _settings.AllowDoubleNounTail = DoubleNounTailBox.IsChecked == true;
        _settings.DoubleNounTailChancePercent = ClampInt((int)Math.Round(DoubleNounChanceSlider.Value), 10, 100);

        EnsureAtLeastOneCase();
        var caseCount = CountTrue(CaseRandPascal.IsChecked, CaseRandCamel.IsChecked, CaseRandLower.IsChecked);

        if (caseCount <= 1)
        {
            _settings.CaseSelectionMode = ChoiceMode.Fixed;

            if (CaseRandCamel.IsChecked == true) _settings.FixedCaseMode = CaseMode.CamelCase;
            else if (CaseRandLower.IsChecked == true) _settings.FixedCaseMode = CaseMode.LowerCase;
            else _settings.FixedCaseMode = CaseMode.PascalCase;

            _settings.RandomCasePascal = _settings.FixedCaseMode == CaseMode.PascalCase;
            _settings.RandomCaseCamel = _settings.FixedCaseMode == CaseMode.CamelCase;
            _settings.RandomCaseLower = _settings.FixedCaseMode == CaseMode.LowerCase;
        }
        else
        {
            _settings.CaseSelectionMode = ChoiceMode.Random;
            _settings.RandomCasePascal = CaseRandPascal.IsChecked == true;
            _settings.RandomCaseCamel = CaseRandCamel.IsChecked == true;
            _settings.RandomCaseLower = CaseRandLower.IsChecked == true;
        }

        EnsureAtLeastOneSep();
        var sepCount = CountTrue(SepRandNone.IsChecked, SepRandUnderscore.IsChecked, SepRandDot.IsChecked, SepRandDash.IsChecked);

        if (sepCount <= 1)
        {
            _settings.SeparatorSelectionMode = ChoiceMode.Fixed;

            if (SepRandUnderscore.IsChecked == true) _settings.FixedSeparatorMode = SeparatorMode.Underscore;
            else if (SepRandDot.IsChecked == true) _settings.FixedSeparatorMode = SeparatorMode.Dot;
            else if (SepRandDash.IsChecked == true) _settings.FixedSeparatorMode = SeparatorMode.Dash;
            else _settings.FixedSeparatorMode = SeparatorMode.None;

            _settings.RandomSepNone = _settings.FixedSeparatorMode == SeparatorMode.None;
            _settings.RandomSepUnderscore = _settings.FixedSeparatorMode == SeparatorMode.Underscore;
            _settings.RandomSepDot = _settings.FixedSeparatorMode == SeparatorMode.Dot;
            _settings.RandomSepDash = _settings.FixedSeparatorMode == SeparatorMode.Dash;
        }
        else
        {
            _settings.SeparatorSelectionMode = ChoiceMode.Random;
            _settings.RandomSepNone = SepRandNone.IsChecked == true;
            _settings.RandomSepUnderscore = SepRandUnderscore.IsChecked == true;
            _settings.RandomSepDot = SepRandDot.IsChecked == true;
            _settings.RandomSepDash = SepRandDash.IsChecked == true;
        }

        _settings.UseNumbers = UseNumbersBox.IsChecked == true;
        FixDigitRange();
        _settings.NumberDigitsMin = ClampInt((int)Math.Round(MinDigitsSlider.Value), 1, 10);
        _settings.NumberDigitsMax = ClampInt((int)Math.Round(MaxDigitsSlider.Value), 1, 10);

        _settings.NumberPosition = NumberPositionCombo.SelectedIndex switch
        {
            0 => NumberPosition.Prefix,
            1 => NumberPosition.Suffix,
            _ => NumberPosition.Both
        };

        _settings.ShowBatchAction = ShowBatchActionBox.IsChecked == true;
        _settings.DefaultBatchCount = ClampInt(BatchCountBox.Text, 1, 500, 20);
        BatchCountBox.Text = _settings.DefaultBatchCount.ToString();
        _settings.EnsureUniqueInBatch = UniqueBatchBox.IsChecked == true;
        _settings.OutputFormat = (OutputFormat)Math.Clamp(OutputFormatCombo.SelectedIndex, 0, 1);

        UpdateLabels();
        UpdateEnabledStates();
        UpdateFormattingModeLabels();
        RebuildEndingsPage();
        _context.API.SaveSettingJsonStorage<PluginSettings>();
    }

    private static ImageSource LoadImageOrFallback(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new DrawingImage();

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return new DrawingImage();
        }
    }

    private static int CountTrue(params bool?[] values) => values.Count(v => v == true);

    private static int ClampInt(string text, int min, int max, int fallback)
    {
        if (!int.TryParse((text ?? "").Trim(), out var v))
            v = fallback;
        return Math.Clamp(v, min, max);
    }

    private static int ClampInt(int v, int min, int max) => Math.Clamp(v, min, max);

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }
}