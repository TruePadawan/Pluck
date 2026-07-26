using Dapper;
using Pluck.Api.Data;
using Pluck.Shared.Models;

namespace Pluck.Api.Repositories;

public class UserRepository(DbConnectionFactory dbFactory)
{
    public async Task<User?> GetByApiKeyHash(string apiKeyHash)
    {
        using var dbConnection = dbFactory.CreateConnection();
        const string userLookUpQuery = "SELECT * FROM Users WHERE ApiKeyHash = @Hash";
        return await dbConnection.QuerySingleOrDefaultAsync<User>(userLookUpQuery, new { Hash = apiKeyHash });
    }
}