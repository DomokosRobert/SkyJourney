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
- Two-phase flow: a `Pending` attempt is saved first (claiming the booking through its `xmin` version), the gateway is
  charged outside any transaction, then the outcome is recorded. Holding a database transaction open across a network
  call to a payment provider would block expiry and seat changes for the duration and couple our DB to their latency.
- A pending attempt younger than `Booking.PaymentTimeout` (2 minutes) keeps the hold alive and blocks other changes;
  older ones count as abandoned and are failed with `payment_timeout`.
- A declined payment is a recorded business outcome, not a client error: `201` with `status: Failed` and a
  `failureCode` (`card_declined`, `insufficient_funds`, `invalid_payment_token`). The booking stays payable with a new key.
- Payment requests are rate limited per user (`RateLimiting:PaymentPermitLimit`, default 20 per minute).

## Consequences
A real provider can report success after our window closed. The code logs that case as an error for refunding; a real
integration would void or refund automatically, and use provider webhooks to settle attempts left `Pending`.
Swapping in Stripe means implementing `IPaymentGateway` (PaymentIntents + webhooks) and changing the
client to use the Stripe SDK. The booking state machine stays the same.
