using RazorLight;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "CupidSession";
});

builder.Services.AddSingleton<FlagService>();

builder.Services.AddSingleton<IRazorLightEngine>(sp =>
{
    var engine = new RazorLightEngineBuilder()
        .UseMemoryCachingProvider()
        .Build();
    return engine;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapRazorPages();

app.Run();

public class FlagService
{
    private const string FlagBase = "cupids_arrow_struck_my_heart_this_valentine";
    private readonly string _ctfdUrl;
    private readonly int _challengeId;
    private readonly HttpClient _httpClient;
    private readonly Random _random = new();
    
    private static readonly Dictionary<char, string[]> LeetMap = new()
    {
        ['a'] = new[] { "4", "@" },
        ['b'] = new[] { "8" },
        ['e'] = new[] { "3" },
        ['g'] = new[] { "6", "9" },
        ['i'] = new[] { "1", "!" },
        ['l'] = new[] { "1", "|" },
        ['o'] = new[] { "0", "O" },
        ['s'] = new[] { "5", "$" },
        ['t'] = new[] { "7", "+" },
        ['c'] = new[] { "(", "<" }
    };
    
    public FlagService()
    {
        _ctfdUrl = Environment.GetEnvironmentVariable("CTFD_URL")?.TrimEnd('/') 
            ?? "https://noobctf.infoseciitr.in";
        _challengeId = int.TryParse(Environment.GetEnvironmentVariable("CHALLENGE_ID"), out var id) 
            ? id : 43;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {GetApiKey()}");
    }
    
    public string GenerateFlag(string userToken)
    {
        var existingFlags = FetchExistingFlags();
        var flag = GenerateUniqueFlag(existingFlags);
        
        try
        {
            UploadFlag(flag).GetAwaiter().GetResult();
            Console.WriteLine($"[+] Flag uploaded successfully: {flag}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Error uploading flag: {ex.Message}");
        }
        
        return flag;
    }
    
    private HashSet<string> FetchExistingFlags()
    {
        try
        {
            var response = _httpClient.GetAsync($"{_ctfdUrl}/api/v1/flags").GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                return new HashSet<string>();
            
            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var result = System.Text.Json.JsonSerializer.Deserialize<FlagsResponse>(json);
            return result?.Data?.Select(f => f.Content).ToHashSet() ?? new HashSet<string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Error fetching existing flags: {ex.Message}");
            return new HashSet<string>();
        }
    }
    
    private string GenerateUniqueFlag(HashSet<string> existing)
    {
        string flag;
        do
        {
            flag = GenFlag();
        } while (existing.Contains(flag));
        return flag;
    }
    
    private string GenFlag()
    {
        // 1. Leetspeak substitution
        var content = new StringBuilder();
        foreach (var c in FlagBase)
        {
            var lowerChar = char.ToLower(c);
            if (LeetMap.TryGetValue(lowerChar, out var subs) && _random.NextDouble() < 0.5)
            {
                content.Append(subs[_random.Next(subs.Length)]);
            }
            else
            {
                content.Append(c);
            }
        }
        
        // 2. Random character casing
        var finalContent = new StringBuilder();
        foreach (var r in content.ToString())
        {
            if (r >= 'a' && r <= 'z')
            {
                finalContent.Append(_random.Next(2) == 0 ? char.ToUpper(r) : r);
            }
            else
            {
                finalContent.Append(r);
            }
        }
        
        return $"n00bCTF{{{finalContent}}}";
    }
    
    private async Task UploadFlag(string flag)
    {
        var payload = new
        {
            challenge_id = _challengeId,
            content = flag,
            type = "static",
            data = ""
        };
        
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_ctfdUrl}/api/v1/flags", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"Bad status: {(int)response.StatusCode}. Response: {body}");
        }
        
        var result = await response.Content.ReadAsStringAsync();
        var apiResult = System.Text.Json.JsonSerializer.Deserialize<ApiResponse>(result);
        if (apiResult?.Success != true)
        {
            throw new Exception("API returned success: false");
        }
    }
    
    private static string GetApiKey()
    {
        byte[] key = { 109, 121, 95, 115, 52, 102, 51, 95, 115, 51, 99, 114, 51, 55, 95, 110, 48, 95, 48, 110, 51, 95, 99, 52, 110, 95, 103, 117, 51, 53, 53, 95, 114, 49, 103, 104, 55 };
        byte[] encrypted = { 14, 13, 57, 23, 107, 0, 1, 57, 71, 1, 86, 16, 82, 3, 111, 94, 7, 105, 7, 89, 81, 107, 90, 13, 15, 110, 84, 71, 6, 87, 80, 57, 67, 83, 2, 88, 2, 90, 31, 106, 74, 6, 86, 1, 109, 68, 82, 81, 67, 87, 84, 57, 15, 82, 57, 81, 86, 5, 109, 87, 13, 92, 103, 84, 16, 86, 13, 80, 105 };
        
        var decrypted = new byte[encrypted.Length];
        for (int i = 0; i < encrypted.Length; i++)
        {
            decrypted[i] = (byte)(encrypted[i] ^ key[i % key.Length]);
        }
        return Encoding.ASCII.GetString(decrypted);
    }
}

public class FlagsResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("data")]
    public List<FlagData>? Data { get; set; }
}

public class FlagData
{
    [System.Text.Json.Serialization.JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class ApiResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("success")]
    public bool Success { get; set; }
}

