#!/usr/bin/env dotnet-script

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

// Configuration from appsettings.json
var baseUrl = "https://music.abcradio.net.au/api/v1/";
var stations = new[] { "TripleJ", "DoubleJ", "Unearthed" };
var regions = new[] { "NSW", "ACT", "VIC", "TAS", "QLD", "WA", "SA", "NT" };

var endpoints = new Dictionary<string, Dictionary<string, string>>
{
    ["TripleJ"] = new()
    {
        ["NSW"] = "plays/triplej/sydney/now.json",
        ["ACT"] = "plays/triplej/canberra/now.json",
        ["VIC"] = "plays/triplej/melbourne/now.json",
        ["TAS"] = "plays/triplej/hobart/now.json",
        ["QLD"] = "plays/triplej/brisbane/now.json",
        ["WA"] = "plays/triplej/perth/now.json",
        ["SA"] = "plays/triplej/adelaide/now.json",
        ["NT"] = "plays/triplej/darwin/now.json"
    },
    ["DoubleJ"] = new()
    {
        ["NSW"] = "plays/doublej/sydney/now.json",
        ["ACT"] = "plays/doublej/canberra/now.json",
        ["VIC"] = "plays/doublej/melbourne/now.json",
        ["TAS"] = "plays/doublej/hobart/now.json",
        ["QLD"] = "plays/doublej/brisbane/now.json",
        ["WA"] = "plays/doublej/perth/now.json",
        ["SA"] = "plays/doublej/adelaide/now.json",
        ["NT"] = "plays/doublej/darwin/now.json"
    },
    ["Unearthed"] = new()
    {
        ["NSW"] = "plays/unearthed/sydney/now.json",
        ["ACT"] = "plays/unearthed/canberra/now.json",
        ["VIC"] = "plays/unearthed/melbourne/now.json",
        ["TAS"] = "plays/unearthed/hobart/now.json",
        ["QLD"] = "plays/unearthed/brisbane/now.json",
        ["WA"] = "plays/unearthed/perth/now.json",
        ["SA"] = "plays/unearthed/adelaide/now.json",
        ["NT"] = "plays/unearthed/darwin/now.json"
    }
};

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("User-Agent", "jaybird-api-test/1.0");

var results = new List<(string Station, string Region, bool Success, string Message, string? Response)>();

Console.WriteLine("Testing jaybird regional API endpoints...\n");

foreach (var station in stations)
{
    foreach (var region in regions)
    {
        var endpoint = endpoints[station][region];
        var url = $"{baseUrl}{endpoint}";
        
        try
        {
            Console.WriteLine($"Testing {station} - {region}: {url}");
            
            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                // Try to parse JSON and check structure
                try
                {
                    using var jsonDoc = JsonDocument.Parse(content);
                    var root = jsonDoc.RootElement;
                    
                    // Check for expected fields
                    var hasArtist = root.TryGetProperty("artist", out var artistProp) && !string.IsNullOrEmpty(artistProp.GetString());
                    var hasTitle = root.TryGetProperty("title", out var titleProp) && !string.IsNullOrEmpty(titleProp.GetString());
                    var hasRelease = root.TryGetProperty("release", out var releaseProp);
                    
                    if (hasArtist && hasTitle)
                    {
                        var artist = artistProp.GetString();
                        var title = titleProp.GetString();
                        var release = hasRelease ? releaseProp.GetString() : "N/A";
                        
                        results.Add((station, region, true, $"✓ {artist} - {title} ({release})", content));
                        Console.WriteLine($"  ✓ SUCCESS: {artist} - {title} ({release})");
                    }
                    else
                    {
                        results.Add((station, region, false, "✗ Missing required fields (artist/title)", content));
                        Console.WriteLine($"  ✗ FAILED: Missing required fields (artist/title)");
                    }
                }
                catch (JsonException ex)
                {
                    results.Add((station, region, false, $"✗ Invalid JSON: {ex.Message}", content));
                    Console.WriteLine($"  ✗ FAILED: Invalid JSON - {ex.Message}");
                }
            }
            else
            {
                results.Add((station, region, false, $"✗ HTTP {response.StatusCode}: {response.ReasonPhrase}", content));
                Console.WriteLine($"  ✗ FAILED: HTTP {response.StatusCode} - {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            results.Add((station, region, false, $"✗ Exception: {ex.Message}", null));
            Console.WriteLine($"  ✗ FAILED: Exception - {ex.Message}");
        }
        
        // Small delay to avoid overwhelming the API
        await Task.Delay(100);
    }
    Console.WriteLine();
}

// Summary
Console.WriteLine("\n=== SUMMARY ===");
var successCount = 0;
var failureCount = 0;

foreach (var station in stations)
{
    Console.WriteLine($"\n{station}:");
    foreach (var region in regions)
    {
        var result = results.Find(r => r.Station == station && r.Region == region);
        if (result.Success)
        {
            successCount++;
            Console.WriteLine($"  {region}: {result.Message}");
        }
        else
        {
            failureCount++;
            Console.WriteLine($"  {region}: {result.Message}");
        }
    }
}

Console.WriteLine($"\n=== OVERALL RESULTS ===");
Console.WriteLine($"Total endpoints tested: {results.Count}");
Console.WriteLine($"Successful: {successCount}");
Console.WriteLine($"Failed: {failureCount}");
Console.WriteLine($"Success rate: {(double)successCount / results.Count * 100:F1}%");

if (failureCount > 0)
{
    Console.WriteLine($"\nFailed endpoints:");
    foreach (var result in results.Where(r => !r.Success))
    {
        Console.WriteLine($"  {result.Station} - {result.Region}: {result.Message}");
    }
}