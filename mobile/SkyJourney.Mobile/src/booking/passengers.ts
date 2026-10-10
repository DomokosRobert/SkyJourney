import { z } from 'zod';

import type { CreateBookingRequest, PassengerType } from '@/api/bookings';
import type { FareSelection } from './BookingDraftContext';

/** Mirrors the API's passenger name rule (1-50 characters after trimming). */
export const PASSENGER_NAME_MAX_LENGTH = 50;

const name = (label: string) =>
  z
    .string()
    .trim()
    .min(1, `Enter the ${label}.`)
    .max(PASSENGER_NAME_MAX_LENGTH, `The ${label} can be at most ${PASSENGER_NAME_MAX_LENGTH} characters.`);

export const passengersSchema = z.object({
  passengers: z.array(
    z.object({
      type: z.enum(['Adult', 'Child']),
      firstName: name('first name'),
      lastName: name('last name'),
    }),
  ),
});

export type PassengersForm = z.infer<typeof passengersSchema>;

/** One empty entry per traveller from the search, adults first; the first adult is pre-filled with the account holder. */
export function initialPassengers(
  adults: number,
  children: number,
  accountHolder?: { firstName: string; lastName: string },
): PassengersForm {
  const entry = (type: PassengerType) => ({ type, firstName: '', lastName: '' });
  const passengers = [
    ...Array.from({ length: adults }, () => entry('Adult')),
    ...Array.from({ length: children }, () => entry('Child')),
  ];
  if (accountHolder && passengers.length > 0) {
    passengers[0] = { type: 'Adult', firstName: accountHolder.firstName, lastName: accountHolder.lastName };
  }
  return { passengers };
}

/** `Adult 1`, `Child 2`: numbered per type, as on a boarding list. */
export function passengerLabel(passengers: { type: PassengerType }[], index: number): string {
  const type = passengers[index]?.type;
  if (!type) {
    return '';
  }
  const number = passengers.slice(0, index + 1).filter((p) => p.type === type).length;
  return `${type} ${number}`;
}

export function toCreateBookingRequest(
  outbound: FareSelection,
  inbound: FareSelection | null,
  form: PassengersForm,
): CreateBookingRequest {
  return {
    outbound: { flightId: outbound.flight.flightId, fareFamily: outbound.fare.family },
    return: inbound ? { flightId: inbound.flight.flightId, fareFamily: inbound.fare.family } : null,
    passengers: form.passengers.map((p) => ({ firstName: p.firstName.trim(), lastName: p.lastName.trim(), type: p.type })),
  };
}
