using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Primitives;

namespace Qyl;

/// <summary>
/// The agent contract of a Qyl API: the W3C <c>baggage</c> request header's <c>session.id</c> member becomes the
/// <c>session.id</c> tag on the server span. Qyl.Telemetry.Hosting's session processor copies that tag to every
/// descendant span in the process, so an agent that sends the header once finds everything the request triggered
/// under one session in the collector — no code in the API and no code in the agent's target beyond this filter.
/// </summary>
/// <remarks>
/// <para>
/// It is a startup filter rather than a piece of the endpoint pipeline because the tag has to be on the span before
/// anything else runs: the middleware is the outermost one in the application, inside the server activity the hosting
/// layer has already started, so every child activity a handler creates is a descendant of a span that already carries
/// the session.
/// </para>
/// <para>
/// A request that carries no <c>baggage</c> header, or one without a <c>session.id</c> member, is left alone. Nothing
/// is invented: an untagged trace is grouped by trace id, which is what a Qyl API without an agent in front of it did
/// before. Public only so the linked <c>AddQylApi</c> can register it.
/// </para>
/// </remarks>
public sealed class QylSessionBaggageStartupFilter : IStartupFilter
{
    private const string BaggageHeaderName = "baggage";

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            app.Use(static (context, nextMiddleware) =>
            {
                if (Activity.Current is { } activity &&
                    activity.GetTagItem(QylApiContract.SessionIdName) is null &&
                    TryReadSessionId(context.Request.Headers[BaggageHeaderName], out var sessionId))
                {
                    activity.SetTag(QylApiContract.SessionIdName, sessionId);
                }

                return nextMiddleware(context);
            });

            next(app);
        };
    }

    /// <summary>
    /// Reads the <c>session.id</c> member out of a W3C <c>baggage</c> header value: comma-separated members,
    /// each <c>key=value</c> with optional <c>;</c>-separated properties, the value percent-encoded.
    /// </summary>
    internal static bool TryReadSessionId(StringValues header, out string sessionId)
    {
        foreach (var headerValue in header)
        {
            var remaining = headerValue.AsSpan();
            while (!remaining.IsEmpty)
            {
                var separator = remaining.IndexOf(',');
                var member = separator < 0 ? remaining : remaining[..separator];
                remaining = separator < 0 ? [] : remaining[(separator + 1)..];

                var properties = member.IndexOf(';');
                if (properties >= 0)
                    member = member[..properties];

                var assignment = member.IndexOf('=');
                if (assignment < 0)
                    continue;

                if (!member[..assignment].Trim().Equals(QylApiContract.SessionIdName, StringComparison.Ordinal))
                    continue;

                var encoded = member[(assignment + 1)..].Trim();
                if (encoded.IsEmpty || encoded.Length > QylApiContract.MaxSessionIdLength * 3)
                    continue;

                var candidate = Uri.UnescapeDataString(encoded.ToString()).Trim();
                if (IsAcceptable(candidate))
                {
                    sessionId = candidate;
                    return true;
                }
            }
        }

        sessionId = string.Empty;
        return false;
    }

    // The decoded value becomes a storage key and a query parameter in the collector's read API, so it is bounded
    // and free of control characters. Anything else is not a session id an agent can ask for again afterwards.
    private static bool IsAcceptable(string sessionId)
    {
        if (sessionId.Length is 0 or > QylApiContract.MaxSessionIdLength)
            return false;

        foreach (var character in sessionId)
        {
            if (char.IsControl(character))
                return false;
        }

        return true;
    }
}
