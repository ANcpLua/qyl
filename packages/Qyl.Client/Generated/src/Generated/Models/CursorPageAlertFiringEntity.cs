
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Domains.Alerting;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageAlertFiringEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal CursorPageAlertFiringEntity(IEnumerable<AlertFiringEntity> items, bool hasMore)
        {
            Items = items.ToList();
            HasMore = hasMore;
        }

        internal CursorPageAlertFiringEntity(IList<AlertFiringEntity> items, string nextCursor, string prevCursor, bool hasMore, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Items = items;
            NextCursor = nextCursor;
            PrevCursor = prevCursor;
            HasMore = hasMore;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<AlertFiringEntity> Items { get; }

        public string NextCursor { get; }

        public string PrevCursor { get; }

        public bool HasMore { get; }
    }
}
