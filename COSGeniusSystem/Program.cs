using System.Text;
using COSGeniusSystem.Data;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Middlewares;
using COSGeniusSystem.Repositories;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Mvc;
using COSGeniusSystem.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using COSGeniusSystem.Services.Doctors;
using COSGeniusSystem.Services.Herbs;
using COSGeniusSystem.Services.TreatmentServices;
using COSGeniusSystem.Services.Examinations;
using COSGeniusSystem.Services.Treatments;
using COSGeniusSystem.Services.Reception;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register MemoryCache
builder.Services.AddMemoryCache();

// Register Repositories and Services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<COSGeniusSystem.Services.Doctors.IDoctorService, COSGeniusSystem.Services.Doctors.DoctorService>();
builder.Services.AddScoped<COSGeniusSystem.Services.TreatmentServices.ITreatmentService, COSGeniusSystem.Services.TreatmentServices.TreatmentService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IPatientContactService, PatientContactService>();
builder.Services.AddScoped<COSGeniusSystem.Services.Examinations.IExaminationService, COSGeniusSystem.Services.Examinations.ExaminationService>();
builder.Services.AddScoped<COSGeniusSystem.Services.Treatments.ITreatmentWorkflowService, COSGeniusSystem.Services.Treatments.TreatmentWorkflowService>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    // Avoid MVC's default ProblemDetails wrapper alongside ApiResponseModel.
    options.SuppressMapClientErrors = true;
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = SystemMessages.ValidationFailed;
        var data = ValidationErrorData.FromModelState(context.ModelState);
        return new BadRequestObjectResult(new ApiResponseModel<ValidationErrorData>(message.Code, message.Message, false, data));
    };
});

// Configure JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "COSGeniusSystem";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "COSGeniusClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(
                new ApiResponseModel<object?>(401, "Authentication is required.", false, null), context.HttpContext.RequestAborted);
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new ApiResponseModel<object?>(403, "Access is forbidden.", false, null), context.HttpContext.RequestAborted);
        },
        OnTokenValidated = context =>
        {
            var blacklistService = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklistService>();
            var authHeader = context.Request.Headers.Authorization.ToString();
            var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader["Bearer ".Length..].Trim()
                : authHeader.Trim();

            if (blacklistService.IsTokenBlacklisted(token))
            {
                context.Fail("This token has been revoked.");
            }

            return Task.CompletedTask;
        }
    };
});

// Đăng ký các Service vào DI container
builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<ITreatmentService, TreatmentService>();
builder.Services.AddScoped<IHerbService, HerbService>();
builder.Services.AddScoped<COSGeniusSystem.Services.Suppliers.ISupplierService, COSGeniusSystem.Services.Suppliers.SupplierService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IExaminationService, ExaminationService>();
builder.Services.AddScoped<ITreatmentProtocolService, TreatmentProtocolService>();
builder.Services.AddScoped<ITreatmentQueueService, TreatmentProtocolService>();
builder.Services.AddScoped<IReceptionDashboardService, ReceptionDashboardService>();

builder.Services.AddAuthorization();

// Configure OpenAPI & Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Automatically apply migrations on startup (with retry for container startup)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    for (var i = 0; i < 10; i++)
    {
        try
        {
            if (dbContext.Database.GetPendingMigrations().Any())
            {
                dbContext.Database.Migrate();
                logger.LogInformation("Database migrations applied successfully.");
            }
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Waiting for database to be ready... Attempt {Attempt}/10. Reason: {Message}", i + 1, ex.Message);
            Console.WriteLine(ex.ToString());
            Thread.Sleep(3000);
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Supply a wrapper fallback so the middleware needs no ProblemDetails service.
// Registered IExceptionHandler implementations run before this fallback.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    ExceptionHandler = async context =>
    {
        var message = SystemMessages.InternalError;
        context.Response.StatusCode = message.Code;
        await context.Response.WriteAsJsonAsync(
            new ApiResponseModel<object?>(message.Code, message.Message, false, null), context.RequestAborted);
    }
});

app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    if (!context.Request.Path.StartsWithSegments("/api")) return;
    var code = context.Response.StatusCode;
    await context.Response.WriteAsJsonAsync(
        new ApiResponseModel<object?>(code,
            Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(code), false, null), context.RequestAborted);
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
