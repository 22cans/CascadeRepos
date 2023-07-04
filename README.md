# CascadeRepos
​
[![NuGet version (CascadeRepos)](https://img.shields.io/nuget/v/CascadeRepos.svg?style=flat-square)](https://www.nuget.org/packages/CascadeRepos/)
[![CI](https://github.com/22cans/CascadeRepos/actions/workflows/push.yml/badge.svg?event=release)](https://github.com/22cans/CascadeRepos/actions/workflows/push.yml?query=event%3Arelease)
[![NuGet Downloads](https://img.shields.io/nuget/dt/CascadeRepos.svg)](https://www.nuget.org/packages/CascadeRepos/)
![GitHub contributors](https://img.shields.io/github/contributors/22cans/CascadeRepos)
![GitHub last commit (by committer)](https://img.shields.io/github/last-commit/22cans/CascadeRepos)
![GitHub commit activity (branch)](https://img.shields.io/github/commit-activity/w/22cans/CascadeRepos)
![GitHub Repo stars](https://img.shields.io/github/stars/22cans/CascadeRepos)
![GitHub forks](https://img.shields.io/github/forks/22cans/CascadeRepos)

CascadeRepos is a flexible and extensible repository pattern implementation that allows cascading operations across multiple repositories in a chain. It provides a convenient way to work with layered data access, caching, and synchronization strategies.
​
## Overview​

CascadeRepos follows the repository pattern and introduces the concept of cascading operations. It allows retrieving and storing data in a chain of repositories, where each repository has the option to delegate operations to the next repository in the chain.
​
The main benefits of using CascadeRepos are:​

- **Layered caching:** CascadeRepos enables efficient caching strategies by providing options to read and write data to various cache repositories such as memory cache and Redis.

- **Flexible data access:** CascadeRepos allows chaining multiple data access repositories, such as DynamoDB, SQL databases, or other custom repositories, and provides a unified interface to work with the data.

- **Easy extensibility:** CascadeRepos can be extended with custom repositories by implementing the `ICascadeRepository<T, K>` interface.
​
## Features​

- **Cascading operations:** CascadeRepos supports cascading operations across multiple repositories. When retrieving data, it searches the repositories in the chain until it finds the desired item, updating the downstream repositories if needed. When storing data, it propagates the changes to the previous repositories in the chain.

- **Skip reading/writing:** Repositories in the chain can be configured to skip reading or writing operations, allowing fine-grained control over caching and data synchronization.

- **Key adaptation:** CascadeRepos supports key adaptation functions that can be used to adapt keys before accessing the repositories, enabling key transformation and customization.

## Getting Started

### Installation

The CascadeRepos library is available as a NuGet package. You can install it using the NuGet Package Manager or by using the .NET CLI.

```shell
dotnet add package CascadeRepos
```

### Dependency Injection

To use CascadeRepos with MemoryCache and Redis, you can set up the required services in your dependency injection container.

#### MemoryCache

Add the MemoryCache service to your service configuration:

```csharp
services.AddMemoryCache();
```

#### Redis

To use Redis, you need to set up the `IConnectionMultiplexer` service. Here's an example of setting up Redis with the StackExchange.Redis library:

```csharp
services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(new ConfigurationOptions
    {
        EndPoints = { { "your-host-come-here", 6379 } },
        DefaultDatabase = 0,
        AbortOnConnectFail = false
    }));
```

#### DynamoDB

To use DynamoDB, you need to set up the `IDynamoDBContext` service from the AWS SDK. Here's an example of setting up DynamoDB with the `AmazonDynamoDBClient` and `DynamoDBContext`:

```csharp
var client = new AmazonDynamoDBClient();
var dbContext = new DynamoDBContext(client);
services.AddSingleton<IDynamoDBContext>(dbContext);
```

#### CascadeRepos

To use CascadeRepos with MemoryCache and Redis repositories, you can set it up as follows:

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

If

 you plan to use MemoryCache, Redis, and DynamoDb repositories, you can use the `ConfigureCascade(IConfiguration)` method to set it up easily:

```csharp
services
    .ConfigureCascade(config)
    .AddCascade<ProductCategory, string>(
        typeof(MemoryCacheRepository<,>),
        typeof(RedisHashRepository<,>),
        typeof(DynamoDbRepository<,>));
```

### Configuration

CascadeRepos can be configured using app settings. Here's an example configuration for MemoryCache and Redis:

```json
"CascadeRepos": {
  "MemoryCache": {
    "TimeToLiveInSeconds": 60
  },
  "Redis": {
    "TimeToLiveInSeconds": null,
    "TimeToLiveInSecondsByEntity": {
      "ProductCategory": 300
    }
  }
}
```

### Usage

Once you have set up the services and configuration, you can use CascadeRepos in your code. Here's an example of how to use it:

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

        return await _cascade.Get(id, true, cancellationToken);
    }
}
```

In the above example, the `ProductCategoryService` class demonstrates how to use CascadeRepos. It retrieves the product category by id, with an option to skip the cache. The key adapters and cache skipping are shown as examples of how to customize the behavior.

Make sure to adjust the class names and types to match your application.

That's it! You're now ready to use CascadeRepos in your application.

## Contributing

Contributions to CascadeRepos are welcome! There are a lot of repositories that still need support. If you find any issues or have suggestions for improvement, please open an issue or submit a pull request on this repository.

When contributing, please follow the existing coding style and conventions. Make sure to add tests for any new functionality or bug fixes.

## Contributors

![GitHub Contributors Image](https://contrib.rocks/image?repo=22cans/CascadeRepos)