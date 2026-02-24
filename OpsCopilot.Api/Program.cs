using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

static string GetServiceVersion()
{
    var assembly = typeof(Program).Assembly;

    var informationalVersion = assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion;

    if (!string.IsNullOrWhiteSpace(informationalVersion))
    {
        return informationalVersion;
    }

    return assembly.GetName().Version?.ToString() ?? "unknown";
}

var healthVersion = GetServiceVersion();

app.MapGet("/health", (IHostEnvironment env) =>
{
    return Results.Ok(new
    {
        status = "ok",
        service = env.ApplicationName,
        utcTime = DateTimeOffset.UtcNow,
        version = healthVersion,
    });
})
.WithName("Health");

app.Run();
