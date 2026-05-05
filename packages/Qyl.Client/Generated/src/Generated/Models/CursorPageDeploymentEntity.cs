
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Domains.Ops.Deployment;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageDeploymentEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal CursorPageDeploymentEntity(IEnumerable<DeploymentEntity> items, bool hasMore)
        {
            Items = items.ToList();
            HasMore = hasMore;
        }

        internal CursorPageDeploymentEntity(IList<DeploymentEntity> items, string nextCursor, string prevCursor, bool hasMore, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Items = items;
            NextCursor = nextCursor;
            PrevCursor = prevCursor;
            HasMore = hasMore;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<DeploymentEntity> Items { get; }

        public string NextCursor { get; }

        public string PrevCursor { get; }

        public bool HasMore { get; }
    }
}
