using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HotelManagement.Infrastructure.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add …</c> build the model without booting the API
/// host, which would otherwise try to connect to a database that isn't configured
/// locally. The connection string is a placeholder — <c>migrations add</c> only
/// needs the provider, not a live database.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=hotelos_designtime;Username=postgres;Password=postgres",
                sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        return new ApplicationDbContext(options);
    }
}
