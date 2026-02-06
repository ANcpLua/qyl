---
description: Set up qyl development environment and project configuration
---

# qyl Setup

1. Check/install .NET 10.0 SDK (`dotnet --version`)
2. Restore NuGet packages (`dotnet restore`)
3. Build the solution (`dotnet build`)
4. Review environment variables (`QYL_PORT`, `QYL_GRPC_PORT`, `QYL_DATA_PATH`)
5. Start the collector (`dotnet run --project src/qyl.collector`)
