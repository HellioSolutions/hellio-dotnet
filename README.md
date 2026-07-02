# Hellio Messaging - Official .NET SDK

[![tests](https://github.com/HellioSolutions/hellio-dotnet/actions/workflows/tests.yml/badge.svg)](https://github.com/HellioSolutions/hellio-dotnet/actions/workflows/tests.yml)
[![NuGet](https://img.shields.io/nuget/v/Hellio.Messaging.svg)](https://www.nuget.org/packages/Hellio.Messaging)
[![Downloads](https://img.shields.io/nuget/dt/Hellio.Messaging.svg)](https://www.nuget.org/packages/Hellio.Messaging)
[![License](https://img.shields.io/nuget/l/Hellio.Messaging.svg)](LICENSE)

.NET client for the [Hellio Messaging](https://helliomessaging.com) API v1:
**SMS**, **OTP** (SMS / email / voice), **Voice broadcasts**, **Number Lookup (HLR)**,
**Email Verification**, and **Webhooks**.

Targets `netstandard2.0` and `net8.0`, so it runs on .NET Framework 4.6.1+, .NET Core,
Xamarin, and modern .NET.

## Install
```bash
dotnet add package Hellio.Messaging
```
Or with the Package Manager Console:
```powershell
Install-Package Hellio.Messaging
```

## Configure
Generate a token in your dashboard (Settings, then API, then Generate API token), then
construct the client. You can pass values directly or set the environment variables
`HELLIO_API_TOKEN`, `HELLIO_BASE_URL`, and `HELLIO_DEFAULT_SENDER`.

```csharp
using Hellio.Messaging;

var hellio = new HellioClient(
    token: "your-token-here",
    baseUrl: "https://api.helliomessaging.com/v1", // optional, this is the default
    defaultSender: "HellioSMS");                   // optional Sender ID for SMS
```

Reading from the environment instead:
```csharp
// Uses HELLIO_API_TOKEN, HELLIO_BASE_URL, HELLIO_DEFAULT_SENDER when arguments are omitted.
var hellio = new HellioClient();
```

Every call returns a `System.Text.Json.JsonElement` (payloads live under a `data` key),
except `VerifyAsync`, which returns a `bool`. All methods are async and accept an optional
`CancellationToken`.

## Usage

```csharp
using Hellio.Messaging;
using System.Text.Json;

var hellio = new HellioClient(token: "your-token-here", defaultSender: "HellioSMS");

// Account
JsonElement balance = await hellio.BalanceAsync();   // data.balance, data.available, ...
JsonElement pricing = await hellio.PricingAsync("GH"); // optional ISO-2 country filter

// SMS (recipients: single string, comma list, or IEnumerable<string>)
await hellio.SendSmsAsync("233241234567", "Hello!");
await hellio.SendSmsAsync(new[] { "233241234567", "233201234567" }, "Hi all", "HellioSMS");
await hellio.MessageAsync(1024);   // delivery status
await hellio.CampaignAsync(1024);  // campaign summary

// OTP - sender (Sender ID) is REQUIRED for sms/voice and must be approved on your account.
// Optional length (4 to 10 digits) and expiry (minutes). Returns status "queued".
await hellio.SendOtpAsync("233241234567", "HellioSMS");                       // SMS
await hellio.SendOtpAsync("233241234567", "HellioSMS", channel: "voice");     // Voice (TTS reads the code)
await hellio.SendOtpAsync("233241234567", "HellioSMS", length: 6, expiry: 10); // custom length / expiry
await hellio.SendOtpAsync("user@example.com", channel: "email");              // Email (no sender)

bool ok = await hellio.VerifyAsync("233241234567", "123456");                 // bool convenience
JsonElement res = await hellio.VerifyOtpAsync("user@example.com", "123456", "email"); // full response

// Voice broadcast - text (we TTS it) or a hosted audioUrl
await hellio.SendVoiceAsync("233241234567", "HELLIO", text: "Your code is 1 2 3 4");
await hellio.SendVoiceAsync(new[] { "233241234567" }, "HELLIO", audioUrl: "https://cdn.example.com/promo.mp3");
await hellio.VoiceStatusAsync(2048);

// Number lookup (HLR) - async; poll results
await hellio.LookupAsync(new[] { "233241234567" });
await hellio.LookupsAsync();
await hellio.LookupResultAsync(5);

// Email verification
await hellio.VerifyEmailAsync(new[] { "user@gmail.com", "bad@nodomain.invalid" });

// Webhooks (receive delivery reports)
await hellio.CreateWebhookAsync("https://your-app.com/hooks/hellio",
    new[] { "message.delivered", "message.failed" });
await hellio.WebhooksAsync();
await hellio.DeleteWebhookAsync(1);
```

### Reading responses
Responses are `JsonElement`, so you can navigate them directly:
```csharp
JsonElement balance = await hellio.BalanceAsync();
string available = balance.GetProperty("data").GetProperty("available").GetString();
```

## Error handling
Non-2xx responses throw typed exceptions (all extend `HellioException`). Each carries the
HTTP `StatusCode` and the parsed `Response` body; `ValidationException` exposes field errors
via the `Errors` property.

| Exception | Status |
|---|---|
| `InvalidApiTokenException` | 401 |
| `InsufficientBalanceException` | 402 |
| `ValidationException` (`.Errors`) | 422 |
| `RateLimitException` | 429 |
| `HellioException` | other |

```csharp
using Hellio.Messaging;

try
{
    await hellio.SendSmsAsync("233241234567", "Hi");
}
catch (InsufficientBalanceException)
{
    // top up
}
catch (ValidationException ex)
{
    // ex.Errors holds the field-level messages
}
```

Rate limit: **120 requests/minute** per token. A `RateLimitException` (429) is thrown when
you exceed it.

## Testing
The client accepts an injected `HttpClient`, so you can mock the transport in your own tests:
```csharp
var handler = new YourMockHandler(); // an HttpMessageHandler
var hellio = new HellioClient(token: "test", httpClient: new HttpClient(handler));
```
See `tests/Hellio.Messaging.Tests` for a working `HttpMessageHandler` mock and full coverage.

## License
MIT
