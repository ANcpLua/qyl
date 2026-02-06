using System.Text.Json.Serialization;
using Qyl.Hosting.Resources;

namespace Qyl.Hosting;

[JsonSerializable(typeof(QylOptions))]
[JsonSerializable(typeof(PortBinding))]
internal partial class QylHostingJsonContext : JsonSerializerContext;
