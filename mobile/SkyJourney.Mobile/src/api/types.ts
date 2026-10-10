/** Wire types of the SkyJourney REST API (`/api/v1`). Dates are ISO strings. */

export type Money = { amount: number; currency: string };

export type Airport = { iataCode: string; name: string; city: string; countryCode: string; timeZoneId: string };

export type FareFamily = 'Basic' | 'Plus' | 'Flex';

export type FareConditions = {
  includesCabinTrolley: boolean;
  checkedBaggageKg: number;
  includesSeatSelection: boolean;
  refundPercentage: number;
};

export type FareOffer = { family: FareFamily; pricePerPassenger: Money; totalPrice: Money; conditions: FareConditions };

export type FlightOffer = {
  flightId: string;
  flightNumber: string;
  airline: { code: string; name: string };
  origin: Airport;
  destination: Airport;
  /** Airport-local time with offset, e.g. `2026-11-02T07:05:00+01:00`. */
  departureLocal: string;
  departureUtc: string;
  arrivalLocal: string;
  arrivalUtc: string;
  durationMinutes: number;
  aircraftName: string;
  isBookable: boolean;
  /** Present only when few seats remain. */
  seatsLeft: number | null;
  lowestPricePerPassenger: Money | null;
  fares: FareOffer[];
};

export type FlightSearchResponse = {
  origin: Airport;
  destination: Airport;
  departureDate: string;
  returnDate: string | null;
  passengers: { adults: number; children: number };
  outbound: FlightOffer[];
  return: FlightOffer[] | null;
  dataSource: string;
};

export type LowestFaresResponse = {
  origin: string;
  destination: string;
  days: { date: string; pricePerPassenger: Money }[];
  dataSource: string;
};
