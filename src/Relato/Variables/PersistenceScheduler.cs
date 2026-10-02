namespace Relato.Variables;

/// <summary>
/// Debounced file writes. Every change marks a file dirty and restarts its timer; the file is written
/// when the timer reaches zero, so ten quick changes produce one write. Ticked from the game loop
/// (main thread), so no lock is needed. <see cref="FlushAll"/> writes everything pending at once
/// (quit, return to title).
/// </summary>
public static class PersistenceScheduler
{
    private sealed class Entry(Action write)
    {
        public readonly Action Write = write;
        public double Remaining = -1;
    }

    private static readonly Dictionary<string, Entry> Entries = new();

    public static double DelaySeconds { get; set; } = 1.0;

    public static void Register(string key, Action write) => Entries[key] = new Entry(write);

    public static void MarkDirty(string key)
    {
        if (Entries.TryGetValue(key, out Entry? entry)) entry.Remaining = DelaySeconds;
    }

    public static bool IsDirty(string key) => Entries.TryGetValue(key, out Entry? e) && e.Remaining >= 0;

    public static void Tick(double seconds)
    {
        foreach (var (key, entry) in Entries)
        {
            if (entry.Remaining < 0) continue;
            entry.Remaining -= seconds;
            if (entry.Remaining <= 0) Run(key, entry);
        }
    }

    public static void FlushAll()
    {
        foreach (var (key, entry) in Entries)
            if (entry.Remaining >= 0) Run(key, entry);
    }

    private static void Run(string key, Entry entry)
    {
        entry.Remaining = -1;
        try { entry.Write(); }
        catch (Exception e) { Core.Log.Error(e, $"Could not write {key}"); }
    }
}
