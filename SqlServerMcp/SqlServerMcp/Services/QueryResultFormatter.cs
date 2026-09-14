using System.Globalization;
using System.Text;
using SqlServerMcp.Models;

namespace SqlServerMcp.Services;

internal static class QueryResultFormatter
{
    internal static int EstimateRowSize(QueryRow row)
    {
        var size = 0;
        foreach (var (column, value) in row)
        {
            size += column.Length;
            size += FormatValue(value).Length;
            size += 3;
        }

        return size + Environment.NewLine.Length;
    }

    internal static string FormatValue(object? value)
    {
        if (value is null or DBNull)
        {
            return "NULL";
        }

        return value switch
        {
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            byte[] bytes => Convert.ToBase64String(bytes),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    internal static string FormatSelectResult(string database, string sql, SelectQueryResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Database: {database}");
        builder.AppendLine("Query:");
        builder.AppendLine(sql);
        builder.AppendLine(CultureInfo.InvariantCulture, $"Max rows: {result.MaxRows}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Max result size (bytes): {result.MaxResultSizeBytes}");
        builder.AppendLine();

        if (result.Rows.Count == 0)
        {
            builder.AppendLine("No rows returned.");
        }
        else
        {
            var columns = result.Rows[0].Keys.ToList();
            builder.AppendLine(string.Join(" | ", columns));
            builder.AppendLine(string.Join(" | ", columns.Select(_ => "----")));

            foreach (var row in result.Rows)
            {
                builder.AppendLine(string.Join(" | ", columns.Select(column => FormatValue(row.GetValueOrDefault(column)))));
            }
        }

        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Returned rows: {result.Rows.Count}");

        if (result.RowLimitReached)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Result truncated: row limit of {result.MaxRows} was reached.");
        }

        if (result.ResultSizeLimitReached)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Result truncated: maximum result size of {result.MaxResultSizeBytes} bytes was reached.");
        }

        return builder.ToString();
    }
}
