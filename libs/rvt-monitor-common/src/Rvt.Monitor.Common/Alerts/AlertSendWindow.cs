namespace Rvt.Monitor.Common.Alerts;

/// <summary>
/// A contact's quiet-hours window, expressed as a UTC time of day.
/// </summary>
/// <remarks>
/// The window is evaluated against the <em>send</em> clock, not the alert's
/// event time (product ruling, 2026-07-30). Quiet hours mean "do not contact
/// me now", so a backfilled alert waits for the window to reopen instead of
/// arriving at midnight for a breach that happened at 14:00. Planning no
/// longer filters on it; the dispatcher defers a closed-window delivery.
/// </remarks>
public readonly record struct AlertSendWindow(TimeSpan Start, TimeSpan End)
{
    private static readonly TimeSpan _day = TimeSpan.FromDays(1);

    /// <summary>
    /// Builds a window from a contact's configured pair, or returns null when
    /// no usable window is configured. A contact with no window — or with a
    /// bound outside a single day, which cannot be interpreted — must still
    /// be contacted immediately rather than silently held.
    /// </summary>
    public static AlertSendWindow? TryCreate(TimeSpan? start, TimeSpan? end)
    {
        if (start is not { } startTime || end is not { } endTime)
        {
            return null;
        }

        return IsTimeOfDay(startTime) && IsTimeOfDay(endTime)
            ? new AlertSendWindow(startTime, endTime)
            : null;
    }

    /// <summary>
    /// True when <paramref name="utcNow"/> falls inside the window. Windows
    /// that span midnight (start &gt; end) are inclusive of both ends of the
    /// day.
    /// </summary>
    public bool IsOpenAt(DateTime utcNow)
    {
        TimeSpan time = utcNow.TimeOfDay;
        return Start <= End
            ? time >= Start && time <= End
            : time >= Start || time <= End;
    }

    /// <summary>
    /// The next instant strictly after <paramref name="utcNow"/> at which the
    /// window opens.
    /// </summary>
    /// <remarks>
    /// Callers only reach this while the window is closed, so the next opening
    /// is always the next occurrence of <see cref="Start"/> — today's if it
    /// has not passed, otherwise tomorrow's. Both are strictly in the future
    /// for same-day and midnight-spanning windows alike, which is what keeps a
    /// deferred outbox row from being reclaimed in a tight loop.
    /// </remarks>
    public DateTime NextOpeningAfter(DateTime utcNow)
    {
        DateTime todaysOpening = utcNow.Date + Start;
        return todaysOpening > utcNow
            ? todaysOpening
            : utcNow.Date.AddDays(1) + Start;
    }

    private static bool IsTimeOfDay(TimeSpan value) => value >= TimeSpan.Zero && value < _day;
}
