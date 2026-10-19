using System.Net;
using System.Net.Http.Json;
using Pluck.Shared.Dtos;
using Pluck.Tests.TestInfrastructure;

namespace Pluck.Tests.Integration.Endpoints;

public class MiscEndpointsTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;

    public MiscEndpointsTests(TestApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ping_WithoutApiKey_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/ping");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ping_WithValidApiKey_Returns200()
    {
        // Arrange
        _client.SetUserApiKey();

        // Act
        var response = await _client.GetAsync("/api/ping");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ping_ReturnsCorrectUserName()
    {
        // Arrange
        _client.SetUserApiKey();

        // Act
        var response = await _client.GetAsync("/api/ping");
        var dto = await response.Content.ReadFromJsonAsync<PingUserResponseDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto!.Name.Should().Be("test_user");
    }
}