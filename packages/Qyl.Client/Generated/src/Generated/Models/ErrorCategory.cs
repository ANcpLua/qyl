
#nullable disable

namespace Qyl.Domains.Observe.Error
{
    public enum ErrorCategory
    {
        Client,
        Server,
        Network,
        Timeout,
        Validation,
        Authentication,
        Authorization,
        RateLimit,
        NotFound,
        Conflict,
        Internal,
        External,
        Database,
        Configuration,
        Unknown
    }
}
