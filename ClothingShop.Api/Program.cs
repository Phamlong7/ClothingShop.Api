using ClothingShop.Api.Data;
using ClothingShop.Api.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using ClothingShop.Api.Middleware;
using ClothingShop.Api.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("Default")
         ?? builder.Configuration["DATABASE_URL"]
         ?? throw new Exception("No connection string configured.");

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(cs));

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// VNPAY
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<VnPayService>();
builder.Services.AddScoped<ExchangeRateService>();
builder.Services.AddHttpClient("exchange", c =>
{
    c.BaseAddress = new Uri("https://api.exchangerate.host");
    c.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ClothingShop.Api", Version = "v1" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    c.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

// Add Identity services for password hashing
builder.Services.AddIdentityCore<User>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<AppDbContext>();

// CORS cho frontend t?nh
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()
));

// JWT Authentication (required for security)
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? jwtIssuer;

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("JWT key is required for authentication. Please configure 'Jwt:Key' in appsettings.json");
}

var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer),
            ValidateAudience = !string.IsNullOrWhiteSpace(jwtAudience),
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            ClockSkew = TimeSpan.Zero
        };
    });

// PayOS HttpClient
builder.Services.AddHttpClient("payos", (sp, http) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>().GetSection("PayOS");
    var baseUrl = cfg["ApiBaseUrl"] ?? "https://api.payos.vn/v2";
    http.BaseAddress = new Uri(baseUrl);
    var clientId = cfg["ClientId"];
    var apiKey = cfg["ApiKey"];
    if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(apiKey))
    {
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{apiKey}"));
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);
    }
});

// ProblemDetails & Validation responses
builder.Services.AddProblemDetails();

builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = ctx =>
    {
        var pd = new ValidationProblemDetails(ctx.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Failed"
        };
        pd.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        pd.Extensions["correlationId"] = ctx.HttpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        return new BadRequestObjectResult(pd);
    };
});

var app = builder.Build();

// Auto-apply EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

// Correlation Id
app.UseMiddleware<CorrelationIdMiddleware>();

// Exception handler that returns ProblemDetails
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async ctx =>
    {
        var exception = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
        var status = exception switch
        {
            FluentValidation.ValidationException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            DbUpdateConcurrencyException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };

        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        var factory = ctx.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateProblemDetails(ctx, statusCode: status, title: status switch
        {
            StatusCodes.Status400BadRequest => "Validation Failed",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status404NotFound => "Not Found",
            _ => "An error occurred"
        });

        // Attach validation errors if any
        if (exception is FluentValidation.ValidationException vex)
        {
            problem.Extensions["errors"] = vex.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        if (builder.Environment.IsDevelopment() && exception is not null)
        {
            problem.Extensions["detail"] = exception.Message;
            problem.Extensions["stackTrace"] = exception.StackTrace;
        }

        problem.Extensions["traceId"] = ctx.TraceIdentifier;
        problem.Extensions["correlationId"] = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        await ctx.Response.WriteAsJsonAsync(problem);
    });
});

// Optionally render status code pages
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
