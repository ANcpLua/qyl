

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Faas;

public static class FaasAttributes
{
    public const string Coldstart = "faas.coldstart";

    public const string Cron = "faas.cron";

    public const string DocumentCollection = "faas.document.collection";

    public const string DocumentName = "faas.document.name";

    public const string DocumentOperation = "faas.document.operation";

    public static class DocumentOperationValues
    {
        public const string Delete = "delete";

        public const string Edit = "edit";

        public const string Insert = "insert";
    }

    public const string DocumentTime = "faas.document.time";

    public const string Instance = "faas.instance";

    public const string InvocationId = "faas.invocation_id";

    public const string InvokedName = "faas.invoked_name";

    public const string InvokedProvider = "faas.invoked_provider";

    public static class InvokedProviderValues
    {
        public const string AlibabaCloud = "alibaba_cloud";

        public const string Aws = "aws";

        public const string Azure = "azure";

        public const string Gcp = "gcp";

        public const string TencentCloud = "tencent_cloud";
    }

    public const string InvokedRegion = "faas.invoked_region";

    public const string MaxMemory = "faas.max_memory";

    public const string Name = "faas.name";

    public const string Time = "faas.time";

    public const string Trigger = "faas.trigger";

    public static class TriggerValues
    {
        public const string Datasource = "datasource";

        public const string Http = "http";

        public const string Other = "other";

        public const string Pubsub = "pubsub";

        public const string Timer = "timer";
    }

    public const string Version = "faas.version";
}
