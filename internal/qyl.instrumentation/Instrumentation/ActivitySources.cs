
namespace Qyl.Instrumentation.Instrumentation;

public static class ActivitySources
{
    public const string GenAi = GenAiConstants.SourceName;

    public const string Db = "qyl.db";

    public const string ErrorCapture = "Qyl.Instrumentation.ErrorCapture";

    private const string Version = BuildVersion.ProductVersion;

    private static readonly ActivitySource s_genAiSource = new(GenAi, Version);

    public static ActivitySource GenAiSource => s_genAiSource;
}
