

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Attributes.Jvm;

public static class JvmAttributes
{
    public const string GcAction = "jvm.gc.action";

    public const string GcName = "jvm.gc.name";

    public const string MemoryPoolName = "jvm.memory.pool.name";

    public const string MemoryType = "jvm.memory.type";

    public static class MemoryTypeValues
    {
        public const string Heap = "heap";

        public const string NonHeap = "non_heap";
    }

    public const string ThreadDaemon = "jvm.thread.daemon";

    public const string ThreadState = "jvm.thread.state";

    public static class ThreadStateValues
    {
        public const string Blocked = "blocked";

        public const string New = "new";

        public const string Runnable = "runnable";

        public const string Terminated = "terminated";

        public const string TimedWaiting = "timed_waiting";

        public const string Waiting = "waiting";
    }
}
