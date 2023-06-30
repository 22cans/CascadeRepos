# Cascade Repository
​
The Cascade Repository is a flexible and extensible repository pattern implementation that allows cascading operations across multiple repositories in a chain. It provides a convenient way to work with layered data access, caching, and synchronization strategies.
​
## Overview​

The Cascade Repository follows the repository pattern and introduces the concept of cascading operations. It allows retrieving and storing data in a chain of repositories, where each repository has the option to delegate operations to the next repository in the chain.
​
The main benefits of using the Cascade Repository are:​

- **Layered caching:** The Cascade Repository enables efficient caching strategies by providing options to read and write data to various cache repositories such as memory cache and Redis.

- **Flexible data access:** The Cascade Repository allows chaining multiple data access repositories, such as DynamoDB, SQL databases, or other custom repositories, and provides a unified interface to work with the data.

- **Easy extensibility:** The Cascade Repository can be extended with custom repositories by implementing the `ICascadeRepository<T, K>` interface.
​
## Features​

- **Cascading operations:** The Cascade Repository supports cascading operations across multiple repositories. When retrieving data, it searches the repositories in the chain until it finds the desired item, updating the downstream repositories if needed. When storing data, it propagates the changes to the previous repositories in the chain.

- **Skip reading/writing:** Repositories in the chain can be configured to skip reading or writing operations, allowing fine-grained control over caching and data synchronization.

- **Key adaptation:** The Cascade Repository supports key adaptation functions that can be used to adapt keys before accessing the repositories, enabling key transformation and customization.

## Getting Started

### Instalation

The Cascade Repository library is available as a NuGet package. You can install it using the NuGet Package Manager or by using the .NET CLI.

```shell
dotnet add package CascadeRepository
```

### Dependency Injection

To use the Cascade Repository with MemoryCache and Redis repositories, you can set it up as follows:

```csharp
services
    .AddMemoryCacheRepository(config)
    .AddRedisRepository(config)
    .AddCascade<Product, string>(
        typeof(MemoryCacheRepository<,>),
        typeof(RedisHashRepository<,>))
    .AddCascade<CheckResponse, string>(
        typeof(MemoryCacheRepository<,>),
        typeof(RedisRepository<,>),
        typeof(GenericRepository<,>));
```

If you plan to use MemoryCache, Redis, and DynamoDb repositories, you can use the `ConfigureCascade(IConfiguration)` method to set it up easily:

```csharp
services
    .ConfigureCascade(config)
    .AddCascade<ProductCategory, string>(
        typeof(MemoryCacheRepository<,>),
        typeof(RedisHashRepository<,>),
        typeof(DynamoDbRepository<,>));
```

### Usage

Here's an example of how to use the Cascade Repository in a `ProductCategoryService` class. In this example, the `GetById()` method retrieves a `ProductCategory` by its ID, with the option to bypass caching.

```csharp
public class ProductCategoryService 
{
    private readonly ICascadeRepository<ProductCategory, string> _cascade;

    public ProductCategoryService(ICascadeRepository<ProductCategory, string> cascade)
    {
        _cascade = cascade;
        SetCascadeKeyAdapters();
    }

    private void SetCascadeKeyAdapters()
    {
        _cascade
            .FindRepository<IMemoryCacheRepository<ProductCategory, string>>()!
            .AdaptKeyToKey(key => $"{nameof(ProductCategory)}:{key}");
    }

    private void SkipCascadeCache()
    {
        _cascade
            .SkipRead<IMemoryCacheRepository>()
            .SkipRead<IRedisHashRepository>();
    }

    public async Task<ProductCategory> GetById(string id, bool useCache = true, CancellationToken cancellationToken = default)
    {
        if (!useCache)
            SkipCascadeCache();

        return _cascade.Get(id, true, cancellationToken);
    }
}

```

In this example, if you call ProductCategoryService.GetById() with useCache=false, the Cascade Repository skips the MemoryCache and Redis repositories and goes straight to DynamoDb. However, it will still update both repositories after retrieving the value from DynamoDb.

## Contributing

Contributions to the Cascade Repository are welcome! There are a lot of repositories that still need support. If you find any issues or have suggestions for improvement, please open an issue or submit a pull request on this repository.

When contributing, please follow the existing coding style and conventions. Make sure to add tests for any new functionality or bug fixes.