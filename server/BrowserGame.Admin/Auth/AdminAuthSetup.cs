using BrowserGame.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Admin.Auth;

/// <summary>Anmeldung der Admins: eigenes Identity-Modell, Passkey oder Passwort + 2FA, 8 Stunden (004 AK-2–4d).</summary>
internal static class AdminAuthSetup
{
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    /// <summary>Eigener Zweck für Admin-Cookies: Spiel und Admin teilen den Schlüsselring, ein Spieler-Cookie ist hier trotzdem wertlos.</summary>
    private const string CookieProtectionPurpose = "BrowserGame.Admin.Cookies";

    public static void AddAdminAuth(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        services.AddIdentityCore<AdminUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false; // Admins kommen über Einladungen

                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;

                DatabaseSetup.ConfigureStores(options);
            })
            .AddEntityFrameworkStores<AdminDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<GermanIdentityErrorDescriber>();

        var secure = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        foreach (var scheme in new[]
                 {
                     IdentityConstants.ApplicationScheme, IdentityConstants.ExternalScheme,
                     IdentityConstants.TwoFactorUserIdScheme, IdentityConstants.TwoFactorRememberMeScheme,
                 })
        {
            services.AddOptions<CookieAuthenticationOptions>(scheme).Configure<IDataProtectionProvider>((cookie, dataProtection) =>
            {
                cookie.DataProtectionProvider = dataProtection.CreateProtector(CookieProtectionPurpose);
                cookie.Cookie.Name = $"bg.admin.{scheme.Split('.')[^1].ToLowerInvariant()}";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Strict;
                cookie.Cookie.SecurePolicy = secure;
            });
        }

        services.ConfigureApplicationCookie(cookie =>
        {
            cookie.ExpireTimeSpan = SessionLifetime; // AK-4d
            cookie.SlidingExpiration = false;
            cookie.LoginPath = "/Login";
            cookie.LogoutPath = "/Logout";
            cookie.AccessDeniedPath = "/Login";
        });

        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

        services.Configure<IdentityPasskeyOptions>(options =>
        {
            var domain = builder.Configuration["Passkeys:ServerDomain"];
            if (!string.IsNullOrWhiteSpace(domain))
            {
                options.ServerDomain = domain;
            }
        });

        if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration["App:PublicUrl"]))
        {
            throw new InvalidOperationException("App:PublicUrl fehlt (Basis für Einrichtungs- und Einladungslinks).");
        }

        services.AddAuthRateLimiting(builder.Configuration);
        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<AdminInvites>();
        services.AddScoped<AuditLog>();
        services.AddScoped<AdminCredentials>();
    }
}
