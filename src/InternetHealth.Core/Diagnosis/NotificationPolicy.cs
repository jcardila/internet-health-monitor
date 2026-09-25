using InternetHealth.Core.Model;

namespace InternetHealth.Core.Diagnosis;

public sealed record NotificationRequest(string Title, string Message, bool IsRecovery, DiagnosisCode Code);

/// <summary>
/// Decide cuándo molestar al usuario. Solo avisa de problemas que persisten, con mucha más
/// sensibilidad durante una llamada, sin repetir el mismo aviso y avisando cuando se recupera.
/// </summary>
public sealed class NotificationPolicy
{
    public static readonly TimeSpan HoldInCall = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan HoldNormal = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan RepeatCooldown = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan RecoveryWindow = TimeSpan.FromHours(2);

    private DiagnosisCode? _lastCode;
    private DateTimeOffset _lastAt;
    private bool _problemNotified;

    public bool Enabled { get; set; } = true;
    public DateTimeOffset? DoNotDisturbUntil { get; set; }

    public NotificationRequest? Evaluate(Diagnosis stable, DateTimeOffset stableSince, bool inCall, DateTimeOffset now)
    {
        bool muted = !Enabled || (DoNotDisturbUntil is { } dnd && now < dnd);

        if (IsWorthNotifying(stable, inCall))
        {
            var hold = inCall ? HoldInCall : HoldNormal;
            if (now - stableSince < hold) return null;
            if (_lastCode == stable.Code && now - _lastAt < RepeatCooldown) return null;
            if (muted) return null;

            _lastCode = stable.Code;
            _lastAt = now;
            _problemNotified = true;
            string title = inCall ? "Tu reunión puede verse afectada" : stable.Text.Title;
            string message = inCall ? stable.Text.Title + ". " + FirstStepOrSummary(stable) : FirstStepOrSummary(stable);
            return new NotificationRequest(title, message, false, stable.Code);
        }

        if (_problemNotified && stable.Severity == Health.Good && now - stableSince >= TimeSpan.FromSeconds(5))
        {
            _problemNotified = false;
            _lastCode = null;
            if (muted || now - _lastAt > RecoveryWindow) return null;
            return new NotificationRequest("Tu conexión se recuperó", "Todo funciona bien de nuevo.", true, stable.Code);
        }

        return null;
    }

    private static bool IsWorthNotifying(Diagnosis d, bool inCall)
    {
        if (d.Code is DiagnosisCode.Checking or DiagnosisCode.AllGood or DiagnosisCode.GoodButWeakLink) return false;
        if (d.Code == DiagnosisCode.NoConnection) return inCall; // Windows ya lo muestra
        if (d.Severity >= Health.Poor) return true;
        // Problemas leves: solo durante una llamada y solo si el usuario puede hacer algo.
        return inCall && d.Severity == Health.Fair && d.Culprit is Segment.Link or Segment.Device or Segment.Router;
    }

    private static string FirstStepOrSummary(Diagnosis d) =>
        d.Text.Steps.Count > 0 ? d.Text.Steps[0] : d.Text.Summary;
}
