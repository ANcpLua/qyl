import "../dist/index.js";

using TypeSpec.Reflection;

namespace Qyl.Emit.DuckDb;

extern dec duckdbTable(target: Model, name: valueof string);
extern dec duckdbColumn(target: ModelProperty, type?: valueof string);
extern dec duckdbPrimaryKey(target: ModelProperty);
extern dec duckdbIndex(target: ModelProperty, name: valueof string);
