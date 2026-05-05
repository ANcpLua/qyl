
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.OTel.Enums
{


    public enum SpanKind
    {

        Unspecified = 0,

        InternalName = 1,

        Server = 2,

        Client = 3,

        Producer = 4,

        Consumer = 5
    }
}
