using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RoboDock.Core;
using Lang = RoboDock.Core.Language;

namespace RoboDock.App;

public partial class MainWindow : Window
{
    /// <summary>结论卡的三种状态。切开语言时按状态重绘，而不是把文本写死。</summary>
    private enum CardState
    {
        Hidden,
        Preview,
        Verdict,
    }

    private readonly RobocopyRunner _runner = new();

    private CancellationTokenSource? _cts;
    private bool _initializing = true;
    private bool _busy;
    private bool _previewed;
    private int? _expectedFiles;

    // 结论卡/状态栏的"可重绘状态"：切语言时从这里重建文本
    private CardState _cardState = CardState.Hidden;
    private PreviewResult? _lastPreview;
    private RobocopyRunResult? _lastRun;
    private string? _statusKey;
    private string? _statusPath;

    public MainWindow()
    {
        InitializeComponent();

        CmbLanguage.SelectedIndex = UiText.Language == Lang.Zh ? 0 : 1;
        CmbTemplate.SelectedIndex = 0;
        CmbMultiThread.SelectedIndex = 2;
        CmbRetry.SelectedIndex = 1;
        RbBackup.IsChecked = true;

        ApplyTexts();
        _initializing = false;
        RefreshCommand();
    }

    // ---------- 状态 ----------

    private CopyStrategy SelectedStrategy
    {
        get
        {
            if (RbMirror.IsChecked == true) return CopyStrategy.KeepIdentical;
            if (RbUpdate.IsChecked == true) return CopyStrategy.UpdateChanged;
            if (RbFill.IsChecked == true) return CopyStrategy.FillMissing;
            return CopyStrategy.BackupOver;
        }
    }

    private RobocopyOptions BuildOptions() => new()
    {
        Source = PathValidator.Normalize(TxtSource.Text),
        Destination = PathValidator.Normalize(TxtDestination.Text),
        Strategy = SelectedStrategy,
        MultiThread = CmbMultiThread.SelectedIndex > 0 ? int.Parse(SelectedComboText(CmbMultiThread)) : null,
        Restartable = ChkRestartable.IsChecked == true,
        UnbufferedIo = ChkUnbuffered.IsChecked == true,
        BackupMode = ChkBackupMode.IsChecked == true,
        RetryCount = int.TryParse(SelectedComboText(CmbRetry), out var retry) ? retry : 2,
        RetryWaitSeconds = 2,
        ExcludedFiles = TxtExclude.Text
            .Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        LogFile = string.IsNullOrWhiteSpace(TxtLogFile.Text) ? null : TxtLogFile.Text.Trim(),
    };

    private static string SelectedComboText(ComboBox combo)
        => (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

    // ---------- 文本与刷新 ----------

    private void ApplyTexts()
    {
        var t = UiText.T;
        Title = "RoboDock";
        LblSource.Text = t("Source");
        LblDestination.Text = t("Destination");
        BtnBrowseSource.Content = t("Browse");
        BtnBrowseDestination.Content = t("Browse");
        LblTemplates.Text = t("Templates");
        LblWhatToDo.Text = t("WhatToDo");
        TxtDanger.Text = t("DangerMirror");
        ExpAdvanced.Header = t("Advanced");
        ChkRestartable.Content = t("Restartable");
        ChkUnbuffered.Content = t("Unbuffered");
        ChkBackupMode.Content = t("BackupMode");
        LblMultiThread.Text = t("MultiThread");
        LblRetry.Text = t("Retry");
        LblExclude.Text = t("Exclude");
        LblLogFile.Text = t("LogFile");
        TxtExclude.ToolTip = t("ExcludeTip");
        ItemMtOff.Content = t("Off");
        LblCommand.Text = t("Command");
        BtnCopyCommand.Content = t("Copy");
        BtnPreview.Content = t("Preview");
        BtnStart.Content = t("Start");
        BtnCancel.Content = t("Cancel");
        LblLanguage.Text = UiText.Language == Lang.Zh ? "语言" : "Language";

        RbBackup.Content = t("StrategyBackup");
        RbMirror.Content = t("StrategyMirror");
        RbUpdate.Content = t("StrategyUpdate");
        RbFill.Content = t("StrategyFill");

        if (CmbTemplate.Items.Count == 5)
        {
            ((ComboBoxItem)CmbTemplate.Items[0]).Content = t("TemplateNone");
            ((ComboBoxItem)CmbTemplate.Items[1]).Content = t("TemplatePhotos");
            ((ComboBoxItem)CmbTemplate.Items[2]).Content = t("TemplateNas");
            ((ComboBoxItem)CmbTemplate.Items[3]).Content = t("TemplateLarge");
            ((ComboBoxItem)CmbTemplate.Items[4]).Content = t("TemplateBulk");
        }
    }

    private void RefreshCommand()
    {
        TxtCommand.Text = CommandBuilder.BuildDisplayCommand(BuildOptions());
        TxtDanger.Visibility = SelectedStrategy.DeletesDestinationFiles() ? Visibility.Visible : Visibility.Collapsed;
    }

    private void InvalidatePreview()
    {
        _previewed = false;
        _expectedFiles = null;
        _cardState = CardState.Hidden;
        _lastPreview = null;
        RenderCard();
        if (!_busy) BtnStart.IsEnabled = false;
    }

    // ---------- 结论卡与状态栏（切语言时可重绘） ----------

    private void RenderCard()
    {
        switch (_cardState)
        {
            case CardState.Preview when _lastPreview is not null:
                TxtConclusionTitle.Text = UiText.T("WillHappen");
                TxtConclusionBody.Text = BuildPreviewBody(_lastPreview);
                TxtConclusionBody.Foreground = _lastPreview.HasDeletions
                    ? System.Windows.Media.Brushes.Firebrick
                    : System.Windows.Media.Brushes.Black;
                CardConclusion.Visibility = Visibility.Visible;
                break;

            case CardState.Verdict when _lastRun is not null:
            {
                var verdict = ExitCodeTranslator.Translate(_lastRun.ExitCode, UiText.Language);
                TxtConclusionTitle.Text = $"{UiText.T("ExitCode")} {_lastRun.ExitCode} — {verdict.Title}";
                TxtConclusionBody.Text = verdict.Detail + Environment.NewLine +
                                         UiText.T("Duration") + _lastRun.Duration.ToString(@"hh\:mm\:ss");
                TxtConclusionBody.Foreground = verdict.IsSuccess
                    ? System.Windows.Media.Brushes.DarkGreen
                    : System.Windows.Media.Brushes.Firebrick;
                CardConclusion.Visibility = Visibility.Visible;
                break;
            }

            default:
                CardConclusion.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private string BuildPreviewBody(PreviewResult preview)
    {
        var copy = FormatCount(preview.FileCountToCopy, preview.BytesToCopy);
        var create = FormatCount(preview.DirectoryCountToCreate, 0, isDirectory: true);
        var delete = FormatCount(preview.FileCountToDelete, preview.BytesToDelete);

        return string.Join(Environment.NewLine,
            $"  {UiText.T("CardCopy")}    {copy}",
            $"  {UiText.T("CardCreate")}    {create}",
            $"  {UiText.T("CardDelete")}    {delete}");
    }

    private string FormatCount(int count, long bytes, bool isDirectory = false)
    {
        var unit = " " + UiText.T(isDirectory ? "CardDirs" : "CardFiles");
        var size = isDirectory ? "" : "  " + FormatBytes(bytes);
        return $"{count:N0}{unit}{size}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return index == 0 ? $"{bytes} B" : $"{value:0.0} {units[index]}";
    }

    private void RenderStatus()
    {
        TxtProgress.Text = _statusPath ?? (_statusKey is null ? "" : UiText.T(_statusKey));
    }

    private void SetStatus(string? key, string? path = null)
    {
        _statusKey = key;
        _statusPath = path;
        RenderStatus();
    }

    private void UpdateStats()
    {
        var summary = _lastRun?.Summary;
        TxtStats.Text = summary is null
            ? ""
            : string.Format(UiText.T("StatsLine"),
                summary.FilesCopied, summary.FilesSkipped, summary.FilesFailed, summary.FilesExtras);
    }

    // ---------- 事件 ----------

    private void OnTextChanged(object sender, TextChangedEventArgs e) => HandleOptionChange();

    private void OnComboChanged(object sender, SelectionChangedEventArgs e) => HandleOptionChange();

    private void OnCheckChanged(object sender, RoutedEventArgs e) => HandleOptionChange();

    private void HandleOptionChange()
    {
        if (_initializing) return;
        InvalidatePreview();
        RefreshCommand();
    }

    private void OnStrategyChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        InvalidatePreview();
        RefreshCommand();
    }

    private void OnTemplateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;

        switch (CmbTemplate.SelectedIndex)
        {
            case 1: // 照片备份
                RbBackup.IsChecked = true;
                CmbMultiThread.SelectedIndex = 1;
                ChkRestartable.IsChecked = false;
                ChkUnbuffered.IsChecked = false;
                break;
            case 2: // 同步到 NAS
                RbMirror.IsChecked = true;
                CmbMultiThread.SelectedIndex = 2;
                break;
            case 3: // 大文件迁移
                RbBackup.IsChecked = true;
                CmbMultiThread.SelectedIndex = 1;
                ChkRestartable.IsChecked = true;
                ChkUnbuffered.IsChecked = true;
                break;
            case 4: // 大量小文件
                RbBackup.IsChecked = true;
                CmbMultiThread.SelectedIndex = 3;
                CmbRetry.SelectedIndex = 0;
                break;
        }

        InvalidatePreview();
        RefreshCommand();
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;

        UiText.Language = CmbLanguage.SelectedIndex == 0 ? Lang.Zh : Lang.En;
        ApplyTexts();
        RefreshCommand();

        // 这两处是"运行时生成"的文本，必须跟着重绘
        RenderCard();
        RenderStatus();
        UpdateStats();
    }

    private void OnBrowseSource(object sender, RoutedEventArgs e) => PickFolder(TxtSource);

    private void OnBrowseDestination(object sender, RoutedEventArgs e) => PickFolder(TxtDestination);

    private void PickFolder(TextBox target)
    {
        var dialog = new OpenFolderDialog { Title = "RoboDock", Multiselect = false };
        if (Directory.Exists(target.Text)) dialog.InitialDirectory = target.Text;

        if (dialog.ShowDialog(this) == true)
        {
            target.Text = dialog.FolderName;
        }
    }

    private void OnCopyCommand(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(TxtCommand.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "RoboDock", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---------- 试运行 ----------

    private async void OnPreview(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        var options = BuildOptions();
        var issues = PathValidator.Validate(options.Source, options.Destination, UiText.Language);
        var errors = issues.Where(i => i.IsError).ToList();
        if (errors.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, errors.Select(i => i.Message)),
                "RoboDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AppendLog("> " + TxtCommand.Text + " /L");
        SetBusy(true);
        SetStatus("Running");
        _cts = new CancellationTokenSource();

        try
        {
            var result = await _runner.RunAsync(options, RobocopyMode.Preview,
                progress: null, onLine: AppendLog, cancellationToken: _cts.Token, language: UiText.Language);

            if (!result.IsUsable)
            {
                _lastRun = result;
                _cardState = CardState.Verdict;
            }
            else
            {
                _lastPreview = result.Preview;
                _cardState = CardState.Preview;
                _previewed = true;
                _expectedFiles = result.Preview.FileCountToCopy;
                BtnStart.IsEnabled = true;
            }

            RenderCard();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "RoboDock", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
            SetStatus(null);
            _cts.Dispose();
            _cts = null;
        }
    }

    // ---------- 执行 ----------

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        if (_busy || !_previewed) return;

        var options = BuildOptions();

        if (options.IsDestructive)
        {
            var answer = MessageBox.Show(this, UiText.T("ConfirmMirror"),
                "RoboDock", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (answer != MessageBoxResult.Yes) return;
        }

        SetBusy(true);
        BarProgress.Value = 0;
        BtnCancel.IsEnabled = true;
        TxtLog.Clear();
        SetStatus("Running");
        _cts = new CancellationTokenSource();

        var total = _expectedFiles ?? 0;
        var progress = new Progress<RunProgress>(p =>
        {
            if (p.FilesTotal is > 0)
            {
                BarProgress.Value = Math.Clamp(p.FilesDone * 100.0 / p.FilesTotal.Value, 0, 100);
            }
            else if (total > 0)
            {
                BarProgress.Value = Math.Clamp(p.FilesDone * 100.0 / total, 0, 100);
            }
            else
            {
                BarProgress.IsIndeterminate = true;
            }

            if (p.CurrentFile is null) SetStatus("Running");
            else SetStatus(null, ShortPath(p.CurrentFile));
        });

        try
        {
            AppendLog("> " + CommandBuilder.BuildDisplayCommand(options));
            var result = await _runner.RunAsync(options, RobocopyMode.Execute, progress, AppendLog, _cts.Token, UiText.Language);

            BarProgress.IsIndeterminate = false;
            BarProgress.Value = 100;

            _lastRun = result;
            _cardState = CardState.Verdict;
            RenderCard();
            UpdateStats();
            SetStatus("Done");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Cancelled");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "RoboDock", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
            _cts.Dispose();
            _cts = null;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        BtnCancel.IsEnabled = false;
        SetStatus("Stopping");
    }

    // ---------- 杂项 ----------

    private void SetBusy(bool busy)
    {
        _busy = busy;
        BtnPreview.IsEnabled = !busy;
        BtnStart.IsEnabled = !busy && _previewed;
        BtnCancel.IsEnabled = busy;
        BarProgress.IsIndeterminate = busy && _expectedFiles is null;

        if (!busy) BarProgress.IsIndeterminate = false;
    }

    private void AppendLog(string line)
    {
        // 纯百分比行（robocopy 用 '\r' 原地刷新）由进度条表达，不必刷屏
        if (ProgressParser.TryParsePercent(line, out _)) return;

        if (TxtLog.Text.Length > 400_000) TxtLog.Clear();
        TxtLog.AppendText(line + Environment.NewLine);
        TxtLog.ScrollToEnd();
    }

    private static string ShortPath(string path)
    {
        const int max = 90;
        return path.Length <= max ? path : "…" + path[^max..];
    }
}
