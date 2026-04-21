// Copyright (c) 2025-2026 ancplua

using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;

namespace Qyl.Fleet.Hosting;

/// <summary>
/// A distributed-application resource that fronts multiple <c>qyl.collector</c> backends with a
/// single HTTP endpoint and serves the qyl dashboard frontend.
/// </summary>
/// <remarks>
/// The aggregator runs in-process inside the AppHost, requiring no container image. It serves
/// the dashboard SPA from embedded assembly resources when <c>Qyl.Dashboard.Embedded</c> is
/// available, otherwise falls back to proxying from the first configured backend.
/// </remarks>
/// <param name="name">The resource name.</param>
public class QylDashboardResource(string name) : Resource(name), IResourceWithEndpoints, IResourceWithWaitSupport
{
    internal const string PrimaryEndpointName = "http";

    internal QylDashboardResource(string name, int? port) : this(name)
    {
        Port = port;
        Annotations.Add(new EndpointAnnotation(
            ProtocolType.Tcp,
            uriScheme: "http",
            name: PrimaryEndpointName,
            port: port,
            isProxied: false)
        {
            TargetHost = "localhost",
        });
    }

    internal int? Port { get; }

    /// <summary>The primary HTTP endpoint that serves the dashboard and the aggregated REST surface.</summary>
    public EndpointReference PrimaryEndpoint => field ??= new(this, PrimaryEndpointName);
}
