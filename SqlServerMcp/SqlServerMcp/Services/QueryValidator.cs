using System.Text;
using System.Text.RegularExpressions;

namespace SqlServerMcp.Services;

public sealed class QueryValidator
{
    private static readonly string[] BlockedKeywords =
    [
        "INSERT",
        "UPDATE",
        "DELETE",
        "MERGE",
        "DROP",
        "ALTER",
        "TRUNCATE",
        "CREATE",
        "EXEC",
        "EXECUTE",
        "GRANT",
        "REVOKE",
        "DENY",
        "INTO",
        "OPENROWSET",
        "OPENDATASOURCE",
        "OPENQUERY",
        "BULK",
        "DBCC",
        "BACKUP",
        "RESTORE",
        "KILL",
        "RECONFIGURE",
        "WAITFOR",
        "SHUTDOWN",
        "SP_EXECUTESQL"
    ];

    private static readonly Regex BlockedKeywordPattern = new(
        $@"\b(?:{string.Join("|", BlockedKeywords)})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CrossJoinPattern = new(
        @"\bCROSS\s+JOIN\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ExtendedProcedurePattern = new(
        @"\bxp_[a-z0-9_]+\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HasFromClausePattern = new(
        @"\bFROM\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HasTopClausePattern = new(
        @"\bTOP\s+(?:\(\s*)?\d+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HasWhereClausePattern = new(
        @"\bWHERE\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AggregateSelectPattern = new(
        @"\bSELECT\s+(?:COUNT|SUM|MIN|MAX|AVG)\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public void ValidateOrThrow(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new QueryValidationException("SQL query cannot be empty.");
        }

        var statements = SplitStatements(sql);
        if (statements.Count > 1)
        {
            throw new QueryValidationException("Multiple SQL statements are not allowed.");
        }

        var normalized = NormalizeForAnalysis(statements[0]);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new QueryValidationException("SQL query cannot be empty.");
        }

        ValidateNoBlockedKeywords(normalized);
        ValidateAllowedStart(normalized);
        ValidateNoCrossJoin(normalized);
        ValidateNoExtendedProcedures(normalized);
        ValidateQueryScopeLimits(normalized);
    }

    private static void ValidateNoCrossJoin(string normalizedSql)
    {
        if (CrossJoinPattern.IsMatch(normalizedSql))
        {
            throw new QueryValidationException("CROSS JOIN is not allowed.");
        }
    }

    private static void ValidateNoExtendedProcedures(string normalizedSql)
    {
        if (ExtendedProcedurePattern.IsMatch(normalizedSql))
        {
            throw new QueryValidationException("Extended stored procedures are not allowed.");
        }
    }

    private static void ValidateQueryScopeLimits(string normalizedSql)
    {
        if (!HasFromClausePattern.IsMatch(normalizedSql))
        {
            return;
        }

        if (HasTopClausePattern.IsMatch(normalizedSql)
            || HasWhereClausePattern.IsMatch(normalizedSql)
            || AggregateSelectPattern.IsMatch(normalizedSql))
        {
            return;
        }

        throw new QueryValidationException(
            "Queries against tables must include TOP or WHERE, or use an aggregate function such as COUNT, to limit resource usage.");
    }

    private static void ValidateAllowedStart(string normalizedSql)
    {
        var trimmed = normalizedSql.TrimStart();
        if (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new QueryValidationException("Only SELECT queries are allowed.");
    }

    private static void ValidateNoBlockedKeywords(string normalizedSql)
    {
        var match = BlockedKeywordPattern.Match(normalizedSql);
        if (match.Success)
        {
            throw new QueryValidationException($"Blocked SQL keyword: {match.Value.ToUpperInvariant()}");
        }
    }

    internal static List<string> SplitStatements(string sql)
    {
        var statements = new List<string>();
        var current = new StringBuilder();
        var inString = false;
        var inBlockComment = false;
        var inLineComment = false;

        for (var i = 0; i < sql.Length; i++)
        {
            var currentChar = sql[i];
            var nextChar = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (inLineComment)
            {
                if (currentChar is '\r' or '\n')
                {
                    inLineComment = false;
                    current.Append(currentChar);
                }

                continue;
            }

            if (inBlockComment)
            {
                if (currentChar == '*' && nextChar == '/')
                {
                    inBlockComment = false;
                    i++;
                }

                continue;
            }

            if (inString)
            {
                current.Append(currentChar);
                if (currentChar == '\'')
                {
                    if (nextChar == '\'')
                    {
                        current.Append(nextChar);
                        i++;
                    }
                    else
                    {
                        inString = false;
                    }
                }

                continue;
            }

            if (currentChar == '-' && nextChar == '-')
            {
                inLineComment = true;
                i++;
                continue;
            }

            if (currentChar == '/' && nextChar == '*')
            {
                inBlockComment = true;
                i++;
                continue;
            }

            if (currentChar == '\'')
            {
                inString = true;
                current.Append(currentChar);
                continue;
            }

            if (currentChar == ';')
            {
                var statement = current.ToString().Trim();
                if (statement.Length > 0)
                {
                    statements.Add(statement);
                }

                current.Clear();
                continue;
            }

            current.Append(currentChar);
        }

        var lastStatement = current.ToString().Trim();
        if (lastStatement.Length > 0)
        {
            statements.Add(lastStatement);
        }

        return statements;
    }

    internal static string NormalizeForAnalysis(string sql)
    {
        var normalized = new StringBuilder(sql.Length);
        var inString = false;
        var inBlockComment = false;
        var inLineComment = false;

        for (var i = 0; i < sql.Length; i++)
        {
            var currentChar = sql[i];
            var nextChar = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (inLineComment)
            {
                if (currentChar is '\r' or '\n')
                {
                    inLineComment = false;
                    normalized.Append(' ');
                }

                continue;
            }

            if (inBlockComment)
            {
                if (currentChar == '*' && nextChar == '/')
                {
                    inBlockComment = false;
                    normalized.Append(' ');
                    i++;
                }

                continue;
            }

            if (inString)
            {
                normalized.Append(' ');
                if (currentChar == '\'')
                {
                    if (nextChar == '\'')
                    {
                        normalized.Append(' ');
                        i++;
                    }
                    else
                    {
                        inString = false;
                    }
                }

                continue;
            }

            if (currentChar == '-' && nextChar == '-')
            {
                inLineComment = true;
                i++;
                continue;
            }

            if (currentChar == '/' && nextChar == '*')
            {
                inBlockComment = true;
                i++;
                continue;
            }

            if (currentChar == '\'')
            {
                inString = true;
                normalized.Append(' ');
                continue;
            }

            normalized.Append(currentChar);
        }

        return normalized.ToString();
    }
}
