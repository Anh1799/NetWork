using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.OpenApi.Models;
using NetLab.Api.Clients;
using NetLab.Api.Configuration;
using NetLab.Api.Middleware;
using NetLab.Api.Services;
using NetLab.Api.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// ---------- Configuration ----------
builder.Services.Configure<NetworkOptions>(builder.Configuration.GetSection(NetworkOptions.SectionName));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));

var security = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();
if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(security.ApiKey))
    throw new InvalidOperationException(
        "Security:ApiKey is required in Production because this API opens outbound network connections. Set Security__ApiKey.");

// ---------- Networking services ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IHostGuard, HostGuard>();
builder.Services.AddScoped<INetworkDiagnostics, NetworkDiagnostics>();
builder.Services.AddSingleton<ChatHub>();
builder.Services.AddTransient<CorrelationIdHandler>();

// HttpClient dành cho "probe": handler tự kiểm tra IP lúc kết nối, không theo redirect, timeout do code điều khiển
builder.Services
    .AddHttpClient("probe", client =>
    {
        client.Timeout = Timeout.InfiniteTimeSpan;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NetLab/1.0");
    })
    .ConfigurePrimaryHttpMessageHandler(sp => GuardedHandler.Create(sp.GetRequiredService<IHostGuard>()));

// Typed clients gọi API bên ngoài: correlation id + retry/timeout/circuit breaker
builder.Services
    .AddHttpClient<IHolidayClient, HolidayClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["HolidayApi:BaseUrl"] ?? "https://date.nager.at/api/v3/");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NetLab/1.0");
    })
    .AddHttpMessageHandler<CorrelationIdHandler>()
    .AddStandardResilienceHandler();

builder.Services
    .AddHttpClient<IPublicIpClient, PublicIpClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["PublicIpApi:BaseUrl"] ?? "https://api.ipify.org/");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NetLab/1.0");
    })
    .AddHttpMessageHandler<CorrelationIdHandler>()
    .AddStandardResilienceHandler();

// ---------- CORS ----------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));

// ---------- Rate limiting (theo IP, 100 request/phút) ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// ---------- Controllers, health, Swagger ----------
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NetLab API",
        Version = "v1",
        Description = "Phòng thí nghiệm networking: DNS, ping, TCP, HTTP, TLS, WebSocket, SSE, typed HttpClient."
    });

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = ApiKeyMiddleware.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "API key (chỉ cần khi server bật Security:ApiKey)."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } },
            Array.Empty<string>()
        }
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// ---------- HTTP pipeline (thứ tự quan trọng) ----------
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();   // wwwroot/index.html: trang thử nghiệm

app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

app.MapControllers();
app.MapHealthChecks("/health");

app.Map("/ws/echo", (HttpContext context) => EchoWebSocket.HandleAsync(context));
app.Map("/ws/chat", (HttpContext context, ChatHub hub) => hub.HandleAsync(context));

app.Run();

// Cho phép WebApplicationFactory<Program> trong project test
public partial class Program { }
