using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SqlServerMcp.Models;
using System.Globalization;

namespace SqlServerMcp.Services;

public sealed class SqlServerService
{
    private const int DefaultCommandTimeoutSeconds = 30;
    private const string ConnectionStringEnvironmentVariable = "SQLSERVER_CONNECTION_STRING";
    private const string CommandTimeoutEnvironmentVariable = "SQLSERVER_COMMAND_TIMEOUT_SECONDS";

    private readonly ILogger<SqlServerService> _logger;
    private readonly QueryValidator _queryValidator;
    private readonly int _commandTimeoutSeconds;

    public SqlServerService(ILogger<SqlServerService> logger, QueryValidator queryValidator)
    {
        _logger = logger;
        _queryValidator = queryValidator;
        _commandTimeoutSeconds = int.TryParse(
            Environment.GetEnvironmentVariable(CommandTimeoutEnvironmentVariable),
            out var timeoutSeconds) && timeoutSeconds > 0
            ? timeoutSeconds
            : DefaultCommandTimeoutSeconds;
    }

    public int CommandTimeoutSeconds => _commandTimeoutSeconds;

    public string GetConnectionStringOrThrow()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"SQL Server connection string not configured. Set the {ConnectionStringEnvironmentVariable} environment variable.");
        }

        return connectionString;
    }

    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = GetConnectionStringOrThrow();

        try
        {
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (SqlException ex)
        {
            _logger.LogError(
                ex,
                "Failed to connect to SQL Server. DataSource={DataSource}, Database={Database}",
                TryGetConnectionStringValue(connectionString, "Data Source")
                    ?? TryGetConnectionStringValue(connectionString, "Server"),
                TryGetConnectionStringValue(connectionString, "Initial Catalog")
                    ?? TryGetConnectionStringValue(connectionString, "Database"));

            throw new InvalidOperationException($"Failed to connect to SQL Server: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error while opening SQL Server connection.");
            throw new InvalidOperationException($"Failed to connect to SQL Server: {ex.Message}", ex);
        }
    }

    public async Task<object?> ExecuteScalarAsync(
        string sql,
        CancellationToken cancellationToken = default)
    {
        _queryValidator.ValidateOrThrow(sql);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteTrustedScalarAsync("SELECT 1", cancellationToken);
        if (result is null || Convert.ToInt32(result) != 1)
        {
            throw new InvalidOperationException("SQL Server connection test failed: unexpected response.");
        }
    }

    public async Task<IReadOnlyList<QueryRow>> ListDatabasesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                name AS Name,
                database_id AS DatabaseId,
                create_date AS CreateDate,
                state_desc AS State
            FROM sys.databases
            WHERE HAS_DBACCESS(name) = 1
            ORDER BY name
            """;

        return await ExecuteTrustedQueryAsync(sql, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<QueryRow>> ListTablesAsync(
        string database,
        CancellationToken cancellationToken = default)
    {
        SqlIdentifierHelper.ValidateIdentifier(database, nameof(database));
        var quotedDatabase = SqlIdentifierHelper.QuoteIdentifier(database);

        var sql = $"""
            SELECT
                TABLE_SCHEMA AS SchemaName,
                TABLE_NAME AS TableName,
                TABLE_TYPE AS TableType
            FROM {quotedDatabase}.INFORMATION_SCHEMA.TABLES
            ORDER BY TABLE_SCHEMA, TABLE_NAME
            """;

        return await ExecuteTrustedQueryAsync(sql, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<QueryRow>> DescribeTableAsync(
        string database,
        string table,
        CancellationToken cancellationToken = default)
    {
        SqlIdentifierHelper.ValidateIdentifier(database, nameof(database));
        var (schema, tableName) = SqlIdentifierHelper.ParseTableReference(table);
        var quotedDatabase = SqlIdentifierHelper.QuoteIdentifier(database);

        var sql = $"""
            SELECT
                c.COLUMN_NAME AS ColumnName,
                c.DATA_TYPE AS DataType,
                c.CHARACTER_MAXIMUM_LENGTH AS CharacterMaximumLength,
                c.NUMERIC_PRECISION AS NumericPrecision,
                c.NUMERIC_SCALE AS NumericScale,
                c.IS_NULLABLE AS IsNullable,
                c.ORDINAL_POSITION AS OrdinalPosition,
                CASE
                    WHEN pk.COLUMN_NAME IS NOT NULL THEN 'PK'
                    ELSE NULL
                END AS KeyInfo
            FROM {quotedDatabase}.INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT
                    ku.TABLE_SCHEMA,
                    ku.TABLE_NAME,
                    ku.COLUMN_NAME
                FROM {quotedDatabase}.INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                INNER JOIN {quotedDatabase}.INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                    AND tc.TABLE_NAME = ku.TABLE_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk
                ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA
                AND c.TABLE_NAME = pk.TABLE_NAME
                AND c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @schema
                AND c.TABLE_NAME = @tableName
            ORDER BY c.ORDINAL_POSITION
            """;

        return await ExecuteTrustedQueryAsync(
            sql,
            new Dictionary<string, object?>
            {
                ["@schema"] = schema,
                ["@tableName"] = tableName
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<QueryRow>> FindColumnsAsync(
        string database,
        string columnSearch,
        CancellationToken cancellationToken = default)
    {
        SqlIdentifierHelper.ValidateIdentifier(database, nameof(database));
        SqlIdentifierHelper.ValidateColumnSearchTerm(columnSearch, nameof(columnSearch));

        var quotedDatabase = SqlIdentifierHelper.QuoteIdentifier(database);
        var sql = $"""
            SELECT
                TABLE_SCHEMA AS SchemaName,
                TABLE_NAME AS TableName,
                COLUMN_NAME AS ColumnName,
                DATA_TYPE AS DataType,
                IS_NULLABLE AS IsNullable,
                ORDINAL_POSITION AS OrdinalPosition
            FROM {quotedDatabase}.INFORMATION_SCHEMA.COLUMNS
            WHERE COLUMN_NAME LIKE @columnPattern
            ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION
            """;

        return await ExecuteTrustedQueryAsync(
            sql,
            new Dictionary<string, object?>
            {
                ["@columnPattern"] = $"%{columnSearch}%"
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<QueryRow>> FindRelationshipsAsync(
        string database,
        string table,
        CancellationToken cancellationToken = default)
    {
        SqlIdentifierHelper.ValidateIdentifier(database, nameof(database));
        var (schema, tableName) = SqlIdentifierHelper.ParseOptionalTableReference(table);
        var quotedDatabase = SqlIdentifierHelper.QuoteIdentifier(database);

        var sql = $"""
            SELECT
                SCHEMA_NAME(parent.schema_id) AS FromSchema,
                parent.name AS FromTable,
                parentColumn.name AS FromColumn,
                SCHEMA_NAME(referenced.schema_id) AS ToSchema,
                referenced.name AS ToTable,
                referencedColumn.name AS ToColumn,
                foreignKey.name AS ForeignKeyName
            FROM {quotedDatabase}.sys.foreign_keys foreignKey
            INNER JOIN {quotedDatabase}.sys.foreign_key_columns foreignKeyColumn
                ON foreignKey.object_id = foreignKeyColumn.constraint_object_id
            INNER JOIN {quotedDatabase}.sys.tables parent
                ON foreignKeyColumn.parent_object_id = parent.object_id
            INNER JOIN {quotedDatabase}.sys.columns parentColumn
                ON foreignKeyColumn.parent_object_id = parentColumn.object_id
                AND foreignKeyColumn.parent_column_id = parentColumn.column_id
            INNER JOIN {quotedDatabase}.sys.tables referenced
                ON foreignKeyColumn.referenced_object_id = referenced.object_id
            INNER JOIN {quotedDatabase}.sys.columns referencedColumn
                ON foreignKeyColumn.referenced_object_id = referencedColumn.object_id
                AND foreignKeyColumn.referenced_column_id = referencedColumn.column_id
            WHERE
                (
                    @schema IS NULL
                    AND (parent.name = @tableName OR referenced.name = @tableName)
                )
                OR
                (
                    @schema IS NOT NULL
                    AND (
                        (SCHEMA_NAME(parent.schema_id) = @schema AND parent.name = @tableName)
                        OR (SCHEMA_NAME(referenced.schema_id) = @schema AND referenced.name = @tableName)
                    )
                )
            ORDER BY FromSchema, FromTable, FromColumn, ToSchema, ToTable, ToColumn
            """;

        return await ExecuteTrustedQueryAsync(
            sql,
            new Dictionary<string, object?>
            {
                ["@schema"] = schema ?? (object)DBNull.Value,
                ["@tableName"] = tableName
            },
            cancellationToken);
    }

    public int MaxSampleRows => DataAnalysisHelper.GetMaxSampleRows();

    public async Task<IReadOnlyList<QueryRow>> SampleDataAsync(
        string database,
        string table,
        int limit,
        CancellationToken cancellationToken = default)
    {
        SqlIdentifierHelper.ValidateIdentifier(database, nameof(database));
        var (schema, tableName) = await ResolveTableReferenceAsync(database, table, cancellationToken);
        var effectiveLimit = DataAnalysisHelper.ClampSampleLimit(limit, MaxSampleRows);
        var quotedDatabase = SqlIdentifierHelper.QuoteIdentifier(database);
        var quotedSchema = SqlIdentifierHelper.QuoteIdentifier(schema);
        var quotedTable = SqlIdentifierHelper.QuoteIdentifier(tableName);

        var sql = $"""
            SELECT TOP (@limit) *
            FROM {quotedDatabase}.{quotedSchema}.{quotedTable}
            """;

        return await ExecuteTrustedQueryAsync(
            sql,
            new Dictionary<string, object?> { ["@limit"] = effectiveLimit },
            cancellationToken);
    }

    public async Task<long> RowCountAsync(
        string database,
        string table,
        CancellationToken cancellationToken = default)
    {
        SqlIdentifierHelper.ValidateIdentifier(database, nameof(database));
        var (schema, tableName) = await ResolveTableReferenceAsync(database, table, cancellationToken);
        var quotedDatabase = SqlIdentifierHelper.QuoteIdentifier(database);
        var quotedSchema = SqlIdentifierHelper.QuoteIdentifier(schema);
        var quotedTable = SqlIdentifierHelper.QuoteIdentifier(tableName);

        var sql = $"""
            SELECT COUNT_BIG(1) AS TotalRows
            FROM {quotedDatabase}.{quotedSchema}.{quotedTable}
            """;

        var result = await ExecuteTrustedScalarAsync(sql, cancellationToken);
        return ConvertScalarToInt64(result);
    }

    private static long ConvertScalarToInt64(object? result)
    {
        return result switch
        {
            null => throw new InvalidOperationException("Row count query returned no value."),
            long value => value,
            int value => value,
            decimal value => (long)value,
            _ => Convert.ToInt64(result, CultureInfo.InvariantCulture)
        };
    }

    public async Task<QueryRow> GetColumnStatsAsync(
        string database,
        string table,
        string column,
        CancellationToken cancellationToken = default)
    {
        SqlIdentifierHelper.ValidateIdentifier(database, nameof(database));
        SqlIdentifierHelper.ValidateColumnSearchTerm(column, nameof(column));

        var (schema, tableName) = await ResolveTableReferenceAsync(database, table, cancellationToken);
        var columnInfo = await GetColumnMetadataAsync(database, schema, tableName, column, cancellationToken);
        var dataType = columnInfo.GetValueOrDefault("DataType")?.ToString();

        var quotedDatabase = SqlIdentifierHelper.QuoteIdentifier(database);
        var quotedSchema = SqlIdentifierHelper.QuoteIdentifier(schema);
        var quotedTable = SqlIdentifierHelper.QuoteIdentifier(tableName);
        var quotedColumn = SqlIdentifierHelper.QuoteIdentifier(column);

        var includeMinMax = DataAnalysisHelper.SupportsMinMax(dataType);
        var sql = includeMinMax
            ? $"""
                SELECT
                    COUNT(1) AS TotalRows,
                    SUM(CASE WHEN {quotedColumn} IS NULL THEN 1 ELSE 0 END) AS NullCount,
                    COUNT(DISTINCT {quotedColumn}) AS DistinctCount,
                    MIN({quotedColumn}) AS MinValue,
                    MAX({quotedColumn}) AS MaxValue
                FROM {quotedDatabase}.{quotedSchema}.{quotedTable}
                """
            : $"""
                SELECT
                    COUNT(1) AS TotalRows,
                    SUM(CASE WHEN {quotedColumn} IS NULL THEN 1 ELSE 0 END) AS NullCount,
                    COUNT(DISTINCT {quotedColumn}) AS DistinctCount
                FROM {quotedDatabase}.{quotedSchema}.{quotedTable}
                """;

        var rows = await ExecuteTrustedQueryAsync(sql, cancellationToken: cancellationToken);
        if (rows.Count == 0)
        {
            throw new InvalidOperationException($"Unable to calculate statistics for column '{column}'.");
        }

        var stats = rows[0];
        stats["ColumnName"] = column;
        stats["DataType"] = dataType;
        stats["MinMaxSupported"] = includeMinMax;
        return stats;
    }

    public int MaxSelectRows => DataAnalysisHelper.GetMaxSelectRows();

    public int MaxResultSizeBytes => DataAnalysisHelper.GetMaxResultSizeBytes();

    public async Task<SelectQueryResult> ExecuteSelectAsync(
        string database,
        string sql,
        CancellationToken cancellationToken = default)
    {
        SqlIdentifierHelper.ValidateIdentifier(database, nameof(database));
        _queryValidator.ValidateOrThrow(sql);

        var maxRows = MaxSelectRows;
        var maxResultSizeBytes = MaxResultSizeBytes;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ChangeDatabaseAsync(database, cancellationToken);

        await using var command = CreateCommand(connection, sql);

        var rows = new List<QueryRow>();
        var totalSize = 0;
        var rowLimitReached = false;
        var resultSizeLimitReached = false;

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (rows.Count >= maxRows)
                {
                    rowLimitReached = true;
                    break;
                }

                var row = ReadRow(reader);
                var rowSize = QueryResultFormatter.EstimateRowSize(row);

                if (rows.Count > 0 && totalSize + rowSize > maxResultSizeBytes)
                {
                    resultSizeLimitReached = true;
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        rowLimitReached = true;
                    }

                    break;
                }

                rows.Add(row);
                totalSize += rowSize;

                if (totalSize >= maxResultSizeBytes)
                {
                    resultSizeLimitReached = true;
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        rowLimitReached = true;
                    }

                    break;
                }
            }

            if (!rowLimitReached && rows.Count == maxRows && await reader.ReadAsync(cancellationToken))
            {
                rowLimitReached = true;
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to execute SELECT query.");
            throw new InvalidOperationException($"Failed to execute SELECT query: {ex.Message}", ex);
        }

        return new SelectQueryResult
        {
            Rows = rows,
            MaxRows = maxRows,
            MaxResultSizeBytes = maxResultSizeBytes,
            RowLimitReached = rowLimitReached,
            ResultSizeLimitReached = resultSizeLimitReached
        };
    }

    private static QueryRow ReadRow(SqlDataReader reader)
    {
        var row = new QueryRow();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    private async Task<(string Schema, string Table)> ResolveTableReferenceAsync(
        string database,
        string table,
        CancellationToken cancellationToken)
    {
        var (schema, tableName) = SqlIdentifierHelper.ParseOptionalTableReference(table);
        if (schema is not null)
        {
            return (schema, tableName);
        }

        var quotedDatabase = SqlIdentifierHelper.QuoteIdentifier(database);
        var matches = await ExecuteTrustedQueryAsync(
            $"""
            SELECT TABLE_SCHEMA AS SchemaName
            FROM {quotedDatabase}.INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = @tableName
                AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA
            """,
            new Dictionary<string, object?> { ["@tableName"] = tableName },
            cancellationToken);

        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"Table '{tableName}' was not found in database '{database}'.");
        }

        if (matches.Count > 1)
        {
            var schemas = string.Join(", ", matches.Select(row => row["SchemaName"]?.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)));
            throw new InvalidOperationException(
                $"Table '{tableName}' exists in multiple schemas ({schemas}). Specify schema.table.");
        }

        return (matches[0]["SchemaName"]!.ToString()!, tableName);
    }

    private async Task<QueryRow> GetColumnMetadataAsync(
        string database,
        string schema,
        string tableName,
        string column,
        CancellationToken cancellationToken)
    {
        var quotedDatabase = SqlIdentifierHelper.QuoteIdentifier(database);
        var rows = await ExecuteTrustedQueryAsync(
            $"""
            SELECT DATA_TYPE AS DataType
            FROM {quotedDatabase}.INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema
                AND TABLE_NAME = @tableName
                AND COLUMN_NAME = @columnName
            """,
            new Dictionary<string, object?>
            {
                ["@schema"] = schema,
                ["@tableName"] = tableName,
                ["@columnName"] = column
            },
            cancellationToken);

        if (rows.Count == 0)
        {
            throw new InvalidOperationException($"Column '{column}' was not found in table '{schema}.{tableName}'.");
        }

        return rows[0];
    }

    private async Task<object?> ExecuteTrustedScalarAsync(
        string sql,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<QueryRow>> ExecuteTrustedQueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);

        if (parameters is not null)
        {
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }
        }

        var rows = new List<QueryRow>();

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = ReadRow(reader);
                rows.Add(row);
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to execute SQL query.");
            throw new InvalidOperationException($"Failed to execute SQL query: {ex.Message}", ex);
        }

        return rows;
    }

    private SqlCommand CreateCommand(SqlConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = _commandTimeoutSeconds;
        return command;
    }

    private static string? TryGetConnectionStringValue(string connectionString, string key)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return key switch
            {
                "Data Source" => builder.DataSource,
                "Server" => builder.DataSource,
                "Initial Catalog" => builder.InitialCatalog,
                "Database" => builder.InitialCatalog,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}
