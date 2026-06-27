namespace QMSofts.Identity;

/// <summary>
/// Railway/Heroku expose Postgres as a URL (postgres://user:pass@host:port/db).
/// Npgsql wants keyword=value form. This converts when needed.
/// </summary>
public static class NpgsqlConnectionHelper
{
    public static string Normalize(string connectionString)
    {
        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString; // already keyword form
        }

        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':', 2);
        var db = uri.AbsolutePath.TrimStart('/');

        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            Database = db,
            SslMode = Npgsql.SslMode.Require
        };

        return builder.ConnectionString;
    }
}
