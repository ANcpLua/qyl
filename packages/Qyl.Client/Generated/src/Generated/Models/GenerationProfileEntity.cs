
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Configurator
{
    public partial class GenerationProfileEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal GenerationProfileEntity(string id, string projectId, string name, string targetFramework, string targetLanguage, string semconvVersion, string featuresJson, bool isDefault, DateTimeOffset createdAt, DateTimeOffset updatedAt)
        {
            Id = id;
            ProjectId = projectId;
            Name = name;
            TargetFramework = targetFramework;
            TargetLanguage = targetLanguage;
            SemconvVersion = semconvVersion;
            FeaturesJson = featuresJson;
            IsDefault = isDefault;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        internal GenerationProfileEntity(string id, string projectId, string name, string description, string targetFramework, string targetLanguage, string semconvVersion, string featuresJson, string templateOverridesJson, bool isDefault, DateTimeOffset createdAt, DateTimeOffset updatedAt, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            ProjectId = projectId;
            Name = name;
            Description = description;
            TargetFramework = targetFramework;
            TargetLanguage = targetLanguage;
            SemconvVersion = semconvVersion;
            FeaturesJson = featuresJson;
            TemplateOverridesJson = templateOverridesJson;
            IsDefault = isDefault;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string ProjectId { get; }

        public string Name { get; }

        public string Description { get; }

        public string TargetFramework { get; }

        public string TargetLanguage { get; }

        public string SemconvVersion { get; }

        public string FeaturesJson { get; }

        public string TemplateOverridesJson { get; }

        public bool IsDefault { get; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset UpdatedAt { get; }
    }
}
