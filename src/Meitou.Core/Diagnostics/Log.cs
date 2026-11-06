using System.Collections.Concurrent;

namespace Meitou.Diagnostics;

/// <summary>
/// Warnings about a record the game data does not have (or that cannot be used), said once per subsystem and record id until <see cref="Reset"/> (a save load), so a
/// squad template missing from the data names itself once, not on every tick. Writes to the error output, which the game tees into log.txt like every other line.
/// Nothing is allocated or locked when nothing is wrong: callers reach this only on the failing branch.
/// </summary>
public static class Log
{
    static readonly ConcurrentDictionary<(string Subsystem, string Id), byte> seen = new();

    /// <summary>
    /// Writes <c>warning   {subsystem} {message}</c> the first time this <paramref name="subsystem"/> and <paramref name="id"/> come in; false when it was said already.
    /// <paramref name="message"/> is built only for the first.
    /// </summary>
    public static bool WarnOnce(string subsystem, string id, Func<string> message)
    {
        if (!seen.TryAdd((subsystem, id), 0)) return false;
        Console.Error.WriteLine($"warning   {subsystem} {id}: {message()}");
        return true;
    }

    /// <summary>The same with a message that is a plain string (a constant, or one already built).</summary>
    public static bool WarnOnce(string subsystem, string id, string message)
    {
        if (!seen.TryAdd((subsystem, id), 0)) return false;
        Console.Error.WriteLine($"warning   {subsystem} {id}: {message}");
        return true;
    }

    sealed class Tally { public int Count, Reported; }
    static readonly ConcurrentDictionary<(string Subsystem, string Id), Tally> tallies = new();

    /// <summary>
    /// Counts one more occurrence of <paramref name="id"/> under <paramref name="subsystem"/>; true for the first, whose caller writes its full line (with the detail that names the owner),
    /// false for the rest, which write nothing: <see cref="TakeRepeats"/> reports their number later. Only failing paths call this.
    /// </summary>
    public static bool First(string subsystem, string id)
    {
        var t = tallies.GetOrAdd((subsystem, id), static _ => new Tally());
        return Interlocked.Increment(ref t.Count) == 1;
    }

    /// <summary>
    /// One line for each id seen more than once whose count grew since it was last reported (all subsystems when <paramref name="subsystem"/> is null), most frequent first:
    /// <c>{subsystem} {id}: {count} in all, {count - 1} more after the first line</c>.
    /// </summary>
    public static List<string> TakeRepeats(string? subsystem = null)
    {
        var lines = new List<(int Count, string Line)>();
        foreach (var (key, t) in tallies)
        {
            int count = Volatile.Read(ref t.Count);
            if (count < 2 || count == t.Reported || subsystem is not null && key.Subsystem != subsystem) continue;
            t.Reported = count;
            lines.Add((count, $"{key.Subsystem} {key.Id}: {count} in all, {count - 1} more after the first line"));
        }
        return [.. lines.OrderByDescending(l => l.Count).ThenBy(l => l.Line, StringComparer.Ordinal).Select(l => l.Line)];
    }

    /// <summary>Every id counted under <paramref name="subsystem"/> with its count, most frequent first (for a summary line at the end of a load).</summary>
    public static List<(string Id, int Count)> Snapshot(string subsystem) =>
        [.. tallies.Where(p => p.Key.Subsystem == subsystem).Select(p => (p.Key.Id, Volatile.Read(ref p.Value.Count))).OrderByDescending(p => p.Item2).ThenBy(p => p.Id, StringComparer.Ordinal)];

    /// <summary>Forgets what was said, so a newly loaded world warns again about the same records.</summary>
    public static void Reset() { seen.Clear(); tallies.Clear(); }
}
