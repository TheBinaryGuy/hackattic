using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("HAClient", config =>
{
    config.BaseAddress = new Uri("https://hackattic.com/");
});

builder.Services.AddHttpContextAccessor();

var accumulatedValue = string.Empty;
var handler = new JwtSecurityTokenHandler();
var parameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    ValidateAudience = false,
    ValidateIssuer = false,
    LifetimeValidator = (DateTime? notBefore, DateTime? expires, SecurityToken _, TokenValidationParameters __) => _ switch
    {
        _ when expires is null && notBefore is null => true,
        _ when expires is null => notBefore <= DateTime.UtcNow,
        _ when notBefore is null => expires > DateTime.UtcNow,
        _ => expires > DateTime.UtcNow && notBefore <= DateTime.UtcNow,
    }
};

var app = builder.Build();

app.MapPost("/", async (IHttpContextAccessor accessor) =>
{
    using var reader = new StreamReader(accessor.HttpContext!.Request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync();

    var validatedToken = await handler.ValidateTokenAsync(body, parameters);
    if (!validatedToken.IsValid)
    {
        return Results.BadRequest();
    }

    var hasClaim = validatedToken.Claims.TryGetValue("append", out var value);

    if (hasClaim)
    {
        accumulatedValue += value;
        return Results.Ok();
    }

    return Results.Ok(new { solution = accumulatedValue });
});

await app.StartAsync();

var config = app.Services.GetRequiredService<IConfiguration>();
var webhookUrl = config.GetValue<string>("WEBHOOK_URL");
if (string.IsNullOrWhiteSpace(webhookUrl))
{
    throw new Exception("WEBHOOK_URL is not set.");
}

var accessToken = config.GetValue<string>("ACCESS_TOKEN");
if (string.IsNullOrWhiteSpace(accessToken))
{
    throw new Exception("ACCESS_TOKEN is not set.");
}

var factory = app.Services.GetRequiredService<IHttpClientFactory>();
var client = factory.CreateClient("HAClient");

var problemSet = await client.GetFromJsonAsync<GetProblemSet>($"challenges/jotting_jwts/problem?access_token={accessToken}");
parameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(problemSet!.jwt_secret));

var solutionResponse = await client.PostAsJsonAsync<SubmitSolution>(
    $"challenges/jotting_jwts/solve?access_token={accessToken}", // add "&playground=1" to replay this challenge
    new(webhookUrl)
);

// var responseBody = await solutionResponse.Content.ReadAsStringAsync();
// var logger = app.Services.GetRequiredService<ILogger<Program>>();
// logger.LogInformation(responseBody);
// await app.WaitForShutdownAsync();

internal record GetProblemSet(string jwt_secret);
internal record SubmitSolution(string app_url);