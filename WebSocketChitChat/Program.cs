using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", true)
    .AddUserSecrets(typeof(Program).Assembly)
    .Build();

using var client = new HttpClient
{
    BaseAddress = new Uri("https://hackattic.com/")
};

var accessToken = config.GetValue<string>("ACCESS_TOKEN");
if (string.IsNullOrWhiteSpace(accessToken))
{
    throw new Exception("ACCESS_TOKEN is not set.");
}

var problemSet = await client.GetFromJsonAsync<GetProblemSet>($"challenges/websocket_chit_chat/problem?access_token={accessToken}");

using var wsClient = new ClientWebSocket();
var secret = "square limit withered water yellow sun";
var sw = new Stopwatch();

await wsClient.ConnectAsync(new Uri($"wss://hackattic.com/_/ws/{problemSet!.token}"), CancellationToken.None);
sw.Start();

while (wsClient.State == WebSocketState.Open)
{
    var buffer = new ArraySegment<byte>(new byte[1024]);
    await wsClient.ReceiveAsync(buffer, CancellationToken.None);
    var message = Encoding.UTF8.GetString(buffer);
    Console.WriteLine($"Message: {message}");
    if (message.StartsWith("ping!"))
    {
        var timeElapsed = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        var interval = GetInterval(timeElapsed);
        await wsClient.SendAsync(Encoding.UTF8.GetBytes(interval.ToString()), WebSocketMessageType.Text, true, CancellationToken.None);
        Console.WriteLine($"Time Elapsed: {timeElapsed} | Interval Sent: {interval}");
    }

    if (message.StartsWith("congratulations!"))
    {
        var from = message.IndexOf("\"");
        var to = message.LastIndexOf("\"");
        secret = message[(from + 1)..to];
    }
}

sw.Stop();

if (string.IsNullOrWhiteSpace(secret))
{
    return;
}

Console.WriteLine($"Solution: {secret}");

var solutionResponse = await client.PostAsJsonAsync<SubmitSolution>(
    $"challenges/websocket_chit_chat/solve?access_token={accessToken}", // add "&playground=1" to replay this challenge
    new(secret)
);

var responseBody = await solutionResponse.Content.ReadAsStringAsync();
Console.WriteLine($"Response: {responseBody}");

static int GetInterval(double elapsedMs)
{
    var possibleIntervals = new int[] { 700, 1500, 2000, 2500, 3000 };
    var values = new ConcurrentBag<(int interval, double value)>();
    Parallel.ForEach(possibleIntervals, interval => values.Add((interval, elapsedMs / interval)));

    return values.OrderBy(x => Math.Abs(x.value - 1)).First().interval;
}

internal record GetProblemSet(string token);
internal record SubmitSolution(string secret);