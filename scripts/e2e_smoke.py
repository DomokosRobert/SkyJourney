#!/usr/bin/env python3
"""Black-box end-to-end smoke test of a running SkyJourney API.

Walks the customer journey over plain HTTP: health -> search -> register -> book -> pay (simulated) -> booking
details -> cancellation quote -> cancel. Exits non-zero on the first unexpected response.

    python3 scripts/e2e_smoke.py                          # http://localhost:5080 (dotnet run)
    python3 scripts/e2e_smoke.py http://localhost:8080    # docker compose --profile api

It CREATES DATA in the target: one user (smoke-<random>@example.com) and one booking, which it cancels again.
Run it only against development or staging environments. Standard library only (Python 3.9+).
"""

import datetime
import json
import sys
import urllib.error
import urllib.request
import uuid

BASE_URL = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080").rstrip("/")
ORIGIN, DESTINATION = "BUD", "LHR"
DAYS_AHEAD = 7  # comfortably inside the booking horizon and outside the 24 h cancellation cutoff


def call(method, path, body=None, token=None, headers=None, expect=(200,)):
    data = json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(BASE_URL + path, data=data, method=method)
    request.add_header("Accept", "application/json")
    if data is not None:
        request.add_header("Content-Type", "application/json")
    if token:
        request.add_header("Authorization", f"Bearer {token}")
    for name, value in (headers or {}).items():
        request.add_header(name, value)
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            status, raw, kind = response.status, response.read(), response.headers.get_content_type()
    except urllib.error.HTTPError as error:
        status, raw, kind = error.code, error.read(), error.headers.get_content_type()
    payload = json.loads(raw) if raw and kind.endswith("json") else raw.decode(errors="replace")
    if status not in expect:
        fail(f"{method} {path} returned {status}, expected {expect}: {payload}")
    return payload


def fail(message):
    print(f"FAIL  {message}", file=sys.stderr)
    sys.exit(1)


def step(message):
    print(f"ok    {message}")


def main():
    call("GET", "/health/ready")
    step(f"API ready at {BASE_URL}")

    date = (datetime.date.today() + datetime.timedelta(days=DAYS_AHEAD)).isoformat()
    search = call("GET", f"/api/v1/flights/search?origin={ORIGIN}&destination={DESTINATION}&departureDate={date}&adults=1")
    offers = [o for o in search["outbound"] if o["isBookable"]]
    if not offers:
        fail(f"no bookable {ORIGIN}-{DESTINATION} flight on {date}")
    flight = offers[0]
    fare = next(f for f in flight["fares"] if f["family"] == "Flex")
    step(f"search: {len(offers)} bookable offer(s); using {flight['flightNumber']} Flex {fare['totalPrice']}")

    email = f"smoke-{uuid.uuid4().hex[:12]}@example.com"
    auth = call("POST", "/api/v1/auth/register", {
        "email": email, "password": "Smoke-Test-Passw0rd!", "firstName": "Smoke", "lastName": "Test",
    }, expect=(201,))
    token = auth["accessToken"]
    step(f"registered {email}")

    key = str(uuid.uuid4())
    booking_request = {
        "outbound": {"flightId": flight["flightId"], "fareFamily": "Flex"},
        "passengers": [{"firstName": "Smoke", "lastName": "Test", "type": "Adult"}],
    }
    booking = call("POST", "/api/v1/bookings", booking_request, token, {"Idempotency-Key": key}, expect=(201,))
    replay = call("POST", "/api/v1/bookings", booking_request, token, {"Idempotency-Key": key}, expect=(200,))
    if replay["reference"] != booking["reference"]:
        fail("idempotent retry created a second booking")
    if booking["totalPrice"] != fare["totalPrice"]:
        fail(f"booked price {booking['totalPrice']} differs from the quoted {fare['totalPrice']}")
    reference = booking["reference"]
    step(f"booked {reference} ({booking['status']}); retry with the same key replayed it")

    payment = call("POST", f"/api/v1/bookings/{reference}/payments", {"paymentToken": "sim_success"}, token,
                   {"Idempotency-Key": str(uuid.uuid4())}, expect=(201,))
    if payment["status"] != "Succeeded" or payment["booking"]["status"] != "Confirmed":
        fail(f"payment did not confirm the booking: {payment['status']} / {payment['booking']['status']}")
    step("simulated payment succeeded; booking confirmed")

    details = call("GET", f"/api/v1/bookings/{reference}", token=token)
    trips = call("GET", "/api/v1/bookings", token=token)
    if details["status"] != "Confirmed" or reference not in [t["reference"] for t in trips["items"]]:
        fail("confirmed booking missing from details or trip list")
    step("booking details and trip list agree")

    quote = call("GET", f"/api/v1/bookings/{reference}/cancellation", token=token)
    if not quote["canCancel"] or quote["refund"] != booking["totalPrice"]:
        fail(f"unexpected Flex cancellation quote: {quote}")
    cancelled = call("POST", f"/api/v1/bookings/{reference}/cancel", token=token)
    if cancelled["status"] != "Cancelled" or cancelled["refundedAmount"] != booking["totalPrice"]:
        fail(f"cancellation result: {cancelled['status']} refunded {cancelled['refundedAmount']}")
    step(f"cancelled with a full (simulated) refund of {cancelled['refundedAmount']}")

    call("GET", "/api/v1/bookings", token=None, expect=(401,))
    step("bookings require authentication")
    print("PASS  end-to-end smoke test")


if __name__ == "__main__":
    main()
