namespace SqlServerMcp.Services;

internal static class DataAnalysisHelper
{
    public const int DefaultMaxSampleRows = 100;
    public const string MaxSampleRowsEnvironmentVariable = "SQLSERVER_MAX_SAMPLE_ROWS";

    public const int DefaultMaxSelectRows = 100;
    public const string MaxSelectRowsEnvironmentVariable = "SQLSERVER_MAX_SELECT_ROWS";

    public const int DefaultMaxResultSizeBytes = 512 * 1024;
    public const string MaxResultSizeEnvironmentVariable = "SQLSERVER_MAX_RESULT_SIZE_BYTES";

    private static readonly HashSet<string> MinMaxIncompatibleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text",
        "ntext",
        "image",
        "xml",
        "sql_variant",
        "geography",
        "geometry",
        "hierarchyid",
        "timestamp",
        "rowversion"
    };

    public static int GetMaxSampleRows()
    {
        return GetPositiveIntFromEnvironment(MaxSampleRowsEnvironmentVariable, DefaultMaxSampleRows);
    }

    public static int GetMaxSelectRows()
    {
        return GetPositiveIntFromEnvironment(MaxSelectRowsEnvironmentVariable, DefaultMaxSelectRows);
    }

    public static int GetMaxResultSizeBytes()
    {
        return GetPositiveIntFromEnvironment(MaxResultSizeEnvironmentVariable, DefaultMaxResultSizeBytes);
    }

    private static int GetPositiveIntFromEnvironment(string variableName, int defaultValue)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(variableName), out var value) && value > 0
            ? value
            : defaultValue;
    }

    public static int ClampSampleLimit(int requestedLimit, int maxSampleRows)
    {
        if (requestedLimit <= 0)
        {
            return Math.Min(10, maxSampleRows);
        }

        return Math.Min(requestedLimit, maxSampleRows);
    }

    public static bool SupportsMinMax(string? dataType)
    {
        return !string.IsNullOrWhiteSpace(dataType) && !MinMaxIncompatibleTypes.Contains(dataType);
    }
}
