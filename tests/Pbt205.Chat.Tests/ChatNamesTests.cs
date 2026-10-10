namespace Pbt205.Chat.Tests;

public class ChatNamesTests
{
    [Fact]
    public void NormalUsernameIsAccepted() => Assert.Null(ChatNames.ValidateUsername("Jane"));

    [Fact]
    public void EmptyUsernameIsRejected() => Assert.NotNull(ChatNames.ValidateUsername("   "));

    [Fact]
    public void LongUsernameIsRejected() =>
        Assert.NotNull(ChatNames.ValidateUsername(new string('a', ChatNames.MaxUsernameLength + 1)));

    [Fact]
    public void UsernameWithControlCharacterIsRejected() => Assert.NotNull(ChatNames.ValidateUsername("Ja\tne"));

    [Fact]
    public void RoomWithLettersDigitsHyphenAndUnderscoreIsAccepted() =>
        Assert.Null(ChatNames.ValidateRoom("study_group-1"));

    // ".", "*" and "#" mean something in RabbitMQ routing keys, so they are not allowed in room names.
    [Theory]
    [InlineData("study.room")]
    [InlineData("study*")]
    [InlineData("#")]
    public void RoomWithRoutingCharactersIsRejected(string room) => Assert.NotNull(ChatNames.ValidateRoom(room));

    [Fact]
    public void LongRoomIsRejected() =>
        Assert.NotNull(ChatNames.ValidateRoom(new string('a', ChatNames.MaxRoomLength + 1)));

    [Fact]
    public void RoomNamesAreCaseInsensitive() => Assert.Equal("general", ChatNames.NormaliseRoom(" General "));
}
