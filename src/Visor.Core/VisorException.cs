namespace Visor.Core;

// Базовое исключение для всех ошибок Visor
public abstract class VisorException(string message, string? procedureName = null, Exception? innerException = null) : Exception(message, innerException)
{
    public string? ProcedureName { get; } = procedureName;
}