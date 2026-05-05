

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Messaging;

public static class MessagingAttributes
{
    public const string BatchMessageCount = "messaging.batch.message_count";

    public const string ClientId = "messaging.client.id";

    public const string ConsumerGroupName = "messaging.consumer.group.name";

    public const string DestinationAnonymous = "messaging.destination.anonymous";

    public const string DestinationName = "messaging.destination.name";

    public const string DestinationPartitionId = "messaging.destination.partition.id";

    public const string DestinationSubscriptionName = "messaging.destination.subscription.name";

    public const string DestinationTemplate = "messaging.destination.template";

    public const string DestinationTemporary = "messaging.destination.temporary";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string DestinationPublishAnonymous = "messaging.destination_publish.anonymous";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string DestinationPublishName = "messaging.destination_publish.name";

    [global::System.Obsolete("Replaced by messaging.consumer.group.name.", false)]
    public const string EventhubsConsumerGroup = "messaging.eventhubs.consumer.group";

    public const string EventhubsMessageEnqueuedTime = "messaging.eventhubs.message.enqueued_time";

    public const string GcpPubsubMessageAckDeadline = "messaging.gcp_pubsub.message.ack_deadline";

    public const string GcpPubsubMessageAckId = "messaging.gcp_pubsub.message.ack_id";

    public const string GcpPubsubMessageDeliveryAttempt = "messaging.gcp_pubsub.message.delivery_attempt";

    public const string GcpPubsubMessageOrderingKey = "messaging.gcp_pubsub.message.ordering_key";

    [global::System.Obsolete("Replaced by messaging.consumer.group.name.", false)]
    public const string KafkaConsumerGroup = "messaging.kafka.consumer.group";

    [global::System.Obsolete("Record string representation of the partition id in `messaging.destination.partition.id` attribute.", false)]
    public const string KafkaDestinationPartition = "messaging.kafka.destination.partition";

    public const string KafkaMessageKey = "messaging.kafka.message.key";

    [global::System.Obsolete("Replaced by messaging.kafka.offset.", false)]
    public const string KafkaMessageOffset = "messaging.kafka.message.offset";

    public const string KafkaMessageTombstone = "messaging.kafka.message.tombstone";

    public const string KafkaOffset = "messaging.kafka.offset";

    public const string MessageBodySize = "messaging.message.body.size";

    public const string MessageConversationId = "messaging.message.conversation_id";

    public const string MessageEnvelopeSize = "messaging.message.envelope.size";

    public const string MessageId = "messaging.message.id";

    [global::System.Obsolete("Replaced by messaging.operation.type.", false)]
    public const string Operation = "messaging.operation";

    public const string OperationName = "messaging.operation.name";

    public const string OperationType = "messaging.operation.type";

    public static class OperationTypeValues
    {
        public const string Create = "create";

        [global::System.Obsolete("{\"note\": \"Replaced by `process`.\", \"reason\": \"renamed\", \"renamed_to\": \"process\"}", false)]
        public const string Deliver = "deliver";

        public const string Process = "process";

        [global::System.Obsolete("{\"note\": \"Replaced by `send`.\", \"reason\": \"renamed\", \"renamed_to\": \"send\"}", false)]
        public const string Publish = "publish";

        public const string Receive = "receive";

        public const string Send = "send";

        public const string Settle = "settle";
    }

    public const string RabbitmqDestinationRoutingKey = "messaging.rabbitmq.destination.routing_key";

    public const string RabbitmqMessageDeliveryTag = "messaging.rabbitmq.message.delivery_tag";

    [global::System.Obsolete("Replaced by `messaging.consumer.group.name` on the consumer spans. No replacement for producer spans.", false)]
    public const string RocketmqClientGroup = "messaging.rocketmq.client_group";

    public const string RocketmqConsumptionModel = "messaging.rocketmq.consumption_model";

    public static class RocketmqConsumptionModelValues
    {
        public const string Broadcasting = "broadcasting";

        public const string Clustering = "clustering";
    }

    public const string RocketmqMessageDelayTimeLevel = "messaging.rocketmq.message.delay_time_level";

    public const string RocketmqMessageDeliveryTimestamp = "messaging.rocketmq.message.delivery_timestamp";

    public const string RocketmqMessageGroup = "messaging.rocketmq.message.group";

    public const string RocketmqMessageKeys = "messaging.rocketmq.message.keys";

    public const string RocketmqMessageTag = "messaging.rocketmq.message.tag";

    public const string RocketmqMessageType = "messaging.rocketmq.message.type";

    public static class RocketmqMessageTypeValues
    {
        public const string Delay = "delay";

        public const string Fifo = "fifo";

        public const string Normal = "normal";

        public const string Transaction = "transaction";
    }

    public const string RocketmqNamespace = "messaging.rocketmq.namespace";

    [global::System.Obsolete("Replaced by messaging.destination.subscription.name.", false)]
    public const string ServicebusDestinationSubscriptionName = "messaging.servicebus.destination.subscription_name";

    public const string ServicebusDispositionStatus = "messaging.servicebus.disposition_status";

    public static class ServicebusDispositionStatusValues
    {
        public const string Abandon = "abandon";

        public const string Complete = "complete";

        public const string DeadLetter = "dead_letter";

        public const string Defer = "defer";
    }

    public const string ServicebusMessageDeliveryCount = "messaging.servicebus.message.delivery_count";

    public const string ServicebusMessageEnqueuedTime = "messaging.servicebus.message.enqueued_time";

    public const string System = "messaging.system";

    public static class SystemValues
    {
        public const string Activemq = "activemq";

        public const string AwsSns = "aws.sns";

        public const string AwsSqs = "aws_sqs";

        public const string Eventgrid = "eventgrid";

        public const string Eventhubs = "eventhubs";

        public const string GcpPubsub = "gcp_pubsub";

        public const string Jms = "jms";

        public const string Kafka = "kafka";

        public const string Pulsar = "pulsar";

        public const string Rabbitmq = "rabbitmq";

        public const string Rocketmq = "rocketmq";

        public const string Servicebus = "servicebus";
    }
}
