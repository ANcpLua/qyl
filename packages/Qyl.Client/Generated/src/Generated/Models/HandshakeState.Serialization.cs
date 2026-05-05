
#nullable disable

using System;

namespace Qyl.Domains.Workspace
{
    internal static partial class HandshakeStateExtensions
    {
        public static string ToSerialString(this HandshakeState value) => value switch
        {
            HandshakeState.Pending => "pending",
            HandshakeState.Verified => "verified",
            HandshakeState.Expired => "expired",
            HandshakeState.Rejected => "rejected",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown HandshakeState value.")
        };

        public static HandshakeState ToHandshakeState(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "pending"))
            {
                return HandshakeState.Pending;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "verified"))
            {
                return HandshakeState.Verified;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "expired"))
            {
                return HandshakeState.Expired;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "rejected"))
            {
                return HandshakeState.Rejected;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown HandshakeState value.");
        }
    }
}
