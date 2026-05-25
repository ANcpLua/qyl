using qyl.mcp.Hosting;
using qyl.mcp.Scoping;

var skills = SkillConfiguration.FromEnvironment();
var scope = QylScope.FromEnvironment();

await QylMcpStdioHost.RunAsync(args, skills, scope).ConfigureAwait(false);
