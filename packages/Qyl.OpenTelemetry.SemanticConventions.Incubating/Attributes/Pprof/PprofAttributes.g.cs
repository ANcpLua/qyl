

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Pprof;

public static class PprofAttributes
{
    public const string LocationIsFolded = "pprof.location.is_folded";

    public const string MappingHasFilenames = "pprof.mapping.has_filenames";

    public const string MappingHasFunctions = "pprof.mapping.has_functions";

    public const string MappingHasInlineFrames = "pprof.mapping.has_inline_frames";

    public const string MappingHasLineNumbers = "pprof.mapping.has_line_numbers";

    public const string ProfileComment = "pprof.profile.comment";

    public const string ProfileDocUrl = "pprof.profile.doc_url";

    public const string ProfileDropFrames = "pprof.profile.drop_frames";

    public const string ProfileKeepFrames = "pprof.profile.keep_frames";

    public const string ScopeDefaultSampleType = "pprof.scope.default_sample_type";

    public const string ScopeSampleTypeOrder = "pprof.scope.sample_type_order";
}
