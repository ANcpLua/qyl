
#nullable disable

using System;

namespace Qyl.Domains.Observe.Session
{
    internal static partial class SessionStateExtensions
    {
        public static string ToSerialString(this SessionState value) => value switch
        {
            SessionState.Active => "active",
            SessionState.Idle => "idle",
            SessionState.Ended => "ended",
            SessionState.TimedOut => "timed_out",
            SessionState.Invalidated => "invalidated",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown SessionState value.")
        };

        public static SessionState ToSessionState(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "active"))
            {
                return SessionState.Active;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "idle"))
            {
                return SessionState.Idle;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "ended"))
            {
                return SessionState.Ended;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "timed_out"))
            {
                return SessionState.TimedOut;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "invalidated"))
            {
                return SessionState.Invalidated;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown SessionState value.");
        }
    }
}
