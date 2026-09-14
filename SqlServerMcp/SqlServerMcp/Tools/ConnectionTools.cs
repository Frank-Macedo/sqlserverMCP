using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class ConnectionTools(SqlServerService sqlServerService)
{
    [McpServerTool(Name = "test_connection"), Description("Verifies that the SQL Server MCP server is running and can connect to SQL Server.")]
    public async Task<string> TestConnection(CancellationToken cancellationToken)
    {
        await sqlServerService.TestConnectionAsync(cancellationToken);

        var connectionString = sqlServerService.GetConnectionStringOrThrow();
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);

        return $"SQL Server MCP is running. Connection test successful. Server={builder.DataSource}; Database={builder.InitialCatalog}; CommandTimeout={sqlServerService.CommandTimeoutSeconds}s";
    }
}
