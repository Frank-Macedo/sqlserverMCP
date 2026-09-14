using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class QueryTools(SqlServerService sqlServerService)
{
    [McpServerTool(Name = "execute_select"), Description("Executes a read-only SELECT query against a database. The query is validated, respects configured timeouts, and results are limited by row count and size.")]
    public async Task<string> ExecuteSelect(
        [Description("Database name")] string database,
        [Description("Read-only SELECT or WITH query")] string sql,
        CancellationToken cancellationToken)
    {
        var result = await sqlServerService.ExecuteSelectAsync(database, sql, cancellationToken);
        return QueryResultFormatter.FormatSelectResult(database, sql, result);
    }
}
