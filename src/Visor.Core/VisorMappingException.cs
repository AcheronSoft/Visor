namespace Visor.Core;

// Ошибка маппинга (когда схема БД не соответствует коду C#)
public class VisorMappingException(string message, string procedureName, Exception? innerException = null) : VisorException(message, procedureName, innerException);