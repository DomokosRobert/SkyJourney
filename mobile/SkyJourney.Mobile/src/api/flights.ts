import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { apiRequest, toQueryString } from './client';
import type { Airport, FlightOffer, FlightSearchResponse, LowestFaresResponse } from './types';

export type FlightSearchParams = {
  origin: string;
  destination: string;
  departureDate: string;
  returnDate?: string | null;
  adults: number;
  children: number;
};

export const flightKeys = {
  airports: (query: string) => ['airports', query] as const,
  destinations: (origin: string) => ['airports', origin, 'destinations'] as const,
  search: (params: FlightSearchParams) => ['flights', 'search', params] as const,
  lowestFares: (origin: string, destination: string, from: string, to: string) =>
    ['flights', 'lowest-fares', origin, destination, from, to] as const,
  flight: (flightId: string, passengers: number) => ['flights', flightId, passengers] as const,
};

export function useAirportSearch(query: string) {
  return useQuery({
    queryKey: flightKeys.airports(query),
    queryFn: ({ signal }) => apiRequest<Airport[]>(`/api/v1/airports?${toQueryString({ query, limit: 30 })}`, { signal }),
    placeholderData: keepPreviousData,
    staleTime: 10 * 60_000,
  });
}

export function useDestinations(origin: string | undefined) {
  return useQuery({
    queryKey: flightKeys.destinations(origin ?? ''),
    queryFn: ({ signal }) => apiRequest<Airport[]>(`/api/v1/airports/${origin}/destinations`, { signal }),
    enabled: !!origin,
    staleTime: 10 * 60_000,
  });
}

export function useFlightSearch(params: FlightSearchParams | null) {
  return useQuery({
    queryKey: params ? flightKeys.search(params) : ['flights', 'search', 'idle'],
    queryFn: ({ signal }) =>
      apiRequest<FlightSearchResponse>(`/api/v1/flights/search?${toQueryString({ ...params })}`, { signal }),
    enabled: !!params,
    // Availability and prices change; keep results fresh but avoid refetching on every focus.
    staleTime: 30_000,
  });
}

export function useLowestFares(origin: string | undefined, destination: string | undefined, from: string, to: string) {
  return useQuery({
    queryKey: flightKeys.lowestFares(origin ?? '', destination ?? '', from, to),
    queryFn: ({ signal }) =>
      apiRequest<LowestFaresResponse>(
        `/api/v1/flights/lowest-fares?${toQueryString({ origin, destination, from, to })}`,
        { signal },
      ),
    enabled: !!origin && !!destination,
    staleTime: 5 * 60_000,
    retry: false,
  });
}

export function useFlight(flightId: string, passengers: number) {
  return useQuery({
    queryKey: flightKeys.flight(flightId, passengers),
    queryFn: ({ signal }) =>
      apiRequest<FlightOffer>(`/api/v1/flights/${encodeURIComponent(flightId)}?passengers=${passengers}`, { signal }),
    staleTime: 15_000,
  });
}
