
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.OTel.Logs;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageLogRecord
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal CursorPageLogRecord(IEnumerable<LogRecord> items, bool hasMore)
        {
            Items = items.ToList();
            HasMore = hasMore;
        }

        internal CursorPageLogRecord(IList<LogRecord> items, string nextCursor, string prevCursor, bool hasMore, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Items = items;
            NextCursor = nextCursor;
            PrevCursor = prevCursor;
            HasMore = hasMore;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<LogRecord> Items { get; }

        public string NextCursor { get; }

        public string PrevCursor { get; }

        public bool HasMore { get; }
    }
}
