using Amazon.DynamoDBv2.Model;

namespace CascadeRepos.UnitTests;

public class SomeObject
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
}

public class SomeExpirableObject : SomeObject
{
    public DateTime ExpirationTime { get; set; } = default!;
}