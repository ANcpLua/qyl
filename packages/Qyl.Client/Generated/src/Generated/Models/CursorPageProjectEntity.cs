
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Domains.Workspace;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageProjectEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal CursorPageProjectEntity(IEnumerable<ProjectEntity> items, bool hasMore)
        {
            Items = items.ToList();
            HasMore = hasMore;
        }

        internal CursorPageProjectEntity(IList<ProjectEntity> items, string nextCursor, string prevCursor, bool hasMore, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Items = items;
            NextCursor = nextCursor;
            PrevCursor = prevCursor;
            HasMore = hasMore;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<ProjectEntity> Items { get; }

        public string NextCursor { get; }

        public string PrevCursor { get; }

        public bool HasMore { get; }
    }
}
