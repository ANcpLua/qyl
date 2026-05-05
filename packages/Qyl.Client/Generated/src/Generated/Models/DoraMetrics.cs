
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Api
{
    public partial class DoraMetrics
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal DoraMetrics(double deploymentFrequency, double leadTimeHours, double changeFailureRate, double mttrHours, DoraPerformanceLevel performanceLevel)
        {
            DeploymentFrequency = deploymentFrequency;
            LeadTimeHours = leadTimeHours;
            ChangeFailureRate = changeFailureRate;
            MttrHours = mttrHours;
            PerformanceLevel = performanceLevel;
        }

        internal DoraMetrics(double deploymentFrequency, double leadTimeHours, double changeFailureRate, double mttrHours, DoraPerformanceLevel performanceLevel, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            DeploymentFrequency = deploymentFrequency;
            LeadTimeHours = leadTimeHours;
            ChangeFailureRate = changeFailureRate;
            MttrHours = mttrHours;
            PerformanceLevel = performanceLevel;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public double DeploymentFrequency { get; }

        public double LeadTimeHours { get; }

        public double ChangeFailureRate { get; }

        public double MttrHours { get; }

        public DoraPerformanceLevel PerformanceLevel { get; }
    }
}
