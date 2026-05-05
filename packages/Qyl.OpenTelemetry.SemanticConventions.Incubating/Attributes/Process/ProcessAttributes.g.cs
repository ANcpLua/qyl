

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Process;

public static class ProcessAttributes
{
    public const string ArgsCount = "process.args_count";

    public const string Command = "process.command";

    public const string CommandArgs = "process.command_args";

    public const string CommandLine = "process.command_line";

    public const string ContextSwitchType = "process.context_switch.type";

    public static class ContextSwitchTypeValues
    {
        public const string Involuntary = "involuntary";

        public const string Voluntary = "voluntary";
    }

    [global::System.Obsolete("Replaced by cpu.mode.", false)]
    public const string CpuState = "process.cpu.state";

    public static class CpuStateValues
    {
        public const string System = "system";

        public const string User = "user";

        public const string Wait = "wait";
    }

    public const string CreationTime = "process.creation.time";

    public const string EnvironmentVariable = "process.environment_variable";

    public const string ExecutableBuildIdGnu = "process.executable.build_id.gnu";

    public const string ExecutableBuildIdGo = "process.executable.build_id.go";

    public const string ExecutableBuildIdHtlhash = "process.executable.build_id.htlhash";

    [global::System.Obsolete("Replaced by process.executable.build_id.htlhash.", false)]
    public const string ExecutableBuildIdProfiling = "process.executable.build_id.profiling";

    public const string ExecutableName = "process.executable.name";

    public const string ExecutablePath = "process.executable.path";

    public const string ExitCode = "process.exit.code";

    public const string ExitTime = "process.exit.time";

    public const string GroupLeaderPid = "process.group_leader.pid";

    public const string Interactive = "process.interactive";

    public const string LinuxCgroup = "process.linux.cgroup";

    public const string Owner = "process.owner";

    [global::System.Obsolete("Replaced by system.paging.fault.type.", false)]
    public const string PagingFaultType = "process.paging.fault_type";

    public static class PagingFaultTypeValues
    {
        public const string Major = "major";

        public const string Minor = "minor";
    }

    public const string ParentPid = "process.parent_pid";

    public const string Pid = "process.pid";

    public const string RealUserId = "process.real_user.id";

    public const string RealUserName = "process.real_user.name";

    public const string RuntimeDescription = "process.runtime.description";

    public const string RuntimeName = "process.runtime.name";

    public const string RuntimeVersion = "process.runtime.version";

    public const string SavedUserId = "process.saved_user.id";

    public const string SavedUserName = "process.saved_user.name";

    public const string SessionLeaderPid = "process.session_leader.pid";

    public const string State = "process.state";

    public static class StateValues
    {
        public const string Defunct = "defunct";

        public const string Running = "running";

        public const string Sleeping = "sleeping";

        public const string Stopped = "stopped";
    }

    public const string Title = "process.title";

    public const string UserId = "process.user.id";

    public const string UserName = "process.user.name";

    public const string Vpid = "process.vpid";

    public const string WorkingDirectory = "process.working_directory";
}
