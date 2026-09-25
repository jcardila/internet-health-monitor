using System.Globalization;
using System.Text;
using InternetHealth.Core.Model;
using InternetHealth.Core.Monitoring;
using InternetHealth.Core.Settings;

namespace InternetHealth.Core.History;

public enum HistoryChannel { Router, Provider, Internet, Cloud }

/// <summary>
/// Acumula mediciones por minuto y las guarda en CSV diarios (historial/AAAA-MM-DD.csv).
/// También registra los cambios de estado en eventos.csv.
/// </summary>
public sealed class HistoryStore
{
    public const string Header =
        "minute_local,minute_utc,device,user,app_version,connection_type,adapter,ssid,bssid,gateway_ip,gateway_mac,provider_hop,vpn," +
        "wifi_signal_pct,wifi_rssi_dbm,wifi_band,wifi_channel,link_rate_mbps," +
        "router_sent,router_lost,router_avg_ms,router_max_ms,router_jitter_ms," +
        "provider_sent,provider_lost,provider_avg_ms,provider_max_ms,provider_jitter_ms," +
        "internet_sent,internet_lost,internet_avg_ms,internet_max_ms,internet_jitter_ms," +
        "cloud_sent,cloud_failed,cloud_avg_ms,cloud_max_ms," +
        "mos_avg,mos_min,rx_mbps_avg,tx_mbps_avg,in_call,worst_severity,worst_diagnosis," +
        "seconds_good,seconds_fair,seconds_poor,seconds_down";

    public const string EventsHeader =
        "time_local,time_utc,device,user,severity,diagnosis,culprit,title,connection_type,ssid,gateway_ip,gateway_mac,in_call";

    private readonly AppPaths _paths;
    private readonly string _appVersion;
    private readonly string _device = Environment.MachineName;
    private readonly string _user = Environment.UserName;
    private readonly object _gate = new();

    private readonly Accumulator[] _channels = [new(), new(), new(), new()];
    private DateTimeOffset _minute = DateTimeOffset.MinValue;
    private MonitorSnapshot? _lastSnapshot;
    private DateTimeOffset _lastObserve;
    private double _mosSum; private int _mosN; private double _mosMin = double.MaxValue;
    private double _rxSum, _txSum; private int _tpN;
    private bool _inCall;
    private Health _worst; private DiagnosisCode _worstCode;
    private readonly double[] _secondsBySeverity = new double[5];

    public HistoryStore(AppPaths paths, string appVersion)
    {
        _paths = paths;
        _appVersion = appVersion;
    }

    public event Action<MinuteRow>? MinuteWritten;

    public void AddSample(HistoryChannel channel, DateTimeOffset time, double? rttMs)
    {
        lock (_gate)
        {
            Roll(time);
            _channels[(int)channel].Add(rttMs);
        }
    }

    public void Observe(MonitorSnapshot s)
    {
        lock (_gate)
        {
            Roll(s.Time);
            if (_lastSnapshot is not null && _lastObserve != default)
            {
                double secs = Math.Clamp((s.Time - _lastObserve).TotalSeconds, 0, 60);
                _secondsBySeverity[(int)_lastSnapshot.Severity] += secs;
            }
            _lastSnapshot = s;
            _lastObserve = s.Time;
            if (s.Stable.Mos is double m)
            {
                _mosSum += m; _mosN++;
                if (m < _mosMin) _mosMin = m;
            }
            _rxSum += s.Throughput.RxMbps; _txSum += s.Throughput.TxMbps; _tpN++;
            _inCall |= s.Call.InCall;
            if (s.Severity >= _worst) { _worst = s.Severity; _worstCode = s.Stable.Code; }
        }
    }

    public void RecordEvent(MonitorSnapshot s)
    {
        try
        {
            Directory.CreateDirectory(_paths.DataRoot);
            bool newFile = !File.Exists(_paths.EventsFile);
            var d = s.Stable;
            var ctx = s.Context;
            var line = string.Join(',',
                s.Time.ToString("yyyy-MM-dd HH:mm:ss", Csv.Inv),
                s.Time.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", Csv.Inv),
                Csv.Escape(_device), Csv.Escape(_user),
                d.Severity.ToString(), d.Code.ToString(), d.Culprit?.ToString() ?? "",
                Csv.Escape(d.Text.Title), ctx.LinkType.ToString(), Csv.Escape(ctx.Wifi?.Ssid),
                ctx.Gateway?.ToString() ?? "", Csv.Escape(ctx.GatewayMac), Csv.Bool(s.Call.InCall));
            using var w = new StreamWriter(_paths.EventsFile, append: true, Csv.Utf8Bom);
            if (newFile) w.WriteLine(EventsHeader);
            w.WriteLine(line);
        }
        catch (Exception ex)
        {
            _paths.AppendError("Evento: " + ex.Message);
        }
    }

    /// <summary>Fuerza el guardado del minuto en curso (al cerrar la app).</summary>
    public void Flush()
    {
        lock (_gate)
        {
            if (_minute != DateTimeOffset.MinValue) WriteCurrent();
            ResetMinute(_minute);
        }
    }

    private void Roll(DateTimeOffset time)
    {
        var minute = new DateTimeOffset(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, time.Offset);
        if (_minute == DateTimeOffset.MinValue) { _minute = minute; return; }
        if (minute == _minute) return;
        WriteCurrent();
        ResetMinute(minute);
    }

    private void ResetMinute(DateTimeOffset minute)
    {
        _minute = minute;
        foreach (var c in _channels) c.Reset();
        _mosSum = 0; _mosN = 0; _mosMin = double.MaxValue;
        _rxSum = 0; _txSum = 0; _tpN = 0;
        _inCall = false;
        _worst = Health.Unknown; _worstCode = DiagnosisCode.Checking;
        Array.Clear(_secondsBySeverity);
    }

    private void WriteCurrent()
    {
        if (_lastSnapshot is null && _channels.All(c => c.Sent == 0)) return;
        var ctx = _lastSnapshot?.Context ?? Network.NetworkContext.None;
        var row = new MinuteRow
        {
            Minute = _minute,
            Device = _device,
            User = _user,
            AppVersion = _appVersion,
            ConnectionType = ctx.LinkType.ToString(),
            Adapter = ctx.AdapterName,
            Ssid = ctx.Wifi?.Ssid,
            Bssid = ctx.Wifi?.Bssid,
            GatewayIp = ctx.Gateway?.ToString(),
            GatewayMac = ctx.GatewayMac,
            ProviderHop = _lastSnapshot?.ProviderHop?.Address.ToString(),
            Vpn = ctx.VpnActive,
            WifiSignalPct = ctx.Wifi?.SignalQuality,
            WifiRssiDbm = ctx.Wifi?.RssiDbm,
            WifiBand = ctx.Wifi?.Band,
            WifiChannel = ctx.Wifi?.Channel,
            LinkRateMbps = ctx.EffectiveLinkRateMbps,
            Router = _channels[0].Summary(),
            Provider = _channels[1].Summary(),
            Internet = _channels[2].Summary(),
            Cloud = _channels[3].Summary(),
            MosAvg = _mosN > 0 ? _mosSum / _mosN : null,
            MosMin = _mosN > 0 ? _mosMin : null,
            RxMbpsAvg = _tpN > 0 ? _rxSum / _tpN : 0,
            TxMbpsAvg = _tpN > 0 ? _txSum / _tpN : 0,
            InCall = _inCall,
            WorstSeverity = _worst,
            WorstDiagnosis = _worstCode,
            SecondsGood = (int)Math.Round(_secondsBySeverity[(int)Health.Good]),
            SecondsFair = (int)Math.Round(_secondsBySeverity[(int)Health.Fair]),
            SecondsPoor = (int)Math.Round(_secondsBySeverity[(int)Health.Poor]),
            SecondsDown = (int)Math.Round(_secondsBySeverity[(int)Health.Down]),
        };

        try
        {
            Directory.CreateDirectory(_paths.HistoryDir);
            var file = Path.Combine(_paths.HistoryDir, _minute.ToString("yyyy-MM-dd", Csv.Inv) + ".csv");
            bool newFile = !File.Exists(file);
            using var w = new StreamWriter(file, append: true, Csv.Utf8Bom);
            if (newFile) w.WriteLine(Header);
            w.WriteLine(ToCsv(row));
        }
        catch (Exception ex)
        {
            _paths.AppendError("Historial: " + ex.Message);
        }

        try { MinuteWritten?.Invoke(row); } catch { /* suscriptores no deben romper el motor */ }
    }

    public static string ToCsv(MinuteRow r)
    {
        var sb = new StringBuilder(512);
        void Add(string s) { if (sb.Length > 0) sb.Append(','); sb.Append(s); }
        Add(r.Minute.ToString("yyyy-MM-dd HH:mm", Csv.Inv));
        Add(r.Minute.UtcDateTime.ToString("yyyy-MM-ddTHH:mmZ", Csv.Inv));
        Add(Csv.Escape(r.Device)); Add(Csv.Escape(r.User)); Add(Csv.Escape(r.AppVersion));
        Add(r.ConnectionType); Add(Csv.Escape(r.Adapter)); Add(Csv.Escape(r.Ssid)); Add(Csv.Escape(r.Bssid));
        Add(r.GatewayIp ?? ""); Add(Csv.Escape(r.GatewayMac)); Add(r.ProviderHop ?? ""); Add(Csv.Bool(r.Vpn));
        Add(Csv.Num(r.WifiSignalPct)); Add(Csv.Num(r.WifiRssiDbm)); Add(Csv.Escape(r.WifiBand)); Add(Csv.Num(r.WifiChannel));
        Add(Csv.Num(r.LinkRateMbps, "0"));
        foreach (var c in new[] { r.Router, r.Provider, r.Internet })
        {
            Add(c.Sent.ToString(Csv.Inv)); Add(c.Lost.ToString(Csv.Inv));
            Add(Csv.Num(c.AvgMs)); Add(Csv.Num(c.MaxMs)); Add(Csv.Num(c.JitterMs));
        }
        Add(r.Cloud.Sent.ToString(Csv.Inv)); Add(r.Cloud.Lost.ToString(Csv.Inv));
        Add(Csv.Num(r.Cloud.AvgMs)); Add(Csv.Num(r.Cloud.MaxMs));
        Add(Csv.Num(r.MosAvg, "0.00")); Add(Csv.Num(r.MosMin, "0.00"));
        Add(Csv.Num(r.RxMbpsAvg, "0.00")); Add(Csv.Num(r.TxMbpsAvg, "0.00"));
        Add(Csv.Bool(r.InCall)); Add(r.WorstSeverity.ToString()); Add(r.WorstDiagnosis.ToString());
        Add(r.SecondsGood.ToString(Csv.Inv)); Add(r.SecondsFair.ToString(Csv.Inv));
        Add(r.SecondsPoor.ToString(Csv.Inv)); Add(r.SecondsDown.ToString(Csv.Inv));
        return sb.ToString();
    }

    /// <summary>Lee el historial local entre dos fechas (hora local).</summary>
    public static List<MinuteRow> Read(AppPaths paths, DateTimeOffset from, DateTimeOffset to)
    {
        var rows = new List<MinuteRow>();
        if (!Directory.Exists(paths.HistoryDir)) return rows;
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            var file = Path.Combine(paths.HistoryDir, day.ToString("yyyy-MM-dd", Csv.Inv) + ".csv");
            if (!File.Exists(file)) continue;
            try
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs, Encoding.UTF8);
                var header = reader.ReadLine();
                if (header is null) continue;
                var idx = Csv.Split(header).Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i);
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    var row = Parse(Csv.Split(line), idx);
                    if (row is not null && row.Minute >= from && row.Minute <= to) rows.Add(row);
                }
            }
            catch { /* archivo en uso o dañado: se omite */ }
        }
        return rows;
    }

    private static MinuteRow? Parse(List<string> f, Dictionary<string, int> idx)
    {
        string Get(string name) => idx.TryGetValue(name, out var i) && i < f.Count ? f[i] : "";
        if (!DateTime.TryParseExact(Get("minute_local"), "yyyy-MM-dd HH:mm", Csv.Inv, DateTimeStyles.None, out var local))
            return null;
        ChannelSummary Ch(string p, string lostName = "lost") => new(
            Csv.ParseInt(Get(p + "_sent")) ?? 0, Csv.ParseInt(Get(p + "_" + lostName)) ?? 0,
            Csv.ParseDouble(Get(p + "_avg_ms")), Csv.ParseDouble(Get(p + "_max_ms")), Csv.ParseDouble(Get(p + "_jitter_ms")));
        return new MinuteRow
        {
            Minute = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)),
            Device = Get("device"), User = Get("user"), AppVersion = Get("app_version"),
            ConnectionType = Get("connection_type"), Adapter = Get("adapter"), Ssid = Get("ssid"), Bssid = Get("bssid"),
            GatewayIp = Get("gateway_ip"), GatewayMac = Get("gateway_mac"), ProviderHop = Get("provider_hop"),
            Vpn = Get("vpn") == "1",
            WifiSignalPct = Csv.ParseInt(Get("wifi_signal_pct")), WifiRssiDbm = Csv.ParseInt(Get("wifi_rssi_dbm")),
            WifiBand = Get("wifi_band"), WifiChannel = Csv.ParseInt(Get("wifi_channel")),
            LinkRateMbps = Csv.ParseDouble(Get("link_rate_mbps")),
            Router = Ch("router"), Provider = Ch("provider"), Internet = Ch("internet"), Cloud = Ch("cloud", "failed"),
            MosAvg = Csv.ParseDouble(Get("mos_avg")), MosMin = Csv.ParseDouble(Get("mos_min")),
            RxMbpsAvg = Csv.ParseDouble(Get("rx_mbps_avg")) ?? 0, TxMbpsAvg = Csv.ParseDouble(Get("tx_mbps_avg")) ?? 0,
            InCall = Get("in_call") == "1",
            WorstSeverity = Enum.TryParse<Health>(Get("worst_severity"), out var h) ? h : Health.Unknown,
            WorstDiagnosis = Enum.TryParse<DiagnosisCode>(Get("worst_diagnosis"), out var dc) ? dc : DiagnosisCode.Checking,
            SecondsGood = Csv.ParseInt(Get("seconds_good")) ?? 0, SecondsFair = Csv.ParseInt(Get("seconds_fair")) ?? 0,
            SecondsPoor = Csv.ParseInt(Get("seconds_poor")) ?? 0, SecondsDown = Csv.ParseInt(Get("seconds_down")) ?? 0,
        };
    }

    public static List<(DateTimeOffset Time, Health Severity, DiagnosisCode Code, string Title)> ReadEvents(
        AppPaths paths, DateTimeOffset from)
    {
        var list = new List<(DateTimeOffset, Health, DiagnosisCode, string)>();
        if (!File.Exists(paths.EventsFile)) return list;
        try
        {
            using var fs = new FileStream(paths.EventsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var header = reader.ReadLine();
            if (header is null) return list;
            var idx = Csv.Split(header).Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var f = Csv.Split(line);
                string Get(string n) => idx.TryGetValue(n, out var i) && i < f.Count ? f[i] : "";
                if (!DateTime.TryParseExact(Get("time_local"), "yyyy-MM-dd HH:mm:ss", Csv.Inv, DateTimeStyles.None, out var t)) continue;
                var time = new DateTimeOffset(t, TimeZoneInfo.Local.GetUtcOffset(t));
                if (time < from) continue;
                list.Add((time,
                    Enum.TryParse<Health>(Get("severity"), out var h) ? h : Health.Unknown,
                    Enum.TryParse<DiagnosisCode>(Get("diagnosis"), out var c) ? c : DiagnosisCode.Checking,
                    Get("title")));
            }
        }
        catch { /* ignorar */ }
        return list;
    }

    /// <summary>Borra historial más antiguo que la retención y recorta eventos.csv.</summary>
    public static void ApplyRetention(AppPaths paths, int days, DateTimeOffset now)
    {
        try
        {
            if (Directory.Exists(paths.HistoryDir))
            {
                var cutoff = now.Date.AddDays(-days);
                foreach (var file in Directory.EnumerateFiles(paths.HistoryDir, "*.csv"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (DateTime.TryParseExact(name, "yyyy-MM-dd", Csv.Inv, DateTimeStyles.None, out var d) && d < cutoff)
                        File.Delete(file);
                }
            }

            if (File.Exists(paths.EventsFile) && new FileInfo(paths.EventsFile).Length > 1024 * 1024)
            {
                var lines = File.ReadAllLines(paths.EventsFile, Encoding.UTF8);
                var keep = lines.Skip(1).TakeLast(5000).Prepend(EventsHeader);
                File.WriteAllLines(paths.EventsFile, keep, Csv.Utf8Bom);
            }
        }
        catch (Exception ex)
        {
            paths.AppendError("Retención: " + ex.Message);
        }
    }

    private sealed class Accumulator
    {
        public int Sent, Lost;
        private double _sum, _max, _jSum, _prev = double.NaN;
        private int _jN;

        public void Add(double? v)
        {
            Sent++;
            if (v is not double d) { Lost++; return; }
            _sum += d;
            if (d > _max) _max = d;
            if (!double.IsNaN(_prev)) { _jSum += Math.Abs(d - _prev); _jN++; }
            _prev = d;
        }

        public void Reset()
        {
            Sent = Lost = 0; _sum = _max = _jSum = 0; _jN = 0; _prev = double.NaN;
        }

        public ChannelSummary Summary()
        {
            int ok = Sent - Lost;
            return new ChannelSummary(Sent, Lost, ok > 0 ? _sum / ok : null, ok > 0 ? _max : null, _jN > 0 ? _jSum / _jN : null);
        }
    }
}
