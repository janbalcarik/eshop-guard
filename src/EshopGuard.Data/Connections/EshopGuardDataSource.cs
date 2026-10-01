using Microsoft.Extensions.Configuration;
using Npgsql;

namespace EshopGuard.Data.Connections;

/// <summary>
/// Connection pool of one host for its fixed <see cref="DatabaseRole"/>. Created on first use, so a missing
/// connection string surfaces in the startup guard as <c>config.connection_string_missing</c>, not in DI.
/// </summary>
public sealed class EshopGuardDataSource : IAsyncDisposable, IDisposable
{
    private readonly Lazy<NpgsqlDataSource> _source;

    /// <summary>Creates the holder; reads the configuration lazily.</summary>
    public EshopGuardDataSource(IConfiguration configuration, DatabaseRole role)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Role = role;
        _source = new Lazy<NpgsqlDataSource>(() => new NpgsqlDataSourceBuilder(BuildConnectionString(configuration, role)).Build());
    }

    /// <summary>Role the host expects to connect as.</summary>
    public DatabaseRole Role { get; }

    /// <summary>The pool. Throws <see cref="EshopGuardConfigurationException"/> when the connection string is missing.</summary>
    public NpgsqlDataSource Source => _source.Value;

    /// <summary>
    /// Reads <c>ConnectionStrings:{name}</c> for the role, sets <c>Application Name</c> and forces <c>Include Error Detail</c>
    /// off (error details would carry row values, i.e. customer texts). No default connection is ever filled in.
    /// </summary>
    public static string BuildConnectionString(IConfiguration configuration, DatabaseRole role)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var value = configuration.GetConnectionString(role.ConnectionStringName());
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new EshopGuardConfigurationException(DataErrorCodes.ConnectionStringMissing, role.ConnectionStringKey());
        }

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(value);
        }
        catch (ArgumentException)
        {
            // The parser's message may quote the value; report only the key.
            throw new EshopGuardConfigurationException("config.connection_string_invalid", role.ConnectionStringKey());
        }

        if (string.IsNullOrEmpty(builder.ApplicationName))
        {
            builder.ApplicationName = role.ApplicationName();
        }

        builder.IncludeErrorDetail = false;
        return builder.ConnectionString;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_source.IsValueCreated)
        {
            await _source.Value.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_source.IsValueCreated)
        {
            _source.Value.Dispose();
        }
    }
}
