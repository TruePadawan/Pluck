namespace Pluck.Api.Services.Analytics;

public interface IGeoLocationService
{
    (string? City, string? Country) LookupLocation(string ipAddress);
}
