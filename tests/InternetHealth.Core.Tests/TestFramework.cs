using System.Reflection;
using System.Runtime.CompilerServices;

namespace InternetHealth.Core.Tests;

/// <summary>
/// Mini framework de pruebas sin dependencias externas (se ejecuta con `dotnet run`).
/// Marca los métodos con [Test]; pueden ser sync o async.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute;

public sealed class AssertionException(string message) : Exception(message);

public static class Assert
{
    public static void True(bool condition, string? message = null, [CallerArgumentExpression(nameof(condition))] string? expr = null)
    {
        if (!condition) throw new AssertionException(message ?? $"Se esperaba verdadero: {expr}");
    }

    public static void False(bool condition, string? message = null, [CallerArgumentExpression(nameof(condition))] string? expr = null)
    {
        if (condition) throw new AssertionException(message ?? $"Se esperaba falso: {expr}");
    }

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertionException($"{message} Esperado: <{expected}> Obtenido: <{actual}>".Trim());
    }

    public static void Near(double expected, double? actual, double tolerance, string? message = null)
    {
        if (actual is null || Math.Abs(expected - actual.Value) > tolerance)
            throw new AssertionException($"{message} Esperado: {expected}±{tolerance} Obtenido: {actual?.ToString() ?? "null"}".Trim());
    }

    public static void Null(object? value, string? message = null)
    {
        if (value is not null) throw new AssertionException(message ?? $"Se esperaba null, se obtuvo {value}");
    }

    public static void NotNull(object? value, string? message = null)
    {
        if (value is null) throw new AssertionException(message ?? "Se esperaba un valor, se obtuvo null");
    }

    public static void Contains(string needle, string haystack)
    {
        if (!haystack.Contains(needle, StringComparison.Ordinal))
            throw new AssertionException($"No se encontró «{needle}» en el texto.");
    }
}

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var filter = args.FirstOrDefault();
        var tests = typeof(Program).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<TestAttribute>() is not null)
                .Select(m => (Type: t, Method: m)))
            .Where(x => filter is null || $"{x.Type.Name}.{x.Method.Name}".Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Type.Name).ThenBy(x => x.Method.Name)
            .ToList();

        int passed = 0, failed = 0;
        foreach (var (type, method) in tests)
        {
            var name = $"{type.Name}.{method.Name}";
            try
            {
                var instance = method.IsStatic ? null : Activator.CreateInstance(type);
                var result = method.Invoke(instance, null);
                if (result is Task task) await task;
                passed++;
                Console.WriteLine($"  ✓ {name}");
            }
            catch (Exception ex)
            {
                failed++;
                var inner = ex is TargetInvocationException tie && tie.InnerException is not null ? tie.InnerException : ex;
                Console.WriteLine($"  ✗ {name}\n      {inner.GetType().Name}: {inner.Message}");
                if (inner is not AssertionException) Console.WriteLine(inner.StackTrace);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{passed} pruebas correctas, {failed} fallidas, {tests.Count} en total.");
        return failed == 0 ? 0 : 1;
    }
}
