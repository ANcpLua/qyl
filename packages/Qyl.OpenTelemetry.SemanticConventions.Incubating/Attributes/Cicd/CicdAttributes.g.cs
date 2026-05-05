

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Cicd;

public static class CicdAttributes
{
    public const string PipelineActionName = "cicd.pipeline.action.name";

    public static class PipelineActionNameValues
    {
        public const string Build = "BUILD";

        public const string Run = "RUN";

        public const string Sync = "SYNC";
    }

    public const string PipelineName = "cicd.pipeline.name";

    public const string PipelineResult = "cicd.pipeline.result";

    public static class PipelineResultValues
    {
        public const string Cancellation = "cancellation";

        public const string Error = "error";

        public const string Failure = "failure";

        public const string Skip = "skip";

        public const string Success = "success";

        public const string Timeout = "timeout";
    }

    public const string PipelineRunId = "cicd.pipeline.run.id";

    public const string PipelineRunState = "cicd.pipeline.run.state";

    public static class PipelineRunStateValues
    {
        public const string Executing = "executing";

        public const string Finalizing = "finalizing";

        public const string Pending = "pending";
    }

    public const string PipelineRunUrlFull = "cicd.pipeline.run.url.full";

    public const string PipelineTaskName = "cicd.pipeline.task.name";

    public const string PipelineTaskRunId = "cicd.pipeline.task.run.id";

    public const string PipelineTaskRunResult = "cicd.pipeline.task.run.result";

    public static class PipelineTaskRunResultValues
    {
        public const string Cancellation = "cancellation";

        public const string Error = "error";

        public const string Failure = "failure";

        public const string Skip = "skip";

        public const string Success = "success";

        public const string Timeout = "timeout";
    }

    public const string PipelineTaskRunUrlFull = "cicd.pipeline.task.run.url.full";

    public const string PipelineTaskType = "cicd.pipeline.task.type";

    public static class PipelineTaskTypeValues
    {
        public const string Build = "build";

        public const string Deploy = "deploy";

        public const string Test = "test";
    }

    public const string SystemComponent = "cicd.system.component";

    public const string WorkerId = "cicd.worker.id";

    public const string WorkerName = "cicd.worker.name";

    public const string WorkerState = "cicd.worker.state";

    public static class WorkerStateValues
    {
        public const string Available = "available";

        public const string Busy = "busy";

        public const string Offline = "offline";
    }

    public const string WorkerUrlFull = "cicd.worker.url.full";
}
