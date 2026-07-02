using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Hellio.Messaging.Tests
{
    public class HellioClientTests
    {
        private static HellioClient Client(MockHttpMessageHandler handler, string? defaultSender = null)
            => new HellioClient(
                token: "test-token",
                baseUrl: "https://api.helliomessaging.com/v1",
                defaultSender: defaultSender,
                httpClient: new HttpClient(handler));

        // --------------------------------------------------------------- SMS

        [Fact]
        public async Task SendSms_PostsExpectedBodyAndUrl()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK,
                "{\"data\":{\"message_id\":42,\"status\":\"queued\"}}");
            var client = Client(handler, defaultSender: "HellioSMS");

            var result = await client.SendSmsAsync("233241234567", "Hello!");

            Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
            Assert.Equal("https://api.helliomessaging.com/v1/sms/send",
                handler.LastRequest!.RequestUri!.ToString());

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            var root = body.RootElement;
            Assert.Equal("Hello!", root.GetProperty("message").GetString());
            Assert.Equal("HellioSMS", root.GetProperty("sender").GetString());
            var recipients = root.GetProperty("recipients");
            Assert.Equal(1, recipients.GetArrayLength());
            Assert.Equal("233241234567", recipients[0].GetString());

            Assert.Equal(42, result.GetProperty("data").GetProperty("message_id").GetInt32());
        }

        [Fact]
        public async Task SendSms_SendsBearerAndAcceptHeaders()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
            var client = Client(handler, defaultSender: "HellioSMS");

            await client.SendSmsAsync("233241234567", "Hi");

            Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
            Assert.Equal("test-token", handler.LastRequest!.Headers.Authorization!.Parameter);
            Assert.Contains(handler.LastRequest!.Headers.Accept,
                h => h.MediaType == "application/json");
        }

        [Fact]
        public async Task SendSms_OmitsGatewayWhenNull()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
            var client = Client(handler, defaultSender: "HellioSMS");

            await client.SendSmsAsync("233241234567", "Hi");

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            Assert.False(body.RootElement.TryGetProperty("gateway", out _));
        }

        // ------------------------------------------------- Recipient normalization

        [Fact]
        public async Task SendSms_NormalizesCommaSeparatedRecipients()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
            var client = Client(handler, defaultSender: "HellioSMS");

            await client.SendSmsAsync("233241234567, 233201234567 ,", "Hi all");

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            var recipients = body.RootElement.GetProperty("recipients");
            Assert.Equal(2, recipients.GetArrayLength());
            Assert.Equal("233241234567", recipients[0].GetString());
            Assert.Equal("233201234567", recipients[1].GetString());
        }

        [Fact]
        public async Task SendSms_AcceptsListOfRecipients()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
            var client = Client(handler, defaultSender: "HellioSMS");

            await client.SendSmsAsync(new[] { "233241234567", "233201234567" }, "Hi all", "HellioSMS");

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            Assert.Equal(2, body.RootElement.GetProperty("recipients").GetArrayLength());
        }

        // --------------------------------------------------------------- OTP

        [Fact]
        public async Task SendOtp_SmsChannelUsesMobileNumber()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{\"status\":\"queued\"}}");
            var client = Client(handler);

            await client.SendOtpAsync("233241234567", "HellioSMS", length: 6, expiry: 10);

            Assert.Equal("https://api.helliomessaging.com/v1/otp/send",
                handler.LastRequest!.RequestUri!.ToString());

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            var root = body.RootElement;
            Assert.Equal("sms", root.GetProperty("channel").GetString());
            Assert.Equal("233241234567", root.GetProperty("mobile_number").GetString());
            Assert.Equal("HellioSMS", root.GetProperty("sender").GetString());
            Assert.Equal(6, root.GetProperty("length").GetInt32());
            Assert.Equal(10, root.GetProperty("expiry").GetInt32());
            Assert.False(root.TryGetProperty("email", out _));
        }

        [Fact]
        public async Task SendOtp_EmailChannelUsesEmailField()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
            var client = Client(handler);

            await client.SendOtpAsync("user@example.com", channel: "email");

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            var root = body.RootElement;
            Assert.Equal("email", root.GetProperty("channel").GetString());
            Assert.Equal("user@example.com", root.GetProperty("email").GetString());
            Assert.False(root.TryGetProperty("mobile_number", out _));
            Assert.False(root.TryGetProperty("sender", out _));
        }

        [Fact]
        public async Task VerifyOtp_PostsCode()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{\"verified\":true}}");
            var client = Client(handler);

            var result = await client.VerifyOtpAsync("233241234567", "123456");

            Assert.Equal("https://api.helliomessaging.com/v1/otp/verify",
                handler.LastRequest!.RequestUri!.ToString());
            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            Assert.Equal("123456", body.RootElement.GetProperty("code").GetString());
            Assert.True(result.GetProperty("data").GetProperty("verified").GetBoolean());
        }

        [Fact]
        public async Task Verify_ReturnsTrueWhenVerified()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{\"verified\":true}}");
            var client = Client(handler);

            Assert.True(await client.VerifyAsync("233241234567", "123456"));
        }

        [Fact]
        public async Task Verify_ReturnsFalseWhenNotVerified()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{\"verified\":false}}");
            var client = Client(handler);

            Assert.False(await client.VerifyAsync("233241234567", "000000"));
        }

        [Fact]
        public async Task Verify_ReturnsFalseOnValidationError()
        {
            var handler = new MockHttpMessageHandler((HttpStatusCode)422,
                "{\"message\":\"Invalid code\",\"errors\":{\"code\":[\"invalid\"]}}");
            var client = Client(handler);

            Assert.False(await client.VerifyAsync("233241234567", "000000"));
        }

        // ------------------------------------------------------------ Balance (GET)

        [Fact]
        public async Task Balance_GetsBalanceEndpoint()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK,
                "{\"data\":{\"balance\":\"195.0000\",\"available\":\"194.65\"}}");
            var client = Client(handler);

            var result = await client.BalanceAsync();

            Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
            Assert.Equal("https://api.helliomessaging.com/v1/balance",
                handler.LastRequest!.RequestUri!.ToString());
            Assert.Equal("195.0000", result.GetProperty("data").GetProperty("balance").GetString());
        }

        [Fact]
        public async Task Pricing_AddsCountryQueryOnlyWhenGiven()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
            var client = Client(handler);

            await client.PricingAsync("GH");
            Assert.Equal("https://api.helliomessaging.com/v1/pricing?country=GH",
                handler.LastRequest!.RequestUri!.ToString());

            var handler2 = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
            var client2 = Client(handler2);
            await client2.PricingAsync();
            Assert.Equal("https://api.helliomessaging.com/v1/pricing",
                handler2.LastRequest!.RequestUri!.ToString());
        }

        [Fact]
        public async Task DeleteWebhook_UsesDeleteMethod()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
            var client = Client(handler);

            await client.DeleteWebhookAsync(1);

            Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
            Assert.Equal("https://api.helliomessaging.com/v1/webhooks/1",
                handler.LastRequest!.RequestUri!.ToString());
        }

        [Fact]
        public async Task CreateWebhook_OmitsEventsWhenEmpty()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
            var client = Client(handler);

            await client.CreateWebhookAsync("https://example.com/hook");

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            Assert.False(body.RootElement.TryGetProperty("events", out _));
            Assert.Equal("https://example.com/hook", body.RootElement.GetProperty("url").GetString());
        }

        // ----------------------------------------------------------- Error mapping

        [Theory]
        [InlineData(401, typeof(InvalidApiTokenException))]
        [InlineData(402, typeof(InsufficientBalanceException))]
        [InlineData(422, typeof(ValidationException))]
        [InlineData(429, typeof(RateLimitException))]
        [InlineData(500, typeof(HellioException))]
        public async Task ErrorStatus_MapsToTypedException(int status, Type expected)
        {
            var handler = new MockHttpMessageHandler((HttpStatusCode)status,
                "{\"message\":\"boom\"}");
            var client = Client(handler, defaultSender: "HellioSMS");

            var ex = await Assert.ThrowsAnyAsync<HellioException>(
                () => client.SendSmsAsync("233241234567", "Hi"));

            Assert.IsType(expected, ex);
            Assert.Equal(status, ex.StatusCode);
            Assert.Equal("boom", ex.Message);
        }

        [Fact]
        public async Task ValidationException_ExposesErrors()
        {
            var handler = new MockHttpMessageHandler((HttpStatusCode)422,
                "{\"message\":\"Invalid\",\"errors\":{\"recipients\":[\"required\"]}}");
            var client = Client(handler, defaultSender: "HellioSMS");

            var ex = await Assert.ThrowsAsync<ValidationException>(
                () => client.SendSmsAsync("233241234567", "Hi"));

            Assert.Equal(JsonValueKind.Object, ex.Errors.ValueKind);
            Assert.Equal("required", ex.Errors.GetProperty("recipients")[0].GetString());
        }

        [Fact]
        public async Task DefaultMessage_WhenBodyHasNoMessage()
        {
            var handler = new MockHttpMessageHandler((HttpStatusCode)500, "{}");
            var client = Client(handler, defaultSender: "HellioSMS");

            var ex = await Assert.ThrowsAsync<HellioException>(
                () => client.SendSmsAsync("233241234567", "Hi"));

            Assert.Equal("Hellio API request failed.", ex.Message);
        }
    }
}
