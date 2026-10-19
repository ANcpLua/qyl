# qyl 13.2.1 metric storage fixture

Created by the released `qyl.osx-arm64` 13.2.1 collector from commit `40847becae5d58351481d0bffe1a672ec5212176`, not by the current storage models. `schema.sql` and the CSV files are its DuckDB export. `load.sql` uses a test-substituted fixture directory.

The OTLP input was a cumulative sum with integer `9007199254740993`, one exemplar with integer `42`, and a trace/span ID. Version 13 stored the main value as `9007199254740992.0` and retained only the exemplar trace ID. Migration must preserve those historical facts without inventing the lost integer, exemplar value, or span ID.

The metric is `historical.counter`, service `v13-fixture`, timestamp `1700000000000000000`, and start timestamp `1699999999000000000`. All values are synthetic.
