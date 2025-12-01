using Visor.CLI.Metadata;

namespace Visor.CLI.Infrastructure.UI;

public interface IUserInterface
{
    string Ask(string prompt, string? defaultValue = null);
    string Select(string prompt, IEnumerable<string> choices);
    T Select<T>(string prompt, IEnumerable<T> choices, Func<T, string> displayConverter) where T : notnull;
    List<T> MultiSelect<T>(string prompt, IEnumerable<T> choices, Func<T, string> displayConverter) where T : notnull;
    Task Status(string status, Func<Task> action);
    void MarkupLine(string message);
    void WriteException(Exception exception);
    void ShowHeader();
}
