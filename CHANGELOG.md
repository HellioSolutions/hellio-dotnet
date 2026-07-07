# Changelog

All notable changes to this project are documented here. This project follows
[Semantic Versioning](https://semver.org).

## [1.1.0] - 2026-07-07

### Added
- USSD support via `client.Ussd`, requiring a token with the `ussd` ability:
  - `PricingAsync()` and `AvailabilityAsync(code)` for short-code pricing and code availability.
  - `client.Ussd.Apps` with `ListAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`.
  - `client.Ussd.Extensions` with `ListAsync`, `RentAsync`, `ReleaseAsync`.
  - `client.Ussd.Sessions` with `ListAsync` (optional `status` filter) and `GetAsync`.
  - `SimulateAsync(...)` to drive your callback URL without a live dial.
- Typed USSD models: `UssdApp`, `UssdExtension`, `UssdSession`, `UssdPricing`
  (`UssdSessionPrice`, `UssdExtensionPrice`), `UssdAvailability`, and `UssdSimulateResult`.
- `ConflictException` (409), raised when renting an unavailable extension
  (`error: "extension_unavailable"`); insufficient balance still maps to
  `InsufficientBalanceException` (402).
- Cursor pagination on USSD list methods via an optional `cursor` argument.

## [1.0.0] - 2026-07-05

Initial release.

### Added
- `HellioClient` covering the Hellio Messaging API v1: SMS, OTP (SMS / email / voice),
  Voice broadcasts, Number Lookup (HLR), Email Verification, Webhooks, plus account
  balance and pricing.
- Async methods returning `System.Text.Json.JsonElement`, with `VerifyAsync` returning `bool`.
- Recipient normalization: single string, comma-separated string, or list.
- Typed exceptions: `HellioException` base plus `InvalidApiTokenException` (401),
  `InsufficientBalanceException` (402), `ValidationException` (422), and
  `RateLimitException` (429), each carrying `StatusCode` and the parsed response body.
- Environment fallbacks: `HELLIO_API_TOKEN`, `HELLIO_BASE_URL`, `HELLIO_DEFAULT_SENDER`.
- Multi-target build for `netstandard2.0` and `net8.0`.
- xUnit test suite mocking `HttpMessageHandler`.
- GitHub Actions: tests on push/PR and NuGet publish on version tags.
