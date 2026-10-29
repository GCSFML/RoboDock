using System.Reflection;

namespace RoboDock.Core.Tests;

// 这个项目刻意不依赖任何 NuGet 包：开发机可能完全离线（比如没有代理），
// 而测试必须随时能跑。API 形状照着 xunit 写，将来接入 xunit 只需要改 csproj。

[AttributeUsage(AttributeTargets.Method)]
public sealed class FactAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TheoryAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class InlineDataAttribute(params object?[] data) : Attribute
{
    public object?[] Data { get; } = data;
}

public sealed class AssertFailedException(string message) : Exception(message);

public static class Assert
{
    public static void True(bool condition, string? message = null)
    {
        if (!condition) throw new AssertFailedException(message ?? "期望为 true，实际为 false");
    }

    public static void False(bool condition, string? message = null)
    {
        if (condition) throw new AssertFailedException(message ?? "期望为 false，实际为 true");
    }

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new AssertFailedException(message ?? $"期望 {Format(expected)}，实际 {Format(actual)}");
        }
    }

    public static void Equal(double expected, double actual, int precision)
    {
        if (Math.Round(expected - actual, precision) != 0)
        {
            throw new AssertFailedException($"期望 {expected}（精度 {precision}），实际 {actual}");
        }
    }

    public static void NotEqual<T>(T notExpected, T actual, string? message = null)
    {
        if (EqualityComparer<T>.Default.Equals(notExpected, actual))
        {
            throw new AssertFailedException(message ?? $"期望不等于 {Format(notExpected)}，实际相等");
        }
    }

    public static void Null(object? value, string? message = null)
    {
        if (value is not null) throw new AssertFailedException(message ?? $"期望 null，实际 {Format(value)}");
    }

    public static T NotNull<T>(T? value, string? message = null) where T : class
    {
        if (value is null) throw new AssertFailedException(message ?? "期望非 null，实际为 null");
        return value;
    }

    public static void Contains(string expected, string actual, string? message = null)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new AssertFailedException(message ?? $"期望包含 \"{expected}\"，实际 \"{actual}\"");
        }
    }

    public static void Contains(string expected, string actual, StringComparison comparison, string? message = null)
    {
        if (!actual.Contains(expected, comparison))
        {
            throw new AssertFailedException(message ?? $"期望包含 \"{expected}\"，实际 \"{actual}\"");
        }
    }

    public static void Contains<T>(IEnumerable<T> collection, T item, string? message = null)
    {
        if (!collection.Contains(item))
        {
            throw new AssertFailedException(message ?? $"期望集合包含 {Format(item)}");
        }
    }

    /// <summary>xunit 风格的 <c>Assert.Contains(expected, collection)</c>。</summary>
    public static void Contains(string expected, IEnumerable<string> collection, string? message = null)
    {
        if (!collection.Contains(expected, StringComparer.Ordinal))
        {
            throw new AssertFailedException(message ?? $"期望集合包含 \"{expected}\"，实际 [{string.Join(", ", collection)}]");
        }
    }

    /// <summary>xunit 风格的 <c>Assert.DoesNotContain(expected, collection)</c>。</summary>
    public static void DoesNotContain(string unexpected, IEnumerable<string> collection, string? message = null)
    {
        if (collection.Contains(unexpected, StringComparer.Ordinal))
        {
            throw new AssertFailedException(message ?? $"期望集合不包含 \"{unexpected}\"，实际 [{string.Join(", ", collection)}]");
        }
    }

    public static void DoesNotContain(string unexpected, string actual, string? message = null)
    {
        if (actual.Contains(unexpected, StringComparison.Ordinal))
        {
            throw new AssertFailedException(message ?? $"期望不包含 \"{unexpected}\"，实际 \"{actual}\"");
        }
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, T item, string? message = null)
    {
        if (collection.Contains(item))
        {
            throw new AssertFailedException(message ?? $"期望集合不包含 {Format(item)}");
        }
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message = null)
    {
        if (collection.Any(predicate))
        {
            throw new AssertFailedException(message ?? "期望集合中没有满足条件的元素");
        }
    }

    public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message = null)
    {
        if (!collection.Any(predicate))
        {
            throw new AssertFailedException(message ??
                $"期望集合中有满足条件的元素，实际 [{string.Join(" | ", collection)}]");
        }
    }

    public static void StartsWith(string expected, string actual, string? message = null)
    {
        if (!actual.StartsWith(expected, StringComparison.Ordinal))
        {
            throw new AssertFailedException(message ?? $"期望以 \"{expected}\" 开头，实际 \"{actual}\"");
        }
    }

    private static string Format(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        _ => value.ToString() ?? "null",
    };
}

/// <summary>反射扫描并逐个执行测试，输出结果，返回进程退出码。</summary>
public static class TestRunner
{
    public static int RunAll(Assembly assembly)
    {
        var passed = 0;
        var failures = new List<string>();
        var namespaceName = typeof(TestRunner).Namespace;

        var testTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace == namespaceName)
            .Where(t => t.GetConstructor(Type.EmptyTypes) is not null)
            .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Any(m => m.GetCustomAttribute<FactAttribute>() is not null ||
                                   m.GetCustomAttribute<TheoryAttribute>() is not null))
            .OrderBy(t => t.Name);

        foreach (var type in testTypes)
        {
            var instance = Activator.CreateInstance(type);
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var isFact = method.GetCustomAttribute<FactAttribute>() is not null;
                var isTheory = method.GetCustomAttribute<TheoryAttribute>() is not null;
                if (!isFact && !isTheory) continue;

                if (isFact)
                {
                    Execute($"{type.Name}.{method.Name}", method, instance, [], ref passed, failures);
                    continue;
                }

                var rows = method.GetCustomAttributes<InlineDataAttribute>().ToList();
                if (rows.Count == 0)
                {
                    Execute($"{type.Name}.{method.Name}", method, instance, [], ref passed, failures);
                }

                foreach (var row in rows)
                {
                    var name = $"{type.Name}.{method.Name}({string.Join(", ", row.Data.Select(d => d ?? "null"))})";
                    Execute(name, method, instance, row.Data, ref passed, failures);
                }
            }
        }

        Console.WriteLine();
        if (failures.Count == 0)
        {
            Console.WriteLine($"全部通过：{passed} 项测试");
            return 0;
        }

        Console.WriteLine($"失败 {failures.Count} 项 / 通过 {passed} 项");
        foreach (var failure in failures)
        {
            Console.WriteLine("  " + failure);
        }

        return 1;
    }

    private static void Execute(
        string name,
        MethodInfo method,
        object? instance,
        object?[] arguments,
        ref int passed,
        List<string> failures)
    {
        try
        {
            method.Invoke(instance, arguments);
            passed++;
            Console.WriteLine($"  PASS  {name}");
        }
        catch (TargetInvocationException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            failures.Add($"FAIL  {name}: {message}");
            Console.WriteLine($"  FAIL  {name}: {message}");
        }
        catch (Exception ex)
        {
            failures.Add($"FAIL  {name}: {ex.Message}");
            Console.WriteLine($"  FAIL  {name}: {ex.Message}");
        }
    }
}
