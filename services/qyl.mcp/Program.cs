using qyl.mcp.Hosting;
using qyl.mcp.Scoping;

var skills = SkillConfiguration.FromEnvironment();
var scope = QylScope.FromEnvironment();

await QylMcpStdioHost.RunAsync(args, skills, scope).ConfigureAwait(false);

// Explicit internal accessibility — keeps qyl.mcp's top-level Program from
// colliding with Qyl.Collector's public Program when both assemblies are
// referenced by tests (Qyl.Collector.Tests sees both via the qyl.mcp
// project reference).
internal partial class Program;
