# AirQ Secure Import Endpoint Design

## Goal

Replace AirQ's credential-bearing state-changing GET endpoint with an authenticated POST endpoint. Keep vendor credentials in runtime configuration or secret storage and make the local Compose service private by default.

## Endpoint

- Remove `GET /store-noise-levels-for-date`.
- Add `POST /store-noise-levels-for-date`.
- Accept a JSON request body containing only `date`.
- Resolve the AirQ vendor user ID and authorization value from `RVT__AIRQ_USER_ID` and `RVT__AIRQ_USER_AUTH`; neither value is accepted from a client request.
- Keep `GET /liveness` unauthenticated because it has no side effects and is required for health checks.

## Authorization

- Require an `X-Api-Key` request header for the POST endpoint.
- Store the expected key in `RVT__MONITOR_API_KEY`; it must be supplied through an ignored development settings file, Docker/Compose secret flow, or the deployment platform's secret store.
- When `MonitorApi:Enabled` is true, fail application startup if `RVT__MONITOR_API_KEY` is missing or blank.
- Compare supplied and configured keys with a fixed-time comparison. Return `401 Unauthorized` for missing or invalid credentials without indicating which condition failed.

## Ingress

- Remove AirQ's `8081:8080` host-port publication from the base `docker-compose.yml`.
- The AirQ API remains reachable from other services on the Compose network at `http://airqmonitor-api:8080`.
- A developer-only Compose override may publish the port when explicitly needed; it must not add vendor or API secrets to source control.

## Testing

- Endpoint route metadata verifies that only the POST import route exists and the legacy GET route is absent.
- Endpoint integration tests verify missing and invalid API keys return `401` and do not invoke the import service.
- A valid API key invokes the import service with configuration-resolved AirQ credentials and the request date.
- Startup validation tests cover enabled API with a missing key and disabled API without a key.
- Existing AirQ unit and PostgreSQL integration tests remain green.

## Non-Goals

- This change does not add user identity, roles, JWTs, or a shared authentication system across every monitor API.
- This change does not remove the AirQ vendor credentials; it only removes them from HTTP requests and URLs.
