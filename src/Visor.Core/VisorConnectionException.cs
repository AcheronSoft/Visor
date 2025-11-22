namespace Visor.Core;

// Ошибка подключения (до выполнения команды)
public class VisorConnectionException(string message, string connectionString, Exception innerException) : VisorException(message, null, innerException)
{
    public string ConnectionString { get; } = connectionString;
}