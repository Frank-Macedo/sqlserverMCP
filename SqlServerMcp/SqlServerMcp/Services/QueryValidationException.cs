namespace SqlServerMcp.Services;

public sealed class QueryValidationException : Exception
{
    public QueryValidationException(string message)
        : base(message)
    {
    }
}
