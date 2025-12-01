using Spectre.Console;

namespace Visor.CLI.Infrastructure.UI;

public class ConsoleUserInterface : IUserInterface
{
    public string Ask(string prompt, string? defaultValue = null)
    {
        var textPrompt = new TextPrompt<string>(prompt);
        if (defaultValue != null)
        {
            textPrompt.DefaultValue(defaultValue);
        }
        return AnsiConsole.Prompt(textPrompt);
    }

    public string Select(string prompt, IEnumerable<string> choices)
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(prompt)
                .AddChoices(choices));
    }

    public T Select<T>(string prompt, IEnumerable<T> choices, Func<T, string> displayConverter)
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<T>()
                .Title(prompt)
                .AddChoices(choices)
                .UseConverter(displayConverter));
    }

    public List<T> MultiSelect<T>(string prompt, IEnumerable<T> choices, Func<T, string> displayConverter)
    {
        return AnsiConsole.Prompt(
            new MultiSelectionPrompt<T>()
                .Title(prompt)
                .NotRequired()
                .PageSize(15)
                .MoreChoicesText("[grey](Move up and down to reveal more procedures)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
                .AddChoices(choices)
                .UseConverter(displayConverter));
    }

    public async Task Status(string status, Func<Task> action)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(status, async _ => await action());
    }

    public void MarkupLine(string message)
    {
        AnsiConsole.MarkupLine(message);
    }

    public void WriteException(Exception exception)
    {
        AnsiConsole.WriteException(exception);
    }

    public void ShowHeader()
    {
        AnsiConsole.Write(new FigletText("VISOR").Color(Color.Cyan1));
    }
}
