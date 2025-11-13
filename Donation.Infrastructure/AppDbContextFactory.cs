using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Donation.Infrastructure
{
    public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            var connString = string.IsNullOrWhiteSpace(dbUrl)
                ? "Host=;Port=;Database=;Username=;Password=;SslMode=Require"
                : BuildConnStringFromUrl(dbUrl);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                })
                .UseOpenIddict()
                .Options;

            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            return new AppDbContext(options);
        }

        private static string BuildConnStringFromUrl(string url)
        {
            if (!(url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                  url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)))
            {
                return url;
            }

            var uri = new Uri(url);

            var userInfo = Uri.UnescapeDataString(uri.UserInfo ?? "");
            var parts = userInfo.Split(':', 2);
            var user = parts.Length > 0 ? parts[0] : "";
            var pass = parts.Length > 1 ? parts[1] : "";
            var dbName = uri.AbsolutePath.TrimStart('/');

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort ? 5432 : uri.Port,
                Username = user,
                Password = pass,
                Database = dbName,
                SslMode = SslMode.Require
            };

            var query = uri.Query.TrimStart('?');
            if (!string.IsNullOrEmpty(query))
            {
                foreach (var kv in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kvp = kv.Split('=', 2);
                    var key = Uri.UnescapeDataString(kvp[0]);
                    var val = kvp.Length > 1 ? Uri.UnescapeDataString(kvp[1]) : "";
                    builder[key] = val;
                }
            }

            return builder.ConnectionString;
        }
    }
}
