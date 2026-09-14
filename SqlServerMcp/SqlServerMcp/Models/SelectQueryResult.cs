namespace SqlServerMcp.Models;

public sealed class SelectQueryResult
{
    public required IReadOnlyList<QueryRow> Rows { get; init; }

    public int MaxRows { get; init; }

    public int MaxResultSizeBytes { get; init; }

    public bool RowLimitReached { get; init; }

    public bool ResultSizeLimitReached { get; init; }
}
