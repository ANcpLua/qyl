# Dagger: decision pending

Measured 2026-09-17: Dagger v0.21.9 (2026-08-26), pre-1.0, official SDKs now include `dotnet`; mise can pin it (`mise ls-remote dagger`).

What it would replace: the step lists in fourteen workflow files across the six repositories become one function per gate (restore, build, test, the OTLP smoke with its collector, the weaver checks, the contract packing and probes), run in content-cached Linux containers, identical on a laptop and on CI.

What it cannot replace: the four non-Linux NativeAOT lanes (win-x64, win-arm64, osx-x64, osx-arm64). NativeAOT links with the target OS's own toolchain, so those stay GitHub runner jobs, as in `dotnet/aspire` and `dotnet/runtime`. Dagger would own the Linux lane only.

Open question, owner's call: adopt for the container-able gates at pre-1.0, or wait for 1.0. The `mise.toml` tasks in each repository are the seam either way; a Dagger function would be one more task, not a replacement of the file.
