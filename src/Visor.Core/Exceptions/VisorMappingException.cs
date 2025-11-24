namespace Visor.Core.Exceptions;

/// <summary>
/// Represents an error that occurs when mapping database results to a C# object, typically due to a schema mismatch.
/// </summary>
public class VisorMappingException(string message, string procedureName, Exception? innerException = null) : VisorException(message, procedureName, innerException);
