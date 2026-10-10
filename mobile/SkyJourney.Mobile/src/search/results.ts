import type { FlightOffer } from '@/api/types';

export type SortOrder = 'price' | 'departure' | 'arrival' | 'duration';

export type TimeOfDay = 'morning' | 'afternoon' | 'evening';

export type ResultFilters = { timesOfDay: TimeOfDay[]; maxPrice: number | null };

export const noFilters: ResultFilters = { timesOfDay: [], maxPrice: null };

/** A return flight must leave at least this long after the outbound flight lands (matches the API booking rule). */
export const MIN_STAY_MINUTES = 120;

const TIME_WINDOWS: Record<TimeOfDay, [number, number]> = {
  morning: [0, 12 * 60],
  afternoon: [12 * 60, 18 * 60],
  evening: [18 * 60, 24 * 60],
};

function localMinutes(isoLocal: string): number {
  return Number(isoLocal.slice(11, 13)) * 60 + Number(isoLocal.slice(14, 16));
}

export function timeOfDay(isoLocal: string): TimeOfDay {
  const minutes = localMinutes(isoLocal);
  return (Object.keys(TIME_WINDOWS) as TimeOfDay[]).find((key) => {
    const [from, to] = TIME_WINDOWS[key];
    return minutes >= from && minutes < to;
  })!;
}

export function applyFilters(offers: FlightOffer[], filters: ResultFilters): FlightOffer[] {
  return offers.filter(
    (o) =>
      (filters.timesOfDay.length === 0 || filters.timesOfDay.includes(timeOfDay(o.departureLocal))) &&
      (filters.maxPrice === null ||
        (o.lowestPricePerPassenger !== null && o.lowestPricePerPassenger.amount <= filters.maxPrice)),
  );
}

const byUtc = (a: string, b: string) => Date.parse(a) - Date.parse(b);

export function sortOffers(offers: FlightOffer[], order: SortOrder): FlightOffer[] {
  const price = (o: FlightOffer) => o.lowestPricePerPassenger?.amount ?? Number.POSITIVE_INFINITY;
  const compare: Record<SortOrder, (a: FlightOffer, b: FlightOffer) => number> = {
    price: (a, b) => price(a) - price(b) || byUtc(a.departureUtc, b.departureUtc),
    departure: (a, b) => byUtc(a.departureUtc, b.departureUtc),
    arrival: (a, b) => byUtc(a.arrivalUtc, b.arrivalUtc),
    duration: (a, b) => a.durationMinutes - b.durationMinutes || byUtc(a.departureUtc, b.departureUtc),
  };
  // Unbookable (sold out) flights always sink to the bottom.
  return [...offers].sort((a, b) => Number(!a.isBookable) - Number(!b.isBookable) || compare[order](a, b));
}

/** Whether a return flight leaves late enough after the selected outbound flight lands. */
export function isValidReturn(outbound: FlightOffer | null | undefined, candidate: FlightOffer): boolean {
  if (!outbound) {
    return true;
  }
  return Date.parse(candidate.departureUtc) - Date.parse(outbound.arrivalUtc) >= MIN_STAY_MINUTES * 60_000;
}

/** Rounded-up price thresholds for the "max price" filter, derived from the actual results. */
export function priceSteps(offers: FlightOffer[]): number[] {
  const prices = offers.map((o) => o.lowestPricePerPassenger?.amount).filter((p): p is number => p !== undefined);
  if (prices.length < 2) {
    return [];
  }
  const min = Math.min(...prices);
  const max = Math.max(...prices);
  if (max - min < 10) {
    return [];
  }
  const steps = [0.33, 0.66].map((f) => Math.ceil((min + (max - min) * f) / 5) * 5);
  return [...new Set(steps)];
}
