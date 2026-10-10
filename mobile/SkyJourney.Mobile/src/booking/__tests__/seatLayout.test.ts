import { booking, seatMap } from '@/test/fixtures';
import { categoryPrices, nextUnseatedPassenger, seatRows } from '../seatLayout';

describe('seatRows', () => {
  it('puts the aisle in the middle of the seat letters', () => {
    const rows = seatRows(seatMap());

    expect(rows).toHaveLength(2);
    expect(rows[0]!.cells.map((c) => c?.seatNumber ?? null)).toEqual(['1A', '1B', null, '1C', '1D']);
  });

  it('leaves gaps for missing seats and skips empty rows', () => {
    const map = seatMap({ rows: 3 });
    map.seats = map.seats.filter((s) => s.seatNumber !== '2D');

    const rows = seatRows(map);

    expect(rows.map((r) => r.row)).toEqual([1, 2]);
    expect(rows[1]!.cells.at(-1)).toBeNull();
  });
});

describe('categoryPrices', () => {
  it('lists the cheapest price per category, premium first', () => {
    expect(categoryPrices(seatMap())).toEqual([
      { category: 'Front', price: { amount: 9.99, currency: 'EUR' } },
      { category: 'Standard', price: { amount: 6.99, currency: 'EUR' } },
    ]);
  });
});

describe('nextUnseatedPassenger', () => {
  const passengers = [
    ...booking().passengers,
    { id: 'p3', sequence: 3, firstName: 'Grace', lastName: 'Hopper', type: 'Adult' as const },
  ];

  it('starts with the first passenger without a seat', () => {
    expect(nextUnseatedPassenger(passengers, new Set(['p1']), null)).toBe('p2');
  });

  it('continues after the current passenger and wraps around', () => {
    expect(nextUnseatedPassenger(passengers, new Set(['p2', 'p3']), 'p3')).toBe('p1');
  });

  it('returns null once everyone has a seat', () => {
    expect(nextUnseatedPassenger(passengers, new Set(['p1', 'p2', 'p3']), 'p1')).toBeNull();
  });
});
