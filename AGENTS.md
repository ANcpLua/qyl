# qyl

qyl's Collector (api.qyl.at), the `qyl` dotnet tool and `Qyl.Api.Sdk`.

Push to `main` directly; the push deploys the Collector (`qyl-collector`, api.qyl.at, Railway). Gate before every push: `mise run check` (`./eng/build.sh Check`); `mise run check-full` on demand (adds the dotcov coverage gate). The gate prints a summary and the log path under `artifacts/logs/`; `QYL_VERBOSE=1` prints everything. Releases, re-pins and the contract-revision deploy order: [`SKILL.md`](SKILL.md).
