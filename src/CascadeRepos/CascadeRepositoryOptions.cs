using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace CascadeRepos;

/// <summary>
///     Represents the options for the <see cref="CascadeRepository{T, K}" /> class.
/// </summary>
[ExcludeFromCodeCoverage]
public class CascadeRepositoryOptions
{
    /// <summary>
    ///     The time to live (TTL) for the cached items, in seconds.
    /// </summary>
    public int? TimeToLiveInSeconds { get; init; }

    /// <summary>
    ///     The time to live (TTL) per entity, for the cached items, in seconds.
    /// </summary>
    public IDictionary<string, (int? TimeToLiveInSeconds, ExpirationType? ExpirationType)?>? TimeToLiveInSecondsByEntity
    {
        get;
        init;
    }

    /// <summary>
    ///     The default type of expiration for entities not specified in <see cref="TimeToLiveInSecondsByEntity" />.
    /// </summary>
    public ExpirationType DefaultExpirationType { get; init; } = ExpirationType.Absolute;
}

/// <summary>
///     Represents the types of cache entry expiration.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExpirationType
{
    /// <summary>
    ///     Indicates absolute expiration, where the cache entry is considered expired after a specific duration or at a
    ///     specific point in time.
    /// </summary>
    Absolute = 0,

    /// <summary>
    ///     Indicates sliding expiration, where the expiration time for a cache entry is extended each time the entry is
    ///     accessed.
    /// </summary>
    Sliding = 1
}