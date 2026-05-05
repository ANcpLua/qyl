
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;

namespace Qyl.Domains.Search
{
    public partial class SearchRequest
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public SearchRequest(string query)
        {
            Argument.AssertNotNull(query, nameof(query));

            Query = query;
            EntityTypes = new ChangeTrackingList<SearchEntityType>();
        }

        internal SearchRequest(string query, IList<SearchEntityType> entityTypes, string projectId, int? limit, string cursor, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Query = query;
            EntityTypes = entityTypes;
            ProjectId = projectId;
            Limit = limit;
            Cursor = cursor;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Query { get; }

        public IList<SearchEntityType> EntityTypes { get; }

        public string ProjectId { get; set; }

        public int? Limit { get; set; }

        public string Cursor { get; set; }
    }
}
