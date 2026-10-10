import { BUD, LHR } from '@/test/fixtures';
import { initialCriteria, passengerSummary, validateCriteria, type SearchCriteria } from '../criteria';

const complete: SearchCriteria = {
  ...initialCriteria,
  origin: BUD,
  destination: LHR,
  departureDate: '2026-11-02',
  returnDate: '2026-11-09',
};

describe('validateCriteria', () => {
  it('produces API search params for a complete return search', () => {
    expect(validateCriteria(complete)).toEqual({
      ok: true,
      params: { origin: 'BUD', destination: 'LHR', departureDate: '2026-11-02', returnDate: '2026-11-09', adults: 1, children: 0 },
    });
  });

  it('drops the return date for one-way trips', () => {
    const result = validateCriteria({ ...complete, tripType: 'oneWay' });
    expect(result.ok && result.params.returnDate).toBeNull();
  });

  it('reports every missing field', () => {
    const result = validateCriteria(initialCriteria);
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(Object.keys(result.errors).sort()).toEqual(['departureDate', 'destination', 'origin', 'returnDate']);
    }
  });

  it('rejects identical airports, a return before departure and too many passengers', () => {
    const result = validateCriteria({ ...complete, destination: BUD, returnDate: '2026-11-01', adults: 6, children: 4 });
    expect(result).toEqual({
      ok: false,
      errors: {
        destination: 'Origin and destination must differ.',
        returnDate: 'Return must be on or after departure.',
        children: 'At most 9 passengers per booking.',
      },
    });
  });
});

describe('passengerSummary', () => {
  it('pluralises', () => {
    expect(passengerSummary(1, 0)).toBe('1 adult');
    expect(passengerSummary(2, 1)).toBe('2 adults, 1 child');
    expect(passengerSummary(1, 3)).toBe('1 adult, 3 children');
  });
});
