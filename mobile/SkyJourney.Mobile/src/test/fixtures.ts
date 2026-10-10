import type { AuthResponse, UserProfile } from '@/api/auth';
import type { Booking, SeatMap, SeatMapSeat } from '@/api/bookings';
import type { Airport, FlightOffer } from '@/api/types';

export const BUD: Airport = { iataCode: 'BUD', name: 'Budapest Ferenc Liszt International', city: 'Budapest', countryCode: 'HU', timeZoneId: 'Europe/Budapest' };
export const LHR: Airport = { iataCode: 'LHR', name: 'London Heathrow', city: 'London', countryCode: 'GB', timeZoneId: 'Europe/London' };

const conditions = { includesCabinTrolley: false, checkedBaggageKg: 0, includesSeatSelection: false, refundPercentage: 0 };

/** Builds a flight offer; times are given as local `HH:MM` on 2026-11-02 with a +01:00 offset. */
export function offer(overrides: Partial<FlightOffer> & { dep?: string; arr?: string; price?: number | null } = {}): FlightOffer {
  const { dep = '07:00', arr = '08:30', price = 100, ...rest } = overrides;
  const departureLocal = `2026-11-02T${dep}:00+01:00`;
  const arrivalLocal = `2026-11-02T${arr}:00+00:00`;
  return {
    flightId: `id-${dep}-${price}`,
    flightNumber: 'SJ100',
    airline: { code: 'SJ', name: 'SkyJourney Air' },
    origin: BUD,
    destination: LHR,
    departureLocal,
    departureUtc: new Date(departureLocal).toISOString(),
    arrivalLocal,
    arrivalUtc: new Date(arrivalLocal).toISOString(),
    durationMinutes: 150,
    aircraftName: 'Airbus A320',
    isBookable: price !== null,
    seatsLeft: null,
    lowestPricePerPassenger: price === null ? null : { amount: price, currency: 'EUR' },
    fares:
      price === null
        ? []
        : [{ family: 'Basic', pricePerPassenger: { amount: price, currency: 'EUR' }, totalPrice: { amount: price, currency: 'EUR' }, conditions }],
    ...rest,
  };
}

export const TRAVELLER: UserProfile = {
  id: '0199a1b2-0000-7000-8000-000000000001',
  email: 'ada@example.com',
  firstName: 'Ada',
  lastName: 'Lovelace',
  roles: [],
  createdAt: '2026-10-01T10:00:00+00:00',
};

/** An AuthResponse whose tokens carry a sequence number, so rotations are distinguishable. */
export function authResponse(n: number, user: UserProfile = TRAVELLER): AuthResponse {
  return {
    accessToken: `access-${n}`,
    accessTokenExpiresAt: '2026-10-10T12:15:00+00:00',
    refreshToken: `refresh-${n}`,
    refreshTokenExpiresAt: '2026-11-09T12:00:00+00:00',
    user,
  };
}

const eur = (amount: number) => ({ amount, currency: 'EUR' });

/** Builds an unpaid one-way booking for two adults, BUD → LHR. */
export function booking(overrides: Partial<Booking> = {}): Booking {
  const flight = offer({ dep: '09:00', arr: '10:30' });
  return {
    id: 'booking-1',
    reference: 'SJ7K2Q',
    status: 'PendingPayment',
    createdAt: '2026-10-10T12:00:00+00:00',
    expiresAt: '2026-10-10T12:20:00+00:00',
    confirmedAt: null,
    cancelledAt: null,
    totalPrice: eur(200),
    refundedAmount: eur(0),
    passengers: [
      { id: 'p1', sequence: 1, firstName: 'Ada', lastName: 'Lovelace', type: 'Adult' },
      { id: 'p2', sequence: 2, firstName: 'Charles', lastName: 'Babbage', type: 'Adult' },
    ],
    segments: [
      {
        id: 'seg-1',
        direction: 'Outbound',
        flight: { ...flight, status: 'Scheduled' },
        fareFamily: 'Basic',
        farePricePerPassenger: eur(100),
        fareConditions: conditions,
        seats: [],
      },
    ],
    dataSource: 'Mock',
    ...overrides,
  };
}

/** A two-row, four-abreast seat map (`AB CD`); row 1 is Front, 1B is taken. */
export function seatMap(overrides: Partial<SeatMap> = {}): SeatMap {
  const seats: SeatMapSeat[] = [1, 2].flatMap((row) =>
    ['A', 'B', 'C', 'D'].map((letter) => ({
      seatNumber: `${row}${letter}`,
      row,
      letter,
      category: row === 1 ? 'Front' : 'Standard',
      price: eur(row === 1 ? 9.99 : 6.99),
      availability: row === 1 && letter === 'B' ? 'Occupied' : 'Available',
      passengerId: null,
    })),
  );
  return { segmentId: 'seg-1', flightNumber: 'SJ100', aircraftName: 'Airbus A320', rows: 2, seatLetters: 'ABCD', seats, ...overrides };
}
