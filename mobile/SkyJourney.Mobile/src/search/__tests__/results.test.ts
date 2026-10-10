import { offer } from '@/test/fixtures';
import { applyFilters, isValidReturn, noFilters, priceSteps, sortOffers, timeOfDay } from '../results';

describe('timeOfDay', () => {
  it('uses the airport-local departure time', () => {
    expect(timeOfDay('2026-11-02T06:00:00+01:00')).toBe('morning');
    expect(timeOfDay('2026-11-02T12:00:00-05:00')).toBe('afternoon');
    expect(timeOfDay('2026-11-02T23:59:00+04:00')).toBe('evening');
  });
});

describe('applyFilters', () => {
  const offers = [offer({ dep: '07:00', price: 80 }), offer({ dep: '13:00', price: 120 }), offer({ dep: '19:00', price: 60 })];

  it('returns everything without filters', () => {
    expect(applyFilters(offers, noFilters)).toHaveLength(3);
  });

  it('filters by time of day and maximum price', () => {
    expect(applyFilters(offers, { timesOfDay: ['morning', 'evening'], maxPrice: null }).map((o) => o.departureLocal.slice(11, 16))).toEqual([
      '07:00',
      '19:00',
    ]);
    expect(applyFilters(offers, { timesOfDay: [], maxPrice: 80 })).toHaveLength(2);
  });

  it('excludes sold-out flights when a price filter is set', () => {
    expect(applyFilters([offer({ price: null })], { timesOfDay: [], maxPrice: 1000 })).toHaveLength(0);
  });
});

describe('sortOffers', () => {
  const early = offer({ dep: '06:00', arr: '09:00', price: 150, durationMinutes: 240 });
  const cheap = offer({ dep: '10:00', arr: '11:00', price: 50, durationMinutes: 120 });
  const soldOut = offer({ dep: '05:00', arr: '06:00', price: null, durationMinutes: 60 });
  const all = [early, cheap, soldOut];

  it('sorts by price, departure, arrival and duration with sold-out flights last', () => {
    expect(sortOffers(all, 'price')).toEqual([cheap, early, soldOut]);
    expect(sortOffers(all, 'departure')).toEqual([early, cheap, soldOut]);
    expect(sortOffers(all, 'arrival')).toEqual([early, cheap, soldOut]);
    expect(sortOffers(all, 'duration')).toEqual([cheap, early, soldOut]);
  });

  it('does not mutate its input', () => {
    sortOffers(all, 'price');
    expect(all).toEqual([early, cheap, soldOut]);
  });
});

describe('isValidReturn', () => {
  const outbound = offer({ dep: '07:00', arr: '08:30' }); // lands 08:30Z

  it('requires the minimum stay after the outbound flight lands', () => {
    expect(isValidReturn(outbound, offer({ dep: '11:30' }))).toBe(true); // 10:30Z: exactly 2 h
    expect(isValidReturn(outbound, offer({ dep: '11:00' }))).toBe(false);
    expect(isValidReturn(null, offer({ dep: '01:00' }))).toBe(true);
  });
});

describe('priceSteps', () => {
  it('derives rounded thresholds from the price range', () => {
    expect(priceSteps([offer({ price: 40 }), offer({ price: 100 }), offer({ price: 160 })])).toEqual([80, 120]);
  });

  it('offers no steps when prices barely differ', () => {
    expect(priceSteps([offer({ price: 40 }), offer({ price: 45 })])).toEqual([]);
    expect(priceSteps([offer({ price: 40 })])).toEqual([]);
  });
});
