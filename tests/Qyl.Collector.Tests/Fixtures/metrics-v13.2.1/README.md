# qyl 13.2.1 metric storage fixture

A DuckDB export written by the released `qyl.osx-arm64` 13.2.1 Collector (commit
`40847becae5d58351481d0bffe1a672ec5212176`), not by the current storage models: `schema.sql` and
the CSV files. `load.sql` loads them from the directory the tests substitute.

The OTLP input was one cumulative sum with the integer `9007199254740993` and one exemplar with the
integer `42` and a trace and span ID. 13.2.1 stored the value as `9007199254740992.0` and kept only
the exemplar's trace ID. `CanonicalMetricTests` opens the database with the current store and
checks that it keeps those values and invents none of the lost ones; `OtlpRecordValidationTests`
loads it to add released log rows with unknown times.

The metric is `historical.counter` of service `v13-fixture`, at `1700000000000000000` with start
`1699999999000000000`. All values are synthetic.
