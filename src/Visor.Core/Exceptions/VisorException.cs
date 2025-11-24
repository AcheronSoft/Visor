namespace Visor.Core.Exceptions;

/// <summary>
/// Represents the base class for all exceptions thrown by the Visor ORM.
/// </summary>
public abstract class VisorException(string message, string? procedureName = null, Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Gets the name of the stored procedure or function that was being executed when the error occurred.
    /// </summary>
    public string? ProcedureName { get; } = procedureName;
}
