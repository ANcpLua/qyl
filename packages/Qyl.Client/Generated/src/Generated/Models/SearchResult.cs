
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Search
{
    public partial class SearchResult
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal SearchResult(string documentId, SearchEntityType entityType, string entityId, string title, double score)
        {
            DocumentId = documentId;
            EntityType = entityType;
            EntityId = entityId;
            Title = title;
            Score = score;
        }

        internal SearchResult(string documentId, SearchEntityType entityType, string entityId, string title, string snippet, double score, string url, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            DocumentId = documentId;
            EntityType = entityType;
            EntityId = entityId;
            Title = title;
            Snippet = snippet;
            Score = score;
            Url = url;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string DocumentId { get; }

        public SearchEntityType EntityType { get; }

        public string EntityId { get; }

        public string Title { get; }

        public string Snippet { get; }

        public double Score { get; }

        public string Url { get; }
    }
}
