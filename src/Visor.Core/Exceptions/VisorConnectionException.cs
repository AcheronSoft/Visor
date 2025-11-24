namespace Visor.Core.Exceptions;

/// <summary>
/// Represents an error that occurs while trying to establish a connection to the database.
/// </summary>
public class VisorConnectionException(string message, string connectionString, Exception innerException) : VisorException(message, null, innerException)
{
    /// <summary>
    /// Gets the sanitized connection string that was used when the connection attempt failed.
    /// </summary>
    public string ConnectionString { get; } = connectionString;
}
