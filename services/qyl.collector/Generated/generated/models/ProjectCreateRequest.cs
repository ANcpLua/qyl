
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Api
{

    public partial class ProjectCreateRequest
    {
        public string Name { get; set; }

        public string Slug { get; set; }

        public string Description { get; set; }


    }
}
