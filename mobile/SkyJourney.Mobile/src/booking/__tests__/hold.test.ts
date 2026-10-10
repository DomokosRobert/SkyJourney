import { formatCountdown, holdRemainingMs } from '../hold';

describe('hold', () => {
  const expiresAt = '2026-10-10T12:20:00Z';

  it('counts down to the end of the hold and never below zero', () => {
    expect(holdRemainingMs(expiresAt, Date.parse('2026-10-10T12:05:30Z'))).toBe(14.5 * 60_000);
    expect(holdRemainingMs(expiresAt, Date.parse('2026-10-10T12:21:00Z'))).toBe(0);
  });

  it('formats minutes and seconds, rounding partial seconds up', () => {
    expect(formatCountdown(14.5 * 60_000)).toBe('14:30');
    expect(formatCountdown(4_001)).toBe('0:05');
    expect(formatCountdown(0)).toBe('0:00');
  });
});
