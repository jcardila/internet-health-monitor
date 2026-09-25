using System.Globalization;
using System.Text;

namespace InternetHealth.Core.History;

internal static class Csv
{
    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        bool needsQuotes = value.AsSpan().IndexOfAny(",\"\r\n;") >= 0;
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    public static string Num(double? v, string format = "0.#") => v is double d && !double.IsNaN(d) ? d.ToString(format, Inv) : "";
    public static string Num(int? v) => v?.ToString(Inv) ?? "";
    public static string Bool(bool v) => v ? "1" : "0";

    /// <summary>Divide una línea CSV respetando comillas.</summary>
    public static List<string> Split(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        result.Add(sb.ToString());
        return result;
    }

    public static double? ParseDouble(string s) =>
        double.TryParse(s, NumberStyles.Float, Inv, out var d) ? d : null;

    public static int? ParseInt(string s) =>
        int.TryParse(s, NumberStyles.Integer, Inv, out var i) ? i : null;
}
