---
description: Deploy the qyl observability platform
---

# Deploy qyl

1. Check prerequisites (`dotnet --version`, `docker --version`)
2. Build the solution (`dotnet build`)
3. Run tests (`dotnet test`)
4. Deploy via Docker (`docker compose up -d`) or `dotnet run --project src/qyl.collector`
5. Display the collector URL (default: http://localhost:5100)
