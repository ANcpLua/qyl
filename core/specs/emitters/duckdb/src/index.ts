import { setTypeSpecNamespace } from "@typespec/compiler";
import { $duckdbColumn, $duckdbIndex, $duckdbPrimaryKey, $duckdbTable } from "./decorators.js";

setTypeSpecNamespace(
  "Qyl.Emit.DuckDb",
  $duckdbTable,
  $duckdbColumn,
  $duckdbPrimaryKey,
  $duckdbIndex,
);

export { $lib } from "./lib.js";
export { $duckdbColumn, $duckdbIndex, $duckdbPrimaryKey, $duckdbTable } from "./decorators.js";
export { $onEmit } from "./emitter.js";
