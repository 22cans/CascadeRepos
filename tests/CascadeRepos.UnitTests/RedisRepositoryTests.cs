using CascadeRepos.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using StackExchange.Redis;
using Xunit;

namespace CascadeRepos.UnitTests;

public class RedisRepositoryTests
{
    private readonly Mock<IConnectionMultiplexer> _connectionMultiplexerMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly RedisRepository<SomeObject, string> _repository;

    public RedisRepositoryTests()
    {
        _databaseMock = new Mock<IDatabase>();
        _connectionMultiplexerMock = new Mock<IConnectionMultiplexer>();
        _connectionMultiplexerMock
            .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _repository = new RedisRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            _connectionMultiplexerMock.Object,
            Options.Create(new RedisRepositoryOptions { TimeToLiveInSeconds = 60 }));
    }

    [Fact]
    public async Task Get_Returns_Item_When_Present()
    {
        // Arrange
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };
        _databaseMock
            .Setup(d => d.StringGetAsync(key, It.IsAny<CommandFlags>()))
            .ReturnsAsync(JsonConvert.SerializeObject(value));

        // Act
        var result = await _repository.Get(key);

        // Assert
        Assert.Equivalent(value, result);
    }

    [Fact]
    public async Task Get_Returns_Default_Value_When_Item_Not_Found()
    {
        // Arrange
        var key = "cacheKey";
        _databaseMock
            .Setup(d => d.KeyDeleteAsync(key, It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var result = await _repository.Get(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Set_Adds_Item()
    {
        // Arrange
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };

        // Act
        await _repository.Set(key, value, false, CancellationToken.None);

        // Assert
        _databaseMock.Verify(
            d => d.StringSetAsync(key, It.IsAny<RedisValue>(), null, When.Always, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task Delete_Removes_Item()
    {
        // Arrange
        var key = "cacheKey";

        // Act
        await _repository.Delete(key, false, CancellationToken.None);

        // Assert
        _databaseMock.Verify(
            d => d.KeyDeleteAsync(key, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task GetAll_Returns_List()
    {
        // Arrange
        const string key = "SomeObject_List";
        var value = new List<SomeObject>
        {
            new()
            {
                Id = "SomeKey",
                Name = "Some Name"
            }
        };
        _databaseMock
            .Setup(d => d.StringGetAsync(key, It.IsAny<CommandFlags>()))
            .ReturnsAsync(JsonConvert.SerializeObject(value));

        // Act
        var result = await _repository.GetAll();

        // Assert
        Assert.Equivalent(value, result);
    }

    [Fact]
    public async Task GetAll_Returns_List_When_GetSetAllKey()
    {
        // Arrange
        const string key = "SomeObject_Different_List";
        var value = new List<SomeObject>
        {
            new()
            {
                Id = "SomeKey",
                Name = "Some Name"
            }
        };
        _databaseMock
            .Setup(d => d.StringGetAsync(key, It.IsAny<CommandFlags>()))
            .ReturnsAsync(JsonConvert.SerializeObject(value));

        // Act
        var result = await _repository
            .AdaptGetSetAllKey(key)
            .GetAll();

        // Assert
        Assert.Equivalent(value, result);
    }

    [Fact]
    public async Task GetAll_Try_To_Adds_Items_Using_CoreSetAll()
    {
        // Arrange
        var value = new List<SomeObject>
        {
            new()
            {
                Id = "cacheKey",
                Name = "Some Name"
            }
        };

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var memoryCacheRepo = new MemoryCacheRepository<SomeObject, string>(
            new DefaultDateTimeProvider(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));
        await memoryCacheRepo.SetAll(value);

        _repository
            .SkipRead<IRedisRepository>()
            .SetNext(memoryCacheRepo);


        // Assert
        var result = await _repository.GetAll();

        // Assert
        Assert.Equivalent(value, result);
    }

    [Fact]
    public async Task GetList_Returns_ListOfItems()
    {
        // Arrange
        const string listId = "ListId";
        var value = new List<SomeObject>
        {
            new()
            {
                Id = "SomeKey",
                Name = "Some Name"
            }
        };
        _databaseMock
            .Setup(d => d.StringGetAsync($"{nameof(SomeObject)}:{listId}", It.IsAny<CommandFlags>()))
            .ReturnsAsync(JsonConvert.SerializeObject(value))
            .Verifiable();

        // Act
        var result = await _repository.GetList(listId);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equivalent(value[0], result[0]);
        _databaseMock.Verify();
    }

    [Fact]
    public async Task GetList_Returns_Empty_List()
    {
        // Arrange
        const string listId = "ListId";
        _databaseMock
            .Setup(d => d.StringGetAsync(listId, It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null)
            .Verifiable();

        // Act
        var result = await _repository.GetList("ListId");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetList_Adds_Items_Using_CoreSetList()
    {
        // Arrange
        const string listId = "ListId";
        var value = new SomeObject
        {
            Id = "cacheKey",
            Name = "Some Name"
        };

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        memoryCache.Set($"{nameof(SomeObject)}:{listId}", new List<SomeObject> { value });

        var memoryCacheRepo = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

        _databaseMock
            .Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(RedisValue.Null);
        _repository.SetNext(memoryCacheRepo);

        // Act
        var result = await _repository
            //.AdaptRedisKey(() => nameof(SomeObject))
            .AdaptObjectToKey(obj => obj.Id)
            .GetList(listId, true, CancellationToken.None);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(value, result[0]);
        _databaseMock.Verify(
            d => d.StringSetAsync($"{nameof(SomeObject)}:{listId}", It.IsAny<RedisValue>(), null, When.Always,
                CommandFlags.None), Times.Once);
    }
}