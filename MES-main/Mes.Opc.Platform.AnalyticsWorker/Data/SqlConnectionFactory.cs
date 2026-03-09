using Mes.Opc.Platform.AnalyticsWorker.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Mes.Opc.Platform.AnalyticsWorker.Data;

public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration, IOptions<AnalyticsWorkerOptions> options)
    {
        var name = options.Value.ConnectionStringName;
        _connectionString = configuration.GetConnectionString(name)
            ?? throw new InvalidOperationException($"Missing connection string '{name}'.");
    }

    public SqlConnection Create() => new(_connectionString);
}
