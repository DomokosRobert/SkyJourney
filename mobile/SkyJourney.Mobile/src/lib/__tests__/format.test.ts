import { addDays, dayOffset, formatDate, formatDuration, formatLocalTime, formatMoney } from '../format';

describe('format helpers', () => {
  it('reads airport-local wall-clock time without converting to the device time zone', () => {
    expect(formatLocalTime('2026-11-02T07:05:00+01:00')).toBe('07:05');
    expect(formatLocalTime('2026-11-02T23:55:00-05:00')).toBe('23:55');
  });

  it('formats dates with weekday', () => {
    expect(formatDate('2026-11-02')).toBe('Mon 2 Nov');
    expect(formatDate('2026-12-31T22:10:00+02:00')).toBe('Thu 31 Dec');
  });

  it('computes overnight day offset from local dates', () => {
    expect(dayOffset('2026-11-02T22:30:00+00:00', '2026-11-03T04:10:00+04:00')).toBe(1);
    expect(dayOffset('2026-11-02T07:00:00+01:00', '2026-11-02T08:30:00+00:00')).toBe(0);
  });

  it('formats durations', () => {
    expect(formatDuration(45)).toBe('45m');
    expect(formatDuration(120)).toBe('2h');
    expect(formatDuration(140)).toBe('2h 20m');
  });

  it('formats money', () => {
    expect(formatMoney({ amount: 89.5, currency: 'EUR' })).toBe('€89.50');
  });

  it('adds days across month and year boundaries', () => {
    expect(addDays('2026-12-30', 3)).toBe('2027-01-02');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
  });

  it('rejects malformed input', () => {
    expect(() => formatLocalTime('yesterday')).toThrow();
  });
});
