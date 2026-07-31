using Microsoft.EntityFrameworkCore;
using Pluck.Api.Persistence;
using Pluck.Shared.Dtos.Users;
using Pluck.Shared.Models;

namespace Pluck.Api.Repositories;

public class UserRepository(AppDbContext db)
{
    public async Task<User?> GetByApiKeyHash(string apiKeyHash)
    {
        return await db.Users.SingleOrDefaultAsync(u => u.ApiKeyHash == apiKeyHash);
    }

    public async Task<User> CreateUser(CreateUserDto userDto)
    {
        var user = User.Create(userDto.Name, userDto.ApiKeyHash, userDto.Role);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<bool> NameExists(string name)
    {
        return await db.Users.AnyAsync(u => u.Name == name);
    }
}