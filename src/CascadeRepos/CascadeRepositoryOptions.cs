using System.Diagnostics.CodeAnalysis;

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
    public int? TimeToLiveInSeconds { get; set; }

    /// <summary>
    ///     The time to live (TTL) per entity, for the cached items, in seconds.
    /// </summary>
    public IDictionary<string, int?>? TimeToLiveInSecondsByEntity { get; set; }
}