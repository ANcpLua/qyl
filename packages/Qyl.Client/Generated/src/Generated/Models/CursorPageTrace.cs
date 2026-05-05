
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.OTel.Traces;
using Trace = Qyl.OTel.Traces.Trace;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageTrace
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal CursorPageTrace(IEnumerable<Trace> items, bool hasMore)
        {
            Items = items.ToList();
            HasMore = hasMore;
        }

        internal CursorPageTrace(IList<Trace> items, string nextCursor, string prevCursor, bool hasMore, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Items = items;
            NextCursor = nextCursor;
            PrevCursor = prevCursor;
            HasMore = hasMore;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<Trace> Items { get; }

        public string NextCursor { get; }

        public string PrevCursor { get; }

        public bool HasMore { get; }
    }
}
