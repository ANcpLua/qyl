using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Qyl;

/// <summary>
/// The agent contract of a Qyl API: the W3C <c>baggage</c> request header's <c>session.id</c> member becomes the
/// <c>session.id</c> tag on the ASP.NET Core server span. Qyl.Telemetry.Hosting's session processor copies that
/// tag to every descendant span in the process, so an agent that sends the header once finds everything the
/// request triggered under one session in the collector — no code in the API and none in the agent's target
/// beyond this filter.
/// </summary>
/// <remarks>
/// <para>
/// The header is not parsed here. ASP.NET Core's hosting layer already parsed it when it started the server
/// activity, and <see cref="Activity.GetBaggageItem"/> returns the decoded, first-wins value; a second parser
/// would be a second opinion about the same bytes, and the two disagreed on <c>;</c>-separated member
/// properties. What this adds is the tag and the admission rule, not the parsing.
/// </para>
/// <para>
/// It stamps the ASP.NET Core server span and nothing else. <c>Activity.Current</c> in a middleware is whatever
/// the pipeline above it left there: a legacy <c>HttpRequestIn</c> activity with no source when nothing is
/// subscribed, a non-recording propagation-only activity when the incoming <c>traceparent</c> says
/// <c>sampled=0</c>, or an unrelated activity from a startup filter registered ahead of <c>AddQylApi</c>. None
/// of those is the span the collector will receive, so the guard is the same one Qyl.Telemetry.Hosting's own
/// filter uses.
/// </para>
/// <para>
/// A request that carries no <c>baggage</c> header, no <c>session.id</c> member, or one that is not a session id
/// by <see cref="QylApiContract.SessionIdPattern"/> is left alone. Nothing is invented: an untagged trace is
/// grouped by trace id, which is what a Qyl API without an agent in front of it did before. Public only so the
/// linked <c>AddQylApi</c> can register it.
/// </para>
/// </remarks>
public sealed class QylSessionBaggageStartupFilter : IStartupFilter
{
    /// <summary>The activity source of the one server span per request that ASP.NET Core starts and qyl exports.</summary>
    internal const string ServerActivitySourceName = "Microsoft.AspNetCore";

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            app.Use(static (_, nextMiddleware) =>
            {
                Stamp(Activity.Current);
                return nextMiddleware();
            });

            next(app);
        };
    }

    /// <summary>
    /// The one thing the runtime's baggage value still carries: W3C allows <c>;</c>-separated properties on a
    /// member and .NET leaves them in the value, so <c>session.id=agent-1;p=x</c> arrives as
    /// <c>agent-1;p=x</c>. Cutting at the separator is not a second parse of the header — the member and its
    /// value were already found — and without it a spec-legal request would silently lose its session.
    /// The optional whitespace the spec permits before the separator goes with it; nothing else is trimmed,
    /// so a value that decoded to spaces stays exactly as it decoded and is rejected below.
    /// </summary>
    private static string? WithoutMemberProperties(string? member)
    {
        if (member is null)
            return null;

        var properties = member.IndexOf(';', StringComparison.Ordinal);
        return properties < 0 ? member : member[..properties].TrimEnd(' ', '\t');
    }

    /// <summary>Copies the request's <c>session.id</c> baggage member onto the server span, if both are what they must be.</summary>
    internal static void Stamp(Activity? activity)
    {
        if (activity is not { Kind: ActivityKind.Server, IsAllDataRequested: true } ||
            !string.Equals(activity.Source.Name, ServerActivitySourceName, StringComparison.Ordinal) ||
            activity.GetTagItem(QylApiContract.SessionIdName) is not null)
        {
            return;
        }

        var sessionId = WithoutMemberProperties(activity.GetBaggageItem(QylApiContract.SessionIdName));
        if (QylApiContract.IsSessionId(sessionId))
            activity.SetTag(QylApiContract.SessionIdName, sessionId);
    }
}
