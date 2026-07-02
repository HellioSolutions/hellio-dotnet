# Changelog

All notable changes to this project are documented here. This project follows
[Semantic Versioning](https://semver.org).

## [0.1.0] - 2026-07-02

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
