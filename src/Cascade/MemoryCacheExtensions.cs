using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.Caching.Memory;

namespace Cascade;

/// <summary>
///     Provides extension methods for the <see cref="IMemoryCache" /> interface.
/// </summary>
public static class MemoryCacheExtensions
{
    private static readonly Lazy<Func<MemoryCache, object>> LazyGetCoherentState = new(() =>
        CreateGetter<MemoryCache, object>(typeof(MemoryCache).GetField("_coherentState",
            BindingFlags.NonPublic | BindingFlags.Instance)!));

    private static readonly Lazy<Func<object, IDictionary>> LazyGetEntries = new(() =>
        CreateGetter<object, IDictionary>(
            typeof(MemoryCache).GetNestedType("CoherentState", BindingFlags.NonPublic)!.GetField("_entries",
                BindingFlags.NonPublic | BindingFlags.Instance)!));

    private static readonly Func<MemoryCache, IDictionary> GetEntries =
        cache => LazyGetEntries.Value(LazyGetCoherentState.Value(cache));

    private static Func<TParam, TReturn> CreateGetter<TParam, TReturn>(FieldInfo field)
    {
        var methodName = $"{field.ReflectedType!.FullName}.get_{field.Name}";
        var method = new DynamicMethod(methodName, typeof(TReturn), new[] { typeof(TParam) }, typeof(TParam), true);
        var ilGen = method.GetILGenerator();
        ilGen.Emit(OpCodes.Ldarg_0);
        ilGen.Emit(OpCodes.Ldfld, field);
        ilGen.Emit(OpCodes.Ret);
        return (Func<TParam, TReturn>)method.CreateDelegate(typeof(Func<TParam, TReturn>));
    }

    /// <summary>
    ///     Retrieves all values of a specific type from the specified <see cref="IMemoryCache" />.
    /// </summary>
    /// <typeparam name="T">The type of values to retrieve.</typeparam>
    /// <param name="memoryCache">The <see cref="IMemoryCache" /> to retrieve the values from.</param>
    /// <returns>An <see cref="IEnumerable{T}" /> containing all values of the specified type.</returns>
    public static IEnumerable<T> GetValues<T>(this IMemoryCache memoryCache)
    {
        var entries = GetEntries((MemoryCache)memoryCache);
        var cacheValues = entries.Values.OfType<ICacheEntry>();
        var values = cacheValues.Where(x => x.Value is T).Select(x => x.Value).OfType<T>();
        return values;
    }
}