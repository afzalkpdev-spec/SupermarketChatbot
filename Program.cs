using System.Text;
using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Flows;
using SupermarketBot.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
var builder = WebApplication.CreateBuilder(args);

// Postgres via EF Core / Npgsql
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// HttpClient for calling the WhatsApp Graph API
builder.Services.AddHttpClient();


// ---------------------------------------------------------------
// CORS — allows the separate React admin portal (different origin)
// to call this API from the browser.
// ---------------------------------------------------------------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminPortal", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ---------------------------------------------------------------
// JWT auth — protects the /admin/* endpoints the portal calls.
// The WhatsApp webhook endpoints are NOT protected by this (Meta
// calls them directly and can't send a bearer token) — they rely on
// the webhook verify token instead, checked manually in the controller.
// ---------------------------------------------------------------
var jwtSecret = builder.Configuration["Jwt:Secret"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            //IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();


// App services and flows
builder.Services.AddScoped<WhatsAppService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<AddressService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<BotSettingsService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<OrderStatusService>();
builder.Services.AddScoped<GreetingFlow>();
builder.Services.AddScoped<BrowsingFlow>();
builder.Services.AddScoped<CartFlow>();
builder.Services.AddScoped<CheckoutFlow>();
builder.Services.AddScoped<OrderTrackingFlow>();

// Admin portal services
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AdminOrderService>();

builder.Services.AddScoped<OfferService>();
builder.Services.AddScoped<CloudinaryService>();

builder.Services.AddControllers();

var app = builder.Build();

app.UseCors("AdminPortal");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "Supermarket WhatsApp bot is running.");

app.Run();
