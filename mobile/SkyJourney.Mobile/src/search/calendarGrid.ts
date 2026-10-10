import { addDays } from '@/lib/format';

export type Month = { year: number; month: number };

const MONTH_NAMES = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

export function monthLabel({ year, month }: Month): string {
  return `${MONTH_NAMES[month - 1]} ${year}`;
}

export function monthKey({ year, month }: Month): string {
  return `${year}-${String(month).padStart(2, '0')}`;
}

export function firstDay(m: Month): string {
  return `${monthKey(m)}-01`;
}

export function lastDay({ year, month }: Month): string {
  const days = new Date(Date.UTC(year, month, 0)).getUTCDate();
  return `${monthKey({ year, month })}-${String(days).padStart(2, '0')}`;
}

/** Months from the month of `fromDate` through the month of `toDate`, inclusive. */
export function monthsBetween(fromDate: string, toDate: string): Month[] {
  const months: Month[] = [];
  let year = Number(fromDate.slice(0, 4));
  let month = Number(fromDate.slice(5, 7));
  const endKey = toDate.slice(0, 7);
  while (monthKey({ year, month }) <= endKey) {
    months.push({ year, month });
    month += 1;
    if (month > 12) {
      month = 1;
      year += 1;
    }
  }
  return months;
}

/**
 * Weeks of the month as rows of 7 ISO dates (Monday first); `null` pads days outside the month.
 */
export function monthGrid(m: Month): (string | null)[][] {
  const first = firstDay(m);
  const weekday = new Date(`${first}T00:00:00Z`).getUTCDay(); // 0 = Sunday
  const leading = (weekday + 6) % 7;
  const last = lastDay(m);

  const cells: (string | null)[] = Array.from({ length: leading }, () => null);
  for (let day = first; day <= last; day = addDays(day, 1)) {
    cells.push(day);
  }
  while (cells.length % 7 !== 0) {
    cells.push(null);
  }

  const weeks: (string | null)[][] = [];
  for (let i = 0; i < cells.length; i += 7) {
    weeks.push(cells.slice(i, i + 7));
  }
  return weeks;
}

/** Compact price label for a calendar cell, e.g. `€89` (rounded up so the hint never undersells). */
export function compactPrice(amount: number, currency: string): string {
  const symbol = currency === 'EUR' ? '€' : currency === 'GBP' ? '£' : currency === 'USD' ? '$' : '';
  return symbol ? `${symbol}${Math.ceil(amount)}` : `${Math.ceil(amount)} ${currency}`;
}
