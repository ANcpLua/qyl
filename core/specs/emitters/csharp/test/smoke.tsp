import "@qyl/typespec-emit-csharp";

using Qyl.Emit.CSharp;

@csharpNamespace("Qyl.Test")
namespace Qyl.Test;

@csharpRecord
model Point {
  x: int32;
  y: int32;
  label?: string;
}

@csharpEnum
enum Color {
  Red,
  Green,
  Blue,
}
