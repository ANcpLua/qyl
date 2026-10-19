// Absolute wire timestamps are unsigned 64-bit nanosecond counts encoded as decimal strings.
// They exceed Number.MAX_SAFE_INTEGER, so every operation goes through BigInt and only the
// millisecond result is a number.

/** Ascending comparator, exact at nanosecond resolution. */
export function compareUnixNanos(a: string, b: string): number {
  const left = BigInt(a);
  const right = BigInt(b);
  return left < right ? -1 : left > right ? 1 : 0;
}

/** The wire form of an epoch-millisecond instant. */
export function epochMsToUnixNanos(epochMs: number): string {
  return (BigInt(Math.round(epochMs)) * 1_000_000n).toString();
}
