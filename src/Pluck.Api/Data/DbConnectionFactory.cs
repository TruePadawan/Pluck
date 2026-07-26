using System.Data;
using Microsoft.Data.Sqlite;

namespace Pluck.Api.Data;

public class DbConnectionFactory(IConfiguration configuration)
{
    public IDbConnection CreateConnection()
    {
        return new SqliteConnection(configuration.GetConnectionString("PluckDb"));
    }
}