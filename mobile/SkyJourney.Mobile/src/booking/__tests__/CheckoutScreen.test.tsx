import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { router } from 'expo-router';

import { bookingsApi, IDEMPOTENCY_HEADER, type Booking, type PaymentResult } from '@/api/bookings';
import { ApiError } from '@/api/client';
import CheckoutScreen from '@/app/booking/[reference]/checkout';
import { booking } from '@/test/fixtures';

jest.mock('expo-router', () => ({
  router: { replace: jest.fn(), push: jest.fn(), dismissAll: jest.fn() },
  useLocalSearchParams: () => ({ reference: 'SJ7K2Q' }),
  Redirect: () => null,
}));

let mockUuid = 0;
jest.mock('expo-crypto', () => ({ randomUUID: () => `uuid-${++mockUuid}` }));

const pending = () => booking({ expiresAt: new Date(Date.now() + 15 * 60_000).toISOString() });

function result(status: PaymentResult['status'], updated: Booking, failureMessage: string | null = null): PaymentResult {
  return { paymentId: 'pay-1', status, amount: updated.totalPrice, failureCode: null, failureMessage, booking: updated };
}

function renderCheckout() {
  // gcTime: Infinity schedules no cache timers, so Jest can exit once the tests finish.
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { gcTime: Infinity } },
  });
  return render(
    <QueryClientProvider client={client}>
      <CheckoutScreen />
    </QueryClientProvider>,
  );
}

const payButton = () => screen.getByRole('button', { name: /^Pay / });
const keysUsed = (pay: jest.SpyInstance) => pay.mock.calls.map((call) => call[2] as string);

describe('CheckoutScreen', () => {
  let get: jest.SpyInstance;
  let pay: jest.SpyInstance;

  beforeEach(() => {
    get = jest.spyOn(bookingsApi, 'get').mockImplementation(async () => pending());
    pay = jest.spyOn(bookingsApi, 'pay');
  });
  afterEach(() => jest.restoreAllMocks());

  it('shows the server total and pays with the chosen test method', async () => {
    pay.mockResolvedValue(result('Succeeded', { ...pending(), status: 'Confirmed', expiresAt: null }));
    await renderCheckout();

    expect(await screen.findByText(/Demo checkout/)).toBeOnTheScreen();
    expect(payButton()).toHaveTextContent('Pay €200.00');
    await fireEvent.press(payButton());

    await waitFor(() =>
      expect(router.replace).toHaveBeenCalledWith({
        pathname: '/booking/[reference]/confirmation',
        params: { reference: 'SJ7K2Q' },
      }),
    );
    expect(pay).toHaveBeenCalledWith('SJ7K2Q', 'sim_success', expect.any(String));
  });

  it('keeps the booking payable after a decline and uses a new key for the next attempt', async () => {
    pay
      .mockResolvedValueOnce(result('Failed', pending(), 'Your card was declined.'))
      .mockResolvedValueOnce(result('Succeeded', { ...pending(), status: 'Confirmed', expiresAt: null }));
    await renderCheckout();

    await fireEvent.press(await screen.findByRole('radio', { name: /declined/ }));
    await fireEvent.press(payButton());
    expect(await screen.findByText('Your card was declined.')).toBeOnTheScreen();

    await fireEvent.press(screen.getByRole('radio', { name: /approved/ }));
    await fireEvent.press(payButton());

    await waitFor(() => expect(router.replace).toHaveBeenCalled());
    expect(pay.mock.calls.map((call) => call[1])).toEqual(['sim_declined', 'sim_success']);
    expect(new Set(keysUsed(pay)).size).toBe(2);
  });

  it('retries with the same key when the response was lost', async () => {
    pay
      .mockRejectedValueOnce(new ApiError('Network request failed', 0, 'network_error'))
      .mockResolvedValueOnce(result('Succeeded', { ...pending(), status: 'Confirmed', expiresAt: null }));
    await renderCheckout();

    await fireEvent.press(await screen.findByRole('button', { name: /^Pay / }));
    expect(await screen.findByText('Network request failed')).toBeOnTheScreen();
    await fireEvent.press(payButton());

    await waitFor(() => expect(router.replace).toHaveBeenCalled());
    const [first, second] = keysUsed(pay);
    expect(first).toBe(second);
    expect(IDEMPOTENCY_HEADER).toBe('Idempotency-Key');
  });

  it('explains an expired hold instead of offering payment', async () => {
    pay.mockRejectedValue(new ApiError('expired', 409, 'booking_expired'));
    await renderCheckout();
    await screen.findByRole('button', { name: /^Pay / });
    get.mockImplementation(async () => booking({ status: 'Expired', expiresAt: null }));

    await fireEvent.press(payButton());

    expect(await screen.findByText('Your hold has expired')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: /^Pay / })).toBeNull();
  });
});
