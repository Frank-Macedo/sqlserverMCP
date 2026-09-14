using SqlServerMcp.Services;

namespace SqlServerMcp.Tests;

public class DataAnalysisHelperTests
{
    [Theory]
    [InlineData(100000, 100, 100)]
    [InlineData(50, 100, 50)]
    [InlineData(0, 100, 10)]
    [InlineData(-5, 100, 10)]
    [InlineData(1000, 25, 25)]
    public void ClampSampleLimit_EnforcesMaximum(int requestedLimit, int maxSampleRows, int expected)
    {
        var result = DataAnalysisHelper.ClampSampleLimit(requestedLimit, maxSampleRows);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("int", true)]
    [InlineData("varchar", true)]
    [InlineData("datetime", true)]
    [InlineData("text", false)]
    [InlineData("ntext", false)]
    [InlineData("image", false)]
    [InlineData("xml", false)]
    public void SupportsMinMax_RespectsDataType(string dataType, bool expected)
    {
        Assert.Equal(expected, DataAnalysisHelper.SupportsMinMax(dataType));
    }

    [Fact]
    public void GetMaxSampleRows_UsesDefaultWhenEnvironmentVariableMissing()
    {
        var previous = Environment.GetEnvironmentVariable(DataAnalysisHelper.MaxSampleRowsEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(DataAnalysisHelper.MaxSampleRowsEnvironmentVariable, null);
            Assert.Equal(DataAnalysisHelper.DefaultMaxSampleRows, DataAnalysisHelper.GetMaxSampleRows());
        }
        finally
        {
            Environment.SetEnvironmentVariable(DataAnalysisHelper.MaxSampleRowsEnvironmentVariable, previous);
        }
    }

    [Fact]
    public void GetMaxSelectRows_UsesDefaultWhenEnvironmentVariableMissing()
    {
        var previous = Environment.GetEnvironmentVariable(DataAnalysisHelper.MaxSelectRowsEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(DataAnalysisHelper.MaxSelectRowsEnvironmentVariable, null);
            Assert.Equal(DataAnalysisHelper.DefaultMaxSelectRows, DataAnalysisHelper.GetMaxSelectRows());
        }
        finally
        {
            Environment.SetEnvironmentVariable(DataAnalysisHelper.MaxSelectRowsEnvironmentVariable, previous);
        }
    }
}
