
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;

namespace Qyl.Domains.Observe.Session
{
    public partial class SessionStats
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal SessionStats(long activeSessions, long totalSessions, long uniqueUsers, double avgDurationMs, long sessionsWithErrors, long sessionsWithGenAi, double bounceRate)
        {
            ActiveSessions = activeSessions;
            TotalSessions = totalSessions;
            UniqueUsers = uniqueUsers;
            AvgDurationMs = avgDurationMs;
            SessionsWithErrors = sessionsWithErrors;
            SessionsWithGenAi = sessionsWithGenAi;
            BounceRate = bounceRate;
            ByDeviceType = new ChangeTrackingList<SessionDeviceStats>();
            ByCountry = new ChangeTrackingList<SessionCountryStats>();
        }

        internal SessionStats(long activeSessions, long totalSessions, long uniqueUsers, double avgDurationMs, long sessionsWithErrors, long sessionsWithGenAi, double bounceRate, IList<SessionDeviceStats> byDeviceType, IList<SessionCountryStats> byCountry, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            ActiveSessions = activeSessions;
            TotalSessions = totalSessions;
            UniqueUsers = uniqueUsers;
            AvgDurationMs = avgDurationMs;
            SessionsWithErrors = sessionsWithErrors;
            SessionsWithGenAi = sessionsWithGenAi;
            BounceRate = bounceRate;
            ByDeviceType = byDeviceType;
            ByCountry = byCountry;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public long ActiveSessions { get; }

        public long TotalSessions { get; }

        public long UniqueUsers { get; }

        public double AvgDurationMs { get; }

        public long SessionsWithErrors { get; }

        public long SessionsWithGenAi { get; }

        public double BounceRate { get; }

        public IList<SessionDeviceStats> ByDeviceType { get; }

        public IList<SessionCountryStats> ByCountry { get; }
    }
}
