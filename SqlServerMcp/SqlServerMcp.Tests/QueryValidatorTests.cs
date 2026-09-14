using SqlServerMcp.Services;

namespace SqlServerMcp.Tests;

public class QueryValidatorTests
{
    private readonly QueryValidator _validator = new();

    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("select * from PESSOA where id = 1")]
    [InlineData("WITH cte AS (SELECT 1 AS n) SELECT n FROM cte WHERE n = 1")]
    [InlineData("SELECT 'DELETE' AS col")]
    [InlineData("SELECT 'UPDATE test' AS col")]
    [InlineData("SELECT 1 -- safe comment")]
    [InlineData("SELECT 1 /* safe comment */")]
    [InlineData("SELECT TOP 10 * FROM PESSOA")]
    [InlineData("SELECT COUNT(*) FROM PESSOA")]
    public void ValidateOrThrow_AllowsReadOnlyQueries(string sql)
    {
        var exception = Record.Exception(() => _validator.ValidateOrThrow(sql));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("DELETE FROM PESSOA", "DELETE")]
    [InlineData("UPDATE PESSOA SET x = 1", "UPDATE")]
    [InlineData("DROP TABLE PESSOA", "DROP")]
    [InlineData("EXEC sp_help", "EXEC")]
    [InlineData("EXECUTE sp_help", "EXECUTE")]
    [InlineData("INSERT INTO PESSOA (id) VALUES (1)", "INSERT")]
    [InlineData("ALTER TABLE PESSOA ADD x int", "ALTER")]
    [InlineData("TRUNCATE TABLE PESSOA", "TRUNCATE")]
    [InlineData("MERGE PESSOA AS target USING source ON 1=1", "MERGE")]
    [InlineData("GRANT SELECT TO user1", "GRANT")]
    [InlineData("REVOKE SELECT FROM user1", "REVOKE")]
    [InlineData("DENY SELECT TO user1", "DENY")]
    [InlineData("CREATE TABLE x (id int)", "CREATE")]
    public void ValidateOrThrow_BlocksDangerousKeywords(string sql, string expectedKeyword)
    {
        var exception = Assert.Throws<QueryValidationException>(() => _validator.ValidateOrThrow(sql));
        Assert.Contains(expectedKeyword, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SELECT 1; DELETE FROM PESSOA")]
    [InlineData("SELECT 1; UPDATE PESSOA SET x = 1")]
    public void ValidateOrThrow_BlocksMultipleStatements(string sql)
    {
        var exception = Assert.Throws<QueryValidationException>(() => _validator.ValidateOrThrow(sql));
        Assert.Contains("Multiple SQL statements", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateOrThrow_BlocksEmptyQueries(string sql)
    {
        var exception = Assert.Throws<QueryValidationException>(() => _validator.ValidateOrThrow(sql));
        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateOrThrow_BlocksNonSelectStatements()
    {
        var exception = Assert.Throws<QueryValidationException>(() => _validator.ValidateOrThrow("WAITFOR DELAY '00:00:01'"));
        Assert.Contains("Blocked SQL keyword: WAITFOR", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SELECT * FROM PESSOA")]
    [InlineData("SELECT ID_PESS, NM_PESS FROM PESSOA")]
    public void ValidateOrThrow_BlocksUnboundedTableQueries(string sql)
    {
        var exception = Assert.Throws<QueryValidationException>(() => _validator.ValidateOrThrow(sql));
        Assert.Contains("TOP or WHERE", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SELECT * FROM A CROSS JOIN B")]
    [InlineData("SELECT a.id FROM tableA a cross join tableB b")]
    public void ValidateOrThrow_BlocksCrossJoin(string sql)
    {
        var exception = Assert.Throws<QueryValidationException>(() => _validator.ValidateOrThrow(sql));
        Assert.Contains("CROSS JOIN", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SELECT * FROM OPENROWSET('SQLNCLI', 'Server=localhost;Trusted_Connection=yes;', 'SELECT 1')")]
    [InlineData("SELECT * FROM OPENDATASOURCE('SQLNCLI', 'Server=localhost;Trusted_Connection=yes;')")]
    [InlineData("SELECT * FROM OPENQUERY(LINKED, 'SELECT 1')")]
    public void ValidateOrThrow_BlocksExternalDataSources(string sql)
    {
        var exception = Assert.Throws<QueryValidationException>(() => _validator.ValidateOrThrow(sql));
        Assert.NotNull(exception.Message);
    }

    [Fact]
    public void ValidateOrThrow_BlocksSelectInto()
    {
        var exception = Assert.Throws<QueryValidationException>(() => _validator.ValidateOrThrow("SELECT * INTO #temp FROM PESSOA"));
        Assert.Contains("INTO", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateOrThrow_BlocksExtendedProcedures()
    {
        var exception = Assert.Throws<QueryValidationException>(() => _validator.ValidateOrThrow("SELECT xp_cmdshell('dir')"));
        Assert.Contains("Extended stored procedures", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SplitStatements_IgnoresSemicolonInsideStringLiteral()
    {
        var statements = QueryValidator.SplitStatements("SELECT 'a;b' AS value");
        Assert.Single(statements);
        Assert.Equal("SELECT 'a;b' AS value", statements[0]);
    }

    [Fact]
    public void NormalizeForAnalysis_IgnoresKeywordsInsideStringLiteral()
    {
        var normalized = QueryValidator.NormalizeForAnalysis("SELECT 'DELETE FROM PESSOA' AS value");
        Assert.DoesNotContain("DELETE", normalized, StringComparison.OrdinalIgnoreCase);
    }
}
