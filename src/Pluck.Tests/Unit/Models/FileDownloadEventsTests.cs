using Pluck.Shared.Models.Events;

namespace Pluck.Tests.Unit.Models;

public class FileDownloadEventsTests
{
    [Fact]
    public void Create_ValidParams_ReturnsEvent()
    {
        var fileId = Guid.NewGuid();
        var result = FileDownloadEvents.Create(fileId, "hashed_ip", "London", "UK", ClientType.Browser);

        result.Should().NotBeNull();
        result.FileId.Should().Be(fileId);
        result.HashedIp.Should().Be("hashed_ip");
        result.City.Should().Be("London");
        result.Country.Should().Be("UK");
        result.ClientType.Should().Be(ClientType.Browser);
    }

    [Fact]
    public void Create_NullLocation_ReturnsEventWithNulls()
    {
        var fileId = Guid.NewGuid();
        var result = FileDownloadEvents.Create(fileId, "hashed_ip", null, null, ClientType.PluckCli);

        result.Should().NotBeNull();
        result.City.Should().BeNull();
        result.Country.Should().BeNull();
        result.ClientType.Should().Be(ClientType.PluckCli);
    }

    [Fact]
    public void Create_ClientType_PluckCli_IsSetCorrectly()
    {
        var result = FileDownloadEvents.Create(Guid.NewGuid(), "ip", null, null, ClientType.PluckCli);
        result.ClientType.Should().Be(ClientType.PluckCli);
    }

    [Fact]
    public void Create_ClientType_Script_IsSetCorrectly()
    {
        var result = FileDownloadEvents.Create(Guid.NewGuid(), "ip", null, null, ClientType.Script);
        result.ClientType.Should().Be(ClientType.Script);
    }

    [Fact]
    public void Create_ClientType_Unknown_IsSetCorrectly()
    {
        var result = FileDownloadEvents.Create(Guid.NewGuid(), "ip", null, null, ClientType.Unknown);
        result.ClientType.Should().Be(ClientType.Unknown);
    }
}
