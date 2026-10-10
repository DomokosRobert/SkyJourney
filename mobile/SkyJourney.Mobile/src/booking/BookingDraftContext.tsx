import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';

import type { FareOffer, FlightOffer } from '@/api/types';
import { initialCriteria, type SearchCriteria } from '@/search/criteria';
import { createIdempotencyKeys, type IdempotencyKeys } from './idempotency';

export type Leg = 'outbound' | 'return';

export type FareSelection = { flight: FlightOffer; fare: FareOffer };

type Draft = {
  criteria: SearchCriteria;
  outbound: FareSelection | null;
  inbound: FareSelection | null;
};

type DraftContextValue = Draft & {
  updateCriteria: (changes: Partial<SearchCriteria>) => void;
  swapAirports: () => void;
  /** Selecting an outbound fare clears any return selection, which may no longer be valid. */
  selectFare: (leg: Leg, selection: FareSelection) => void;
  clearSelection: () => void;
  /**
   * Idempotency keys for creating the booking. They live as long as the app session, so going back and pressing
   * "Continue" again with the same flights and passengers returns the booking already created instead of a second one.
   */
  createBookingKeys: IdempotencyKeys;
};

const DraftContext = createContext<DraftContextValue | null>(null);

/**
 * Client-side draft of the trip being planned: search criteria plus the selected flights and fares.
 * Prices shown from here are indicative; the API recomputes everything when the booking is created.
 */
export function BookingDraftProvider({ children }: { children: ReactNode }) {
  const [draft, setDraft] = useState<Draft>({ criteria: initialCriteria, outbound: null, inbound: null });
  const [createBookingKeys] = useState(() => createIdempotencyKeys());

  const value = useMemo<DraftContextValue>(
    () => ({
      ...draft,
      updateCriteria: (changes) =>
        setDraft((d) => {
          const criteria = { ...d.criteria, ...changes };
          if (criteria.returnDate && criteria.departureDate && criteria.returnDate < criteria.departureDate) {
            criteria.returnDate = null;
          }
          return { criteria, outbound: null, inbound: null };
        }),
      swapAirports: () =>
        setDraft((d) => ({
          criteria: { ...d.criteria, origin: d.criteria.destination, destination: d.criteria.origin },
          outbound: null,
          inbound: null,
        })),
      selectFare: (leg, selection) =>
        setDraft((d) =>
          leg === 'outbound' ? { ...d, outbound: selection, inbound: null } : { ...d, inbound: selection },
        ),
      clearSelection: () =>
        setDraft((d) => (d.outbound || d.inbound ? { ...d, outbound: null, inbound: null } : d)),
      createBookingKeys,
    }),
    [draft, createBookingKeys],
  );

  return <DraftContext.Provider value={value}>{children}</DraftContext.Provider>;
}

export function useBookingDraft(): DraftContextValue {
  const context = useContext(DraftContext);
  if (!context) {
    throw new Error('useBookingDraft must be used inside BookingDraftProvider');
  }
  return context;
}
