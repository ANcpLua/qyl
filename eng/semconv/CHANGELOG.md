# Semantic Conventions Schema Changelog

Source: [open-telemetry/semantic-conventions/schemas](https://github.com/open-telemetry/semantic-conventions/tree/main/schemas)

Cumulative rename chain from v1.4.0 to v1.40.0. Each entry shows what was renamed in that version.
Only the **right-hand side** (target) names are valid in v1.40.0.

---

## v1.8.0

| Old | v1.40.0 Name |
|-----|-------------|
| `db.cassandra.keyspace` | `db.namespace` (via `db.name` in v1.8 then renamed again in v1.25) |
| `db.hbase.namespace` | `db.namespace` (via `db.name` in v1.8 then renamed again in v1.25) |

## v1.13.0

| Old | v1.40.0 Name |
|-----|-------------|
| `net.host.ip` | `server.socket.address` (via `net.sock.host.addr` in v1.13 then renamed in v1.21) |
| `net.peer.ip` | `net.sock.peer.addr` |

## v1.15.0

| Old | v1.40.0 Name |
|-----|-------------|
| `http.retry_count` | `http.request.resend_count` (via `http.resend_count` in v1.15 then renamed in v1.22) |

## v1.17.0

| Old | v1.40.0 Name |
|-----|-------------|
| `messaging.consumer_id` | `messaging.consumer.id` |
| `messaging.conversation_id` | `messaging.message.conversation_id` |
| `messaging.destination` | `messaging.destination.name` |
| `messaging.destination_kind` | `messaging.destination.kind` |
| `messaging.kafka.consumer_group` | `messaging.consumer.group.name` (via `messaging.kafka.consumer.group` in v1.17 then renamed in v1.27) |
| `messaging.kafka.message_key` | `messaging.kafka.message.key` |
| `messaging.kafka.partition` | `messaging.destination.partition.id` (via `messaging.kafka.destination.partition` in v1.17 then renamed in v1.25) |
| `messaging.kafka.tombstone` | `messaging.kafka.message.tombstone` |
| `messaging.message_id` | `messaging.message.id` |
| `messaging.message_payload_compressed_size_bytes` | `messaging.message.payload_compressed_size_bytes` |
| `messaging.message_payload_size_bytes` | `messaging.message.body.size` (via `messaging.message.payload_size_bytes` in v1.17 then renamed in v1.22) |
| `messaging.protocol` | `network.protocol.name` (via `net.app.protocol.name` in v1.17 -> `net.protocol.name` in v1.20 -> `network.protocol.name` in v1.21) |
| `messaging.protocol_version` | `network.protocol.version` (via `net.app.protocol.version` in v1.17 -> `net.protocol.version` in v1.20 -> `network.protocol.version` in v1.21) |
| `messaging.rabbitmq.routing_key` | `messaging.rabbitmq.destination.routing_key` |
| `messaging.rocketmq.message_keys` | `messaging.rocketmq.message.keys` |
| `messaging.rocketmq.message_tag` | `messaging.rocketmq.message.tag` |
| `messaging.rocketmq.message_type` | `messaging.rocketmq.message.type` |
| `messaging.temp_destination` | `messaging.destination.temporary` |

## v1.19.0

| Old | v1.40.0 Name |
|-----|-------------|
| `browser.user_agent` | `user_agent.original` |
| `faas.execution` | `faas.invocation_id` |
| `faas.id` | `cloud.resource_id` |
| `http.user_agent` | `user_agent.original` |

## v1.20.0

| Old | v1.40.0 Name |
|-----|-------------|
| `net.app.protocol.name` | `network.protocol.name` (via `net.protocol.name` in v1.20 then renamed in v1.21) |
| `net.app.protocol.version` | `network.protocol.version` (via `net.protocol.version` in v1.20 then renamed in v1.21) |

## v1.21.0

The great HTTP/network rename.

| Old | v1.40.0 Name |
|-----|-------------|
| `http.client_ip` | `client.address` |
| `http.method` | `http.request.method` |
| `http.request_content_length` | `http.request.body.size` |
| `http.response_content_length` | `http.response.body.size` |
| `http.scheme` | `url.scheme` |
| `http.status_code` | `http.response.status_code` |
| `http.url` | `url.full` |
| `messaging.kafka.client_id` | `messaging.client.id` (via `messaging.client_id` in v1.21 then renamed in v1.26) |
| `messaging.rocketmq.client_id` | `messaging.client.id` (via `messaging.client_id` in v1.21 then renamed in v1.26) |
| `net.host.carrier.icc` | `network.carrier.icc` |
| `net.host.carrier.mcc` | `network.carrier.mcc` |
| `net.host.carrier.mnc` | `network.carrier.mnc` |
| `net.host.carrier.name` | `network.carrier.name` |
| `net.host.connection.subtype` | `network.connection.subtype` |
| `net.host.connection.type` | `network.connection.type` |
| `net.host.name` | `server.address` |
| `net.host.port` | `server.port` |
| `net.protocol.name` | `network.protocol.name` |
| `net.protocol.version` | `network.protocol.version` |
| `net.sock.host.addr` | `server.socket.address` |
| `net.sock.host.port` | `server.socket.port` |
| `net.sock.peer.name` | `server.socket.domain` |

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `process.runtime.jvm.cpu.utilization` | `jvm.cpu.recent_utilization` (via `process.runtime.jvm.cpu.recent_utilization` in v1.21 then renamed in v1.22) |

## v1.22.0

The JVM and system restructure.

**Attribute renames (27):**

| Old | v1.40.0 Name |
|-----|-------------|
| `http.resend_count` | `http.request.resend_count` |
| `messaging.message.payload_size_bytes` | `messaging.message.body.size` |
| `telemetry.auto.version` | `telemetry.distro.version` |

Plus 24 context-dependent renames (`state`, `type`, `direction`, `device`, `pool`, `name`, `action`, etc.) that now carry their domain prefix (e.g. `state` -> `system.cpu.state` -> `cpu.mode` in v1.27).

**Metric renames (21):**

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `http.client.duration` | `http.client.request.duration` |
| `http.server.duration` | `http.server.request.duration` |
| `http.server.request.size` | `http.server.request.body.size` |
| `http.server.response.size` | `http.server.response.body.size` |
| `process.runtime.jvm.buffer.count` | `jvm.buffer.count` |
| `process.runtime.jvm.buffer.limit` | `jvm.buffer.memory.limit` |
| `process.runtime.jvm.buffer.usage` | `jvm.buffer.memory.used` (via `jvm.buffer.memory.usage` in v1.22 then renamed in v1.27) |
| `process.runtime.jvm.classes.current_loaded` | `jvm.class.count` |
| `process.runtime.jvm.classes.loaded` | `jvm.class.loaded` |
| `process.runtime.jvm.classes.unloaded` | `jvm.class.unloaded` |
| `process.runtime.jvm.cpu.recent_utilization` | `jvm.cpu.recent_utilization` |
| `process.runtime.jvm.cpu.time` | `jvm.cpu.time` |
| `process.runtime.jvm.gc.duration` | `jvm.gc.duration` |
| `process.runtime.jvm.memory.committed` | `jvm.memory.committed` |
| `process.runtime.jvm.memory.init` | `jvm.memory.init` |
| `process.runtime.jvm.memory.limit` | `jvm.memory.limit` |
| `process.runtime.jvm.memory.usage` | `jvm.memory.used` (via `jvm.memory.usage` in v1.22 then renamed in v1.24) |
| `process.runtime.jvm.memory.usage_after_last_gc` | `jvm.memory.used_after_last_gc` (via `jvm.memory.usage_after_last_gc` in v1.22 then renamed in v1.24) |
| `process.runtime.jvm.system.cpu.load_1m` | `jvm.system.cpu.load_1m` |
| `process.runtime.jvm.system.cpu.utilization` | `jvm.system.cpu.utilization` |
| `process.runtime.jvm.threads.count` | `jvm.thread.count` |

## v1.23.0

| Old | v1.40.0 Name |
|-----|-------------|
| `thread.daemon` | `jvm.thread.daemon` |

## v1.24.0

| Old | v1.40.0 Name |
|-----|-------------|
| `system.disk.io.direction` | `disk.io.direction` |
| `system.network.io.direction` | `network.io.direction` |

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `jvm.memory.usage` | `jvm.memory.used` |
| `jvm.memory.usage_after_last_gc` | `jvm.memory.used_after_last_gc` |

## v1.25.0

| Old | v1.40.0 Name |
|-----|-------------|
| `container.labels` | `container.label` |
| `db.cassandra.table` | `db.collection.name` |
| `db.cosmosdb.container` | `db.collection.name` |
| `db.mongodb.collection` | `db.collection.name` |
| `db.name` | `db.namespace` |
| `db.operation` | `db.operation.name` |
| `db.sql.table` | `db.collection.name` |
| `db.statement` | `db.query.text` |
| `k8s.pod.labels` | `k8s.pod.label` |
| `messaging.kafka.destination.partition` | `messaging.destination.partition.id` |
| `messaging.operation` | `messaging.operation.type` |

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `process.open_file_descriptors` | `process.unix.file_descriptor.count` (via `process.open_file_descriptor.count` in v1.25 then renamed in v1.39) |
| `process.threads` | `process.thread.count` |
| `system.processes.count` | `system.process.count` |
| `system.processes.created` | `system.process.created` |

## v1.26.0

| Old | v1.40.0 Name |
|-----|-------------|
| `enduser.id` | `user.id` |
| `messaging.client_id` | `messaging.client.id` |
| `pool.name` | `db.client.connection.pool.name` (via `db.client.connections.pool.name` in v1.26 then renamed in v1.27) |
| `state` | `db.client.connection.state` (via `db.client.connections.state` in v1.26 then renamed in v1.27) |

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `db.client.connections.idle.max` | `db.client.connection.idle.max` |
| `db.client.connections.idle.min` | `db.client.connection.idle.min` |
| `db.client.connections.max` | `db.client.connection.max` |
| `db.client.connections.pending_requests` | `db.client.connection.pending_requests` |
| `db.client.connections.timeouts` | `db.client.connection.timeouts` |
| `db.client.connections.usage` | `db.client.connection.count` |

## v1.27.0

| Old | v1.40.0 Name |
|-----|-------------|
| `container.cpu.state` | `cpu.mode` |
| `db.client.connections.pool.name` | `db.client.connection.pool.name` |
| `db.client.connections.state` | `db.client.connection.state` |
| `db.elasticsearch.cluster.name` | `db.namespace` |
| `deployment.environment` | `deployment.environment.name` |
| `gen_ai.usage.completion_tokens` | `gen_ai.usage.output_tokens` |
| `gen_ai.usage.prompt_tokens` | `gen_ai.usage.input_tokens` |
| `messaging.eventhubs.consumer.group` | `messaging.consumer.group.name` |
| `messaging.kafka.consumer.group` | `messaging.consumer.group.name` |
| `messaging.kafka.message.offset` | `messaging.kafka.offset` |
| `messaging.rocketmq.client_group` | `messaging.consumer.group.name` |
| `messaging.servicebus.destination.subscription_name` | `messaging.destination.subscription.name` |
| `process.cpu.state` | `cpu.mode` |
| `system.cpu.state` | `cpu.mode` |
| `tls.client.server_name` | `server.address` |

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `jvm.buffer.memory.usage` | `jvm.buffer.memory.used` |
| `messaging.publish.messages` | `messaging.client.sent.messages` (via `messaging.client.published.messages` in v1.27 then renamed in v1.28) |

## v1.28.0

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `messaging.client.published.messages` | `messaging.client.sent.messages` |

## v1.29.0

| Old | v1.40.0 Name |
|-----|-------------|
| `process.executable.build_id.profiling` | `process.executable.build_id.htlhash` |
| `system.device` | `network.interface.name` |
| `vcs.repository.change.id` | `vcs.change.id` |
| `vcs.repository.change.title` | `vcs.change.title` |
| `vcs.repository.ref.name` | `vcs.ref.head.name` |
| `vcs.repository.ref.revision` | `vcs.ref.head.revision` |
| `vcs.repository.ref.type` | `vcs.ref.head.type` |

## v1.30.0

| Old | v1.40.0 Name |
|-----|-------------|
| `code.column` | `code.column.number` |
| `code.filepath` | `code.file.path` |
| `code.function` | `code.function.name` |
| `code.lineno` | `code.line.number` |
| `db.cassandra.consistency_level` | `cassandra.consistency.level` |
| `db.cassandra.coordinator.dc` | `cassandra.coordinator.dc` |
| `db.cassandra.coordinator.id` | `cassandra.coordinator.id` |
| `db.cassandra.idempotence` | `cassandra.query.idempotent` |
| `db.cassandra.page_size` | `cassandra.page.size` |
| `db.cassandra.speculative_execution_count` | `cassandra.speculative_execution.count` |
| `db.cosmosdb.client_id` | `azure.client.id` |
| `db.cosmosdb.connection_mode` | `azure.cosmosdb.connection.mode` |
| `db.cosmosdb.consistency_level` | `azure.cosmosdb.consistency.level` |
| `db.cosmosdb.regions_contacted` | `azure.cosmosdb.operation.contacted_regions` |
| `db.cosmosdb.request_charge` | `azure.cosmosdb.operation.request_charge` |
| `db.cosmosdb.request_content_length` | `azure.cosmosdb.request.body.size` |
| `db.cosmosdb.sub_status_code` | `azure.cosmosdb.response.sub_status_code` |
| `db.elasticsearch.node.name` | `elasticsearch.node.name` |
| `db.system` | `db.system.name` |
| `gen_ai.openai.request.seed` | `gen_ai.request.seed` |
| `system.network.state` | `network.connection.state` |

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `db.client.cosmosdb.active_instance.count` | `azure.cosmosdb.client.active_instance.count` |
| `db.client.cosmosdb.operation.request_charge` | `azure.cosmosdb.client.operation.request_charge` |

## v1.31.0

| Old | v1.40.0 Name |
|-----|-------------|
| `android.state` | `android.app.state` |
| `io.state` | `ios.app.state` |
| `system.cpu.logical_number` | `cpu.logical_number` |

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `k8s.replication_controller.available_pods` | `k8s.replicationcontroller.pod.available` (renamed again in v1.38) |
| `k8s.replication_controller.desired_pods` | `k8s.replicationcontroller.pod.desired` (renamed again in v1.38) |
| `system.cpu.frequency` | `system.cpu.frequency` (renamed to `cpu.frequency` in v1.31, renamed back in v1.34) |
| `system.cpu.time` | `system.cpu.time` (renamed to `cpu.time` in v1.31, renamed back in v1.34) |
| `system.cpu.utilization` | `system.cpu.utilization` (renamed to `cpu.utilization` in v1.31, renamed back in v1.34) |

## v1.32.0

| Old | v1.40.0 Name |
|-----|-------------|
| `feature_flag.evaluation.reason` | `feature_flag.result.reason` |
| `feature_flag.variant` | `feature_flag.result.variant` |

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `otel.sdk.exporter.span.exported.count` | `otel.sdk.exporter.span.exported` |
| `otel.sdk.exporter.span.inflight.count` | `otel.sdk.exporter.span.inflight` |
| `otel.sdk.processor.span.processed.count` | `otel.sdk.processor.span.processed` |
| `otel.sdk.span.ended.count` | `otel.sdk.span.ended` |
| `otel.sdk.span.live.count` | `otel.sdk.span.live` |

## v1.33.0

| Old | v1.40.0 Name |
|-----|-------------|
| `feature_flag.evaluation.error.message` | `feature_flag.error.message` (renamed to `error.message` in v1.33, then to `feature_flag.error.message` in v1.40) |
| `feature_flag.provider_name` | `feature_flag.provider.name` |

## v1.34.0

Reverted the v1.31 cpu metric renames.

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `cpu.frequency` | `system.cpu.frequency` |
| `cpu.time` | `system.cpu.time` |
| `cpu.utilization` | `system.cpu.utilization` |

## v1.35.0

| Old | v1.40.0 Name |
|-----|-------------|
| `az.namespace` | `azure.resource_provider.namespace` |
| `az.service_request_id` | `azure.service.request.id` |

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `system.network.connections` | `system.network.connection.count` |

## v1.37.0

| Old | v1.40.0 Name |
|-----|-------------|
| `container.runtime` | `container.runtime.name` |
| `enduser.role` | `user.roles` |
| `gen_ai.openai.request.service_tier` | `openai.request.service_tier` |
| `gen_ai.openai.response.service_tier` | `openai.response.service_tier` |
| `gen_ai.openai.response.system_fingerprint` | `openai.response.system_fingerprint` |
| `gen_ai.system` | `gen_ai.provider.name` |

## v1.38.0

| Old | v1.40.0 Name |
|-----|-------------|
| `process.context_switch_type` | `process.context_switch.type` |
| `process.paging.fault_type` | `system.paging.fault.type` |
| `system.paging.type` | `system.paging.fault.type` |
| `system.process.status` | `process.state` |
| `system.processes.status` | `process.state` |

**Metric renames (32)** — the k8s restructure:

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `k8s.cronjob.active_jobs` | `k8s.cronjob.job.active` |
| `k8s.daemonset.current_scheduled_nodes` | `k8s.daemonset.node.current_scheduled` |
| `k8s.daemonset.desired_scheduled_nodes` | `k8s.daemonset.node.desired_scheduled` |
| `k8s.daemonset.misscheduled_nodes` | `k8s.daemonset.node.misscheduled` |
| `k8s.daemonset.ready_nodes` | `k8s.daemonset.node.ready` |
| `k8s.deployment.available_pods` | `k8s.deployment.pod.available` |
| `k8s.deployment.desired_pods` | `k8s.deployment.pod.desired` |
| `k8s.hpa.current_pods` | `k8s.hpa.pod.current` |
| `k8s.hpa.desired_pods` | `k8s.hpa.pod.desired` |
| `k8s.hpa.max_pods` | `k8s.hpa.pod.max` |
| `k8s.hpa.min_pods` | `k8s.hpa.pod.min` |
| `k8s.job.active_pods` | `k8s.job.pod.active` |
| `k8s.job.desired_successful_pods` | `k8s.job.pod.desired_successful` |
| `k8s.job.failed_pods` | `k8s.job.pod.failed` |
| `k8s.job.max_parallel_pods` | `k8s.job.pod.max_parallel` |
| `k8s.job.successful_pods` | `k8s.job.pod.successful` |
| `k8s.node.allocatable.cpu` | `k8s.node.cpu.allocatable` |
| `k8s.node.allocatable.ephemeral_storage` | `k8s.node.ephemeral_storage.allocatable` |
| `k8s.node.allocatable.memory` | `k8s.node.memory.allocatable` |
| `k8s.node.allocatable.pods` | `k8s.node.pod.allocatable` |
| `k8s.replicaset.available_pods` | `k8s.replicaset.pod.available` |
| `k8s.replicaset.desired_pods` | `k8s.replicaset.pod.desired` |
| `k8s.replicationcontroller.available_pods` | `k8s.replicationcontroller.pod.available` |
| `k8s.replicationcontroller.desired_pods` | `k8s.replicationcontroller.pod.desired` |
| `k8s.statefulset.current_pods` | `k8s.statefulset.pod.current` |
| `k8s.statefulset.desired_pods` | `k8s.statefulset.pod.desired` |
| `k8s.statefulset.ready_pods` | `k8s.statefulset.pod.ready` |
| `k8s.statefulset.updated_pods` | `k8s.statefulset.pod.updated` |
| `v8js.heap.space.available_size` | `v8js.memory.heap.space.available_size` |
| `v8js.heap.space.physical_size` | `v8js.memory.heap.space.physical_size` |

## v1.39.0

| Old | v1.40.0 Name |
|-----|-------------|
| `linux.memory.slab.state` | `system.memory.linux.slab.state` |
| `peer.service` | `service.peer.name` |
| `rpc.connect_rpc.error_code` | `rpc.response.status_code` |
| `rpc.connect_rpc.request.metadata` | `rpc.request.metadata` |
| `rpc.connect_rpc.response.metadata` | `rpc.response.metadata` |
| `rpc.grpc.request.metadata` | `rpc.request.metadata` |
| `rpc.grpc.response.metadata` | `rpc.response.metadata` |
| `rpc.jsonrpc.request_id` | `jsonrpc.request.id` |
| `rpc.jsonrpc.version` | `jsonrpc.protocol.version` |
| `rpc.system` | `rpc.system.name` |

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `process.open_file_descriptor.count` | `process.unix.file_descriptor.count` |
| `system.linux.memory.available` | `system.memory.linux.available` |
| `system.linux.memory.slab.usage` | `system.memory.linux.slab.usage` |

## v1.40.0

| Old | v1.40.0 Name |
|-----|-------------|
| `feature_flag.evaluation.error.message` | `feature_flag.error.message` |

| Old Metric | v1.40.0 Metric |
|-----------|---------------|
| `system.memory.shared` | `system.memory.linux.shared` |

---

## Totals

- **~170 attribute renames** across 21 versions
- **~90 metric renames** across 14 versions
- Schema URL: `https://opentelemetry.io/schemas/1.40.0`
- Schema source: [schemas/1.40.0](https://github.com/open-telemetry/semantic-conventions/blob/main/schemas/1.40.0)
