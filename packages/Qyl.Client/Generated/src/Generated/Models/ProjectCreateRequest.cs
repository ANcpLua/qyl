
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class ProjectCreateRequest
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public ProjectCreateRequest(string name, string slug)
        {
            Argument.AssertNotNull(name, nameof(name));
            Argument.AssertNotNull(slug, nameof(slug));

            Name = name;
            Slug = slug;
        }

        internal ProjectCreateRequest(string name, string slug, string description, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Name = name;
            Slug = slug;
            Description = description;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Name { get; }

        public string Slug { get; }

        public string Description { get; set; }
    }
}
