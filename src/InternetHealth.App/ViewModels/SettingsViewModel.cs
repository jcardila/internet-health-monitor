using InternetHealth.Core.Settings;

namespace InternetHealth.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly UserPreferences _prefs;
    private readonly Action _onChanged;
    private string _updateStatus = "";
    private bool _isChecking;

    public SettingsViewModel(UserPreferences prefs, AppSettings settings, string version, Action onChanged)
    {
        _prefs = prefs;
        _onChanged = onChanged;
        VersionText = $"Versión {version}";
        SupportText = settings.SupportName;
        SupportUrl = settings.SupportUrl;
        SupportEmail = settings.SupportEmail;
        UpdatesConfigured = !string.IsNullOrWhiteSpace(settings.UpdateFeedUrl);
    }

    public string VersionText { get; }
    public string SupportText { get; }
    public string? SupportUrl { get; }
    public string? SupportEmail { get; }
    public bool UpdatesConfigured { get; }
    public bool HasSupportContact => !string.IsNullOrWhiteSpace(SupportUrl) || !string.IsNullOrWhiteSpace(SupportEmail);

    public bool StartWithWindows
    {
        get => _prefs.StartWithWindows;
        set { if (_prefs.StartWithWindows == value) return; _prefs.StartWithWindows = value; Raise(); _onChanged(); }
    }

    public bool NotificationsEnabled
    {
        get => _prefs.NotificationsEnabled;
        set { if (_prefs.NotificationsEnabled == value) return; _prefs.NotificationsEnabled = value; Raise(); _onChanged(); }
    }

    public bool CallDetectionEnabled
    {
        get => _prefs.CallDetectionEnabled;
        set { if (_prefs.CallDetectionEnabled == value) return; _prefs.CallDetectionEnabled = value; Raise(); _onChanged(); }
    }

    public bool DetailedLog
    {
        get => _prefs.DetailedLog;
        set { if (_prefs.DetailedLog == value) return; _prefs.DetailedLog = value; Raise(); _onChanged(); }
    }

    public string UpdateStatus { get => _updateStatus; set => Set(ref _updateStatus, value); }
    public bool IsChecking { get => _isChecking; set => Set(ref _isChecking, value); }

    public void Refresh()
    {
        Raise(nameof(StartWithWindows));
        Raise(nameof(NotificationsEnabled));
        Raise(nameof(CallDetectionEnabled));
        Raise(nameof(DetailedLog));
    }
}
