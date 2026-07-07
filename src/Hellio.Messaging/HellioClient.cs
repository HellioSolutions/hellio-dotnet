using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hellio.Messaging
{
    /// <summary>
    /// Hellio Messaging API v1 client. Authenticates with a Bearer token and exposes
    /// one async method per endpoint. Every call returns the decoded JSON body as a
    /// <see cref="JsonElement"/> (payloads live under a <c>data</c> key); non-2xx
    /// responses throw a typed <see cref="HellioException"/>.
    /// </summary>
    public class HellioClient
    {
        /// <summary>Default API base URL.</summary>
        public const string DefaultBaseUrl = "https://api.helliomessaging.com/v1";

        private readonly HttpClient _http;
        private readonly string _token;
        private readonly string _baseUrl;
        private readonly string? _defaultSender;

        /// <summary>
        /// Create a client. Values fall back to the <c>HELLIO_API_TOKEN</c>,
        /// <c>HELLIO_BASE_URL</c> and <c>HELLIO_DEFAULT_SENDER</c> environment variables.
        /// </summary>
        /// <param name="token">Bearer token from your dashboard.</param>
        /// <param name="baseUrl">Override the API base URL.</param>
        /// <param name="timeout">Request timeout (default 30s). Ignored when an <paramref name="httpClient"/> is injected.</param>
        /// <param name="defaultSender">Sender ID used by SMS when none is supplied per call.</param>
        /// <param name="httpClient">Inject an <see cref="HttpClient"/> (mainly for tests).</param>
        public HellioClient(
            string? token = null,
            string? baseUrl = null,
            TimeSpan? timeout = null,
            string? defaultSender = null,
            HttpClient? httpClient = null)
        {
            _token = token ?? Environment.GetEnvironmentVariable("HELLIO_API_TOKEN") ?? string.Empty;

            var resolvedBase = baseUrl
                ?? Environment.GetEnvironmentVariable("HELLIO_BASE_URL")
                ?? DefaultBaseUrl;
            _baseUrl = resolvedBase.TrimEnd('/') + "/";

            _defaultSender = defaultSender ?? Environment.GetEnvironmentVariable("HELLIO_DEFAULT_SENDER");

            if (httpClient != null)
            {
                _http = httpClient;
            }
            else
            {
                _http = new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(30) };
            }

            Ussd = new UssdService(this);
        }

        /// <summary>
        /// USSD service: short-code pricing and availability, applications, extension
        /// rentals, sessions, and the callback simulator. Exposed as <c>client.Ussd</c>.
        /// </summary>
        public UssdService Ussd { get; }

        // ------------------------------------------------------------- Account

        /// <summary>GET <c>balance</c>. Wallet balance and available credit.</summary>
        public Task<JsonElement> BalanceAsync(CancellationToken cancellationToken = default)
            => GetAsync("balance", null, cancellationToken);

        /// <summary>GET <c>pricing</c>. Per-network SMS pricing; pass an ISO-2 code to narrow by country.</summary>
        public Task<JsonElement> PricingAsync(string? country = null, CancellationToken cancellationToken = default)
        {
            var query = country != null
                ? new Dictionary<string, string> { ["country"] = country }
                : null;

            return GetAsync("pricing", query, cancellationToken);
        }

        // ----------------------------------------------------------------- SMS

        /// <summary>POST <c>sms/send</c>. Recipients accept a single number or a comma-separated list.</summary>
        public Task<JsonElement> SendSmsAsync(string recipients, string message, string? sender = null, string? gateway = null, CancellationToken cancellationToken = default)
            => SendSmsAsync(ToList(recipients), message, sender, gateway, cancellationToken);

        /// <summary>POST <c>sms/send</c>. Recipients accept a list of numbers.</summary>
        public Task<JsonElement> SendSmsAsync(IEnumerable<string> recipients, string message, string? sender = null, string? gateway = null, CancellationToken cancellationToken = default)
        {
            var body = Compact(new Dictionary<string, object?>
            {
                ["recipients"] = ToList(recipients),
                ["sender"] = sender ?? _defaultSender,
                ["message"] = message,
                ["gateway"] = gateway,
            });

            return PostAsync("sms/send", body, cancellationToken);
        }

        /// <summary>GET <c>messages/{id}</c>. Delivery status for a single message.</summary>
        public Task<JsonElement> MessageAsync(long id, CancellationToken cancellationToken = default)
            => GetAsync("messages/" + id.ToString(CultureInfo.InvariantCulture), null, cancellationToken);

        /// <summary>GET <c>campaigns/{id}</c>. Campaign summary.</summary>
        public Task<JsonElement> CampaignAsync(long id, CancellationToken cancellationToken = default)
            => GetAsync("campaigns/" + id.ToString(CultureInfo.InvariantCulture), null, cancellationToken);

        // ----------------------------------------------------------------- OTP

        /// <summary>
        /// POST <c>otp/send</c>. <paramref name="to"/> is a phone number (sms/voice/whatsapp) or an
        /// email (channel "email"). <paramref name="sender"/> is required and must be approved for
        /// sms/voice; it is ignored for whatsapp and email.
        /// </summary>
        public Task<JsonElement> SendOtpAsync(
            string to,
            string? sender = null,
            string channel = "sms",
            string? purpose = null,
            int? length = null,
            int? expiry = null,
            string? gateway = null,
            CancellationToken cancellationToken = default)
        {
            var body = Compact(new Dictionary<string, object?>
            {
                ["channel"] = channel,
                [channel == "email" ? "email" : "mobile_number"] = to,
                ["sender"] = sender,
                ["purpose"] = purpose,
                ["length"] = length,
                ["expiry"] = expiry,
                ["gateway"] = gateway,
            });

            return PostAsync("otp/send", body, cancellationToken);
        }

        /// <summary>POST <c>otp/verify</c>. Full response for a verification attempt.</summary>
        public Task<JsonElement> VerifyOtpAsync(string to, string code, string channel = "sms", CancellationToken cancellationToken = default)
        {
            var body = Compact(new Dictionary<string, object?>
            {
                ["channel"] = channel,
                [channel == "email" ? "email" : "mobile_number"] = to,
                ["code"] = code,
            });

            return PostAsync("otp/verify", body, cancellationToken);
        }

        /// <summary>Convenience: true when the code is valid. Swallows 422 validation errors as false.</summary>
        public async Task<bool> VerifyAsync(string to, string code, string channel = "sms", CancellationToken cancellationToken = default)
        {
            JsonElement result;

            try
            {
                result = await VerifyOtpAsync(to, code, channel, cancellationToken).ConfigureAwait(false);
            }
            catch (ValidationException)
            {
                return false;
            }

            if (result.ValueKind == JsonValueKind.Object &&
                result.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("verified", out var verified))
            {
                return IsTruthy(verified);
            }

            return false;
        }

        // --------------------------------------------------------------- Voice

        /// <summary>POST <c>voice/send</c>. Provide <paramref name="text"/> (read with TTS) or <paramref name="audioUrl"/>.</summary>
        public Task<JsonElement> SendVoiceAsync(string recipients, string callerId, string? text = null, string? audioUrl = null, string? name = null, CancellationToken cancellationToken = default)
            => SendVoiceAsync(ToList(recipients), callerId, text, audioUrl, name, cancellationToken);

        /// <summary>POST <c>voice/send</c>. Provide <paramref name="text"/> (read with TTS) or <paramref name="audioUrl"/>.</summary>
        public Task<JsonElement> SendVoiceAsync(IEnumerable<string> recipients, string callerId, string? text = null, string? audioUrl = null, string? name = null, CancellationToken cancellationToken = default)
        {
            var body = Compact(new Dictionary<string, object?>
            {
                ["recipients"] = ToList(recipients),
                ["caller_id"] = callerId,
                ["text"] = text,
                ["audio_url"] = audioUrl,
                ["name"] = name,
            });

            return PostAsync("voice/send", body, cancellationToken);
        }

        /// <summary>GET <c>voice/{id}</c>. Status of a voice broadcast.</summary>
        public Task<JsonElement> VoiceStatusAsync(long id, CancellationToken cancellationToken = default)
            => GetAsync("voice/" + id.ToString(CultureInfo.InvariantCulture), null, cancellationToken);

        // --------------------------------------------------------- Number lookup

        /// <summary>POST <c>lookup</c>. Numbers accept a single value or a comma-separated list.</summary>
        public Task<JsonElement> LookupAsync(string numbers, CancellationToken cancellationToken = default)
            => LookupAsync(ToList(numbers), cancellationToken);

        /// <summary>POST <c>lookup</c>. Numbers accept a list.</summary>
        public Task<JsonElement> LookupAsync(IEnumerable<string> numbers, CancellationToken cancellationToken = default)
        {
            var body = new Dictionary<string, object?> { ["numbers"] = ToList(numbers) };
            return PostAsync("lookup", body, cancellationToken);
        }

        /// <summary>GET <c>lookups</c>. Recent lookup requests.</summary>
        public Task<JsonElement> LookupsAsync(CancellationToken cancellationToken = default)
            => GetAsync("lookups", null, cancellationToken);

        /// <summary>GET <c>lookup/{id}</c>. Result for a single lookup.</summary>
        public Task<JsonElement> LookupResultAsync(long id, CancellationToken cancellationToken = default)
            => GetAsync("lookup/" + id.ToString(CultureInfo.InvariantCulture), null, cancellationToken);

        // -------------------------------------------------- Email verification

        /// <summary>POST <c>email/verify</c>. Emails accept a single value or a comma-separated list.</summary>
        public Task<JsonElement> VerifyEmailAsync(string emails, CancellationToken cancellationToken = default)
            => VerifyEmailAsync(ToList(emails), cancellationToken);

        /// <summary>POST <c>email/verify</c>. Emails accept a list.</summary>
        public Task<JsonElement> VerifyEmailAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default)
        {
            var body = new Dictionary<string, object?> { ["emails"] = ToList(emails) };
            return PostAsync("email/verify", body, cancellationToken);
        }

        // ------------------------------------------------------------ Webhooks

        /// <summary>GET <c>webhooks</c>. List configured webhooks.</summary>
        public Task<JsonElement> WebhooksAsync(CancellationToken cancellationToken = default)
            => GetAsync("webhooks", null, cancellationToken);

        /// <summary>POST <c>webhooks</c>. Register a webhook; <paramref name="events"/> is optional.</summary>
        public Task<JsonElement> CreateWebhookAsync(string url, IEnumerable<string>? events = null, CancellationToken cancellationToken = default)
        {
            var list = events?.ToList();
            var body = Compact(new Dictionary<string, object?>
            {
                ["url"] = url,
                ["events"] = (list != null && list.Count > 0) ? list : null,
            });

            return PostAsync("webhooks", body, cancellationToken);
        }

        /// <summary>DELETE <c>webhooks/{id}</c>. Remove a webhook.</summary>
        public Task<JsonElement> DeleteWebhookAsync(long id, CancellationToken cancellationToken = default)
            => DeleteAsync("webhooks/" + id.ToString(CultureInfo.InvariantCulture), cancellationToken);

        // ------------------------------------------------------------ internals

        internal Task<JsonElement> GetAsync(string path, IDictionary<string, string>? query, CancellationToken cancellationToken)
        {
            var relative = path;
            if (query != null && query.Count > 0)
            {
                var pairs = query.Select(kv =>
                    Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value));
                relative = path + "?" + string.Join("&", pairs);
            }

            return SendAsync(HttpMethod.Get, relative, null, cancellationToken);
        }

        internal Task<JsonElement> PostAsync(string path, IDictionary<string, object?> body, CancellationToken cancellationToken)
            => SendAsync(HttpMethod.Post, path, body, cancellationToken);

        internal Task<JsonElement> PutAsync(string path, IDictionary<string, object?> body, CancellationToken cancellationToken)
            => SendAsync(HttpMethod.Put, path, body, cancellationToken);

        internal Task<JsonElement> DeleteAsync(string path, CancellationToken cancellationToken)
            => SendAsync(HttpMethod.Delete, path, null, cancellationToken);

        private async Task<JsonElement> SendAsync(HttpMethod method, string relativePath, IDictionary<string, object?>? body, CancellationToken cancellationToken)
        {
            var uri = new Uri(new Uri(_baseUrl, UriKind.Absolute), relativePath);

            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (body != null)
            {
                var json = JsonSerializer.Serialize(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif

            var data = Parse(payload);
            var status = (int)response.StatusCode;

            if (status >= 200 && status < 300)
            {
                return data;
            }

            var message = "Hellio API request failed.";
            if (data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("message", out var m) &&
                m.ValueKind == JsonValueKind.String)
            {
                message = m.GetString() ?? message;
            }
            else if (data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("error", out var e) &&
                e.ValueKind == JsonValueKind.String)
            {
                // Some endpoints (e.g. USSD) report a machine-readable "error" key
                // instead of a human "message".
                message = e.GetString() ?? message;
            }

            throw status switch
            {
                401 => new InvalidApiTokenException(message, status, data),
                402 => new InsufficientBalanceException(message, status, data),
                409 => new ConflictException(message, status, data),
                422 => new ValidationException(message, status, data),
                429 => new RateLimitException(message, status, data),
                503 => new ServiceUnavailableException(message, status, data),
                _ => new HellioException(message, status, data),
            };
        }

        private static JsonElement Parse(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                // Empty body: return an empty object so callers get a stable shape.
                using var empty = JsonDocument.Parse("{}");
                return empty.RootElement.Clone();
            }

            try
            {
                // Clone so the element survives disposal of the backing document.
                using var doc = JsonDocument.Parse(payload);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                using var empty = JsonDocument.Parse("{}");
                return empty.RootElement.Clone();
            }
        }

        private static bool IsTruthy(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return false;
                case JsonValueKind.Number:
                    return value.TryGetDouble(out var d) && d != 0;
                case JsonValueKind.String:
                    var s = value.GetString();
                    if (string.IsNullOrEmpty(s)) return false;
                    if (bool.TryParse(s, out var b)) return b;
                    return s != "0";
                default:
                    return true;
            }
        }

        /// <summary>Split a comma-separated string into a trimmed, non-empty list.</summary>
        private static List<string> ToList(string value)
        {
            if (value == null) return new List<string>();

            return value
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .ToList();
        }

        private static List<string> ToList(IEnumerable<string> value)
            => value?.ToList() ?? new List<string>();

        /// <summary>Drop null-valued entries from a request body.</summary>
        internal static Dictionary<string, object?> Compact(Dictionary<string, object?> data)
            => data.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}
