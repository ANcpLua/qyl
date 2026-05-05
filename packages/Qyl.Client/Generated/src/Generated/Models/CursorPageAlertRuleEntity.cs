
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Domains.Alerting;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageAlertRuleEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal CursorPageAlertRuleEntity(IEnumerable<AlertRuleEntity> items, bool hasMore)
        {
            Items = items.ToList();
            HasMore = hasMore;
        }

        internal CursorPageAlertRuleEntity(IList<AlertRuleEntity> items, string nextCursor, string prevCursor, bool hasMore, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Items = items;
            NextCursor = nextCursor;
            PrevCursor = prevCursor;
            HasMore = hasMore;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<AlertRuleEntity> Items { get; }

        public string NextCursor { get; }

        public string PrevCursor { get; }

        public bool HasMore { get; }
    }
}
