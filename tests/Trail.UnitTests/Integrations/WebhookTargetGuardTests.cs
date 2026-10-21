using System.Net;
using Trail.Application.Features.Integrations;

namespace Trail.UnitTests.Integrations;

public sealed class WebhookTargetGuardTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.1")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.1")]
    [InlineData("2606:4700:4700::1111")]
    public void Public_addresses_are_allowed(string address) =>
        WebhookTargetGuard.IsPublic(IPAddress.Parse(address)).ShouldBeTrue();

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.255")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.254")]
    [InlineData("127.0.0.1")]
    [InlineData("127.255.0.9")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("192.0.0.8")]
    [InlineData("198.18.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("ff02::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2001:0:4136:e378:8000:63bf:3fff:fdd2")] // Teredo
    [InlineData("::ffff:127.0.0.1")]                      // IPv4-mapped loopback
    [InlineData("::ffff:10.1.2.3")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::7f00:1")]                       // NAT64 of 127.0.0.1
    [InlineData("2002:7f00:1::")]                         // 6to4 of 127.0.0.1
    public void Private_loopback_link_local_and_reserved_addresses_are_refused(string address) =>
        WebhookTargetGuard.IsPublic(IPAddress.Parse(address)).ShouldBeFalse();

    [Theory]
    [InlineData("https://hooks.example.com/trail")]
    [InlineData("https://example.com:8443/a/b?token=1")]
    [InlineData("https://8.8.8.8/hook")]
    public void Public_https_urls_are_valid(string url) =>
        WebhookTargetGuard.Validate(url, allowInsecure: false).ShouldBeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("/relative/path")]
    [InlineData("http://hooks.example.com/trail")]
    [InlineData("ftp://hooks.example.com/trail")]
    [InlineData("https://user:pass@hooks.example.com/trail")]
    [InlineData("https://localhost/hook")]
    [InlineData("https://LOCALHOST:8443/hook")]
    [InlineData("https://api.localhost/hook")]
    [InlineData("https://127.0.0.1/hook")]
    [InlineData("https://2130706433/hook")]              // decimal spelling of 127.0.0.1
    [InlineData("https://0x7f.0.0.1/hook")]              // hex spelling
    [InlineData("https://10.0.0.5/hook")]
    [InlineData("https://192.168.0.10:8080/hook")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://100.64.1.1/hook")]
    [InlineData("https://[::1]/hook")]
    [InlineData("https://[fe80::1]/hook")]
    [InlineData("https://[::ffff:127.0.0.1]/hook")]
    public void Unsafe_or_malformed_urls_are_refused(string? url) =>
        WebhookTargetGuard.Validate(url, allowInsecure: false).ShouldNotBeNull();

    [Theory]
    [InlineData("http://localhost:5000/hook")]
    [InlineData("https://127.0.0.1/hook")]
    [InlineData("http://192.168.0.10/hook")]
    public void Development_flag_allows_insecure_and_local_targets(string url) =>
        WebhookTargetGuard.Validate(url, allowInsecure: true).ShouldBeNull();

    [Theory]
    [InlineData("ftp://example.com/hook")]
    [InlineData("https://user:pass@example.com/hook")]
    [InlineData("nonsense")]
    public void Development_flag_still_refuses_malformed_urls(string url) =>
        WebhookTargetGuard.Validate(url, allowInsecure: true).ShouldNotBeNull();
}
