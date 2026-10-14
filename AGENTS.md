# qyl

qyl's Collector (api.qyl.at), the `qyl` dotnet tool and `Qyl.Api.Sdk`.

Push to `main` directly; the push deploys the Collector (`qyl-collector`, api.qyl.at, Railway). Gate before every push: `mise run check` (`./eng/build.sh Check`); `mise run check-full` on demand (adds the dotcov coverage gate; without a container daemon it skips the NativeAOT smoke and the SDK container stage, which run in `gh workflow run check.yml -f full=true`). Local development runs the collector as a process (`qyl up` or `dotnet run`), not in containers. The gate prints a summary and the log path under `artifacts/logs/`; `QYL_VERBOSE=1` prints everything. CI runs the same gate and only reports (`.github/workflows/check.yml` on push and pull request; `weekly.yml` audits packages, runs CodeQL and checks links). Releases, re-pins and the contract-revision deploy order: [`SKILL.md`](SKILL.md).
