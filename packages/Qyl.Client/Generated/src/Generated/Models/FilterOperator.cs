
#nullable disable

namespace Qyl.Domains.Observe.Log
{
    public enum FilterOperator
    {
        Eq,
        Neq,
        Contains,
        StartsWith,
        EndsWith,
        Regex,
        Gt,
        Gte,
        Lt,
        Lte,
        In,
        NotIn,
        Exists,
        NotExists
    }
}
