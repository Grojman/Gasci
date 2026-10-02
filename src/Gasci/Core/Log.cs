namespace Gasci.Core;

/// <summary>
/// Session log: warnings and errors only, written to log.txt in the user data folder (the previous
/// session is kept as log.previous.txt). The file is created on the first entry of a session, so a
/// clean run leaves no new log. Entries are also printed to the standard error output.
/// </summary>
public static class Log
{
    private static readonly object Lock = new();
    private static bool _started;

    /// <summary>Where the log goes; null disables the file (used by tests).</summary>
    public static string? FilePath { get; set; }

    public static int Warnings { get; private set; }
    public static int Errors { get; private set; }

    public static void Warn(string message) { Warnings++; Write("WARNING", message); }

    public static void Error(string message) { Errors++; Write("ERROR", message); }

    public static void Error(Exception e, string context) => Error($"{context}: {e.GetType().Name}: {e.Message}");

    private static void Write(string level, string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss} {level,-7} {message}";
        System.Console.Error.WriteLine(line);
        if (FilePath is null) return;
        lock (Lock)
        {
            try
            {
                if (!_started)
                {
                    _started = true;
                    if (File.Exists(FilePath))
                        File.Copy(FilePath, Path.ChangeExtension(FilePath, ".previous.txt"), overwrite: true);
                    File.WriteAllText(FilePath, $"{GameDefinitionTitle()} — session started {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
                }
                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
            catch (Exception e)
            {
                System.Console.Error.WriteLine($"Could not write the log: {e.Message}");
                FilePath = null;
            }
        }
    }

    private static string GameDefinitionTitle()
    {
        try { return GameServices.Game?.Title ?? "Game"; }
        catch { return "Game"; }
    }
}

/// <summary>
/// A mistake in the game content (missing reference, wrong type, undeclared variable...). It is meant
/// to stop the game loudly: Program logs it before closing. --validate reports the same problems
/// without running the game.
/// </summary>
public class ContentException(string message) : Exception(message);
