import { offer } from '@/test/fixtures';
import { initialPassengers, passengerLabel, passengersSchema, toCreateBookingRequest } from '../passengers';

describe('passengers', () => {
  it('lists adults first and pre-fills the first adult with the account holder', () => {
    const { passengers } = initialPassengers(2, 1, { firstName: 'Ada', lastName: 'Lovelace' });

    expect(passengers.map((p) => p.type)).toEqual(['Adult', 'Adult', 'Child']);
    expect(passengers[0]).toMatchObject({ firstName: 'Ada', lastName: 'Lovelace' });
    expect(passengers[1]).toMatchObject({ firstName: '', lastName: '' });
  });

  it('numbers passengers per type', () => {
    const { passengers } = initialPassengers(2, 2);

    expect(passengers.map((_, i) => passengerLabel(passengers, i))).toEqual(['Adult 1', 'Adult 2', 'Child 1', 'Child 2']);
  });

  it('requires trimmed names of at most 50 characters', () => {
    const result = passengersSchema.safeParse({
      passengers: [{ type: 'Adult', firstName: '   ', lastName: 'x'.repeat(51) }],
    });

    expect(result.success).toBe(false);
    expect(result.error?.issues.map((i) => i.message)).toEqual([
      'Enter the first name.',
      'The last name can be at most 50 characters.',
    ]);
  });

  it('builds the API request with trimmed names and both legs', () => {
    const outbound = offer({ dep: '07:00' });
    const inbound = offer({ dep: '18:00' });
    const fare = outbound.fares[0]!;

    const request = toCreateBookingRequest(
      { flight: outbound, fare },
      { flight: inbound, fare },
      { passengers: [{ type: 'Adult', firstName: ' Ada ', lastName: ' Lovelace' }] },
    );

    expect(request).toEqual({
      outbound: { flightId: outbound.flightId, fareFamily: 'Basic' },
      return: { flightId: inbound.flightId, fareFamily: 'Basic' },
      passengers: [{ firstName: 'Ada', lastName: 'Lovelace', type: 'Adult' }],
    });
  });
});
