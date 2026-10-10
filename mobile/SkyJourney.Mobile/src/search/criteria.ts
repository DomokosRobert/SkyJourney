import { z } from 'zod';

import type { FlightSearchParams } from '@/api/flights';
import type { Airport } from '@/api/types';

export const MAX_PASSENGERS = 9;

export type TripType = 'oneWay' | 'return';

export type SearchCriteria = {
  tripType: TripType;
  origin: Airport | null;
  destination: Airport | null;
  departureDate: string | null;
  returnDate: string | null;
  adults: number;
  children: number;
};

export const initialCriteria: SearchCriteria = {
  tripType: 'return',
  origin: null,
  destination: null,
  departureDate: null,
  returnDate: null,
  adults: 1,
  children: 0,
};

const airport = z.object({ iataCode: z.string().length(3) }, { error: 'Choose an airport.' });

/** Mirrors the API's search validation so users get immediate feedback. */
export const searchCriteriaSchema = z
  .object({
    tripType: z.enum(['oneWay', 'return']),
    origin: airport.nullable().refine((a) => a !== null, 'Choose where you are flying from.'),
    destination: airport.nullable().refine((a) => a !== null, 'Choose where you are flying to.'),
    departureDate: z.string().nullable().refine((d) => d !== null, 'Choose a departure date.'),
    returnDate: z.string().nullable(),
    adults: z.number().int().min(1, 'At least one adult must travel.').max(MAX_PASSENGERS),
    children: z.number().int().min(0).max(MAX_PASSENGERS - 1),
  })
  .superRefine((c, ctx) => {
    if (c.origin && c.destination && c.origin.iataCode === c.destination.iataCode) {
      ctx.addIssue({ code: 'custom', path: ['destination'], message: 'Origin and destination must differ.' });
    }
    if (c.tripType === 'return') {
      if (!c.returnDate) {
        ctx.addIssue({ code: 'custom', path: ['returnDate'], message: 'Choose a return date.' });
      } else if (c.departureDate && c.returnDate < c.departureDate) {
        ctx.addIssue({ code: 'custom', path: ['returnDate'], message: 'Return must be on or after departure.' });
      }
    }
    if (c.adults + c.children > MAX_PASSENGERS) {
      ctx.addIssue({ code: 'custom', path: ['children'], message: `At most ${MAX_PASSENGERS} passengers per booking.` });
    }
  });

export type CriteriaErrors = Partial<Record<keyof SearchCriteria, string>>;

export function validateCriteria(
  criteria: SearchCriteria,
): { ok: true; params: FlightSearchParams } | { ok: false; errors: CriteriaErrors } {
  const result = searchCriteriaSchema.safeParse(criteria);
  if (!result.success) {
    const errors: CriteriaErrors = {};
    for (const issue of result.error.issues) {
      const key = issue.path[0] as keyof SearchCriteria;
      errors[key] ??= issue.message;
    }
    return { ok: false, errors };
  }

  return { ok: true, params: toSearchParams(criteria) };
}

export function toSearchParams(c: SearchCriteria): FlightSearchParams {
  return {
    origin: c.origin!.iataCode,
    destination: c.destination!.iataCode,
    departureDate: c.departureDate!,
    returnDate: c.tripType === 'return' ? c.returnDate : null,
    adults: c.adults,
    children: c.children,
  };
}

export function passengerSummary(adults: number, children: number): string {
  const parts = [`${adults} adult${adults === 1 ? '' : 's'}`];
  if (children > 0) {
    parts.push(`${children} child${children === 1 ? '' : 'ren'}`);
  }
  return parts.join(', ');
}
