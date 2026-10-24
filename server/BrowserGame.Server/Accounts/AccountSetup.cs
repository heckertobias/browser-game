using System.Threading.RateLimiting;
using BrowserGame.Server.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Accounts;

/// <summary>Anmeldung mit ASP.NET Core Identity: Passwort + Passkeys, Cookie, Lockout, Rate-Limiting (ADR 0005).</summary>
internal static class AccountSetup
{
    public const string AuthRateLimitPolicy = "auth";

    /// <summary>Höchstens so viele Formular-Absendungen pro Minute und IP auf den Anmelde-Seiten (AK-10).</summary>
    public const int AuthRequestsPerMinute = 10;

    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    public static void AddGameAccounts(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        services.AddIdentityCore<Account>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
                options.SignIn.RequireConfirmedEmail = true; // AK-3

                // NIST: Länge statt Zeichenklassen
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                // AK-10 (pro Account)
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;

                DatabaseSetup.ConfigureStores(options);
            })
            .AddEntityFrameworkStores<GameDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<GermanIdentityErrorDescriber>();

        if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration["App:PublicUrl"]))
        {
            throw new InvalidOperationException("App:PublicUrl fehlt (Basis für Links in Mails und Origin-Prüfung).");
        }

        services.ConfigureApplicationCookie(cookie =>
        {
            cookie.Cookie.Name = "bg.auth";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Lax;
            cookie.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            cookie.ExpireTimeSpan = SessionLifetime; // AK-11: 30 Tage fest
            cookie.SlidingExpiration = false;
            cookie.LoginPath = "/Account/Login";
            cookie.LogoutPath = "/Account/Logout";
            cookie.AccessDeniedPath = "/Account/Login";
        });

        // Abmelden, Passwort-Reset und Löschen ändern den Security-Stamp → andere Sitzungen enden spätestens nach 1 min (AK-15).
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

        // Bestätigungs- und Reset-Links (AK-15: befristet; einmalig, weil sich der Security-Stamp danach ändert).
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(3));

        // Schlüssel in der DB: Cookies und Links überstehen Neustarts trotz read-only-Container.
        services.AddDataProtection().SetApplicationName("browsergame").PersistKeysToDbContext<GameDbContext>();

        services.Configure<IdentityPasskeyOptions>(options =>
        {
            var domain = builder.Configuration["Passkeys:ServerDomain"];
            if (!string.IsNullOrWhiteSpace(domain))
            {
                options.ServerDomain = domain;
            }
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimitPolicy, context =>
                HttpMethods.IsPost(context.Request.Method)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = AuthRequestsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter("get"));
        });

        services.AddAuthorization();
    }
}
