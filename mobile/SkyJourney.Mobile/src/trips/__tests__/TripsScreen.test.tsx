import { fireEvent, screen } from '@testing-library/react-native';
import { router } from 'expo-router';

import { bookingsApi, type BookingSummary, type PagedResult } from '@/api/bookings';
import TripsScreen from '@/app/(tabs)/trips';
import { BUD, LHR } from '@/test/fixtures';
import { renderWithProviders } from '@/test/renderWithProviders';

jest.mock('expo-router', () => ({ router: { push: jest.fn(), navigate: jest.fn() } }));

function summary(reference: string, overrides: Partial<BookingSummary> = {}): BookingSummary {
  return {
    id: reference,
    reference,
    status: 'Confirmed',
    createdAt: '2026-10-10T12:00:00+00:00',
    totalPrice: { amount: 200, currency: 'EUR' },
    passengerCount: 2,
    origin: BUD,
    destination: LHR,
    departureLocal: '2026-11-02T09:00:00+01:00',
    departureUtc: '2026-11-02T08:00:00Z',
    returnDepartureLocal: null,
    isUpcoming: true,
    ...overrides,
  };
}

const page = (items: BookingSummary[], n = 1, totalPages = 1): PagedResult<BookingSummary> => ({
  items,
  page: n,
  pageSize: 20,
  totalCount: items.length,
  totalPages,
});

describe('TripsScreen', () => {
  afterEach(() => jest.restoreAllMocks());

  it('asks signed-out users to sign in', async () => {
    const list = jest.spyOn(bookingsApi, 'list');
    await renderWithProviders(<TripsScreen />, { signedIn: false });

    await fireEvent.press(await screen.findByRole('button', { name: 'Sign in' }));

    expect(router.push).toHaveBeenCalledWith('/auth/login');
    expect(list).not.toHaveBeenCalled();
  });

  it('lists bookings with their status and opens one', async () => {
    jest.spyOn(bookingsApi, 'list').mockResolvedValue(
      page([
        summary('SJAAAA', { status: 'PendingPayment' }),
        summary('SJBBBB', { status: 'Confirmed', isUpcoming: false }),
        summary('SJCCCC', { status: 'Cancelled', isUpcoming: false }),
      ]),
    );
    await renderWithProviders(<TripsScreen />);

    expect(await screen.findByText('Awaiting payment')).toBeOnTheScreen();
    expect(screen.getByText('Completed')).toBeOnTheScreen();
    expect(screen.getByText('Cancelled')).toBeOnTheScreen();

    await fireEvent.press(screen.getByLabelText(/booking SJBBBB/));
    expect(router.push).toHaveBeenCalledWith({ pathname: '/trips/[reference]', params: { reference: 'SJBBBB' } });
  });

  it('offers a search when there are no bookings', async () => {
    jest.spyOn(bookingsApi, 'list').mockResolvedValue(page([]));
    await renderWithProviders(<TripsScreen />);

    expect(await screen.findByText('No trips yet')).toBeOnTheScreen();
  });

  it('shows an error with a retry when loading fails', async () => {
    const list = jest
      .spyOn(bookingsApi, 'list')
      .mockRejectedValueOnce(new Error('boom'))
      .mockResolvedValueOnce(page([summary('SJAAAA')]));
    await renderWithProviders(<TripsScreen />);

    await fireEvent.press(await screen.findByRole('button', { name: /try again/i }));

    expect(await screen.findByText('Confirmed')).toBeOnTheScreen();
    expect(list).toHaveBeenCalledTimes(2);
  });
});
