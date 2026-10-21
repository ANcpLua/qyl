/**
 * The one rule every contract time is read by, this server's input schemas' as the Collector's routes': RFC 3339 §5.6
 * date-time with `T` and `Z` in either case, a numeric offset with its colon, a second from 00 to 59 and a fraction of
 * any length, and no space in place of `T`. Ajv's own `date-time` also takes a space, an offset without its colon or
 * its minutes and a leap second, which no instant either side reads holds; the Collector's ContractDateTime is the
 * same rule. https://www.rfc-editor.org/rfc/rfc3339#section-5.6
 */
const DATE_TIME = /^(\d{4})-(\d{2})-(\d{2})[Tt](\d{2}):(\d{2}):(\d{2})(?:\.\d+)?(?:[Zz]|[+-](\d{2}):(\d{2}))$/u;

export function isContractDateTime(value: string): boolean {
  const match = DATE_TIME.exec(value);
  if (match === null) return false;
  const [year, month, day, hour, minute, second, offsetHour, offsetMinute] = match.slice(1).map((part) => Number(part ?? 0));
  const leap = year! % 4 === 0 && (year! % 100 !== 0 || year! % 400 === 0);
  const days = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  return month! >= 1 && month! <= 12 && day! >= 1 && day! <= days[month! - 1]! &&
    hour! <= 23 && minute! <= 59 && second! <= 59 && offsetHour! <= 23 && offsetMinute! <= 59;
}
