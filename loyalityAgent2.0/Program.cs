using loyalityAgent2._0.Services;
using loyalityAgent2._0.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register SignalR
builder.Services.AddSignalR();

// Register Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register HttpContextAccessor for accessing HttpContext
builder.Services.AddHttpContextAccessor();

// Register connection context and API call logging services
builder.Services.AddScoped<IConnectionContextService, ConnectionContextService>();
builder.Services.AddScoped<IApiCallLoggerService, ApiCallLoggerService>();

// Register the logging handler - must be transient for HttpClientFactory
builder.Services.AddTransient<ApiCallLoggingHandler>();

// Register HttpClient for API services with logging handler
// Note: AddHttpClient automatically registers the service, so we don't need AddScoped for these
builder.Services.AddHttpClient<IGeminiService, GeminiService>()
    .AddHttpMessageHandler<ApiCallLoggingHandler>();
builder.Services.AddHttpClient<IGeoapifyService, GeoapifyService>()
    .AddHttpMessageHandler<ApiCallLoggingHandler>();
builder.Services.AddHttpClient<IMenuScraperService, MenuScraperService>()
    .AddHttpMessageHandler<ApiCallLoggingHandler>();

// Register application services (non-HttpClient services)
builder.Services.AddScoped<IAgentOrchestratorService, AgentOrchestratorService>();
builder.Services.AddScoped<ISimilarBusinessService, SimilarBusinessService>();

// Enable CORS for development (SignalR compatible)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Allow any origin
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();
app.MapHub<loyalityAgent2._0.Hubs.WorkflowHub>("/workflowHub")
    .RequireCors("AllowAll"); // Apply CORS to SignalR hub

app.Run();
