import { statusPresentation } from '../status';

describe('statusPresentation', () => {
  it('names each booking status', () => {
    expect(statusPresentation('PendingPayment', true).label).toBe('Awaiting payment');
    expect(statusPresentation('Confirmed', true).label).toBe('Confirmed');
    expect(statusPresentation('Cancelled', true).label).toBe('Cancelled');
    expect(statusPresentation('Expired', false).label).toBe('Expired');
  });

  it('shows a confirmed trip that has departed as completed', () => {
    expect(statusPresentation('Confirmed', false).label).toBe('Completed');
  });
});
