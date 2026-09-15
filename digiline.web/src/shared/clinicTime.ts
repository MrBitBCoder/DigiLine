export const defaultClinicTimeZone = "Asia/Kolkata";

export function formatClinicTime(value: string, timeZoneId = defaultClinicTimeZone, date = false) {
  return new Intl.DateTimeFormat(undefined, {
    timeZone: timeZoneId, ...(date ? { year: "numeric", month: "short", day: "numeric" } : {}),
    hour: "numeric", minute: "2-digit",
  }).format(new Date(value));
}

export function clinicDateTimeInput(value: string, timeZoneId: string) {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: timeZoneId, year: "numeric", month: "2-digit", day: "2-digit",
    hour: "2-digit", minute: "2-digit", hourCycle: "h23",
  }).formatToParts(new Date(value));
  const field = (type: string) => parts.find(part => part.type === type)?.value ?? "";
  return `${field("year")}-${field("month")}-${field("day")}T${field("hour")}:${field("minute")}`;
}

export function clinicInputToUtc(value: string, timeZoneId: string) {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value)) throw new Error("Enter a valid date and time.");
  const wallTime = Date.parse(`${value}:00Z`);
  if (!Number.isFinite(wallTime)) throw new Error("Enter a valid date and time.");
  const matches: number[] = [];
  // All current IANA offsets fit within one day. Sampling detects both sides of a DST transition.
  for (let offset = -14 * 60; offset <= 14 * 60; offset += 15) {
    const instant = wallTime - offset * 60_000;
    if (clinicDateTimeInput(new Date(instant).toISOString(), timeZoneId) === value) matches.push(instant);
  }
  if (matches.length === 0) throw new Error("This time does not exist in the clinic time zone. Choose another time.");
  if (matches.length > 1) throw new Error("This time occurs twice in the clinic time zone. Choose another time.");
  return new Date(matches[0]).toISOString();
}
