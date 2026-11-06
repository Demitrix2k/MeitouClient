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

    /// <summary>Forgets what was said, so a newly loaded world warns again about the same records.</summary>
    public static void Reset() => seen.Clear();
}
