using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using ZXing;
using ZXing.CoreCompat.System.Drawing;

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

var problemSet = await client.GetFromJsonAsync<GetProblemSet>($"challenges/reading_qr/problem?access_token={accessToken}");
var imageResponse = await client.GetAsync(problemSet!.image_url);
imageResponse.EnsureSuccessStatusCode();

var reader = new BarcodeReaderGeneric();
using var bitmap = new System.Drawing.Bitmap(System.Drawing.Image.FromStream(await imageResponse.Content.ReadAsStreamAsync()));
var source = new BitmapLuminanceSource(bitmap);
var decoded = reader.Decode(source);

var code = decoded.Text;
Console.WriteLine($"Decoded Code: {code}");

var solutionResponse = await client.PostAsJsonAsync<SubmitSolution>(
    $"challenges/reading_qr/solve?access_token={accessToken}", // add "&playground=1" to replay this challenge
    new(code)
);

var responseBody = await solutionResponse.Content.ReadAsStringAsync();
Console.WriteLine($"Response: {responseBody}");

internal record GetProblemSet(string image_url);
internal record SubmitSolution(string code);