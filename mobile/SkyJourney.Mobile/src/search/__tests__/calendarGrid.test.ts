import { compactPrice, lastDay, monthGrid, monthLabel, monthsBetween } from '../calendarGrid';

describe('calendar grid', () => {
  it('lays out a month Monday-first, padded to whole weeks', () => {
    const weeks = monthGrid({ year: 2026, month: 11 }); // 1 Nov 2026 is a Sunday
    expect(weeks[0]).toEqual([null, null, null, null, null, null, '2026-11-01']);
    expect(weeks.at(-1)).toEqual(['2026-11-30', null, null, null, null, null, null]);
    expect(weeks.flat().filter(Boolean)).toHaveLength(30);
  });

  it('handles leap years', () => {
    expect(lastDay({ year: 2028, month: 2 })).toBe('2028-02-29');
    expect(lastDay({ year: 2027, month: 2 })).toBe('2027-02-28');
  });

  it('lists months across a year boundary', () => {
    expect(monthsBetween('2026-11-15', '2027-02-01').map(monthLabel)).toEqual([
      'November 2026',
      'December 2026',
      'January 2027',
      'February 2027',
    ]);
  });

  it('rounds compact prices up', () => {
    expect(compactPrice(89.01, 'EUR')).toBe('€90');
    expect(compactPrice(12, 'HUF')).toBe('12 HUF');
  });
});
