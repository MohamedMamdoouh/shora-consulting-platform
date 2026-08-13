using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Shora.Api.BackgroundJobs;
using Shora.Api.Infrastructure;
using Shora.Application.Options;
using Shora.Domain.Entities;
using Shora.Infrastructure.Data;

namespace Shora.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(entry => entry.Value?.Errors.Count > 0)
                        .ToDictionary(
                            entry => entry.Key,
                            entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

                    var problem = ApiProblemDetailsMapper.FromValidationErrors(errors, context.HttpContext);
                    return new BadRequestObjectResult(problem);
                };
            });

        services.AddOpenApi();

        services.Configure<BackgroundJobOptions>(configuration.GetSection(BackgroundJobOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));
        services.Configure<OpsMonitoringOptions>(configuration.GetSection(OpsMonitoringOptions.SectionName));
        services.Configure<ReceiptUploadOptions>(configuration.GetSection(ReceiptUploadOptions.SectionName));
        services.AddRateLimiting(configuration);
        services.AddHostedService<ReceiptUploadDeadlineCleanupJob>();
        services.AddHostedService<ReceiptRetentionPurgeJob>();
        services.AddHostedService<TempBlobCleanupJob>();
        services.AddHostedService<OutboxDispatcherJob>();
        services.AddHostedService<CancellationRequestAutoDeclineJob>();
        services.AddHostedService<BookingAutoCompleteJob>();
        services.AddHostedService<RefreshTokenPurgeJob>();
        services.AddHostedService<ReceiptBlobReconciliationJob>();
        services.AddHostedService<AvailabilityTopUpJob>();
        services.AddHostedService<OpsMonitoringJob>();

        return services;
    }

    private static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rateLimitOptions = configuration
            .GetSection(RateLimitOptions.SectionName)
            .Get<RateLimitOptions>() ?? new RateLimitOptions();

        var receiptUploadOptions = configuration
            .GetSection(ReceiptUploadOptions.SectionName)
            .Get<ReceiptUploadOptions>() ?? new ReceiptUploadOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsync("Too many requests.", cancellationToken);
            };

            options.AddPolicy(RateLimitPolicies.AuthCredential, httpContext =>
                RateLimitPartitionFactory.FixedWindowByIp(
                    httpContext,
                    "auth-credential",
                    rateLimitOptions.AuthCredentialPermitLimit,
                    TimeSpan.FromMinutes(rateLimitOptions.AuthCredentialWindowMinutes)));

            options.AddPolicy(RateLimitPolicies.AuthRecovery, httpContext =>
                RateLimitPartitionFactory.FixedWindowByIp(
                    httpContext,
                    "auth-recovery",
                    rateLimitOptions.AuthRecoveryPermitLimit,
                    TimeSpan.FromMinutes(rateLimitOptions.AuthRecoveryWindowMinutes)));

            options.AddPolicy(RateLimitPolicies.AuthRefresh, httpContext =>
                RateLimitPartitionFactory.FixedWindowByIp(
                    httpContext,
                    "auth-refresh",
                    rateLimitOptions.AuthRefreshPermitLimit,
                    TimeSpan.FromMinutes(rateLimitOptions.AuthRefreshWindowMinutes)));

            options.AddPolicy(RateLimitPolicies.PublicAvailability, httpContext =>
                RateLimitPartitionFactory.FixedWindowByIp(
                    httpContext,
                    "availability",
                    rateLimitOptions.PublicAvailabilityPermitLimit,
                    TimeSpan.FromMinutes(rateLimitOptions.PublicAvailabilityWindowMinutes)));

            options.AddPolicy(RateLimitPolicies.BookingReserve, httpContext =>
                RateLimitPartitionFactory.FixedWindowByIp(
                    httpContext,
                    "booking-reserve",
                    rateLimitOptions.BookingReservePermitLimit,
                    TimeSpan.FromMinutes(rateLimitOptions.BookingReserveWindowMinutes)));

            options.AddPolicy(RateLimitPolicies.CancellationRequest, httpContext =>
                RateLimitPartitionFactory.FixedWindowByIp(
                    httpContext,
                    "cancellation-request",
                    rateLimitOptions.CancellationRequestPermitLimit,
                    TimeSpan.FromMinutes(rateLimitOptions.CancellationRequestWindowMinutes)));

            options.AddPolicy(RateLimitPolicies.ReceiptUpload, httpContext =>
                RateLimitPartitionFactory.FixedWindowByIp(
                    httpContext,
                    "receipt-upload",
                    receiptUploadOptions.RateLimitPermitLimit,
                    TimeSpan.FromMinutes(receiptUploadOptions.RateLimitWindowMinutes)));
        });

        return services;
    }

    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }

    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = GetValidatedJwtOptions(configuration);
        var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
            ?? new CorsOptions();

        services.AddCors(options =>
        {
            options.AddPolicy(CorsOptions.PolicyName, policy =>
            {
                policy.WithOrigins(corsOptions.EffectiveOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = true;
                options.TokenHandlers.Clear();
                options.TokenHandlers.Add(new JwtSecurityTokenHandler
                {
                    MapInboundClaims = true
                });
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };
            });

        services.AddAuthorization();

        return services;
    }

    private static JwtOptions GetValidatedJwtOptions(IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is missing.");

        var validation = new JwtOptionsValidator().Validate(null, jwtOptions);
        if (validation.Failed)
        {
            throw new InvalidOperationException(validation.FailureMessage);
        }

        return jwtOptions;
    }
}
