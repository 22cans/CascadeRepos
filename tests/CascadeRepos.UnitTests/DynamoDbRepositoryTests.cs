using Amazon.DynamoDBv2.DataModel;
using CascadeRepos.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CascadeRepos.UnitTests;

public class DynamoDbRepositoryTests
{
    private readonly Mock<IDynamoDBContext> _dynamoDbContextMock;
    private readonly DynamoDbRepository<SomeObject, string> _repository;

    public DynamoDbRepositoryTests()
    {
        _dynamoDbContextMock = new Mock<IDynamoDBContext>();
        _repository = new DynamoDbRepository<SomeObject, string>(
            Mock.Of<ILogger<CascadeRepository<SomeObject, string>>>(),
            Mock.Of<IDateTimeProvider>(),
            _dynamoDbContextMock.Object);
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
        _dynamoDbContextMock
            .Setup(d => d.LoadAsync<SomeObject>(It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value);

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
        SomeObject? value = null;
        _dynamoDbContextMock
            .Setup(d => d.LoadAsync<SomeObject?>(key, default))
            .ReturnsAsync(value);

        // Act
        var result = await _repository.Get(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Get_With_Range_Key_Adapter_Loads_Async_With_Key_And_Range_Key()
    {
        // Arrange
        var key = "cacheKey";
        var rangeKey = "rangeKey";
        var originalKey = $"{key}:{rangeKey}";
        var value = new SomeObject
        {
            Id = originalKey,
            Name = "Some Name"
        };

        _repository.AdaptKeyToKey(obj => obj.Split(':')[0]);
        _repository.SetRangeKey(obj => obj.Split(':')[1]);

        _dynamoDbContextMock
            .Setup(d => d.LoadAsync<SomeObject>(key, rangeKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value);

        // Act
        var result = await _repository.Get(originalKey);

        // Assert
        Assert.Same(value, result);
    }

    [Fact]
    public async Task Get_With_Range_Key_Adapter_And_DynamoDbOperationConfig_Loads_Async_With_Key_And_Range_Key()
    {
        // Arrange
        var key = "cacheKey";
        var rangeKey = "rangeKey";
        var originalKey = $"{key}:{rangeKey}";
        var config = new DynamoDBOperationConfig
        {
            IndexName = "IndexName"
        };
        var value = new SomeObject
        {
            Id = originalKey,
            Name = "Some Name"
        };

        _repository.AdaptKeyToKey(obj => obj.Split(':')[0]);
        _repository.SetRangeKey(obj => obj.Split(':')[1]);
        _repository.SetOperationConfig(config);

        _dynamoDbContextMock
            .Setup(d => d.LoadAsync<SomeObject>(key, rangeKey, config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value);

        // Act
        var result = await _repository.Get(originalKey);

        // Assert
        Assert.Same(value, result);
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
        _dynamoDbContextMock.Verify(d => d.SaveAsync(value, default), Times.Once);
    }

    [Fact]
    public async Task Set_Configures_TimeToLive()
    {
        // Arrange
        var key = "cacheKey";
        var defaultExpirationTime = DateTime.Now.AddDays(2); 
        var value = new SomeExpirableObject
        {
            Id = key,
            Name = "Some Name",
            ExpirationTime = defaultExpirationTime
        };
        var repository = new DynamoDbRepository<SomeExpirableObject, string>(
            Mock.Of<ILogger<CascadeRepository<SomeExpirableObject, string>>>(),
            Mock.Of<IDateTimeProvider>(),
            _dynamoDbContextMock.Object);
        
        // Act
        var expirationTime = DateTimeOffset.Now.AddMinutes(5);
        repository.SetTimeToLiveProperty(nameof(SomeExpirableObject.ExpirationTime));
        repository.SetAbsoluteExpiration(expirationTime);
        await repository.Set(key, value, false, CancellationToken.None);

        // Assert
        Assert.Equal(expirationTime.UtcDateTime, value.ExpirationTime); 
    }
    
    [Fact]
    public async Task GetAll_Returns_ListOfItems()
    {
        // Arrange
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };
        var search = new FakeAsyncSearch<SomeObject>()
            .SetResults(new List<SomeObject> { value });

        _dynamoDbContextMock
            .Setup(x => x.ScanAsync<SomeObject>(null, null))
            .Returns(search);

        // Act
        var result = await _repository.GetAll();

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(value, result[0]);
    }

    [Fact]
    public async Task GetAll_Returns_Empty_List()
    {
        // Arrange
        var search = new FakeAsyncSearch<SomeObject>();

        _dynamoDbContextMock
            .Setup(x => x.ScanAsync<SomeObject>(null, null))
            .Returns(search);

        // Act
        var result = await _repository.GetAll();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAll_Returns_List_With_DynamoDBOperationConfig()
    {
        // Arrange
        var search = new FakeAsyncSearch<SomeObject>();
        var config = new DynamoDBOperationConfig
        {
            IndexName = "IndexName"
        };

        _dynamoDbContextMock
            .Setup(x => x.ScanAsync<SomeObject>(null, config))
            .Returns(search);

        // Act
        _repository.SetOperationConfig(config);

        var result = await _repository.GetAll();

        // Assert
        Assert.Empty(result);
    }

    [Fact(Skip = "The method `dbContext.CreateBatchWrite` is not mockable, which blocks the tests.")]
    public async Task GetAll_Adds_Items_Using_CoreSetAll()
    {
        // Arrange
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var memoryCacheRepo = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<ILogger<CascadeRepository<SomeObject, string>>>(),
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));
        await memoryCacheRepo.Set(key, value);

        var search = new FakeAsyncSearch<SomeObject>();
        var batch = new Mock<BatchWrite>();

        _dynamoDbContextMock
            .Setup(x => x.CreateBatchWrite<SomeObject>(null))
            .Returns((BatchWrite<SomeObject>)batch.Object);
        _repository.SetNext(memoryCacheRepo);
        _dynamoDbContextMock
            .Setup(x => x.ScanAsync<SomeObject>(null, null))
            .Returns(search);
        _repository.SetNext(memoryCacheRepo);

        // Act
        var result = await _repository.GetAll(true, CancellationToken.None);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(value, result[0]);
        _dynamoDbContextMock.Verify(x => x.CreateBatchWrite<SomeObject>(null), Times.Once);
        _dynamoDbContextMock.Verify(x => x.SaveAsync(It.IsAny<SomeObject>(), It.IsAny<CancellationToken>()),
            Times.Never);

        batch.Verify(x => x.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Without_OperationConfig()
    {
        // Arrange
        const string key = "cacheKey";
        _dynamoDbContextMock
            .Setup(x => x.DeleteAsync<SomeObject>(key, null, null, CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        await _repository.Delete(key, true, CancellationToken.None);

        // Assert
        _dynamoDbContextMock.Verify(x => x.DeleteAsync<SomeObject>(key, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
        _dynamoDbContextMock.Verify(
            x => x.DeleteAsync<SomeObject>(key, null, It.IsNotNull<DynamoDBOperationConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Delete_With_OperationConfig()
    {
        // Arrange
        const string key = "cacheKey";
        var config = new DynamoDBOperationConfig { IndexName = "IndexName" };

        _dynamoDbContextMock
            .Setup(x => x.DeleteAsync<SomeObject>(key, null, config, CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        _repository.SetOperationConfig(config);
        await _repository.Delete(key, true, CancellationToken.None);

        // Assert
        _dynamoDbContextMock.Verify(x => x.DeleteAsync<SomeObject>(key, null, null, It.IsAny<CancellationToken>()),
            Times.Never);
        _dynamoDbContextMock.Verify(
            x => x.DeleteAsync<SomeObject>(key, null, config, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Delete_With_RangeKey_Without_OperationConfig()
    {
        // Arrange
        const string key = "cacheKey";
        const string rangeKey = "rangeKey";
        const string originalKey = $"{key}:{rangeKey}";

        _repository.AdaptKeyToKey(obj => obj.Split(':')[0]);
        _repository.SetRangeKey(obj => obj.Split(':')[1]);

        _dynamoDbContextMock
            .Setup(x => x.DeleteAsync<SomeObject>(key, rangeKey, null, CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        await _repository.Delete(originalKey, true, CancellationToken.None);

        // Assert
        _dynamoDbContextMock.Verify(
            x => x.DeleteAsync<SomeObject>(key, rangeKey, null, It.IsAny<CancellationToken>()),
            Times.Once);
        _dynamoDbContextMock.Verify(x => x.DeleteAsync<SomeObject>(key, null, null, It.IsAny<CancellationToken>()),
            Times.Never);
        _dynamoDbContextMock.Verify(
            x => x.DeleteAsync<SomeObject>(key, null, It.IsNotNull<DynamoDBOperationConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _dynamoDbContextMock.Verify(
            x => x.DeleteAsync<SomeObject>(key, rangeKey, It.IsNotNull<DynamoDBOperationConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Delete_With_RangeKey_With_OperationConfig()
    {
        // Arrange
        const string key = "cacheKey";
        const string rangeKey = "rangeKey";
        const string originalKey = $"{key}:{rangeKey}";
        var config = new DynamoDBOperationConfig { IndexName = "IndexName" };

        _repository.AdaptKeyToKey(obj => obj.Split(':')[0]);
        _repository.SetRangeKey(obj => obj.Split(':')[1]);
        _repository.SetOperationConfig(config);

        _dynamoDbContextMock
            .Setup(x => x.DeleteAsync<SomeObject>(key, rangeKey, config, CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        await _repository.Delete(originalKey, true, CancellationToken.None);

        // Assert
        _dynamoDbContextMock.Verify(
            x => x.DeleteAsync<SomeObject>(key, rangeKey, null, It.IsAny<CancellationToken>()),
            Times.Never);
        _dynamoDbContextMock.Verify(x => x.DeleteAsync<SomeObject>(key, null, null, It.IsAny<CancellationToken>()),
            Times.Never);
        _dynamoDbContextMock.Verify(
            x => x.DeleteAsync<SomeObject>(key, null, It.IsNotNull<DynamoDBOperationConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _dynamoDbContextMock.Verify(
            x => x.DeleteAsync<SomeObject>(key, rangeKey, config, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetList_Returns_ListOfItems()
    {
        // Arrange
        const string listId = "ListId";
        var value = new SomeObject
        {
            Id = "cacheKey",
            Name = "Some Name"
        };
        var search = new FakeAsyncSearch<SomeObject>()
            .SetResults(new List<SomeObject> { value });

        _dynamoDbContextMock
            .Setup(x => x.QueryAsync<SomeObject>(listId, It.IsAny<DynamoDBOperationConfig>()))
            .Returns(search);

        // Act
        var result = await _repository.GetList(listId);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(value, result[0]);
    }

    [Fact]
    public async Task GetList_Returns_Empty_List()
    {
        // Arrange
        const string listId = "ListId";
        var search = new FakeAsyncSearch<SomeObject>();

        _dynamoDbContextMock
            .Setup(x => x.QueryAsync<SomeObject>(listId, It.IsAny<DynamoDBOperationConfig>()))
            .Returns(search);

        // Act
        var result = await _repository.GetList(listId);

        // Assert
        Assert.Empty(result);
    }
}