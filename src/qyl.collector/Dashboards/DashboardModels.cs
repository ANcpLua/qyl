namespace qyl.collector.Dashboards;

/// <summary>
///     Describes an auto-detected dashboard that appears when matching telemetry exists.
/// </summary>
public sealed record DashboardDefinition(
    string Id,
    string Title,
    string Description,
    string Icon,
    bool IsAvailable);

/// <summary>
///     Full dashboard payload with pre-computed widget data.
/// </summary>
public sealed record DashboardData(
    string DashboardId,
    string Title,
    string Description,
    string Icon,
    IReadOnlyList<DashboardWidget> Widgets);

/// <summary>
///     A single widget inside a dashboard (chart, table, or stat card).
/// </summary>
public sealed record DashboardWidget(
    string Id,
    string Title,
    string Type,
    object Data);

/// <summary>
///     Stat card data — a single metric with optional comparison.
/// </summary>
public sealed record StatCardData(
    string Label,
    string Value,
    string? Unit = null,
    double? Change = null);

/// <summary>
///     Time series data point for charts.
/// </summary>
public sealed record TimeSeriesPoint(
    string Time,
    double Value,
    string? Label = null);

/// <summary>
///     A row in a "top N" table widget.
/// </summary>
public sealed record TopNRow(
    string Name,
    double Value,
    string? Unit = null,
    int? Count = null,
    double? ErrorRate = null);
