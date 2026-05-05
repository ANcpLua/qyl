

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Aws;

public static class AwsAttributes
{
    public const string BedrockGuardrailId = "aws.bedrock.guardrail.id";

    public const string BedrockKnowledgeBaseId = "aws.bedrock.knowledge_base.id";

    public const string DynamodbAttributeDefinitions = "aws.dynamodb.attribute_definitions";

    public const string DynamodbAttributesToGet = "aws.dynamodb.attributes_to_get";

    public const string DynamodbConsistentRead = "aws.dynamodb.consistent_read";

    public const string DynamodbConsumedCapacity = "aws.dynamodb.consumed_capacity";

    public const string DynamodbCount = "aws.dynamodb.count";

    public const string DynamodbExclusiveStartTable = "aws.dynamodb.exclusive_start_table";

    public const string DynamodbGlobalSecondaryIndexUpdates = "aws.dynamodb.global_secondary_index_updates";

    public const string DynamodbGlobalSecondaryIndexes = "aws.dynamodb.global_secondary_indexes";

    public const string DynamodbIndexName = "aws.dynamodb.index_name";

    public const string DynamodbItemCollectionMetrics = "aws.dynamodb.item_collection_metrics";

    public const string DynamodbLimit = "aws.dynamodb.limit";

    public const string DynamodbLocalSecondaryIndexes = "aws.dynamodb.local_secondary_indexes";

    public const string DynamodbProjection = "aws.dynamodb.projection";

    public const string DynamodbProvisionedReadCapacity = "aws.dynamodb.provisioned_read_capacity";

    public const string DynamodbProvisionedWriteCapacity = "aws.dynamodb.provisioned_write_capacity";

    public const string DynamodbScanForward = "aws.dynamodb.scan_forward";

    public const string DynamodbScannedCount = "aws.dynamodb.scanned_count";

    public const string DynamodbSegment = "aws.dynamodb.segment";

    public const string DynamodbSelect = "aws.dynamodb.select";

    public const string DynamodbTableCount = "aws.dynamodb.table_count";

    public const string DynamodbTableNames = "aws.dynamodb.table_names";

    public const string DynamodbTotalSegments = "aws.dynamodb.total_segments";

    public const string EcsClusterArn = "aws.ecs.cluster.arn";

    public const string EcsContainerArn = "aws.ecs.container.arn";

    public const string EcsLaunchtype = "aws.ecs.launchtype";

    public static class EcsLaunchtypeValues
    {
        public const string Ec2 = "ec2";

        public const string Fargate = "fargate";
    }

    public const string EcsTaskArn = "aws.ecs.task.arn";

    public const string EcsTaskFamily = "aws.ecs.task.family";

    public const string EcsTaskId = "aws.ecs.task.id";

    public const string EcsTaskRevision = "aws.ecs.task.revision";

    public const string EksClusterArn = "aws.eks.cluster.arn";

    public const string ExtendedRequestId = "aws.extended_request_id";

    public const string KinesisStreamName = "aws.kinesis.stream_name";

    public const string LambdaInvokedArn = "aws.lambda.invoked_arn";

    public const string LambdaResourceMappingId = "aws.lambda.resource_mapping.id";

    public const string LogGroupArns = "aws.log.group.arns";

    public const string LogGroupNames = "aws.log.group.names";

    public const string LogStreamArns = "aws.log.stream.arns";

    public const string LogStreamNames = "aws.log.stream.names";

    public const string RequestId = "aws.request_id";

    public const string S3Bucket = "aws.s3.bucket";

    public const string S3CopySource = "aws.s3.copy_source";

    public const string S3Delete = "aws.s3.delete";

    public const string S3Key = "aws.s3.key";

    public const string S3PartNumber = "aws.s3.part_number";

    public const string S3UploadId = "aws.s3.upload_id";

    public const string SecretsmanagerSecretArn = "aws.secretsmanager.secret.arn";

    public const string SnsTopicArn = "aws.sns.topic.arn";

    public const string SqsQueueUrl = "aws.sqs.queue.url";

    public const string StepFunctionsActivityArn = "aws.step_functions.activity.arn";

    public const string StepFunctionsStateMachineArn = "aws.step_functions.state_machine.arn";
}
