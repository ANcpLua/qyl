

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Test;

public static class TestAttributes
{
    public const string CaseName = "test.case.name";

    public const string CaseResultStatus = "test.case.result.status";

    public static class CaseResultStatusValues
    {
        public const string Fail = "fail";

        public const string Pass = "pass";
    }

    public const string SuiteName = "test.suite.name";

    public const string SuiteRunStatus = "test.suite.run.status";

    public static class SuiteRunStatusValues
    {
        public const string Aborted = "aborted";

        public const string Failure = "failure";

        public const string InProgress = "in_progress";

        public const string Skipped = "skipped";

        public const string Success = "success";

        public const string TimedOut = "timed_out";
    }
}
