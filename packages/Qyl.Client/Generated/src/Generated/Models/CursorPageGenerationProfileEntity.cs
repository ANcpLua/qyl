
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Domains.Configurator;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageGenerationProfileEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal CursorPageGenerationProfileEntity(IEnumerable<GenerationProfileEntity> items, bool hasMore)
        {
            Items = items.ToList();
            HasMore = hasMore;
        }

        internal CursorPageGenerationProfileEntity(IList<GenerationProfileEntity> items, string nextCursor, string prevCursor, bool hasMore, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Items = items;
            NextCursor = nextCursor;
            PrevCursor = prevCursor;
            HasMore = hasMore;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<GenerationProfileEntity> Items { get; }

        public string NextCursor { get; }

        public string PrevCursor { get; }

        public bool HasMore { get; }
    }
}
