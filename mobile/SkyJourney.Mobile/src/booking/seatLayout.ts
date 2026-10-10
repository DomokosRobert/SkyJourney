import type { Passenger, SeatMap, SeatMapSeat } from '@/api/bookings';

export type SeatRow = { row: number; cells: (SeatMapSeat | null)[] };

/**
 * Arranges a seat map into rows, inserting `null` for the aisle in the middle of the seat letters
 * (`ABC DEF` for a six-abreast cabin). Missing seats (e.g. a removed row position) are `null` too.
 */
export function seatRows(map: SeatMap): SeatRow[] {
  const letters = map.seatLetters.split('');
  const aisleAfter = Math.ceil(letters.length / 2);
  const bySeat = new Map(map.seats.map((s) => [s.seatNumber, s]));
  const rows: SeatRow[] = [];
  for (let row = 1; row <= map.rows; row++) {
    const cells: (SeatMapSeat | null)[] = [];
    letters.forEach((letter, index) => {
      if (index === aisleAfter) {
        cells.push(null);
      }
      cells.push(bySeat.get(`${row}${letter}`) ?? null);
    });
    if (cells.some((c) => c !== null)) {
      rows.push({ row, cells });
    }
  }
  return rows;
}

/** The lowest price per seat category, for the legend. */
export function categoryPrices(map: SeatMap): { category: SeatMapSeat['category']; price: SeatMapSeat['price'] }[] {
  const result = new Map<SeatMapSeat['category'], SeatMapSeat['price']>();
  for (const seat of map.seats) {
    const current = result.get(seat.category);
    if (!current || seat.price.amount < current.amount) {
      result.set(seat.category, seat.price);
    }
  }
  const order: SeatMapSeat['category'][] = ['ExtraLegroom', 'Front', 'Standard'];
  return order.filter((c) => result.has(c)).map((category) => ({ category, price: result.get(category)! }));
}

export const CATEGORY_LABELS: Record<SeatMapSeat['category'], string> = {
  ExtraLegroom: 'Extra legroom',
  Front: 'Front rows',
  Standard: 'Standard',
};

/** The first passenger after `current` (wrapping around) without a seat on this segment, or null when all are seated. */
export function nextUnseatedPassenger(
  passengers: Passenger[],
  seatedPassengerIds: ReadonlySet<string>,
  current: string | null,
): string | null {
  const start = current ? passengers.findIndex((p) => p.id === current) + 1 : 0;
  for (let i = 0; i < passengers.length; i++) {
    const candidate = passengers[(start + i) % passengers.length];
    if (candidate && !seatedPassengerIds.has(candidate.id)) {
      return candidate.id;
    }
  }
  return null;
}
