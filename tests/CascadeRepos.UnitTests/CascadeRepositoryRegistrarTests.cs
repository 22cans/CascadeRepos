using Amazon.DynamoDBv2.DataModel;
using CascadeRepos.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace CascadeRepos.UnitTests;

public class CascadeRepositoryRegistrarTests
{
    [Fact]
    public void ConfigureCascade_Registers_Repositories()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient(_ => Mock.Of<IMemoryCache>());
        services.AddTransient(_ => Mock.Of<IConnectionMultiplexer>());
        services.AddTransient(_ => Mock.Of<IDynamoDBContext>());
        services.AddTransient(_ => Mock.Of<IDateTimeProvider>());
        var configValues = new Dictionary<string, string>
        {
            ["CascadeRepos:MemoryCache:TimeToLiveInSeconds"] = "60",
            ["CascadeRepos:Redis:TimeToLiveInSeconds"] = "300"
        };
        var configurationBuilder = new ConfigurationBuilder().AddInMemoryCollection(configValues!);
        var configuration = configurationBuilder.Build();

        // Act
        services.ConfigureCascadeRepos(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var memoryCacheRepo = serviceProvider.GetRequiredService<MemoryCacheRepository<SomeObject, int>>();
        var redisRepo = serviceProvider.GetRequiredService<RedisRepository<SomeObject, int>>();
        var dynamoDbRepo = serviceProvider.GetRequiredService<DynamoDbRepository<SomeObject, int>>();

        Assert.NotNull(memoryCacheRepo);
        Assert.NotNull(redisRepo);
        Assert.NotNull(dynamoDbRepo);
    }

    [Fact]
    public void AddCascade_Registers_Repositories_In_Order()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient(_ => Mock.Of<IMemoryCache>());
        services.AddTransient(_ => Mock.Of<IConnectionMultiplexer>());
        services.AddTransient(_ => Mock.Of<IDynamoDBContext>());
        services.AddTransient(_ => Mock.Of<IDateTimeProvider>());
        var configValues = new Dictionary<string, string>
        {
            ["CascadeRepos:MemoryCache:TimeToLiveInSeconds"] = "60",
            ["CascadeRepos:Redis:TimeToLiveInSeconds"] = "300"
        };
        var configurationBuilder = new ConfigurationBuilder().AddInMemoryCollection(configValues!);
        var configuration = configurationBuilder.Build();
        services.ConfigureCascadeRepos(configuration);

        // Act
        services.AddCascadeRepos<SomeObject, int>(
            typeof(DynamoDbRepository<,>),
            typeof(MemoryCacheRepository<,>),
            typeof(RedisRepository<,>));

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var dynamoRepo = serviceProvider.GetRequiredService<ICascadeRepository<SomeObject, int>>();
        var memoryCacheRepo = dynamoRepo.GetNext();
        var redisRepo = memoryCacheRepo!.GetNext();

        Assert.NotNull(memoryCacheRepo);
        Assert.NotNull(redisRepo);
        Assert.NotNull(dynamoRepo);
    }
}