using Microsoft.EntityFrameworkCore;
using Pluck.Api.Persistence;
using Pluck.Shared.Models;

namespace Pluck.Api.Repositories;

public class UserRepository(AppDbContext db)
{
    public async Task<User?> GetByApiKeyHash(string apiKeyHash)
    {
        return await db.Users.SingleOrDefaultAsync(u => u.ApiKeyHash == apiKeyHash);
    }
}