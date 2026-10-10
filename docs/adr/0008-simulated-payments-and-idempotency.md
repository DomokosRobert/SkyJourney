# ADR-0008: Simulated payments and idempotency keys

## Context
The app needs a realistic checkout without handling card data.

## Decision
- `IPaymentGateway` with a `SimulatedPaymentGateway`. The client sends a **test token**
  (`sim_success`, `sim_declined`, `sim_insufficient_funds`) modelled on Stripe test mode.
  No card number, CVV or bank data is ever requested, transmitted or stored.
- `POST /bookings` and `POST /bookings/{ref}/payments` require an `Idempotency-Key` header (a client-generated UUID).
  Unique indexes enforce one booking per (user, key) and one payment attempt per (booking, key).
  A replay returns the stored result. The same key with a different request body is rejected with `422`.
- Payments run only for `PendingPayment` bookings whose hold has not expired. Confirmation and seat
  confirmation happen in one transaction.

## Consequences
Swapping in Stripe means implementing `IPaymentGateway` (PaymentIntents + webhooks) and changing the
client to use the Stripe SDK. The booking state machine stays the same.
