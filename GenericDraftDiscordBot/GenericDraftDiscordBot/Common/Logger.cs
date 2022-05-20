using Discord;

namespace GreetingsBot.Common;

public static class Logger
{
    public static void Log(LogSeverity severity, string source, string message, Exception? exception = null)
    {
        switch (severity)
        {
            case LogSeverity.Critical:
            case LogSeverity.Error:
                Console.ForegroundColor = ConsoleColor.Red;
                break;
            case LogSeverity.Warning:
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;
            case LogSeverity.Info:
                Console.ForegroundColor = ConsoleColor.White;
                break;
            case LogSeverity.Verbose:
            case LogSeverity.Debug:
                Console.ForegroundColor = ConsoleColor.DarkGray;
                break;
        }
        Console.WriteLine($"{DateTime.Now,-19} [{severity,8}] {source}: {message} {exception}");
        Console.ResetColor();
    }
}