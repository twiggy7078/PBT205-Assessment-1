using Pbt205.Messaging;

namespace Pbt205.Chat.Tests;

public class ConnectionTests
{
    [Fact]
    public void CredentialsNeverShowThePassword()
    {
        var credentials = new BrokerCredentials("jane", "s3cret-password");
        Assert.DoesNotContain("s3cret-password", credentials.ToString());
        Assert.Contains("jane", credentials.ToString());
    }

    [Fact]
    public async Task UnreachableBrokerGivesAReadableError()
    {
        // Nothing listens on port 1, so the connection is refused straight away.
        var endpoint = new BrokerEndpoint("localhost", 1);
        var credentials = new BrokerCredentials("jane", "s3cret-password");

        MessageBusException ex = await Assert.ThrowsAsync<MessageBusException>(
            () => MessageBus.ConnectAsync(endpoint, credentials, "test"));

        Assert.Contains("localhost:1", ex.Message);
        Assert.DoesNotContain("s3cret-password", ex.Message);
    }
}
