# charts

## Chart split for qyl
Use ECharts for heavy observability and operator-grade telemetry surfaces.
Use Recharts for lighter dashboard visuals.

## ECharts use cases
Prefer ECharts for:
- dense time-series exploration
- multi-series observability charts
- high-point-count telemetry screens
- screens where chart performance is a core product requirement
- advanced interaction models

## Recharts use cases
Allow Recharts for:
- KPI cards
- overview dashboards
- simple bar charts
- simple line charts
- admin analytics
- lightweight product visuals where the chart is not the core operator work surface

## Hard rule
Do not use Recharts as the default for heavy observability screens just because it is easy to wire up.

## Selection heuristic
Ask:
- Is this chart decorative or operational?
- Is it light dashboarding or dense telemetry exploration?
- Is performance under scale part of the product requirement?

If the answer points to dense observability, use ECharts.

## Product principle
Charts in qyl are not merely visual garnish. In many screens they are part of the analysis workflow, so chart-library choice is an architectural decision.
