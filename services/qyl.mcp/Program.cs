using qyl.mcp;
using qyl.mcp.Hosting;
using qyl.mcp.Scoping;

var skills = SkillConfiguration.FromEnvironment();
var scope = QylScope.FromEnvironment();
var transport = McpHostOptions.ResolveTransport(args);

if (transport is McpTransportMode.Http)
{
    await QylMcpHttpHost.RunAsync(args, skills, scope).ConfigureAwait(false);
}
else
{
    await QylMcpStdioHost.RunAsync(args, skills, scope).ConfigureAwait(false);
}
