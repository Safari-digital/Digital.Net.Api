using Digital.Net.Lib.Environment;
using Digital.Net.Lib.Exceptions;
using Digital.Net.Lib.Exceptions.types;

namespace Digital.Net.Lib.Messages;

public class ResultMessage
{
    public ResultMessage() {}

    public ResultMessage(Exception ex, string? message = null)
    {
        Code = message?.GetHashCode().ToString() ?? ex.GetFormattedErrorCode();
        Message = ResolveMessage(ex, message);
        Reference = ex.GetReference();
        StackTrace = AspNetEnv.IsDevelopment ? ex.StackTrace : null;
        Exception = ex;
    }

    private static string ResolveMessage(Exception ex, string? explicitMessage) =>
        explicitMessage
        ?? (ex is DigitalException || AspNetEnv.IsDevelopment ? ex.Message : "An unexpected error occurred");

    public ResultMessage(string message)
    {
        Reference = "UNREFERENCED_MESSAGE";
        Message = message;
    }

    public void Throw() => throw Exception ?? new Exception(Message);

    public string? Code { get; init; }
    public string? Reference { get; init; }
    public string? Message { get; init; }
    public string? StackTrace { get; init; }
    private Exception? Exception { get; }
    public bool IsExceptionOfType<TException>() where TException : Exception =>
        Exception is TException;
}