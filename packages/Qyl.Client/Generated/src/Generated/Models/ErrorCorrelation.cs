
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Client;
using Qyl.Common;

namespace Qyl.Domains.Observe.Error
{
    public partial class ErrorCorrelation
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ErrorCorrelation(string errorId, IEnumerable<CorrelatedError> correlatedErrors)
        {
            ErrorId = errorId;
            CorrelatedErrors = correlatedErrors.ToList();
            CommonAttributes = new ChangeTrackingList<Common.Attribute>();
        }

        internal ErrorCorrelation(string errorId, IList<CorrelatedError> correlatedErrors, string rootCause, IList<Common.Attribute> commonAttributes, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            ErrorId = errorId;
            CorrelatedErrors = correlatedErrors;
            RootCause = rootCause;
            CommonAttributes = commonAttributes;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string ErrorId { get; }

        public IList<CorrelatedError> CorrelatedErrors { get; }

        public string RootCause { get; }

        public IList<Common.Attribute> CommonAttributes { get; }
    }
}
