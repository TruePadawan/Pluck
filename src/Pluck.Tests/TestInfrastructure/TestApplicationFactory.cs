using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pluck.Api.Services.Analytics;
using Moq;

namespace Pluck.Tests.TestInfrastructure;

/// <summary>
/// WebApplicationFactory that mocks external services and provides a temporary upload directory.
/// </summary>
public class TestApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly Mock<IAnalyticsQueue> _mockQueue;
    private readonly Mock<IGeoLocationService> _mockGeo;

    /// <summary>Creates a new test host with a fresh temp directory and mocked services.</summary>
    public TestApplicationFactory()
    {
        TempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(TempDir);
        _mockQueue = new Mock<IAnalyticsQueue>();
        _mockGeo = new Mock<IGeoLocationService>();
        _mockGeo.Setup(g => g.LookupLocation(It.IsAny<string>()))
            .Returns((null, null));
    }

    /// <summary>Path to the temporary upload directory for this test instance.</summary>
    public string TempDir { get; }

    /// <summary>Mock of the analytics queue service for assertion setup.</summary>
    public Mock<IAnalyticsQueue> MockQueue => _mockQueue;

    /// <summary>Mock of the geolocation service for assertion setup.</summary>
    public Mock<IGeoLocationService> MockGeo => _mockGeo;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(_mockQueue.Object);
            services.AddSingleton(_mockGeo.Object);
        });
        return base.CreateHost(builder);
    }

    public new void Dispose()
    {
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected new virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            base.Dispose();
            if (Directory.Exists(TempDir))
            {
                try
                {
                    Directory.Delete(TempDir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}