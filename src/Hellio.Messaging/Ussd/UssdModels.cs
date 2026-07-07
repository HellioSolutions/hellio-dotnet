using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hellio.Messaging
{
    /// <summary>Shared JSON options for deserializing USSD payloads.</summary>
    internal static class UssdJson
    {
        internal static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            // Prices may arrive as JSON strings ("1.5000") or numbers; accept both.
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };

        /// <summary>Deserialize the <c>data</c> element of a response body into <typeparamref name="T"/>.</summary>
        internal static T Data<T>(JsonElement body)
        {
            var target = body;
            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("data", out var data))
            {
                target = data;
            }

            return target.Deserialize<T>(Options)!;
        }
    }

    /// <summary>A registered USSD application (holds the callback URL and signing secret).</summary>
    public sealed class UssdApp
    {
        /// <summary>Application id.</summary>
        [JsonPropertyName("id")] public long Id { get; set; }

        /// <summary>Human-readable application name.</summary>
        [JsonPropertyName("name")] public string? Name { get; set; }

        /// <summary>URL Hellio POSTs each USSD step to.</summary>
        [JsonPropertyName("callback_url")] public string? CallbackUrl { get; set; }

        /// <summary>HMAC-SHA256 signing secret; present on create, may be hidden on list.</summary>
        [JsonPropertyName("secret")] public string? Secret { get; set; }

        /// <summary>Whether the application is active and receiving callbacks.</summary>
        [JsonPropertyName("active")] public bool Active { get; set; }

        /// <summary>Creation timestamp (ISO 8601).</summary>
        [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
    }

    /// <summary>A rented USSD extension (a dialable suffix under a shared short code).</summary>
    public sealed class UssdExtension
    {
        /// <summary>Extension id.</summary>
        [JsonPropertyName("id")] public long Id { get; set; }

        /// <summary>The rented extension code, e.g. <c>100</c>.</summary>
        [JsonPropertyName("code")] public string? Code { get; set; }

        /// <summary>The full dial string a subscriber enters, e.g. <c>*920*100#</c>.</summary>
        [JsonPropertyName("dial_string")] public string? DialString { get; set; }

        /// <summary>Number of digits in the extension code.</summary>
        [JsonPropertyName("length")] public int Length { get; set; }

        /// <summary>Rental status, e.g. <c>active</c> or <c>expired</c>.</summary>
        [JsonPropertyName("status")] public string? Status { get; set; }

        /// <summary>Monthly rental price.</summary>
        [JsonPropertyName("monthly_price")] public decimal? MonthlyPrice { get; set; }

        /// <summary>Whether the rental renews automatically each month.</summary>
        [JsonPropertyName("auto_renew")] public bool AutoRenew { get; set; }

        /// <summary>Id of the application bound to this extension, if any.</summary>
        [JsonPropertyName("app_id")] public long? AppId { get; set; }

        /// <summary>Expiry timestamp (ISO 8601).</summary>
        [JsonPropertyName("expires_at")] public string? ExpiresAt { get; set; }
    }

    /// <summary>A single USSD session record.</summary>
    public sealed class UssdSession
    {
        /// <summary>Session id.</summary>
        [JsonPropertyName("id")] public long Id { get; set; }

        /// <summary>Aggregator session reference.</summary>
        [JsonPropertyName("session_ref")] public string? SessionRef { get; set; }

        /// <summary>Subscriber phone number.</summary>
        [JsonPropertyName("msisdn")] public string? Msisdn { get; set; }

        /// <summary>Service (dial) code used.</summary>
        [JsonPropertyName("service_code")] public string? ServiceCode { get; set; }

        /// <summary>Session status, e.g. <c>active</c> or <c>ended</c>.</summary>
        [JsonPropertyName("status")] public string? Status { get; set; }

        /// <summary>Number of steps exchanged in the session.</summary>
        [JsonPropertyName("steps")] public int Steps { get; set; }

        /// <summary>Amount charged for the session.</summary>
        [JsonPropertyName("charge")] public decimal? Charge { get; set; }

        /// <summary>Whether the session was run through the simulator (sandbox).</summary>
        [JsonPropertyName("sandbox")] public bool Sandbox { get; set; }

        /// <summary>Start timestamp (ISO 8601).</summary>
        [JsonPropertyName("started_at")] public string? StartedAt { get; set; }

        /// <summary>End timestamp (ISO 8601), or null while active.</summary>
        [JsonPropertyName("ended_at")] public string? EndedAt { get; set; }
    }

    /// <summary>Per-network session pricing for a short code.</summary>
    public sealed class UssdSessionPrice
    {
        /// <summary>Network display name.</summary>
        [JsonPropertyName("network")] public string? Network { get; set; }

        /// <summary>Network slug.</summary>
        [JsonPropertyName("slug")] public string? Slug { get; set; }

        /// <summary>Price per session on this network.</summary>
        [JsonPropertyName("session_price")] public decimal? SessionPrice { get; set; }
    }

    /// <summary>Monthly rental price for an extension of a given length.</summary>
    public sealed class UssdExtensionPrice
    {
        /// <summary>Number of digits in the extension code.</summary>
        [JsonPropertyName("length")] public int Length { get; set; }

        /// <summary>Monthly rental price for extensions of this length.</summary>
        [JsonPropertyName("monthly_price")] public decimal? MonthlyPrice { get; set; }
    }

    /// <summary>USSD pricing for the account's short code.</summary>
    public sealed class UssdPricing
    {
        /// <summary>The shared short code, e.g. <c>*920#</c>.</summary>
        [JsonPropertyName("short_code")] public string? ShortCode { get; set; }

        /// <summary>Currency code for the prices below.</summary>
        [JsonPropertyName("currency")] public string? Currency { get; set; }

        /// <summary>Session prices per network.</summary>
        [JsonPropertyName("session_prices")]
        public IReadOnlyList<UssdSessionPrice> SessionPrices { get; set; } = new List<UssdSessionPrice>();

        /// <summary>Extension rental prices by length.</summary>
        [JsonPropertyName("extension_prices")]
        public IReadOnlyList<UssdExtensionPrice> ExtensionPrices { get; set; } = new List<UssdExtensionPrice>();
    }

    /// <summary>Availability of a specific extension code.</summary>
    public sealed class UssdAvailability
    {
        /// <summary>The queried extension code.</summary>
        [JsonPropertyName("code")] public string? Code { get; set; }

        /// <summary>Whether the code is a well-formed extension.</summary>
        [JsonPropertyName("valid")] public bool Valid { get; set; }

        /// <summary>Whether the code is free to rent.</summary>
        [JsonPropertyName("available")] public bool Available { get; set; }

        /// <summary>Monthly rental price, or null when not applicable.</summary>
        [JsonPropertyName("monthly_price")] public decimal? MonthlyPrice { get; set; }
    }

    /// <summary>The reply the callback simulator produced for a step.</summary>
    public sealed class UssdSimulateResult
    {
        /// <summary>Text shown to the subscriber for this step.</summary>
        [JsonPropertyName("message")] public string? Message { get; set; }

        /// <summary>Either <c>continue</c> or <c>end</c>.</summary>
        [JsonPropertyName("action")] public string? Action { get; set; }

        /// <summary>True when the session should stay open for more input.</summary>
        [JsonPropertyName("continue")] public bool Continue { get; set; }
    }
}
