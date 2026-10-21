using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Services;
using RealEstateERP.Infrastructure.Services.Email;

namespace RealEstateERP.Tests;

public class EmailSettingsTests
{
    private sealed class FakeEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<(TestDbContext Db, SystemConfigService Config)> SetupAsync(string host = "")
    {
        var (db, _) = TestDbContext.Create();
        db.SystemConfigurations.AddRange(
            new SystemConfiguration { Key = ConfigKeys.SmtpHost, Value = host, ValueType = ConfigValueType.Text, Group = "Email" },
            new SystemConfiguration { Key = ConfigKeys.SmtpFromAddress, Value = "no-reply@acacia.test", ValueType = ConfigValueType.Text, Group = "Email" },
            new SystemConfiguration { Key = ConfigKeys.SmtpPassword, Value = "", ValueType = ConfigValueType.Secret, Group = "Email" });
        await db.SaveChangesAsync();
        return (db, new SystemConfigService(db, new MemoryCache(new MemoryCacheOptions()), new EphemeralDataProtectionProvider()));
    }

    private static EmailService Email(ISystemConfigService config, string env = "Development", Dictionary<string, string?>? appSettings = null)
        => new(config, new ConfigurationBuilder().AddInMemoryCollection(appSettings ?? []).Build(), new FakeEnv(env), new DevMailbox(), NullLogger<EmailService>.Instance);

    [Fact]
    public async Task Secret_settings_are_encrypted_and_never_exposed()
    {
        var (db, config) = await SetupAsync();
        Assert.True((await config.UpdateAsync(ConfigKeys.SmtpPassword, "xsmtpsib-secret-key")).Succeeded);

        var stored = db.SystemConfigurations.Single(s => s.Key == ConfigKeys.SmtpPassword).Value;
        Assert.DoesNotContain("secret-key", stored);                                 // encrypted at rest
        Assert.Null(await config.GetAsync(ConfigKeys.SmtpPassword));                  // never via the normal getter
        Assert.Equal("", (await config.GetAllAsync()).Single(s => s.Key == ConfigKeys.SmtpPassword).Value);   // nor on the admin page
        Assert.Equal("xsmtpsib-secret-key", await config.GetSecretAsync(ConfigKeys.SmtpPassword));
        Assert.True(await config.HasSecretAsync(ConfigKeys.SmtpPassword));

        Assert.True((await config.UpdateAsync(ConfigKeys.SmtpPassword, "")).Succeeded);   // clear
        Assert.False(await config.HasSecretAsync(ConfigKeys.SmtpPassword));
    }

    [Fact]
    public async Task A_secret_saved_with_other_keys_is_treated_as_not_set()
    {
        var (db, config) = await SetupAsync();
        await config.UpdateAsync(ConfigKeys.SmtpPassword, "old-key");
        var otherMachine = new SystemConfigService(db, new MemoryCache(new MemoryCacheOptions()), new EphemeralDataProtectionProvider());
        Assert.Null(await otherMachine.GetSecretAsync(ConfigKeys.SmtpPassword));   // no crash: the admin re-enters it
    }

    [Fact]
    public async Task Settings_page_wins_over_appsettings_and_dev_mailbox_is_the_fallback()
    {
        var appSettings = new Dictionary<string, string?> { ["EmailSettings:Host"] = "smtp-relay.brevo.com", ["EmailSettings:FromAddress"] = "a@b.test" };

        var (_, blank) = await SetupAsync(host: "");
        Assert.Contains("appsettings", await Email(blank, appSettings: appSettings).DescribeAsync());
        Assert.Contains("development mailbox", await Email(blank).DescribeAsync());
        Assert.Contains("NOT being sent", await Email(blank, env: "Production").DescribeAsync());

        var (_, set) = await SetupAsync(host: "smtp.office365.com");
        var described = await Email(set, appSettings: appSettings).DescribeAsync();
        Assert.Contains("smtp.office365.com", described);
        Assert.Contains("Admin > Settings", described);
    }

    [Fact]
    public async Task With_no_server_development_email_goes_to_the_mailbox()
    {
        var (_, config) = await SetupAsync(host: "");
        var mailbox = new DevMailbox();
        var service = new EmailService(config, new ConfigurationBuilder().Build(), new FakeEnv("Development"), mailbox, NullLogger<EmailService>.Instance);
        await service.SendAsync(new EmailMessage("faith@x.test", "Hello", "<p>Hi</p>"));
        Assert.Equal("Hello", Assert.Single(mailbox.All()).Subject);
    }
}
