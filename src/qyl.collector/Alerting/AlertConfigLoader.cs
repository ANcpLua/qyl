using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace qyl.collector.Alerting;

/// <summary>
///     Loads alert rules from YAML files and supports hot-reload via FileSystemWatcher.
/// </summary>
public sealed partial class AlertConfigLoader : IDisposable
{
    private readonly ILogger<AlertConfigLoader> _logger;
    private readonly string _configPath;
    private readonly IDeserializer _deserializer;
    private FileSystemWatcher? _watcher;
    private volatile AlertConfiguration _current = new([]);
    private Action<AlertConfiguration>? _onChanged;

    public AlertConfigLoader(ILogger<AlertConfigLoader> logger, string? configPath = null)
    {
        _logger = logger;
        _configPath = configPath
            ?? Environment.GetEnvironmentVariable("QYL_ALERTS_PATH")
            ?? "./qyl-alerts.yaml";

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public AlertConfiguration Current => _current;

    /// <summary>Registers a callback invoked when the YAML configuration is reloaded.</summary>
    public void OnConfigurationChanged(Action<AlertConfiguration> callback) =>
        _onChanged += callback;

    /// <summary>
    ///     Loads the initial configuration and starts watching for changes.
    /// </summary>
    public AlertConfiguration LoadAndWatch()
    {
        _current = LoadConfiguration();
        StartWatching();
        return _current;
    }

    private AlertConfiguration LoadConfiguration()
    {
        try
        {
            var path = Path.GetFullPath(_configPath);

            if (Directory.Exists(path))
                return LoadFromDirectory(path);

            if (File.Exists(path))
                return LoadFromFile(path);

            LogConfigNotFound(_logger, _configPath);
            return new AlertConfiguration([]);
        }
        catch (Exception ex)
        {
            LogConfigLoadError(_logger, _configPath, ex);
            return new AlertConfiguration([]);
        }
    }

    private AlertConfiguration LoadFromFile(string filePath)
    {
        var yaml = File.ReadAllText(filePath);
        var dto = _deserializer.Deserialize<AlertConfigYaml>(yaml);
        var rules = MapRules(dto);
        LogConfigLoaded(_logger, rules.Count, filePath);
        return new AlertConfiguration(rules);
    }

    private AlertConfiguration LoadFromDirectory(string dirPath)
    {
        var allRules = new List<AlertRule>();

        foreach (var file in Directory.EnumerateFiles(dirPath, "*.yaml")
                     .Concat(Directory.EnumerateFiles(dirPath, "*.yml")))
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var dto = _deserializer.Deserialize<AlertConfigYaml>(yaml);
                allRules.AddRange(MapRules(dto));
            }
            catch (Exception ex)
            {
                LogFileParseError(_logger, file, ex);
            }
        }

        LogConfigLoaded(_logger, allRules.Count, dirPath);
        return new AlertConfiguration(allRules);
    }

    private static List<AlertRule> MapRules(AlertConfigYaml? dto)
    {
        if (dto?.Alerts is null)
            return [];

        var rules = new List<AlertRule>(dto.Alerts.Count);
        foreach (var a in dto.Alerts)
        {
            if (string.IsNullOrWhiteSpace(a.Name) || string.IsNullOrWhiteSpace(a.Query))
                continue;

            var channels = a.Channels?.Select(static c =>
                new NotificationChannel(c.Type ?? "console", c.Url)).ToList()
                ?? [new NotificationChannel("console", null)];

            rules.Add(new AlertRule(
                a.Name,
                a.Description ?? "",
                a.Query,
                a.Condition ?? "> 0",
                TimeSpan.FromSeconds(a.IntervalSeconds > 0 ? a.IntervalSeconds : 60),
                TimeSpan.FromSeconds(a.CooldownSeconds > 0 ? a.CooldownSeconds : 300),
                channels));
        }

        return rules;
    }

    private void StartWatching()
    {
        try
        {
            var fullPath = Path.GetFullPath(_configPath);
            string watchDir;
            string watchFilter;

            if (Directory.Exists(fullPath))
            {
                watchDir = fullPath;
                watchFilter = "*.yaml";
            }
            else if (File.Exists(fullPath))
            {
                watchDir = Path.GetDirectoryName(fullPath)!;
                watchFilter = Path.GetFileName(fullPath);
            }
            else
            {
                return;
            }

            _watcher = new FileSystemWatcher(watchDir, watchFilter)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
        }
        catch (Exception ex)
        {
            LogWatcherError(_logger, ex);
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Debounce: wait briefly for file to stabilize
            Thread.Sleep(250);
            var newConfig = LoadConfiguration();
            _current = newConfig;
            _onChanged?.Invoke(newConfig);
            LogConfigReloaded(_logger, newConfig.Alerts.Count);
        }
        catch (Exception ex)
        {
            LogConfigReloadError(_logger, ex);
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }

    // ==========================================================================
    // YAML DTOs — snake_case mapping
    // ==========================================================================

    private sealed class AlertConfigYaml
    {
        public List<AlertRuleYaml>? Alerts { get; set; }
    }

    private sealed class AlertRuleYaml
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string Query { get; set; } = "";
        public string? Condition { get; set; }

        [YamlMember(Alias = "interval_seconds")]
        public int IntervalSeconds { get; set; }

        [YamlMember(Alias = "cooldown_seconds")]
        public int CooldownSeconds { get; set; }

        public List<ChannelYaml>? Channels { get; set; }
    }

    private sealed class ChannelYaml
    {
        public string? Type { get; set; }
        public string? Url { get; set; }
    }

    // ==========================================================================
    // Log Messages
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} alert rules from {Path}")]
    private static partial void LogConfigLoaded(ILogger logger, int count, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Alert config not found at {Path}, running with no alerts")]
    private static partial void LogConfigNotFound(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load alert config from {Path}")]
    private static partial void LogConfigLoadError(ILogger logger, string path, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse alert file {Path}")]
    private static partial void LogFileParseError(ILogger logger, string path, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start FileSystemWatcher for alert config")]
    private static partial void LogWatcherError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Alert config reloaded: {Count} rules")]
    private static partial void LogConfigReloaded(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to reload alert config")]
    private static partial void LogConfigReloadError(ILogger logger, Exception ex);
}
