using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging;
using MailKit.Security;
using Trail.Api.Infrastructure;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Settings;
using Trail.Infrastructure.Security;

namespace Trail.UnitTests.Common;

public sealed class StartupConfigTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void Relaxed_environments_are_development_and_testing(string env, bool relaxed) =>
        EnvironmentRules.IsRelaxed(env).ShouldBe(relaxed);

    [Theory]
    [InlineData("Log", "Production", false)]
    [InlineData("Log", "Staging", false)]
    [InlineData("Smtp", "Production", true)]
    [InlineData("Log", "Development", true)]
    [InlineData("Log", "Testing", true)]
    public void Email_provider_must_be_smtp_outside_relaxed_environments(string provider, string env, bool valid) =>
        new EmailOptions { Provider = provider }.IsProviderAllowedIn(env).ShouldBe(valid);

    [Theory]
    [InlineData("https://app.trail.example", "Production", true)]
    [InlineData("https://app.trail.example/", "Production", true)]
    [InlineData("http://app.trail.example", "Production", false)]
    [InlineData("https://localhost:5173", "Production", false)]
    [InlineData("https://app.localhost", "Production", false)]
    [InlineData("https://127.0.0.1", "Production", false)]
    [InlineData("not a url", "Production", false)]
    [InlineData("", "Production", false)]
    [InlineData("http://localhost:5173", "Development", true)]
    [InlineData("http://localhost:5173", "Testing", true)]
    public void Frontend_url_must_be_public_https_outside_relaxed_environments(string url, string env, bool valid) =>
        new AppOptions { FrontendBaseUrl = url }.IsValidFor(env).ShouldBe(valid);

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("", "short", true)]
    [InlineData("admin@trail.example", null, false)]
    [InlineData("admin@trail.example", "elevenchars", false)]
    [InlineData("admin@trail.example", "twelve-chars", true)]
    public void Bootstrap_password_is_required_with_an_email(string? email, string? password, bool valid) =>
        new BootstrapOptions { PlatformAdminEmail = email, PlatformAdminPassword = password }.IsConsistent.ShouldBe(valid);

    [Fact]
    public void Smtp_with_credentials_never_uses_opportunistic_tls()
    {
        var withCredentials = new EmailOptions { SmtpUser = "user" };
        SmtpEmailSender.ResolveSecurity(withCredentials).ShouldBe(SecureSocketOptions.StartTls);

        withCredentials.SmtpSecurity = SmtpSecurity.SslOnConnect;
        SmtpEmailSender.ResolveSecurity(withCredentials).ShouldBe(SecureSocketOptions.SslOnConnect);

        SmtpEmailSender.ResolveSecurity(new EmailOptions()).ShouldBe(SecureSocketOptions.StartTlsWhenAvailable);
    }

    [Theory]
    [InlineData("https://app.trail.example", true)]
    [InlineData("http://localhost:3000", true)]
    [InlineData("http://localhost:3000/", false)]
    [InlineData("https://app.trail.example/app", false)]
    [InlineData("app.trail.example", false)]
    [InlineData("ftp://app.trail.example", false)]
    [InlineData("https://user@app.trail.example", false)]
    [InlineData("", false)]
    public void Cors_origins_are_bare_http_origins(string origin, bool valid) =>
        FrontendCorsOptions.IsValidOrigin(origin).ShouldBe(valid);

    [Fact]
    public void Cors_validator_reports_each_invalid_origin()
    {
        var validator = new FrontendCorsOptionsValidator();

        validator.Validate(null, new FrontendCorsOptions { AllowedOrigins = ["https://a.example"] }).Succeeded.ShouldBeTrue();
        var result = validator.Validate(null, new FrontendCorsOptions { AllowedOrigins = ["https://a.example", "bad/", "also bad"] });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("bad/");
        result.FailureMessage.ShouldContain("also bad");
    }

    [Fact]
    public void Rate_limit_defaults_are_positive_and_ranges_require_at_least_one_permit()
    {
        var options = new RateLimitOptions();
        options.GlobalPermitsPerMinute.ShouldBeGreaterThan(0);

        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var invalid = new RateLimitOptions { AuthPermitsPerMinute = 0 };
        System.ComponentModel.DataAnnotations.Validator
            .TryValidateObject(invalid, new(invalid), results, validateAllProperties: true).ShouldBeFalse();
        results.Single().MemberNames.ShouldBe([nameof(RateLimitOptions.AuthPermitsPerMinute)]);
    }

    [Fact]
    public void Http_options_validate_proxy_and_network_entries()
    {
        var validator = new HttpOptionsValidator();

        validator.Validate(null, new HttpOptions { KnownProxies = ["10.0.0.5", "::1"], KnownNetworks = ["10.0.0.0/8", "fd00::/8"] })
            .Succeeded.ShouldBeTrue();

        var result = validator.Validate(null, new HttpOptions { KnownProxies = ["nope"], KnownNetworks = ["10.0.0.0/99", "10.0.0.0"] });
        result.Failed.ShouldBeTrue();
        result.Failures.Count().ShouldBe(3);
    }

    [Fact]
    public void Http_options_keep_framework_defaults_when_empty()
    {
        var defaults = new ForwardedHeadersOptions();
        var options = new ForwardedHeadersOptions();

        new HttpOptions().ApplyTo(options);

        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        options.ForwardLimit.ShouldBe(1);
        options.KnownProxies.Count.ShouldBe(defaults.KnownProxies.Count);
        options.KnownIPNetworks.Count.ShouldBe(defaults.KnownIPNetworks.Count);
    }

    [Fact]
    public void Http_options_replace_defaults_with_the_configured_proxies()
    {
        var options = new ForwardedHeadersOptions();

        new HttpOptions { KnownProxies = ["10.0.0.5"], KnownNetworks = ["172.16.0.0/12"], ForwardLimit = 2 }.ApplyTo(options);

        options.ForwardLimit.ShouldBe(2);
        options.KnownProxies.Select(p => p.ToString()).ShouldBe(["10.0.0.5"]);
        options.KnownIPNetworks.Select(n => n.ToString()).ShouldBe(["172.16.0.0/12"]);
    }

    [Fact]
    public void Log_email_sender_keeps_the_body_out_of_information_logs()
    {
        var logger = new CapturingLogger<LogEmailSender>(LogLevel.Information);

        new LogEmailSender(logger).SendAsync(new EmailMessage("a@b.example", "Reset", "https://x/reset?token=SECRET"), TestContext.Current.CancellationToken);

        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Text.ShouldContain("a@b.example");
        entry.Text.ShouldContain("Reset");
        entry.Text.ShouldNotContain("SECRET");
    }

    [Fact]
    public void Log_email_sender_writes_the_body_at_debug()
    {
        var logger = new CapturingLogger<LogEmailSender>(LogLevel.Debug);

        new LogEmailSender(logger).SendAsync(new EmailMessage("a@b.example", "Reset", "https://x/reset?token=SECRET"), TestContext.Current.CancellationToken);

        logger.Entries.Single(e => e.Level == LogLevel.Information).Text.ShouldNotContain("SECRET");
        logger.Entries.Single(e => e.Level == LogLevel.Debug).Text.ShouldContain("SECRET");
    }

    private sealed class CapturingLogger<T>(LogLevel minimum) : ILogger<T>
    {
        public List<(LogLevel Level, string Text)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimum;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
