namespace CascadeRepos.Extensions;

/// <summary>
///     Provides methods to retrieve the current date and time in UTC.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    ///     Gets the current date and time in UTC.
    /// </summary>
    /// <returns>The current date and time in UTC.</returns>
    DateTime GetUtcNow();
}

/// <summary>
///     Default implementation of the <see cref="IDateTimeProvider" /> interface that uses <see cref="DateTime.UtcNow" />.
/// </summary>
public class DefaultDateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc />
    public DateTime GetUtcNow()
    {
        return DateTime.UtcNow;
    }
}