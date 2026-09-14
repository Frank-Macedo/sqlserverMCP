using SqlServerMcp.Models;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tests;

public class QueryResultFormatterTests
{
    [Fact]
    public void FormatSelectResult_IncludesTruncationMessages()
    {
        var result = new SelectQueryResult
        {
            Rows =
            [
                new QueryRow { ["Id"] = 1, ["Name"] = "Test" }
            ],
            MaxRows = 100,
            MaxResultSizeBytes = 1024,
            RowLimitReached = true,
            ResultSizeLimitReached = false
        };

        var formatted = QueryResultFormatter.FormatSelectResult(
            "DemoDb",
            "SELECT Id, Name FROM Demo",
            result);

        Assert.Contains("Result truncated: row limit of 100 was reached.", formatted, StringComparison.Ordinal);
        Assert.Contains("Returned rows: 1", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void EstimateRowSize_CountsColumnNamesAndValues()
    {
        var row = new QueryRow
        {
            ["ID"] = 1,
            ["Name"] = "ABC"
        };

        var size = QueryResultFormatter.EstimateRowSize(row);
        Assert.True(size > 0);
    }
}
