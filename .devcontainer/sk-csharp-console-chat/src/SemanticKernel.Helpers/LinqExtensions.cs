using Microsoft.SemanticKernel;

namespace Plugins;

public static class LinqExtensions
{
    private class InlineComparer<T>(Func<T?, T?, bool> equals, Func<T, int> hashCode) : IEqualityComparer<T>
    {
        public bool Equals(T? x, T? y) => equals(x, y);
        public int GetHashCode(T obj) => hashCode(obj);
    }
    public static IEnumerable<T> Union<T>(this IEnumerable<T> first, IEnumerable<T> second,
        Func<T?, T?, bool> equals, Func<T, int>? hashCode = null)
        => first.Union(second, new InlineComparer<T>(equals, hashCode: hashCode ?? (x => x?.GetHashCode() ?? 0)));
    public static KernelArguments ToKernelArguments(this IEnumerable<KeyValuePair<string, object?>> source)
        => new(source.ToDictionary(x => x.Key, x => x.Value));
}