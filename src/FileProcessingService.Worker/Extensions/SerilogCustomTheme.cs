using Serilog.Sinks.SystemConsole.Themes;

namespace FileProcessingService.Worker.Extensions;

public static class SerilogCustomTheme
{
    public static readonly AnsiConsoleTheme SerilogTheme = new(new Dictionary<ConsoleThemeStyle, string>
    {
        // Core text:
        [ConsoleThemeStyle.Text] = "\u001b[37m",       // white
        [ConsoleThemeStyle.SecondaryText] = "\u001b[90m",       // bright black (gray)
        [ConsoleThemeStyle.TertiaryText] = "\u001b[90m",       // same as above
        [ConsoleThemeStyle.Invalid] = "\u001b[31;1m",     // bold red

        // Property names vs. values:
        [ConsoleThemeStyle.Name] = "\u001b[36m",       // cyan names
        [ConsoleThemeStyle.String] = "\u001b[33m",       // yellow strings
        [ConsoleThemeStyle.Number] = "\u001b[36m",       // cyan numbers
        [ConsoleThemeStyle.Boolean] = "\u001b[35m",       // magenta booleans
        [ConsoleThemeStyle.Null] = "\u001b[90m",       // gray nulls
        [ConsoleThemeStyle.Scalar] = "\u001b[38;2;255;102;204m",       // default scalar

        // Levels (you can tweak these too):
        [ConsoleThemeStyle.LevelVerbose] = "\u001b[37m",
        [ConsoleThemeStyle.LevelDebug] = "\u001b[37m",
        [ConsoleThemeStyle.LevelInformation] = "\u001b[32m",      // green “INF”
        [ConsoleThemeStyle.LevelWarning] = "\u001b[33m",       // yellow “WRN”
        [ConsoleThemeStyle.LevelError] = "\u001b[31m",       // red “ERR”
        [ConsoleThemeStyle.LevelFatal] = "\u001b[31;1m"      // bold red “FTL”
    });
}
