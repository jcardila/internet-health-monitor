using System.Collections.ObjectModel;
using System.Globalization;
using InternetHealth.Core.History;
using InternetHealth.Core.Model;
using InternetHealth.Core.Settings;
using InternetHealth.Core.Stats;

namespace InternetHealth.App.ViewModels;

public sealed record EventItem(Health Severity, string TimeText, string SeverityLabel, string Title);

public sealed class HistoryViewModel : ObservableObject
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");
    private readonly AppPaths _paths;
    private int _hours = 24;
    private IReadOnlyList<MinuteRow> _rows = [];
    private DateTimeOffset _from, _to;
    private string _problemsText = "–", _unstableText = "–", _latencyText = "–", _lossText = "–", _measuredText = "", _worstText = "";
    private bool _isEmpty = true;
    private int _loadVersion;

    public HistoryViewModel(AppPaths paths)
    {
        _paths = paths;
        _to = DateTimeOffset.Now;
        _from = _to.AddHours(-_hours);
    }

    public ObservableCollection<EventItem> Events { get; } = [];

    public bool Range1h { get => _hours == 1; set { if (value) SetRange(1); } }
    public bool Range6h { get => _hours == 6; set { if (value) SetRange(6); } }
    public bool Range24h { get => _hours == 24; set { if (value) SetRange(24); } }
    public bool Range7d { get => _hours == 168; set { if (value) SetRange(168); } }

    public IReadOnlyList<MinuteRow> Rows { get => _rows; private set => Set(ref _rows, value); }
    public DateTimeOffset From { get => _from; private set => Set(ref _from, value); }
    public DateTimeOffset To { get => _to; private set => Set(ref _to, value); }
    public string ProblemsText { get => _problemsText; private set => Set(ref _problemsText, value); }
    public string UnstableText { get => _unstableText; private set => Set(ref _unstableText, value); }
    public string LatencyText { get => _latencyText; private set => Set(ref _latencyText, value); }
    public string LossText { get => _lossText; private set => Set(ref _lossText, value); }
    public string MeasuredText { get => _measuredText; private set => Set(ref _measuredText, value); }
    public string WorstText { get => _worstText; private set => Set(ref _worstText, value); }
    public bool IsEmpty { get => _isEmpty; private set => Set(ref _isEmpty, value); }

    private void SetRange(int hours)
    {
        if (_hours == hours) return;
        _hours = hours;
        Raise(nameof(Range1h)); Raise(nameof(Range6h)); Raise(nameof(Range24h)); Raise(nameof(Range7d));
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        int version = ++_loadVersion;
        var to = DateTimeOffset.Now;
        var from = to.AddHours(-_hours);
        var (rows, events) = await Task.Run(() =>
            (HistoryStore.Read(_paths, from, to), HistoryStore.ReadEvents(_paths, from))).ConfigureAwait(true);
        if (version != _loadVersion) return;

        From = from;
        To = to;
        Rows = rows;
        IsEmpty = rows.Count == 0;

        int poor = rows.Sum(r => r.SecondsPoor + r.SecondsDown), fair = rows.Sum(r => r.SecondsFair);
        int measured = rows.Sum(r => r.SecondsGood + r.SecondsFair + r.SecondsPoor + r.SecondsDown);
        var avgs = rows.Where(r => r.Internet.AvgMs is not null).Select(r => r.Internet.AvgMs!.Value).ToList();
        int sent = rows.Sum(r => r.Internet.Sent), lost = rows.Sum(r => r.Internet.Lost);
        ProblemsText = Duration(poor);
        UnstableText = Duration(fair);
        LatencyText = avgs.Count > 0 ? $"{avgs.Average():0} ms" : "–";
        LossText = sent > 0 ? $"{(100.0 * lost / sent).ToString("0.#", Es)} %" : "–";
        MeasuredText = measured > 0 ? $"Tiempo medido en este periodo: {Duration(measured)}" : "";
        var worst = rows.Where(r => r.MosMin is not null).MinBy(r => r.MosMin);
        WorstText = worst is not null && worst.MosMin < 3.5
            ? $"Peor momento: {worst.Minute.ToString("dddd d, HH:mm", Es)} (calidad {CallQuality.Label(worst.MosMin).ToLowerInvariant()})"
            : "";

        Events.Clear();
        foreach (var e in events.OrderByDescending(e => e.Time).Take(300))
            Events.Add(new EventItem(e.Severity, e.Time.ToString(_hours > 24 ? "ddd d, HH:mm" : "HH:mm:ss", Es), e.Severity.ToLabel(), e.Title));
    }

    private static string Duration(int seconds)
    {
        if (seconds <= 0) return "0 min";
        if (seconds < 60) return $"{seconds} s";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours} h {ts.Minutes} min" : $"{ts.Minutes} min";
    }
}
