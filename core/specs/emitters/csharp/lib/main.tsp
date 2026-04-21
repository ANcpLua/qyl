import "../dist/index.js";

using TypeSpec.Reflection;

namespace Qyl.Emit.CSharp;

extern dec csharpNamespace(target: Model | Enum | Namespace, ns: valueof string);
extern dec csharpRecord(target: Model);
extern dec csharpEnum(target: Enum | Union);
