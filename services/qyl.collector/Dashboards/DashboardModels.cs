namespace Qyl.Collector.Dashboards;

public sealed record DashboardDefinition(
    string Id,
    string Title,
    string Description,
    string Icon,
    bool IsAvailable);

public sealed record DashboardData(
    string DashboardId,
    string Title,
    string Description,
    string Icon,
    IReadOnlyList<DashboardWidget> Widgets);

public sealed record DashboardWidget(
    string Id,
    string Title,
    string Type,
    object Data);

public sealed record StatCardData(
    string Label,
    string Value,
    string? Unit = null,
    double? Change = null);

public sealed record TimeSeriesPoint(
    string Time,
    double Value,
    string? Label = null);

public sealed record TopNRow(
    string Name,
    double Value,
    string? Unit = null,
    int? Count = null,
    double? ErrorRate = null);
