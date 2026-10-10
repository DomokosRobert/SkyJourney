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
