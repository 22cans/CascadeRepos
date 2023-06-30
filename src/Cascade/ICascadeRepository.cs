namespace Cascade;

/// <summary>
///     Represents the interface for a cascade repository.
/// </summary>
/// <typeparam name="T">The type of the items stored in the repository.</typeparam>
/// <typeparam name="K">The type of the keys used to access the items.</typeparam>
public interface ICascadeRepository<T, K>
{
    /// <summary>
    ///     Finds the repository of type <paramref name="repositoryType" /> in the repository chain at the specified index.
    /// </summary>
    /// <param name="repositoryType">The type of the repository to find.</param>
    /// <param name="index">The zero-based index of the repository to find in case of multiple repositories of the same type.</param>
    /// <returns>
    ///     The repository of type <paramref name="repositoryType" /> at the specified index if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    ///     This method searches the repository chain to find the repository of the specified type at the given index.
    ///     If there are multiple repositories of the same type in the chain, the index parameter determines which repository
    ///     to return.
    ///     The index is zero-based, where 0 represents the first occurrence of the repository type in the chain.
    ///     If the specified repository type is not found at the given index, the method returns <c>null</c>.
    /// </remarks>
    ICascadeRepository<T, K>? FindRepository(Type repositoryType, uint index = 0);

    /// <summary>
    ///     Finds the repository of type <typeparamref name="R" /> in the repository chain at the specified index.
    /// </summary>
    /// <typeparam name="R">The type of the repository to find.</typeparam>
    /// <param name="index">The zero-based index of the repository to find in case of multiple repositories of the same type.</param>
    /// <returns>
    ///     The repository of type <typeparamref name="R" /> at the specified index if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    ///     This method searches the repository chain to find the repository of the specified type at the given index.
    ///     If there are multiple repositories of the same type in the chain, the index parameter determines which repository
    ///     to return.
    ///     The index is zero-based, where 0 represents the first occurrence of the repository type in the chain.
    ///     If the specified repository type is not found at the given index, the method returns <c>null</c>.
    /// </remarks>
    R? FindRepository<R>(uint index = 0) where R : ICascadeRepository<T, K>;

    /// <summary>
    ///     Sets the key adapter function used to adapt keys before accessing the repository.
    /// </summary>
    /// <param name="keyAdapter">The key adapter function.</param>
    /// <returns>The current repository instance.</returns>
    ICascadeRepository<T, K> AdaptKeyToKey(Func<K, K> keyAdapter);

    /// <summary>
    ///     Sets the key adapter function used to adapt keys before accessing the repository.
    /// </summary>
    /// <param name="keyAdapter">The key adapter function.</param>
    /// <returns>The current repository instance.</returns>
    ICascadeRepository<T, K> AdaptObjectToKey(Func<T, K> keyAdapter);

    /// <summary>
    ///     Sets the key for GetAll/SetAll functions to the repository.
    /// </summary>
    /// <param name="key">The new key. Default value: "SomeObject_List", where "SomeObject" is your T class.</param>
    /// <returns>The current repository instance.</returns>
    ICascadeRepository<T, K> AdaptGetSetAllKey(string key);

    /// <summary>
    ///     Sets the key prefix for GetList function to the repository. It will concatenate the value with the ListId.
    ///     Only used when the ListKey is not set.
    /// </summary>
    /// <param name="prefix">The new key. Default value: "SomeObject", where "SomeObject" is your T class.</param>
    /// <returns>The current repository instance.</returns>
    ICascadeRepository<T, K> AdaptListPrefix(string prefix);

    /// <summary>
    ///     Sets a key for GetList function to the repository. It will use this key, independent from the ListId passed.
    /// </summary>
    /// <param name="key">The new key.</param>
    /// <returns>The current repository instance.</returns>
    ICascadeRepository<T, K> AdaptListKey(string key);

    /// <summary>
    ///     Gets the next repository in the chain.
    /// </summary>
    /// <returns>The next repository in the chain, or <c>null</c> if no next repository exists.</returns>
    ICascadeRepository<T, K>? GetNext();

    /// <summary>
    ///     Refreshes the item associated with the specified key, getting. its value from the last repository in the chain.
    /// </summary>
    /// <param name="originalKey">The original key used to access the item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task Refresh(K originalKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sets the next repository in the chain.
    /// </summary>
    /// <param name="nextRepository">The next repository to set.</param>
    /// <returns>The next repository instance.</returns>
    ICascadeRepository<T, K> SetNext(ICascadeRepository<T, K> nextRepository);

    /// <summary>
    ///     Sets the time to live (TTL) for the items stored in the repository.
    /// </summary>
    /// <param name="time">The time to live duration for the items.</param>
    /// <returns>The updated repository instance.</returns>
    ICascadeRepository<T, K> SetTimeToLive(TimeSpan? time);

    /// <summary>
    ///     Sets the absolute expiration time for the items stored in the repository.
    /// </summary>
    /// <param name="expirationTime">The absolute expiration time for the items.</param>
    /// <returns>The updated repository instance.</returns>
    ICascadeRepository<T, K> SetAbsoluteExpiration(DateTimeOffset expirationTime);

    /// <summary>
    ///     Marks the specified repository type <typeparamref name="R" /> as skipped for read operations.
    /// </summary>
    /// <typeparam name="R">The type of the repository to skip for read operations.</typeparam>
    /// <returns>The current repository instance.</returns>
    ICascadeRepository<T, K> SkipRead<R>();

    /// <summary>
    ///     Marks the current repository as skipped for read operations.
    /// </summary>
    /// <returns>The current repository instance.</returns>
    ICascadeRepository<T, K> SkipReadThis();

    /// <summary>
    ///     Marks the specified repository type <typeparamref name="R" /> as skipped for write operations.
    /// </summary>
    /// <typeparam name="R">The type of the repository to skip for write operations.</typeparam>
    /// <returns>The current repository instance.</returns>
    ICascadeRepository<T, K> SkipWrite<R>();

    /// <summary>
    ///     Marks the current repository as skipped for write operations.
    /// </summary>
    /// <returns>The current repository instance.</returns>
    ICascadeRepository<T, K> SkipWriteThis();

    /// <summary>
    ///     Retrieves the item with the specified key from the repository.
    /// </summary>
    /// <param name="key">The key of the item to retrieve.</param>
    /// <param name="updateDownStream">
    ///     Specifies whether to update downstream repositories if the item is retrieved from the
    ///     next repository in the chain.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retrieved item, or <c>null</c> if the item is not found.</returns>
    Task<T?> Get(K key, bool updateDownStream = true, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves a collection of all objects from the repository and optionally updates downstream repositories.
    /// </summary>
    /// <param name="updateDownStream">Specifies whether to update downstream repositories with the retrieved items.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of all objects retrieved from the repository.</returns>
    Task<IList<T>> GetAll(bool updateDownStream = true, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves a collection of all objects from the repository and optionally updates downstream repositories.
    /// </summary>
    /// <param name="listId">The list id of the items to retrieve.</param>
    /// <param name="updateDownStream">Specifies whether to update downstream repositories with the retrieved items.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of all objects retrieved from the repository.</returns>
    Task<IList<T>> GetList<L>(L listId, bool updateDownStream = true, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sets the item with the specified key in the repository.
    /// </summary>
    /// <param name="key">The key of the item to set.</param>
    /// <param name="item">The item to set.</param>
    /// <param name="updateDownStream">Specifies whether to update downstream repositories.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task Set(K key, T item, bool updateDownStream = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sets the items with a generic key, based on the Type, in the repository.
    /// </summary>
    /// <param name="items">The items List to set.</param>
    /// <param name="updateDownStream">Specifies whether to update downstream repositories.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SetAll(List<T> items, bool updateDownStream = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes the item with the specified key in the repository.
    /// </summary>
    /// <param name="key">The key of the item to set.</param>
    /// <param name="deleteDownStream">Specifies whether to delete of downstream repositories.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task Delete(K key, bool deleteDownStream = false, CancellationToken cancellationToken = default);
}