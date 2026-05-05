
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Client;

namespace Qyl.Domains.Search
{
    public partial class SearchResponse
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal SearchResponse(IEnumerable<SearchResult> results, long totalCount, int durationMs)
        {
            Results = results.ToList();
            TotalCount = totalCount;
            DurationMs = durationMs;
            Suggestions = new ChangeTrackingList<string>();
        }

        internal SearchResponse(IList<SearchResult> results, long totalCount, int durationMs, string nextCursor, IList<string> suggestions, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Results = results;
            TotalCount = totalCount;
            DurationMs = durationMs;
            NextCursor = nextCursor;
            Suggestions = suggestions;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<SearchResult> Results { get; }

        public long TotalCount { get; }

        public int DurationMs { get; }

        public string NextCursor { get; }

        public IList<string> Suggestions { get; }
    }
}
