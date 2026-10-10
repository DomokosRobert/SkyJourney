import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiRequest } from './client';
import type { Airport, FareConditions, FareFamily, Money } from './types';

/** Wire types mirroring the API's booking DTOs. Prices always come from the server. */

export type BookingStatus = 'PendingPayment' | 'Confirmed' | 'Cancelled' | 'Expired';
export type PassengerType = 'Adult' | 'Child';
export type PaymentStatus = 'Pending' | 'Succeeded' | 'Failed' | 'PartiallyRefunded' | 'Refunded';
export type SeatCategory = 'Standard' | 'Front' | 'ExtraLegroom';
export type SeatAvailability = 'Available' | 'Occupied' | 'Selected';

export type Passenger = { id: string; sequence: number; firstName: string; lastName: string; type: PassengerType };

export type SeatAssignment = { passengerId: string; seatNumber: string; price: Money };

export type SegmentFlight = {
  flightId: string;
  flightNumber: string;
  airline: { code: string; name: string };
  origin: Airport;
  destination: Airport;
  departureLocal: string;
  departureUtc: string;
  arrivalLocal: string;
  arrivalUtc: string;
  durationMinutes: number;
  aircraftName: string;
  status: 'Scheduled' | 'Cancelled';
};

export type BookingSegment = {
  id: string;
  direction: 'Outbound' | 'Return';
  flight: SegmentFlight;
  fareFamily: FareFamily;
  farePricePerPassenger: Money;
  fareConditions: FareConditions;
  seats: SeatAssignment[];
};

export type Booking = {
  id: string;
  reference: string;
  status: BookingStatus;
  createdAt: string;
  /** End of the payment hold; null unless the booking awaits payment. */
  expiresAt: string | null;
  confirmedAt: string | null;
  cancelledAt: string | null;
  totalPrice: Money;
  refundedAmount: Money;
  passengers: Passenger[];
  segments: BookingSegment[];
  dataSource: string;
};

export type SeatMapSeat = {
  seatNumber: string;
  row: number;
  letter: string;
  category: SeatCategory;
  price: Money;
  availability: SeatAvailability;
  passengerId: string | null;
};

export type SeatMap = {
  segmentId: string;
  flightNumber: string;
  aircraftName: string;
  rows: number;
  seatLetters: string;
  seats: SeatMapSeat[];
};

export type PaymentResult = {
  paymentId: string;
  status: PaymentStatus;
  amount: Money;
  failureCode: string | null;
  failureMessage: string | null;
  booking: Booking;
};

export type BookingSummary = {
  id: string;
  reference: string;
  status: BookingStatus;
  createdAt: string;
  totalPrice: Money;
  passengerCount: number;
  origin: Airport;
  destination: Airport;
  departureLocal: string;
  departureUtc: string;
  returnDepartureLocal: string | null;
  /** Not cancelled or expired, and the first flight has not departed yet. */
  isUpcoming: boolean;
};

export type PagedResult<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number };

export type CancellationQuote = { canCancel: boolean; refund: Money | null; reasonCode: string | null; reason: string | null };

export type CreateBookingRequest = {
  outbound: { flightId: string; fareFamily: FareFamily };
  return: { flightId: string; fareFamily: FareFamily } | null;
  passengers: { firstName: string; lastName: string; type: PassengerType }[];
};

export const IDEMPOTENCY_HEADER = 'Idempotency-Key';

const path = (reference: string) => `/api/v1/bookings/${encodeURIComponent(reference)}`;

export const bookingsApi = {
  create: (body: CreateBookingRequest, idempotencyKey: string) =>
    apiRequest<Booking>('/api/v1/bookings', { method: 'POST', body, headers: { [IDEMPOTENCY_HEADER]: idempotencyKey } }),
  list: (page: number, pageSize: number, signal?: AbortSignal) =>
    apiRequest<PagedResult<BookingSummary>>(`/api/v1/bookings?page=${page}&pageSize=${pageSize}`, { signal }),
  get: (reference: string, signal?: AbortSignal) => apiRequest<Booking>(path(reference), { signal }),
  seatMap: (reference: string, segmentId: string, signal?: AbortSignal) =>
    apiRequest<SeatMap>(`${path(reference)}/segments/${segmentId}/seat-map`, { signal }),
  selectSeats: (reference: string, segmentId: string, seats: { passengerId: string; seatNumber: string }[]) =>
    apiRequest<Booking>(`${path(reference)}/segments/${segmentId}/seats`, { method: 'PUT', body: { seats } }),
  pay: (reference: string, paymentToken: string, idempotencyKey: string) =>
    apiRequest<PaymentResult>(`${path(reference)}/payments`, {
      method: 'POST',
      body: { paymentToken },
      headers: { [IDEMPOTENCY_HEADER]: idempotencyKey },
    }),
  cancellationQuote: (reference: string, signal?: AbortSignal) =>
    apiRequest<CancellationQuote>(`${path(reference)}/cancellation`, { signal }),
  cancel: (reference: string) => apiRequest<Booking>(`${path(reference)}/cancel`, { method: 'POST' }),
};

export const bookingKeys = {
  all: ['bookings'] as const,
  list: () => ['bookings', 'list'] as const,
  detail: (reference: string) => ['bookings', 'detail', reference] as const,
  seatMap: (reference: string, segmentId: string) => ['bookings', 'detail', reference, 'seat-map', segmentId] as const,
  cancellation: (reference: string) => ['bookings', 'detail', reference, 'cancellation'] as const,
};

export const BOOKINGS_PAGE_SIZE = 20;

/** The signed-in user's bookings, newest first, loaded page by page. */
export function useMyBookings(enabled: boolean) {
  return useInfiniteQuery({
    queryKey: bookingKeys.list(),
    queryFn: ({ pageParam, signal }) => bookingsApi.list(pageParam, BOOKINGS_PAGE_SIZE, signal),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
    enabled,
  });
}

export function useBooking(reference: string) {
  return useQuery({
    queryKey: bookingKeys.detail(reference),
    queryFn: ({ signal }) => bookingsApi.get(reference, signal),
  });
}

export function useSeatMap(reference: string, segmentId: string | undefined) {
  return useQuery({
    queryKey: bookingKeys.seatMap(reference, segmentId ?? ''),
    queryFn: ({ signal }) => bookingsApi.seatMap(reference, segmentId!, signal),
    enabled: !!segmentId,
    // Other customers take seats; refresh whenever the screen asks again.
    staleTime: 0,
  });
}

/** Stores a booking returned by a mutation, so screens showing it update without a refetch. */
function useStoreBooking() {
  const queryClient = useQueryClient();
  return (booking: Booking) => {
    queryClient.setQueryData(bookingKeys.detail(booking.reference), booking);
  };
}

export function useSelectSeat(reference: string, segmentId: string) {
  const queryClient = useQueryClient();
  const store = useStoreBooking();
  return useMutation({
    mutationFn: (seat: { passengerId: string; seatNumber: string }) => bookingsApi.selectSeats(reference, segmentId, [seat]),
    onSuccess: store,
    // Success or conflict, the seat map changed (or was stale): reload it.
    onSettled: () => queryClient.invalidateQueries({ queryKey: bookingKeys.seatMap(reference, segmentId) }),
  });
}

/** Whether the booking can be cancelled now, and the refund. Always fresh: it depends on the clock and the fare. */
export function useCancellationQuote(reference: string, enabled: boolean) {
  return useQuery({
    queryKey: bookingKeys.cancellation(reference),
    queryFn: ({ signal }) => bookingsApi.cancellationQuote(reference, signal),
    enabled,
    staleTime: 0,
  });
}

export function useCancelBooking(reference: string) {
  const queryClient = useQueryClient();
  const store = useStoreBooking();
  return useMutation({
    mutationFn: () => bookingsApi.cancel(reference),
    onSuccess: (booking) => {
      store(booking);
      queryClient.removeQueries({ queryKey: bookingKeys.cancellation(reference) });
      void queryClient.invalidateQueries({ queryKey: bookingKeys.list() });
    },
    // A refused cancellation (cutoff passed, payment running) may mean the booking changed: reload it.
    onError: () => queryClient.invalidateQueries({ queryKey: bookingKeys.detail(reference) }),
  });
}

export function usePayBooking(reference: string) {
  const queryClient = useQueryClient();
  const store = useStoreBooking();
  return useMutation({
    mutationFn: ({ paymentToken, idempotencyKey }: { paymentToken: string; idempotencyKey: string }) =>
      bookingsApi.pay(reference, paymentToken, idempotencyKey),
    onSuccess: (result) => {
      store(result.booking);
      void queryClient.invalidateQueries({ queryKey: bookingKeys.all, refetchType: 'none' });
    },
    // A rejected payment may mean the booking expired or was paid elsewhere: reload it so the screen reflects that.
    onError: () => queryClient.invalidateQueries({ queryKey: bookingKeys.detail(reference) }),
  });
}
