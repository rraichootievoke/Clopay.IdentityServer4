using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.EntityFramework.Storage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Clopay.IDPAdminUI.Configuration;
using Clopay.IDPAdminUI.Configuration.Constants;
using Clopay.IDPAdminUI.Core.Auditing;
using Clopay.IDPAdminUI.Core.Authorization;
using Clopay.IDPAdminUI.Core.Filters;
using Clopay.IDPAdminUI.Core.Middleware;
using Clopay.IDPAdminUI.ExceptionHandling;
using Clopay.IDPAdminUI.Infrastructure.Database.Shared.DbContexts;

namespace Clopay.IDPAdminUI
{
    /// <summary>
    /// Clopay IDP Admin UI: manages the IdentityServer4 configuration (clients, secrets, API resources, scopes,
    /// identity resources) and sessions (persisted grants) stored in the Clopay IDP database.
    ///
    /// The app owns no database: no migrations, no seed data, nothing is created. Logs and the audit trail go to
    /// files; data-protection keys go to App_Data. Administrators sign in through the IDP itself.
    /// </summary>
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            ConfigureServices(builder);

            var app = builder.Build();

            ConfigurePipeline(app);

            try
            {
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "The IDP Admin UI terminated unexpectedly");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void ConfigureServices(WebApplicationBuilder builder)
        {
            var services = builder.Services;
            var configuration = builder.Configuration;
            var environment = builder.Environment;

            // ---- Settings -------------------------------------------------------------------------------------
            var adminConfiguration = configuration.GetSection(nameof(AdminConfiguration)).Get<AdminConfiguration>() ?? new AdminConfiguration();
            StartupChecks.Validate(adminConfiguration, configuration);
            services.AddSingleton(adminConfiguration);

            // ---- Logging: files only (Log\idpadminui-*.log and Log\audit-*.log) --------------------------------
            builder.Host.UseSerilog((context, loggerConfiguration) => ConfigureLogging(context.Configuration, loggerConfiguration));

            // ---- Keys for cookies / anti-forgery: a folder in the site, not a database -------------------------
            var keyDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "DataProtection-Keys");
            Directory.CreateDirectory(keyDirectory);
            services.AddDataProtection()
                .SetApplicationName("ClopayIdpAdminUi")
                .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));

            // ---- The IDP database (IdentityServer4 configuration + operational stores) ------------------------
            var configurationConnection = configuration.GetConnectionString(ConfigurationConsts.ConfigurationDbConnectionStringKey);
            var persistedGrantConnection = configuration.GetConnectionString(ConfigurationConsts.PersistedGrantDbConnectionStringKey);

            services.AddConfigurationDbContext<IdentityServerConfigurationDbContext>(options =>
                options.ConfigureDbContext = b => b.UseSqlServer(configurationConnection));
            services.AddOperationalDbContext<IdentityServerPersistedGrantDbContext>(options =>
                options.ConfigureDbContext = b => b.UseSqlServer(persistedGrantConnection));

            services.AddAdminServices<IdentityServerConfigurationDbContext, IdentityServerPersistedGrantDbContext>();

            // ---- Audit trail (file) -----------------------------------------------------------------------------
            services.AddHttpContextAccessor();
            services.AddSingleton<IAuditEventLogger, FileAuditEventLogger>();

            // ---- MVC + localization (English only) --------------------------------------------------------------
            services.AddSingleton<ITempDataProvider, CookieTempDataProvider>();
            services.AddLocalization(options => options.ResourcesPath = ConfigurationConsts.ResourcesPath);
            services.AddScoped<ControllerExceptionFilterAttribute>();

            services.AddControllersWithViews(options => options.Filters.Add<ReadOnlyModeFilter>())
                .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix, options => options.ResourcesPath = ConfigurationConsts.ResourcesPath)
                .AddDataAnnotationsLocalization();

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var english = new List<CultureInfo> { new CultureInfo("en") };
                options.DefaultRequestCulture = new RequestCulture("en");
                options.SupportedCultures = english;
                options.SupportedUICultures = english;
            });

            // ---- Sign-in through the Clopay IDP (OpenID Connect) + allow-list authorization -------------------
            ConfigureAuthentication(services, adminConfiguration);

            services.AddSingleton<IAuthorizationHandler, AdminAllowListHandler>();
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthorizationConsts.AdministrationPolicy,
                    policy => policy.RequireAuthenticatedUser().AddRequirements(new AdminAllowListRequirement()));
            });

            // ---- Proxy headers (IIS / Azure Front Door), HSTS, health ------------------------------------------
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.All;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });

            services.AddHealthChecks()
                .AddDbContextCheck<IdentityServerConfigurationDbContext>("IdpConfigurationDb");
        }

        private static void ConfigurePipeline(WebApplication app)
        {
            app.UseForwardedHeaders();

            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local"))
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseMiddleware<SecurityHeadersMiddleware>();

            app.UseStaticFiles();

            app.UseRequestLocalization();

            app.UseRouting();

            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseAuthorization();

            // Anonymous: lets IIS / monitoring check that the site and the IDP database answer.
            app.MapHealthChecks("/health").AllowAnonymous();

            app.MapAreaControllerRoute(
                name: CommonConsts.AdminUIArea,
                areaName: CommonConsts.AdminUIArea,
                pattern: "{controller=Home}/{action=Index}/{id?}");
        }

        private static void ConfigureLogging(IConfiguration configuration, LoggerConfiguration loggerConfiguration)
        {
            var logDirectory = configuration["Logging:FilePath"];
            if (string.IsNullOrWhiteSpace(logDirectory)) logDirectory = "Log";

            loggerConfiguration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "Clopay.IDPAdminUI")
                // application log
                .WriteTo.Logger(main => main
                    .Filter.ByExcluding(Matching.FromSource(FileAuditEventLogger.AuditCategory))
                    .WriteTo.Console()
                    .WriteTo.File(
                        Path.Combine(logDirectory, "idpadminui-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        shared: true))
                // audit trail: who changed what
                .WriteTo.Logger(audit => audit
                    .Filter.ByIncludingOnly(Matching.FromSource(FileAuditEventLogger.AuditCategory))
                    .WriteTo.File(
                        Path.Combine(logDirectory, "audit-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 365,
                        shared: true,
                        outputTemplate: "{Timestamp:o} {Message:lj}{NewLine}"));
        }

        private static void ConfigureAuthentication(IServiceCollection services, AdminConfiguration adminConfiguration)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
                options.Secure = CookieSecurePolicy.SameAsRequest;
            });

            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = AuthenticationConsts.OidcAuthenticationScheme;
                    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultSignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Cookie.Name = adminConfiguration.IdentityAdminCookieName;
                    options.Cookie.HttpOnly = true;
                    options.AccessDeniedPath = "/Account/AccessDenied";
                })
                .AddOpenIdConnect(AuthenticationConsts.OidcAuthenticationScheme, options =>
                {
                    options.Authority = adminConfiguration.IdentityServerBaseUrl;
                    options.RequireHttpsMetadata = adminConfiguration.RequireHttpsMetadata;
                    options.ClientId = adminConfiguration.ClientId;
                    options.ClientSecret = adminConfiguration.ClientSecret;
                    options.ResponseType = adminConfiguration.OidcResponseType;
                    options.UsePkce = true;

                    options.Scope.Clear();
                    foreach (var scope in adminConfiguration.Scopes)
                    {
                        options.Scope.Add(scope);
                    }

                    // Keep the claim names exactly as the IDP sends them (sub, preferred_username, ...)
                    options.MapInboundClaims = false;
                    options.SaveTokens = true;
                    options.GetClaimsFromUserInfoEndpoint = true;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = adminConfiguration.TokenValidationClaimName
                    };

                    options.Events = new OpenIdConnectEvents
                    {
                        OnMessageReceived = context =>
                        {
                            context.Properties.IsPersistent = true;
                            context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(adminConfiguration.IdentityAdminCookieExpiresUtcHours);
                            return Task.CompletedTask;
                        },
                        OnRedirectToIdentityProvider = context =>
                        {
                            if (!string.IsNullOrEmpty(adminConfiguration.IdentityAdminRedirectUri))
                            {
                                context.ProtocolMessage.RedirectUri = adminConfiguration.IdentityAdminRedirectUri;
                            }

                            return Task.CompletedTask;
                        }
                    };
                });
        }
    }
}
