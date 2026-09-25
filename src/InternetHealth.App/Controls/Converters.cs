using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using InternetHealth.Core.Model;

namespace InternetHealth.App.Controls;

/// <summary>Health → pincel del tema. Parámetro opcional: "Soft" (fondo tenue) o "Ink" (texto sobre el color).</summary>
public sealed class HealthToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var h = value is Health health ? health : Health.Unknown;
        return BrushFor(h, parameter as string);
    }

    public static Brush BrushFor(Health h, string? variant = null)
    {
        var key = $"Status.{h}{variant}";
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class HealthToGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Glyph(value is Health h ? h : Health.Unknown);

    public static string Glyph(Health h) => h switch
    {
        Health.Good => "",   // marca de verificación
        Health.Fair => "",   // signo de exclamación
        Health.Poor => "",
        Health.Down => "",   // equis
        _ => "",             // puntos suspensivos
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class NullOrEmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || (value is string s && string.IsNullOrWhiteSpace(s)) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>[fracción 0-1, ancho disponible] → ancho.</summary>
public sealed class FractionToWidthConverter : IMultiValueConverter
{
    public static readonly FractionToWidthConverter Instance = new();

    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        double f = values.Length > 0 && values[0] is double d ? d : 0;
        double w = values.Length > 1 && values[1] is double aw ? aw : 0;
        return Math.Max(0, Math.Clamp(f, 0, 1) * w);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) => [];
}

public sealed class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>Valores de <see cref="Health"/> para usar con x:Static en XAML (leyendas).</summary>
public static class HealthValues
{
    public static Health Good => Health.Good;
    public static Health Fair => Health.Fair;
    public static Health Poor => Health.Poor;
    public static Health Down => Health.Down;
}
