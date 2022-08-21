using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json")
    .Build();

var client = new HttpClient
{
    BaseAddress = new Uri("https://hackattic.com/")
};

var accessToken = config.GetValue<string>("ACCESS_TOKEN");
if (string.IsNullOrWhiteSpace(accessToken))
{
    throw new Exception("ACCESS_TOKEN is not set.");
}

var problemSet = await client.GetFromJsonAsync<GetProblemSet>($"challenges/websocket_chit_chat/problem?access_token={accessToken}");

var wsClient = new ClientWebSocket();
var secret = string.Empty;
var sw = new Stopwatch();

await wsClient.ConnectAsync(new Uri($"wss://hackattic.com/_/ws/{problemSet!.token}"), CancellationToken.None);
sw.Start();

while (wsClient.State == WebSocketState.Open)
{
    var buffer = new ArraySegment<byte>(new byte[1024]);
    await wsClient.ReceiveAsync(buffer, CancellationToken.None);
    var message = Encoding.UTF8.GetString(buffer);
    Console.WriteLine(message);
    if (message.StartsWith("ping!"))
    {
        var interval = sw.Elapsed.Milliseconds - (sw.Elapsed.Milliseconds % 100);
        Console.WriteLine(interval);
        await wsClient.SendAsync(Encoding.UTF8.GetBytes(interval.ToString()), WebSocketMessageType.Text, false, CancellationToken.None);
        sw.Restart();
    }
}

sw.Stop();

if (string.IsNullOrWhiteSpace(secret))
{
    return;
}

var solutionResponse = await client.PostAsJsonAsync<SubmitSolution>(
    $"challenges/websocket_chit_chat/solve?access_token={accessToken}", // add "&playground=1" to replay this challenge
    new(secret)
);

var responseBody = await solutionResponse.Content.ReadAsStringAsync();
Console.WriteLine(responseBody);
Console.ReadLine();

internal record GetProblemSet(string token);
internal record SubmitSolution(string secret);