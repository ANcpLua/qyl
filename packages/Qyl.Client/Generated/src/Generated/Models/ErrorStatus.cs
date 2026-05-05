
#nullable disable

namespace Qyl.Domains.Observe.Error
{
    public enum ErrorStatus
    {
        New,
        Acknowledged,
        InProgress,
        Resolved,
        Ignored,
        Regressed,
        WontFix
    }
}
