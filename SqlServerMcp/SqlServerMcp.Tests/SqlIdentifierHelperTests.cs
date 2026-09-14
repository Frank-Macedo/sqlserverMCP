using SqlServerMcp.Services;

namespace SqlServerMcp.Tests;

public class SqlIdentifierHelperTests
{
    [Theory]
    [InlineData("KAIROS-BASE-39168985827")]
    [InlineData("dbo")]
    [InlineData("PESSOA")]
    public void ValidateIdentifier_AllowsValidNames(string identifier)
    {
        var exception = Record.Exception(() => SqlIdentifierHelper.ValidateIdentifier(identifier, "database"));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("db;drop")]
    [InlineData("db name")]
    [InlineData("db'name")]
    public void ValidateIdentifier_RejectsInvalidNames(string identifier)
    {
        Assert.Throws<ArgumentException>(() => SqlIdentifierHelper.ValidateIdentifier(identifier, "database"));
    }

    [Fact]
    public void ParseTableReference_UsesDefaultSchemaWhenMissing()
    {
        var (schema, table) = SqlIdentifierHelper.ParseTableReference("PESSOA");
        Assert.Equal("dbo", schema);
        Assert.Equal("PESSOA", table);
    }

    [Fact]
    public void ParseTableReference_ParsesSchemaAndTable()
    {
        var (schema, table) = SqlIdentifierHelper.ParseTableReference("dbo.PESSOA");
        Assert.Equal("dbo", schema);
        Assert.Equal("PESSOA", table);
    }

    [Fact]
    public void QuoteIdentifier_WrapsNameInBrackets()
    {
        var quoted = SqlIdentifierHelper.QuoteIdentifier("KAIROS-BASE-39168985827");
        Assert.Equal("[KAIROS-BASE-39168985827]", quoted);
    }

    [Fact]
    public void ParseOptionalTableReference_AllowsUnqualifiedTable()
    {
        var (schema, table) = SqlIdentifierHelper.ParseOptionalTableReference("PESSOA");
        Assert.Null(schema);
        Assert.Equal("PESSOA", table);
    }

    [Theory]
    [InlineData("RAIO")]
    [InlineData("RAIO_MARCACAO")]
    public void ValidateColumnSearchTerm_AllowsValidSearchTerms(string columnSearch)
    {
        var exception = Record.Exception(() => SqlIdentifierHelper.ValidateColumnSearchTerm(columnSearch, "column"));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("RAIO%")]
    [InlineData("RAIO;DROP")]
    public void ValidateColumnSearchTerm_RejectsInvalidSearchTerms(string columnSearch)
    {
        Assert.Throws<ArgumentException>(() => SqlIdentifierHelper.ValidateColumnSearchTerm(columnSearch, "column"));
    }
}
