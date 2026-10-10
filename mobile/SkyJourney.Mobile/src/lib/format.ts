/**
 * The API returns airport-local times as ISO strings with their UTC offset
 * (`2026-11-02T07:05:00+01:00`). Displaying them must not convert to the phone's time zone,
 * so these helpers read the wall-clock parts straight from the string.
 */
const ISO_LOCAL = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/;

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

function parts(iso: string) {
  const match = ISO_LOCAL.exec(iso);
  if (!match) {
    throw new Error(`Not an ISO date-time: ${iso}`);
  }
  const [, y, m, d, hh, mm] = match;
  return { year: Number(y), month: Number(m), day: Number(d), time: `${hh}:${mm}` };
}

/** `07:05` */
export function formatLocalTime(iso: string): string {
  return parts(iso).time;
}

/** `Mon 2 Nov` from `2026-11-02` or a local ISO date-time. */
export function formatDate(isoDate: string): string {
  const { year, month, day } = parts(isoDate.length === 10 ? `${isoDate}T00:00` : isoDate);
  const weekday = new Date(Date.UTC(year, month - 1, day)).getUTCDay();
  return `${WEEKDAYS[weekday]} ${day} ${MONTHS[month - 1]}`;
}

/** Number of calendar days between departure and arrival in their own local dates (+1 overnight). */
export function dayOffset(departureLocal: string, arrivalLocal: string): number {
  const a = parts(departureLocal);
  const b = parts(arrivalLocal);
  return (Date.UTC(b.year, b.month - 1, b.day) - Date.UTC(a.year, a.month - 1, a.day)) / 86_400_000;
}

/** `2h 20m` */
export function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return h === 0 ? `${m}m` : m === 0 ? `${h}h` : `${h}h ${m}m`;
}

export function formatMoney(money: { amount: number; currency: string }): string {
  return new Intl.NumberFormat('en-GB', { style: 'currency', currency: money.currency }).format(money.amount);
}

/** `YYYY-MM-DD` for a calendar date in the phone's local time zone. */
export function toIsoDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

export function addDays(isoDate: string, days: number): string {
  const { year, month, day } = parts(`${isoDate}T00:00`);
  const date = new Date(Date.UTC(year, month - 1, day + days));
  return date.toISOString().slice(0, 10);
}
