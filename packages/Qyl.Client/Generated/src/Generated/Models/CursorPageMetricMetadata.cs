
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Api;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageMetricMetadata
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal CursorPageMetricMetadata(IEnumerable<MetricMetadata> items, bool hasMore)
        {
            Items = items.ToList();
            HasMore = hasMore;
        }

        internal CursorPageMetricMetadata(IList<MetricMetadata> items, string nextCursor, string prevCursor, bool hasMore, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Items = items;
            NextCursor = nextCursor;
            PrevCursor = prevCursor;
            HasMore = hasMore;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<MetricMetadata> Items { get; }

        public string NextCursor { get; }

        public string PrevCursor { get; }

        public bool HasMore { get; }
    }
}
