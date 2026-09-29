using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using InternetHealth.Core.Model;

namespace InternetHealth.Mac.Controls;

/// <summary>Health → pincel del tema. Parámetro opcional: "Soft" (fondo tenue) o "Ink" (texto sobre el color).</summary>
public sealed class HealthToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BrushFor(value is Health h ? h : Health.Unknown, parameter as string);

    public static IBrush BrushFor(Health h, string? variant = null) =>
        Resource($"Status.{h}{variant}") as IBrush ?? Brushes.Gray;

    /// <summary>Busca un recurso de la app según el tema (claro/oscuro) activo.</summary>
    public static object? Resource(string key)
    {
        var app = Application.Current;
        return app is not null && app.TryGetResource(key, app.ActualThemeVariant, out var value) ? value : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

/// <summary>Símbolo de cada estado, para que nunca dependa solo del color.</summary>
public sealed class HealthToGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Glyph(value is Health h ? h : Health.Unknown);

    public static string Glyph(Health h) => h switch
    {
        Health.Good => "✓",
        Health.Fair or Health.Poor => "!",
        Health.Down => "✕",
        _ => "…",
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

/// <summary>Clave de recurso (p. ej. "Icon.Wifi") → el recurso de la app (la geometría del ícono).</summary>
public sealed class ResourceByKeyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key && key.Length > 0 ? HealthToBrushConverter.Resource(key) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

/// <summary>Valores de <see cref="Health"/> para usar con x:Static en XAML (leyendas).</summary>
public static class HealthValues
{
    public static Health Good => Health.Good;
    public static Health Fair => Health.Fair;
    public static Health Poor => Health.Poor;
    public static Health Down => Health.Down;
}
