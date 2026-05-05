

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.System;

public static class SystemAttributes
{
    [global::System.Obsolete("Replaced by cpu.logical_number.", false)]
    public const string CpuLogicalNumber = "system.cpu.logical_number";

    [global::System.Obsolete("Replaced by cpu.mode.", false)]
    public const string CpuState = "system.cpu.state";

    public static class CpuStateValues
    {
        public const string Idle = "idle";

        public const string Interrupt = "interrupt";

        public const string Iowait = "iowait";

        public const string Nice = "nice";

        public const string Steal = "steal";

        public const string System = "system";

        public const string User = "user";
    }

    public const string Device = "system.device";

    public const string FilesystemMode = "system.filesystem.mode";

    public const string FilesystemMountpoint = "system.filesystem.mountpoint";

    public const string FilesystemState = "system.filesystem.state";

    public static class FilesystemStateValues
    {
        public const string Free = "free";

        public const string Reserved = "reserved";

        public const string Used = "used";
    }

    public const string FilesystemType = "system.filesystem.type";

    public static class FilesystemTypeValues
    {
        public const string Exfat = "exfat";

        public const string Ext4 = "ext4";

        public const string Fat32 = "fat32";

        public const string Hfsplus = "hfsplus";

        public const string Ntfs = "ntfs";

        public const string Refs = "refs";
    }

    public const string MemoryLinuxHugepagesState = "system.memory.linux.hugepages.state";

    public static class MemoryLinuxHugepagesStateValues
    {
        public const string Free = "free";

        public const string Used = "used";
    }

    public const string MemoryLinuxSlabState = "system.memory.linux.slab.state";

    public static class MemoryLinuxSlabStateValues
    {
        public const string Reclaimable = "reclaimable";

        public const string Unreclaimable = "unreclaimable";
    }

    public const string MemoryState = "system.memory.state";

    public static class MemoryStateValues
    {
        public const string Buffers = "buffers";

        public const string Cached = "cached";

        public const string Free = "free";

        [global::System.Obsolete("{\"note\": \"Removed, report shared memory usage with `metric.system.memory.linux.shared` metric\", \"reason\": \"uncategorized\"}", false)]
        public const string Shared = "shared";

        public const string Used = "used";
    }

    [global::System.Obsolete("Replaced by network.connection.state.", false)]
    public const string NetworkState = "system.network.state";

    public static class NetworkStateValues
    {
        public const string Close = "close";

        public const string CloseWait = "close_wait";

        public const string Closing = "closing";

        public const string Delete = "delete";

        public const string Established = "established";

        public const string FinWait1 = "fin_wait_1";

        public const string FinWait2 = "fin_wait_2";

        public const string LastAck = "last_ack";

        public const string Listen = "listen";

        public const string SynRecv = "syn_recv";

        public const string SynSent = "syn_sent";

        public const string TimeWait = "time_wait";
    }

    public const string PagingDirection = "system.paging.direction";

    public static class PagingDirectionValues
    {
        public const string In = "in";

        public const string Out = "out";
    }

    public const string PagingFaultType = "system.paging.fault.type";

    public static class PagingFaultTypeValues
    {
        public const string Major = "major";

        public const string Minor = "minor";
    }

    public const string PagingState = "system.paging.state";

    public static class PagingStateValues
    {
        public const string Free = "free";

        public const string Used = "used";
    }

    [global::System.Obsolete("Replaced by system.paging.fault.type.", false)]
    public const string PagingType = "system.paging.type";

    public static class PagingTypeValues
    {
        public const string Major = "major";

        public const string Minor = "minor";
    }

    [global::System.Obsolete("Replaced by process.state.", false)]
    public const string ProcessStatus = "system.process.status";

    public static class ProcessStatusValues
    {
        public const string Defunct = "defunct";

        public const string Running = "running";

        public const string Sleeping = "sleeping";

        public const string Stopped = "stopped";
    }

    [global::System.Obsolete("Replaced by process.state.", false)]
    public const string ProcessesStatus = "system.processes.status";

    public static class ProcessesStatusValues
    {
        public const string Defunct = "defunct";

        public const string Running = "running";

        public const string Sleeping = "sleeping";

        public const string Stopped = "stopped";
    }
}
