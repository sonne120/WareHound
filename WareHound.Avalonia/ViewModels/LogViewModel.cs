using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using WareHound.Avalonia.Models;
using WareHound.Avalonia.Services;

namespace WareHound.Avalonia.ViewModels;

public class LogViewModel : ViewModelBase
{
    private readonly ILoggerService _loggerService;

    [Reactive] public string FilterText { get; set; } = "";
    [Reactive] public LogEntry? SelectedLogEntry { get; set; }
    [Reactive] public int SelectedLevelIndex { get; set; }

    public ObservableCollection<LogEntry> LogEntries => _loggerService.LogEntries;
    public string[] LogLevels { get; } = { "All", "Debug", "Info", "Warning", "Error" };

    public int LogCount => LogEntries.Count;
    public int ErrorCount => LogEntries.Count(e => e.Level == LogLevel.Error);
    public int WarningCount => LogEntries.Count(e => e.Level == LogLevel.Warning);

    // Commands
    public ReactiveCommand<Unit, Unit> ClearLogsCommand { get; }
    public ReactiveCommand<Unit, Unit> CopySelectedCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportLogsCommand { get; }

    public LogViewModel(ILoggerService loggerService)
    {
        _loggerService = loggerService;

        ClearLogsCommand = ReactiveCommand.Create(ClearLogs);

        var hasSelectedEntry = this.WhenAnyValue(x => x.SelectedLogEntry)
            .Select(e => e != null);
        CopySelectedCommand = ReactiveCommand.Create(CopySelected, hasSelectedEntry);

        ExportLogsCommand = ReactiveCommand.Create(ExportLogs);

        // Subscribe to collection changes
        LogEntries.CollectionChanged += (s, e) =>
        {
            this.RaisePropertyChanged(nameof(LogCount));
            this.RaisePropertyChanged(nameof(ErrorCount));
            this.RaisePropertyChanged(nameof(WarningCount));
        };

        _loggerService.Log("Log viewer initialized");
    }

    private void ClearLogs()
    {
        _loggerService.ClearLogs();
    }

    private void CopySelected()
    {
        if (SelectedLogEntry != null)
        {
            var text = $"[{SelectedLogEntry.TimestampDisplay}] [{SelectedLogEntry.LevelDisplay}] {SelectedLogEntry.Message}";
        }
    }

    private void ExportLogs()
    {
        // Export functionality placeholder
    }
}
