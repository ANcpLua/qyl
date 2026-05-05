
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Domains.Issues;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageErrorIssueEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal CursorPageErrorIssueEntity(IEnumerable<ErrorIssueEntity> items, bool hasMore)
        {
            Items = items.ToList();
            HasMore = hasMore;
        }

        internal CursorPageErrorIssueEntity(IList<ErrorIssueEntity> items, string nextCursor, string prevCursor, bool hasMore, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Items = items;
            NextCursor = nextCursor;
            PrevCursor = prevCursor;
            HasMore = hasMore;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<ErrorIssueEntity> Items { get; }

        public string NextCursor { get; }

        public string PrevCursor { get; }

        public bool HasMore { get; }
    }
}
