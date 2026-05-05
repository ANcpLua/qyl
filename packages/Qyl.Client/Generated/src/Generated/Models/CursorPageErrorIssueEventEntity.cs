
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Domains.Issues;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageErrorIssueEventEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal CursorPageErrorIssueEventEntity(IEnumerable<ErrorIssueEventEntity> items, bool hasMore)
        {
            Items = items.ToList();
            HasMore = hasMore;
        }

        internal CursorPageErrorIssueEventEntity(IList<ErrorIssueEventEntity> items, string nextCursor, string prevCursor, bool hasMore, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Items = items;
            NextCursor = nextCursor;
            PrevCursor = prevCursor;
            HasMore = hasMore;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<ErrorIssueEventEntity> Items { get; }

        public string NextCursor { get; }

        public string PrevCursor { get; }

        public bool HasMore { get; }
    }
}
