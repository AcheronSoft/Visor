namespace Visor.Core;

// Ошибка выполнения команды (SQL Error, Timeout)
public class VisorExecutionException(string message, string procedureName, int errorCode, Exception innerException)
    : VisorException(message, procedureName, innerException)
{
    public int ErrorCode { get; } = errorCode; // SQL Error Code (абстрактный)
}