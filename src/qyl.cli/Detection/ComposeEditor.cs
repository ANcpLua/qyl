using YamlDotNet.RepresentationModel;

namespace qyl.cli.Detection;

/// <summary>
/// Safe docker-compose YAML manipulation — adds qyl service and OTEL env vars.
/// </summary>
public sealed class ComposeEditor
{
    private readonly string _path;
    private readonly string _content;
    private readonly YamlStream _yaml;
    private readonly YamlMappingNode _root;

    public ComposeEditor(string path)
    {
        _path = path;
        _content = File.ReadAllText(path);
        _yaml = new YamlStream();
        _yaml.Load(new StringReader(_content));
        _root = (YamlMappingNode)_yaml.Documents[0].RootNode;
    }

    /// <summary>
    /// Checks if a 'qyl' service already exists in the compose file.
    /// </summary>
    public bool HasQylService()
    {
        if (!_root.Children.TryGetValue(new YamlScalarNode("services"), out var servicesNode))
        {
            return false;
        }

        if (servicesNode is not YamlMappingNode services)
        {
            return false;
        }

        return services.Children.ContainsKey(new YamlScalarNode("qyl"));
    }

    /// <summary>
    /// Returns a list of planned changes for dry-run output.
    /// </summary>
    public List<string> GetPlannedChanges()
    {
        var changes = new List<string> { "Add 'qyl' service (collector + gRPC)" };

        var services = GetServicesNode();
        if (services is not null)
        {
            foreach (var (key, _) in services.Children)
            {
                if (key is YamlScalarNode { Value: not "qyl" } name)
                {
                    if (!ServiceHasOtelEndpoint(services, name.Value!))
                    {
                        changes.Add($"Add OTEL env vars to service '{name.Value}'");
                    }
                }
            }
        }

        if (!HasVolume("qyl-data"))
        {
            changes.Add("Add 'qyl-data' volume");
        }

        return changes;
    }

    /// <summary>
    /// Applies all changes to the compose file.
    /// </summary>
    public void Apply()
    {
        var services = GetOrCreateServicesNode();

        // Add qyl service
        var qylService = new YamlMappingNode
        {
            { "image", "ghcr.io/ancplua/qyl:latest" },
            {
                "ports", new YamlSequenceNode(
                    new YamlScalarNode("5100:5100"),
                    new YamlScalarNode("4317:4317"))
            },
            {
                "volumes", new YamlSequenceNode(
                    new YamlScalarNode("qyl-data:/data"))
            },
            {
                "environment", new YamlMappingNode
                {
                    { "QYL_DATA_PATH", "/data/qyl.duckdb" },
                }
            },
        };

        services.Children[new YamlScalarNode("qyl")] = qylService;

        // Add OTEL env vars to other services
        foreach (var (key, value) in services.Children)
        {
            if (key is not YamlScalarNode { Value: not "qyl" } name)
            {
                continue;
            }

            if (value is not YamlMappingNode serviceNode)
            {
                continue;
            }

            if (ServiceHasOtelEndpoint(services, name.Value!))
            {
                continue;
            }

            var envNode = GetOrCreateEnvironment(serviceNode);
            envNode.Children[new YamlScalarNode("OTEL_EXPORTER_OTLP_ENDPOINT")] = new YamlScalarNode("http://qyl:4317");
            envNode.Children[new YamlScalarNode("OTEL_SERVICE_NAME")] = new YamlScalarNode(name.Value!);
        }

        // Add volume
        if (!HasVolume("qyl-data"))
        {
            var volumes = GetOrCreateVolumes();
            volumes.Children[new YamlScalarNode("qyl-data")] = new YamlMappingNode();
        }

        using var writer = new StreamWriter(_path);
        _yaml.Save(writer, assignAnchors: false);
    }

    private YamlMappingNode? GetServicesNode()
    {
        if (_root.Children.TryGetValue(new YamlScalarNode("services"), out var node)
            && node is YamlMappingNode services)
        {
            return services;
        }

        return null;
    }

    private YamlMappingNode GetOrCreateServicesNode()
    {
        var services = GetServicesNode();
        if (services is not null)
        {
            return services;
        }

        services = new YamlMappingNode();
        _root.Children[new YamlScalarNode("services")] = services;
        return services;
    }

    private static bool ServiceHasOtelEndpoint(YamlMappingNode services, string serviceName)
    {
        if (!services.Children.TryGetValue(new YamlScalarNode(serviceName), out var serviceNode))
        {
            return false;
        }

        if (serviceNode is not YamlMappingNode service)
        {
            return false;
        }

        if (!service.Children.TryGetValue(new YamlScalarNode("environment"), out var envNode))
        {
            return false;
        }

        if (envNode is YamlMappingNode envMap)
        {
            return envMap.Children.ContainsKey(new YamlScalarNode("OTEL_EXPORTER_OTLP_ENDPOINT"));
        }

        // environment can be a sequence of KEY=VALUE strings
        if (envNode is YamlSequenceNode envSeq)
        {
            return envSeq.Children.Any(static c =>
                c is YamlScalarNode s && s.Value is not null
                && s.Value.StartsWith("OTEL_EXPORTER_OTLP_ENDPOINT=", StringComparison.Ordinal));
        }

        return false;
    }

    private static YamlMappingNode GetOrCreateEnvironment(YamlMappingNode serviceNode)
    {
        if (serviceNode.Children.TryGetValue(new YamlScalarNode("environment"), out var envNode)
            && envNode is YamlMappingNode envMap)
        {
            return envMap;
        }

        envMap = new YamlMappingNode();
        serviceNode.Children[new YamlScalarNode("environment")] = envMap;
        return envMap;
    }

    private bool HasVolume(string volumeName)
    {
        if (!_root.Children.TryGetValue(new YamlScalarNode("volumes"), out var volumesNode))
        {
            return false;
        }

        if (volumesNode is not YamlMappingNode volumes)
        {
            return false;
        }

        return volumes.Children.ContainsKey(new YamlScalarNode(volumeName));
    }

    private YamlMappingNode GetOrCreateVolumes()
    {
        if (_root.Children.TryGetValue(new YamlScalarNode("volumes"), out var node)
            && node is YamlMappingNode volumes)
        {
            return volumes;
        }

        volumes = new YamlMappingNode();
        _root.Children[new YamlScalarNode("volumes")] = volumes;
        return volumes;
    }
}
