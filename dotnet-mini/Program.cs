using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DatabaseService>();

// Make it more fair toward other more strict JSON deserializers (e.g. Rust's serde).
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

// Initialize database on startup
using (var scope = app.Services.CreateScope())
{
    var databaseService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
    await databaseService.InitializeDatabaseAsync();
}

app.MapPost("api/auth/get-user-token",
    static async ([FromBody] LoginRequest request, DatabaseService dbService, ILogger<Program> logger) =>
    {
        var user = await dbService.GetUserByMailAndPasswordAsync(request.Username, request.HashedPassword);
        if (user == null)
            return Results.Ok(new LoginResponse
            {
                Success = false,
                ErrorMessage = "Invalid username or password"
            });

        return Results.Ok(new LoginResponse
        {
            Success = true,
            UserId = user.Id
        });
    });

app.MapGet("/api/auth/create-db", static async (DatabaseService dbService, ILogger<Program> logger) =>
{
    try
    {
        var count = await dbService.CreateUsersAsync(10000);
        return Results.Ok($"Successfully created {count} users in the database");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating database");
        return Results.Problem("An error occurred while creating the database", statusCode: 500);
    }
});

Console.WriteLine("Starting DotnetMini server on:");
foreach (var url in builder.WebHost.GetSetting("urls")?.Split(';')!)
{
    Console.WriteLine($"  {url}");
}

Console.WriteLine("  GET  /api/auth/health");
Console.WriteLine("  POST /api/auth/get-user-token");
Console.WriteLine("  GET  /api/auth/create-db");

app.Run();