// =============================================================================
// QYL v2.0 - Messaging Semantic Conventions (OTel v1.38)
// =============================================================================
// Message broker and queue telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Transport.Messaging;

// =============================================================================
// Messaging Span Attributes
// =============================================================================

@doc("Messaging span attributes")
model MessagingAttributes {
  @doc("The messaging system (e.g., kafka, rabbitmq, aws_sqs)")
  @encodedName("application/json", "messaging.system")
  system: MessagingSystem;

  @doc("Message destination name (queue or topic)")
  @encodedName("application/json", "messaging.destination.name")
  destinationName?: string;

  @doc("Destination kind (queue or topic)")
  @encodedName("application/json", "messaging.destination.kind")
  destinationKind?: MessagingDestinationKind;

  @doc("Whether the destination is temporary")
  @encodedName("application/json", "messaging.destination.temporary")
  destinationTemporary?: boolean;

  @doc("Whether the destination is anonymous")
  @encodedName("application/json", "messaging.destination.anonymous")
  destinationAnonymous?: boolean;

  @doc("Partition number (for partitioned systems)")
  @encodedName("application/json", "messaging.destination.partition.id")
  destinationPartitionId?: string;

  @doc("Destination template (e.g., for subscription patterns)")
  @encodedName("application/json", "messaging.destination.template")
  destinationTemplate?: string;

  @doc("Message ID")
  @encodedName("application/json", "messaging.message.id")
  messageId?: string;

  @doc("Conversation/correlation ID")
  @encodedName("application/json", "messaging.message.conversation_id")
  messageConversationId?: string;

  @doc("Message body size in bytes")
  @encodedName("application/json", "messaging.message.body.size")
  messageBodySize?: ByteSize;

  @doc("Total message size including metadata")
  @encodedName("application/json", "messaging.message.envelope.size")
  messageEnvelopeSize?: ByteSize;

  @doc("Messaging operation type")
  @encodedName("application/json", "messaging.operation.type")
  operationType?: MessagingOperationType;

  @doc("Operation name")
  @encodedName("application/json", "messaging.operation.name")
  operationName?: string;

  @doc("Batch message count")
  @encodedName("application/json", "messaging.batch.message_count")
  batchMessageCount?: int32;

  @doc("Consumer group name")
  @encodedName("application/json", "messaging.consumer.group.name")
  consumerGroupName?: string;

  @doc("Consumer ID")
  @encodedName("application/json", "messaging.client.id")
  clientId?: string;

  // ---------------------------------------------------------------------------
  // Kafka-Specific Attributes
  // ---------------------------------------------------------------------------

  @doc("Kafka consumer group")
  @encodedName("application/json", "messaging.kafka.consumer.group")
  kafkaConsumerGroup?: string;

  @doc("Kafka message key")
  @encodedName("application/json", "messaging.kafka.message.key")
  kafkaMessageKey?: string;

  @doc("Kafka message offset")
  @encodedName("application/json", "messaging.kafka.message.offset")
  kafkaMessageOffset?: int64;

  @doc("Kafka message tombstone")
  @encodedName("application/json", "messaging.kafka.message.tombstone")
  kafkaMessageTombstone?: boolean;

  // ---------------------------------------------------------------------------
  // RabbitMQ-Specific Attributes
  // ---------------------------------------------------------------------------

  @doc("RabbitMQ delivery tag")
  @encodedName("application/json", "messaging.rabbitmq.delivery_tag")
  rabbitmqDeliveryTag?: int64;

  @doc("RabbitMQ routing key")
  @encodedName("application/json", "messaging.rabbitmq.destination.routing_key")
  rabbitmqRoutingKey?: string;

  // ---------------------------------------------------------------------------
  // AWS SQS/SNS-Specific Attributes
  // ---------------------------------------------------------------------------

  @doc("AWS SQS message group ID")
  @encodedName("application/json", "messaging.aws_sqs.message.group_id")
  awsSqsMessageGroupId?: string;

  @doc("AWS SQS message deduplication ID")
  @encodedName("application/json", "messaging.aws_sqs.message.deduplication_id")
  awsSqsMessageDeduplicationId?: string;

  // ---------------------------------------------------------------------------
  // Azure Service Bus-Specific Attributes
  // ---------------------------------------------------------------------------

  @doc("Azure Service Bus message enqueued time")
  @encodedName("application/json", "messaging.servicebus.message.enqueued_time")
  servicebusEnqueuedTime?: utcDateTime;
}

// =============================================================================
// Messaging Enumerations
// =============================================================================

@doc("Messaging systems")
enum MessagingSystem {
  @doc("Apache Kafka")
  kafka: "kafka",

  @doc("RabbitMQ")
  rabbitmq: "rabbitmq",

  @doc("Amazon SQS")
  awsSqs: "aws_sqs",

  @doc("Amazon SNS")
  awsSns: "aws_sns",

  @doc("Azure Service Bus")
  azureServiceBus: "azure_servicebus",

  @doc("Azure Event Hubs")
  azureEventHubs: "azure_eventhubs",

  @doc("Google Cloud Pub/Sub")
  gcpPubsub: "gcp_pubsub",

  @doc("Apache ActiveMQ")
  activemq: "activemq",

  @doc("Apache ActiveMQ Artemis")
  activemqArtemis: "activemq_artemis",

  @doc("Redis Pub/Sub")
  redisPubsub: "redis_pubsub",

  @doc("Redis Streams")
  redisStreams: "redis_streams",

  @doc("NATS")
  nats: "nats",

  @doc("Pulsar")
  pulsar: "pulsar",

  @doc("JMS")
  jms: "jms",

  @doc("IBM MQ")
  ibmmq: "ibmmq",

  @doc("RocketMQ")
  rocketmq: "rocketmq",
}

@doc("Messaging destination kinds")
enum MessagingDestinationKind {
  @doc("Queue (point-to-point)")
  queue: "queue",

  @doc("Topic (publish-subscribe)")
  topic: "topic",
}

@doc("Messaging operation types")
enum MessagingOperationType {
  @doc("Message publish/send")
  publish: "publish",

  @doc("Message receive (non-blocking)")
  receive: "receive",

  @doc("Message process (blocking consumer)")
  process: "process",

  @doc("Create destination")
  create: "create",

  @doc("Settle/acknowledge message")
  settle: "settle",
}

@doc("Message delivery guarantees")
enum MessagingDeliveryGuarantee {
  @doc("At most once delivery")
  atMostOnce: "at_most_once",

  @doc("At least once delivery")
  atLeastOnce: "at_least_once",

  @doc("Exactly once delivery")
  exactlyOnce: "exactly_once",
}

// =============================================================================
// Messaging Metrics
// =============================================================================

@doc("Messaging publish duration metric")
model MessagingPublishDurationMetric {
  @doc("Metric name")
  name: "messaging.publish.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Messaging system")
  @encodedName("application/json", "messaging.system")
  system: MessagingSystem;

  @doc("Destination name")
  @encodedName("application/json", "messaging.destination.name")
  destinationName: string;
}

@doc("Messaging receive duration metric")
model MessagingReceiveDurationMetric {
  @doc("Metric name")
  name: "messaging.receive.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Messaging system")
  @encodedName("application/json", "messaging.system")
  system: MessagingSystem;

  @doc("Destination name")
  @encodedName("application/json", "messaging.destination.name")
  destinationName: string;

  @doc("Consumer group")
  @encodedName("application/json", "messaging.consumer.group.name")
  consumerGroupName?: string;
}

@doc("Messaging process duration metric")
model MessagingProcessDurationMetric {
  @doc("Metric name")
  name: "messaging.process.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Messaging system")
  @encodedName("application/json", "messaging.system")
  system: MessagingSystem;

  @doc("Destination name")
  @encodedName("application/json", "messaging.destination.name")
  destinationName: string;
}

@doc("Messaging publish messages metric")
model MessagingPublishMessagesMetric {
  @doc("Metric name")
  name: "messaging.publish.messages";

  @doc("Metric unit")
  unit: "{message}";

  @doc("Messaging system")
  @encodedName("application/json", "messaging.system")
  system: MessagingSystem;

  @doc("Destination name")
  @encodedName("application/json", "messaging.destination.name")
  destinationName: string;
}
