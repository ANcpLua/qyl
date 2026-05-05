

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.V8js;

public static class V8jsAttributes
{
    public const string GcType = "v8js.gc.type";

    public static class GcTypeValues
    {
        public const string Incremental = "incremental";

        public const string Major = "major";

        public const string Minor = "minor";

        public const string Weakcb = "weakcb";
    }

    public const string HeapSpaceName = "v8js.heap.space.name";

    public static class HeapSpaceNameValues
    {
        public const string CodeSpace = "code_space";

        public const string LargeObjectSpace = "large_object_space";

        public const string MapSpace = "map_space";

        public const string NewSpace = "new_space";

        public const string OldSpace = "old_space";
    }

    public const string ResourceType = "v8js.resource.type";

    public static class ResourceTypeValues
    {
        public const string Immediate = "Immediate";

        public const string Tcpserverwrap = "TCPServerWrap";

        public const string Tcpwrap = "TCPWrap";

        public const string Timeout = "Timeout";

        public const string Ttywrap = "TTYWrap";
    }
}
