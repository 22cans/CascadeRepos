using Amazon.DynamoDBv2.DataModel;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace CascadeRepos.UnitTests;

public class CascadeRepositoryTests
{
    private const string SampleKey1 = "The Key 1";
    private const string SampleKey2 = "The Key 2";
    private const string SampleValue1 = "The Value 1";
    private const string SampleValue2 = "The Value 2";

    private readonly MemoryCacheRepository<string, string> _memoryCacheRepository1, _memoryCacheRepository2;
    private readonly RedisRepository<string, string> _redisRepository1;

    public CascadeRepositoryTests()
    {
        _memoryCacheRepository1 = new MemoryCacheRepository<string, string>(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

        _memoryCacheRepository2 = new MemoryCacheRepository<string, string>(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

        _redisRepository1 = new RedisRepository<string, string>(
            Mock.Of<IConnectionMultiplexer>(),
            Options.Create(new RedisRepositoryOptions { TimeToLiveInSeconds = 60 }));
    }

    [Fact]
    public async Task Get_Goes_To_Next_Repository_When_Value_Not_Present()
    {
        // Arrange
        await _memoryCacheRepository1
            .SetNext(_memoryCacheRepository2)
            .Set(SampleKey1, SampleValue1);

        // Act
        var result = await _memoryCacheRepository1.Get(SampleKey1);

        // Assert
        Assert.Equal(SampleValue1, result);
    }

    [Fact]
    public async Task Get_Goes_To_Next_Repository_When_Value_Expired()
    {
        // Arrange
        _memoryCacheRepository1
            .SetTimeToLive(TimeSpan.FromSeconds(1))
            .SetNext(_memoryCacheRepository2);

        await _memoryCacheRepository1.Set(SampleKey1, SampleValue1);
        await _memoryCacheRepository2.Set(SampleKey1, SampleValue2);

        // Act
        var result1 = await _memoryCacheRepository1.Get(SampleKey1);
        await Task.Delay(TimeSpan.FromSeconds(2));
        var result2 = await _memoryCacheRepository1.Get(SampleKey1);

        // Assert
        Assert.Equal(SampleValue1, result1);
        Assert.Equal(SampleValue2, result2);
    }

    [Fact]
    public void GetNext_Returns_Next_Repository()
    {
        // Arrange
        _memoryCacheRepository1
            .SetNext(_memoryCacheRepository2);

        // Act
        var nextRepository = _memoryCacheRepository1.GetNext();

        // Assert
        Assert.Equal(_memoryCacheRepository2, nextRepository);
    }

    [Fact]
    public void FindRepositoryByType_Returns_Repository_When_Found()
    {
        // Arrange
        _memoryCacheRepository1
            .SetNext(_redisRepository1);

        // Act
        var foundRepository = _memoryCacheRepository1.FindRepository<RedisRepository<string, string>>();

        // Assert
        Assert.Equal(_redisRepository1, foundRepository);
    }

    [Fact]
    public void FindRepositoryByType_Returns_Null_When_Not_Found()
    {
        // Arrange
        _memoryCacheRepository1
            .SetNext(_redisRepository1);

        // Act
        var foundRepository = _memoryCacheRepository1.FindRepository<DynamoDbRepository<string, string>>();

        // Assert
        Assert.Null(foundRepository);
    }

    [Fact]
    public void FindRepositoryByType_Returns_Repository_When_Found_At_Index()
    {
        // Arrange
        _memoryCacheRepository1
            .SetNext(_redisRepository1)
            .SetNext(_memoryCacheRepository2);

        // Act
        var foundRepository = _memoryCacheRepository1.FindRepository<MemoryCacheRepository<string, string>>(index: 1);

        // Assert
        Assert.Equal(_memoryCacheRepository2, foundRepository);
    }

    [Fact]
    public void FindRepositoryByType_Returns_Null_When_Index_Out_Of_Range()
    {
        // Arrange
        _memoryCacheRepository1
            .SetNext(_redisRepository1)
            .SetNext(_memoryCacheRepository2);

        // Act
        var foundRepository = _memoryCacheRepository1.FindRepository<MemoryCacheRepository<string, string>>(index: 2);

        // Assert
        Assert.Null(foundRepository);
    }

    [Fact]
    public async Task SkipRead_Returns_Instance_With_NextType_As_R()
    {
        // Arrange
        var key = "someKey";
        var databaseMock = new Mock<IDatabase>();
        var connectionMultiplexerMock = new Mock<IConnectionMultiplexer>();
        connectionMultiplexerMock
            .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(databaseMock.Object);
        var memoryCacheMock = new Mock<IMemoryCache>();
        var repo1 = new MemoryCacheRepository<string, string>(memoryCacheMock.Object,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 1 }));
        var repo2 = new RedisRepository<string, string>(connectionMultiplexerMock.Object,
            Options.Create(new RedisRepositoryOptions { TimeToLiveInSeconds = 300 }));
        repo1.SetNext(repo2);

        // Act
        await repo1
            .SkipRead<MemoryCacheRepository<string, string>>()
            .Get(key, false);

        // Assert
        memoryCacheMock.Verify(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny!), Times.Never);
        databaseMock.Verify(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
        memoryCacheMock.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task SkipWrite_SetsSkipWritingFlag_AndSkipsWritingForNextRepository()
    {
        // Arrange
        var key = "someKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };
        var databaseMock = new Mock<IDatabase>();
        var connectionMultiplexerMock = new Mock<IConnectionMultiplexer>();
        connectionMultiplexerMock
            .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(databaseMock.Object);
        var memoryCacheMock = new Mock<IMemoryCache>();
        var repo1 = new MemoryCacheRepository<SomeObject, string>(memoryCacheMock.Object,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 1 }));
        var repo2 = new RedisRepository<SomeObject, string>(connectionMultiplexerMock.Object,
            Options.Create(new RedisRepositoryOptions { TimeToLiveInSeconds = 300 }));
        repo1.SetNext(repo2);
        repo1.SkipWrite<IMemoryCacheRepository>();

        // Act
        await repo1.Set(key, value, true, CancellationToken.None);

        // Assert
        memoryCacheMock.Verify(c => c.CreateEntry(key), Times.Never);
    }

    [Fact]
    public async Task Get_Updates_Repositories_When_UpdateDownStream_Is_True()
    {
        // Arrange
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };
        var dynamoDbContextMock = new Mock<IDynamoDBContext>();
        dynamoDbContextMock
            .Setup(d => d.LoadAsync<SomeObject>(It.IsAny<string>(), It.IsAny<object>(),
                It.IsAny<DynamoDBOperationConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value);
        var memoryCacheMock = new Mock<IMemoryCache>();
        memoryCacheMock
            .Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());
        var repo1 = new MemoryCacheRepository<SomeObject, string>(memoryCacheMock.Object,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 1 }));
        var repo2 = new DynamoDbRepository<SomeObject, string>(dynamoDbContextMock.Object);
        repo1.SetNext(repo2);

        // Act
        await repo1
            .SkipRead<MemoryCacheRepository<SomeObject, string>>()
            .Get(key);

        // Assert
        memoryCacheMock.Verify(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny!), Times.Never);
        dynamoDbContextMock.Verify(
            x => x.LoadAsync<SomeObject>(It.IsAny<object>(), It.IsAny<object>(), It.IsAny<DynamoDBOperationConfig>(),
                It.IsAny<CancellationToken>()), Times.Once);
        memoryCacheMock.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task Set_Adds_Item_And_Doesnt_Update_Downstream()
    {
        // Arrange
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };
        var dynamoDbContextMock = new Mock<IDynamoDBContext>();
        var memoryCacheMock = new Mock<IMemoryCache>();
        memoryCacheMock
            .Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());
        var repo1 = new MemoryCacheRepository<SomeObject, string>(memoryCacheMock.Object,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 1 }));
        var repo2 = new DynamoDbRepository<SomeObject, string>(dynamoDbContextMock.Object);
        repo1.SetNext(repo2);

        // Act
        await repo1.Set(key, value, false, CancellationToken.None);

        // Assert
        memoryCacheMock.Verify(c => c.CreateEntry(key), Times.Once);
        dynamoDbContextMock.Verify(x => x.SaveAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Set_Adds_Item_And_Updates_Downstream()
    {
        // Arrange
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };
        var dynamoDbContextMock = new Mock<IDynamoDBContext>();
        var memoryCacheMock = new Mock<IMemoryCache>();
        memoryCacheMock
            .Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());
        var repo1 = new MemoryCacheRepository<SomeObject, string>(memoryCacheMock.Object,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 1 }));
        var repo2 = new DynamoDbRepository<SomeObject, string>(dynamoDbContextMock.Object);
        repo1.SetNext(repo2);

        // Act
        await repo1.Set(key, value, true, CancellationToken.None);

        // Assert
        memoryCacheMock.Verify(c => c.CreateEntry(key), Times.Once);
        dynamoDbContextMock.Verify(x => x.SaveAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Removes_Item_And_Doesnt_Update_Downstream()
    {
        // Arrange
        var key = "cacheKey";
        var dynamoDbContextMock = new Mock<IDynamoDBContext>();
        var memoryCacheMock = new Mock<IMemoryCache>();
        memoryCacheMock
            .Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());
        var repo1 = new MemoryCacheRepository<SomeObject, string>(memoryCacheMock.Object,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 1 }));
        var repo2 = new DynamoDbRepository<SomeObject, string>(dynamoDbContextMock.Object);
        repo1.SetNext(repo2);

        // Act
        await repo1.Delete(key, false, CancellationToken.None);

        // Assert
        memoryCacheMock.Verify(c => c.Remove(key), Times.Once);
        dynamoDbContextMock.Verify(
            x => x.DeleteAsync<SomeObject>(It.IsAny<object>(), It.IsAny<object>(), It.IsAny<DynamoDBOperationConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Delete_Removes_Item_And_Updates_Downstream()
    {
        // Arrange
        var key = "cacheKey";
        var dynamoDbContextMock = new Mock<IDynamoDBContext>();
        var memoryCacheMock = new Mock<IMemoryCache>();
        memoryCacheMock
            .Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());
        var repo1 = new MemoryCacheRepository<SomeObject, string>(memoryCacheMock.Object,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 1 }));
        var repo2 = new DynamoDbRepository<SomeObject, string>(dynamoDbContextMock.Object);
        repo1.SetNext(repo2);

        // Act
        await repo1.Delete(key, true, CancellationToken.None);

        // Assert
        memoryCacheMock.Verify(c => c.Remove(key), Times.Once);
        dynamoDbContextMock.Verify(
            x => x.DeleteAsync<SomeObject>(It.IsAny<object>(), It.IsAny<object>(), It.IsAny<DynamoDBOperationConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Refresh_WhenNextRepositoryIsNull_CallsGetWithUpdateDownStreamTrue()
    {
        // Arrange
        var key = "cacheKey";
        var dynamoDbContextMock = new Mock<IDynamoDBContext>();
        var repo = new DynamoDbRepository<SomeObject, string>(dynamoDbContextMock.Object);

        // Act
        await repo.Refresh(key);

        // Assert
        dynamoDbContextMock.Verify(
            x => x.LoadAsync<SomeObject>(key, It.IsAny<object>(), It.IsAny<DynamoDBOperationConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Refresh_WhenNextRepositoryIsNotNull_CallsRefreshOnNextRepository()
    {
        // Arrange
        var key = "cacheKey";
        var dynamoDbContextMock = new Mock<IDynamoDBContext>();
        var memoryCacheMock = new Mock<IMemoryCache>();
        memoryCacheMock
            .Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());
        var repo1 = new MemoryCacheRepository<SomeObject, string>(memoryCacheMock.Object,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 1 }));
        var repo2 = new DynamoDbRepository<SomeObject, string>(dynamoDbContextMock.Object);
        repo1.SetNext(repo2);

        // Act
        await repo1.Refresh(key);

        // Assert
        memoryCacheMock.Verify(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny!), Times.Never);
        dynamoDbContextMock.Verify(
            x => x.LoadAsync<SomeObject>(key, It.IsAny<object>(), It.IsAny<DynamoDBOperationConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void CalculateExpirationTime_Returns_TimeToLive_Expiration()
    {
        // Arrange
        var timeToLive = TimeSpan.FromMinutes(10);
        var repository = new TestCascadeRepository<string, string>(timeToLive: timeToLive);

        // Act
        var result = repository.CalculateExpirationTime();

        // Assert
        var expectedExpiration = DateTimeOffset.UtcNow.Add(timeToLive);
        expectedExpiration.Should().BeCloseTo(result!.Value, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void CalculateExpirationTime_Returns_ExpirationTime_When_TimeToLive_Is_Null()
    {
        // Arrange
        var expirationTime = DateTimeOffset.UtcNow.AddMinutes(30);
        var repository = new TestCascadeRepository<string, string>(expirationTime: expirationTime);

        // Act
        var result = repository.CalculateExpirationTime();

        // Assert
        result.Should().Be(expirationTime);
    }

    [Fact]
    public void CalculateExpirationTime_Returns_Null_When_Both_TimeToLive_And_ExpirationTime_Are_Null()
    {
        // Arrange
        var repository = new TestCascadeRepository<string, string>();

        // Act
        var result = repository.CalculateExpirationTime();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CalculateExpirationTime_Returns_ExpirationTime_When_TimeToLiveExpiration_Greater_Than_ExpirationTime()
    {
        // Arrange
        var timeToLive = TimeSpan.FromMinutes(10);
        var expirationTime = DateTimeOffset.UtcNow.AddMinutes(5);
        var repository =
            new TestCascadeRepository<string, string>(timeToLive: timeToLive, expirationTime: expirationTime);

        // Act
        var result = repository.CalculateExpirationTime();

        // Assert
        result.Should().Be(expirationTime);
    }

    [Fact]
    public void
        CalculateExpirationTime_Returns_TimeToLiveExpiration_When_TimeToLiveExpiration_Lesser_Than_ExpirationTime()
    {
        // Arrange
        var timeToLive = TimeSpan.FromMinutes(5);
        var expirationTime = DateTimeOffset.UtcNow.AddMinutes(10);
        var repository =
            new TestCascadeRepository<string, string>(timeToLive: timeToLive, expirationTime: expirationTime);

        // Act
        var result = repository.CalculateExpirationTime();

        // Assert
        result.Should().BeCloseTo(DateTimeOffset.UtcNow.Add(timeToLive), TimeSpan.FromMilliseconds(100));
    }
}