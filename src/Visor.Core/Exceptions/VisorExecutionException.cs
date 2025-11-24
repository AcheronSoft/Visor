namespace Visor.Core.Exceptions;

/// <summary>
/// Represents an error that occurs during the execution of a database command, such as a SQL error or a timeout.
/// </summary>
public class VisorExecutionException(string message, string procedureName, int errorCode, Exception innerException)
    : VisorException(message, procedureName, innerException)
{
    /// <summary>
    /// Gets the provider-specific error code for the exception.
    /// </summary>
    public int ErrorCode { get; } = errorCode;
}
