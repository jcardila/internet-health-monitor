using System.Collections.ObjectModel;
using InternetHealth.Core.Monitoring;

namespace InternetHealth.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private int _selectedTab;

    public MainWindowViewModel(DashboardViewModel dashboard, HistoryViewModel history, SettingsViewModel settings)
    {
        Dashboard = dashboard;
        History = history;
        Settings = settings;
    }

    public DashboardViewModel Dashboard { get; }
    public HistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }
    public ObservableCollection<LogEntry> Log { get; } = [];

    public int SelectedTab { get => _selectedTab; set => Set(ref _selectedTab, value); }

    public void ReloadLog(IEnumerable<LogEntry> entries)
    {
        Log.Clear();
        foreach (var e in entries.Reverse()) Log.Add(e);
    }

    public void AddLog(LogEntry entry)
    {
        Log.Insert(0, entry);
        while (Log.Count > 400) Log.RemoveAt(Log.Count - 1);
    }
}
