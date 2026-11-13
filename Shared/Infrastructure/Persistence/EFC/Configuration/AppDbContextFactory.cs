using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace foll_notifications.Shared.Infrastructure.Persistence.EFC.Configuration;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json").AddEnvironmentVariables().Build();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(config.GetConnectionString("DefaultConnection"),
                pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "notification"))
            .UseSnakeCaseNamingConvention().Options;
        return new AppDbContext(options);
    }
}
