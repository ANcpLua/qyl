---
description: View qyl collector logs and telemetry data
---

# View qyl Logs

1. Check running containers with `docker ps` or process with `dotnet`
2. Stream collector logs with `docker logs -f qyl-collector` or console output
3. Query stored telemetry via the REST API: `curl http://localhost:5100/api/v1/spans`
4. Analyze and report any errors found
