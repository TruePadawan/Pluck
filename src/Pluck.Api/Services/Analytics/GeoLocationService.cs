using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Options;
using Pluck.Api.Security;

namespace Pluck.Api.Services.Analytics;

public class GeoLocationService : IGeoLocationService, IDisposable
{
    private readonly DatabaseReader? _reader;
    private readonly ILogger<GeoLocationService> _logger;

    public GeoLocationService(IOptions<PluckApiOptions> options, ILogger<GeoLocationService> logger)
    {
        _logger = logger;
        var path = options.Value.GeoDbPath;
        if (File.Exists(path))
        {
            try
            {
                _reader = new DatabaseReader(path);
                _logger.LogInformation("[LOG] Loaded GeoLite2-City database from {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LOG] Failed to load GeoLite2-City database at {Path}", path);
            }
        }
        else
        {
            _logger.LogWarning("[LOG] GeoLite2-City database not found at {Path}. Geolocation features will be disabled.",
                path);
        }
    }

    public (string? City, string? Country) LookupLocation(string ipAddress)
    {
        if (_reader == null || string.IsNullOrWhiteSpace(ipAddress))
            return (null, null);

        try
        {
            var response = _reader.City(ipAddress);
            return (response.City.Name, response.Country.Name);
        }
        catch (AddressNotFoundException)
        {
            // IP not in the database (e.g., local IPs or unknown)
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LOG] Error looking up location for IP");
            return (null, null);
        }
    }

    public void Dispose()
    {
        _reader?.Dispose();
    }
}