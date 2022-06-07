using Mentalist.BusinessCache.Tests.Samples;
using NSubstitute;
using Xunit;

namespace Mentalist.BusinessCache.Tests;

public class CacheSerializerShould
{
    [Fact]
    public void SerializeAndDeserialize()
    {
        var user = new User();
        user.Accounts.Add(new Account());

        var serializer = new CacheSerializer(Substitute.For<ICacheMetrics>());
        var bytes = serializer.Serialize(new CacheItem<User> {Value = user});
        var item = serializer.Deserialize<CacheItem<User>>(bytes)!;
        var clone = item.Value;

        Assert.Equal(user.Id, clone.Id);
        Assert.Equal(user.Accounts.Count, clone.Accounts.Count);
        Assert.Equal(user.Accounts[0].Id, clone.Accounts[0].Id);
    }

    [Fact]
    public void SerializeAndDeserializeWithUtf8Json()
    {
        var user = new User();
        user.Accounts.Add(new Account());

        var bytes = Utf8Json.JsonSerializer.Serialize(user);
        var clone = Utf8Json.JsonSerializer.Deserialize<User>(bytes)!;

        Assert.Equal(user.Id, clone.Id);
        Assert.Equal(user.Accounts.Count, clone.Accounts.Count);
        Assert.Equal(user.Accounts[0].Id, clone.Accounts[0].Id);
    }
}