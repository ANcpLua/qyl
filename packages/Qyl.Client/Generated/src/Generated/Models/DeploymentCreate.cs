
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;
using Qyl.Domains.Ops.Deployment;

namespace Qyl.Api
{
    public partial class DeploymentCreate
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public DeploymentCreate(string serviceName, string serviceVersion, DeploymentEnvironment environment, DeploymentStrategy strategy)
        {
            Argument.AssertNotNull(serviceName, nameof(serviceName));
            Argument.AssertNotNull(serviceVersion, nameof(serviceVersion));

            ServiceName = serviceName;
            ServiceVersion = serviceVersion;
            Environment = environment;
            Strategy = strategy;
        }

        internal DeploymentCreate(string serviceName, string serviceVersion, DeploymentEnvironment environment, DeploymentStrategy strategy, string deployedBy, string gitCommit, string gitBranch, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            ServiceName = serviceName;
            ServiceVersion = serviceVersion;
            Environment = environment;
            Strategy = strategy;
            DeployedBy = deployedBy;
            GitCommit = gitCommit;
            GitBranch = gitBranch;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string ServiceName { get; }

        public string ServiceVersion { get; }

        public DeploymentEnvironment Environment { get; }

        public DeploymentStrategy Strategy { get; }

        public string DeployedBy { get; set; }

        public string GitCommit { get; set; }

        public string GitBranch { get; set; }
    }
}
