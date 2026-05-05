
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Domains.Workflow;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageWorkflowRunEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal CursorPageWorkflowRunEntity(IEnumerable<WorkflowRunEntity> items, bool hasMore)
        {
            Items = items.ToList();
            HasMore = hasMore;
        }

        internal CursorPageWorkflowRunEntity(IList<WorkflowRunEntity> items, string nextCursor, string prevCursor, bool hasMore, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Items = items;
            NextCursor = nextCursor;
            PrevCursor = prevCursor;
            HasMore = hasMore;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<WorkflowRunEntity> Items { get; }

        public string NextCursor { get; }

        public string PrevCursor { get; }

        public bool HasMore { get; }
    }
}
