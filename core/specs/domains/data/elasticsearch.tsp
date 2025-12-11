// =============================================================================
// QYL v2.0 - Elasticsearch Semantic Conventions (OTel v1.38)
// =============================================================================
// Elasticsearch-specific telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Data.Elasticsearch;

// =============================================================================
// Elasticsearch Span Attributes
// =============================================================================

@doc("Elasticsearch span attributes")
model ElasticsearchAttributes {
  @doc("Elasticsearch cluster name")
  @encodedName("application/json", "elasticsearch.cluster.name")
  clusterName?: string;

  @doc("Elasticsearch node name")
  @encodedName("application/json", "elasticsearch.node.name")
  nodeName?: string;

  @doc("Elasticsearch index name(s)")
  @encodedName("application/json", "elasticsearch.index.name")
  indexName?: string;

  @doc("Elasticsearch operation type")
  @encodedName("application/json", "elasticsearch.action")
  action?: ElasticsearchAction;

  @doc("HTTP method used")
  @encodedName("application/json", "http.request.method")
  httpMethod?: string;

  @doc("Request path")
  @encodedName("application/json", "url.path")
  urlPath?: string;

  @doc("Query body (may be truncated)")
  @encodedName("application/json", "elasticsearch.request.body")
  requestBody?: string;

  @doc("Document ID")
  @encodedName("application/json", "elasticsearch.document.id")
  documentId?: string;

  @doc("Number of shards hit")
  @encodedName("application/json", "elasticsearch.shards.hit")
  shardsHit?: int32;

  @doc("Total shards")
  @encodedName("application/json", "elasticsearch.shards.total")
  shardsTotal?: int32;

  @doc("Took time in milliseconds")
  @encodedName("application/json", "elasticsearch.took")
  took?: int64;

  @doc("Whether request timed out")
  @encodedName("application/json", "elasticsearch.timed_out")
  timedOut?: boolean;

  @doc("Total hits count")
  @encodedName("application/json", "elasticsearch.hits.total")
  hitsTotal?: int64;

  @doc("Scroll ID (for scroll queries)")
  @encodedName("application/json", "elasticsearch.scroll.id")
  scrollId?: string;

  @doc("Bulk operation item count")
  @encodedName("application/json", "elasticsearch.bulk.items_count")
  bulkItemsCount?: int32;
}

// =============================================================================
// Elasticsearch Enumerations
// =============================================================================

@doc("Elasticsearch actions/operations")
enum ElasticsearchAction {
  @doc("Search query")
  search: "search",

  @doc("Multi-search")
  msearch: "msearch",

  @doc("Scroll search")
  scroll: "scroll",

  @doc("Clear scroll")
  clearScroll: "clear_scroll",

  @doc("Index document")
  index: "index",

  @doc("Get document")
  get: "get",

  @doc("Multi-get")
  mget: "mget",

  @doc("Delete document")
  delete: "delete",

  @doc("Delete by query")
  deleteByQuery: "delete_by_query",

  @doc("Update document")
  update: "update",

  @doc("Update by query")
  updateByQuery: "update_by_query",

  @doc("Bulk operation")
  bulk: "bulk",

  @doc("Create index")
  createIndex: "create_index",

  @doc("Delete index")
  deleteIndex: "delete_index",

  @doc("Get index")
  getIndex: "get_index",

  @doc("Exists check")
  exists: "exists",

  @doc("Count documents")
  count: "count",

  @doc("Analyze text")
  analyze: "analyze",

  @doc("Reindex")
  reindex: "reindex",

  @doc("Cluster health")
  clusterHealth: "cluster_health",

  @doc("Cluster stats")
  clusterStats: "cluster_stats",

  @doc("Indices stats")
  indicesStats: "indices_stats",

  @doc("Put mapping")
  putMapping: "put_mapping",

  @doc("Get mapping")
  getMapping: "get_mapping",

  @doc("Put settings")
  putSettings: "put_settings",

  @doc("Refresh index")
  refresh: "refresh",

  @doc("Flush index")
  flush: "flush",

  @doc("Force merge")
  forceMerge: "force_merge",
}

// =============================================================================
// Elasticsearch Metrics
// =============================================================================

@doc("Elasticsearch query duration metric")
model EsQueryDurationMetric {
  @doc("Metric name")
  name: "elasticsearch.query.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Cluster name")
  @encodedName("application/json", "elasticsearch.cluster.name")
  clusterName: string;

  @doc("Index name")
  @encodedName("application/json", "elasticsearch.index.name")
  indexName?: string;

  @doc("Action type")
  @encodedName("application/json", "elasticsearch.action")
  action: ElasticsearchAction;
}

@doc("Elasticsearch documents indexed metric")
model EsDocumentsIndexedMetric {
  @doc("Metric name")
  name: "elasticsearch.documents.indexed";

  @doc("Metric unit")
  unit: "{document}";

  @doc("Cluster name")
  @encodedName("application/json", "elasticsearch.cluster.name")
  clusterName: string;

  @doc("Index name")
  @encodedName("application/json", "elasticsearch.index.name")
  indexName: string;
}

@doc("Elasticsearch index size metric")
model EsIndexSizeMetric {
  @doc("Metric name")
  name: "elasticsearch.index.size";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Cluster name")
  @encodedName("application/json", "elasticsearch.cluster.name")
  clusterName: string;

  @doc("Index name")
  @encodedName("application/json", "elasticsearch.index.name")
  indexName: string;
}
