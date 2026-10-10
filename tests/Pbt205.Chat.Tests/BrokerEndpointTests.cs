using Pbt205.Messaging;

namespace Pbt205.Chat.Tests;

public class BrokerEndpointTests
{
    [Fact]
    public void BarePortMeansThisComputer()
    {
        Assert.True(BrokerEndpoint.TryParse("5672", out BrokerEndpoint? endpoint, out _));
        Assert.Equal(new BrokerEndpoint("localhost", 5672), endpoint);
    }

    [Fact]
    public void HostAndPort()
    {
        Assert.True(BrokerEndpoint.TryParse("100.74.122.113:5673", out BrokerEndpoint? endpoint, out _));
        Assert.Equal(new BrokerEndpoint("100.74.122.113", 5673), endpoint);
    }

    [Fact]
    public void HostOnlyUsesDefaultPort()
    {
        Assert.True(BrokerEndpoint.TryParse("rabbit-host", out BrokerEndpoint? endpoint, out _));
        Assert.Equal(new BrokerEndpoint("rabbit-host", BrokerEndpoint.DefaultPort), endpoint);
    }

    [Fact]
    public void AmqpUrlIsAccepted()
    {
        Assert.True(BrokerEndpoint.TryParse("amqp://rabbit-host:5672/", out BrokerEndpoint? endpoint, out _));
        Assert.Equal(new BrokerEndpoint("rabbit-host", 5672), endpoint);
    }

    [Theory]
    [InlineData("")]
    [InlineData("rabbit-host:abc")]
    [InlineData("rabbit-host:0")]
    [InlineData("rabbit-host:70000")]
    [InlineData("rabbit host:5672")]
    public void InvalidEndpointsAreRejectedWithAnError(string text)
    {
        Assert.False(BrokerEndpoint.TryParse(text, out BrokerEndpoint? endpoint, out string error));
        Assert.Null(endpoint);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
