namespace OpsCopilot.Api.Tools.LogSearch;

public sealed class LogSearchValidationException : Exception
{
    public LogSearchValidationException(string message)
        : base(message)
    {
    }
}
