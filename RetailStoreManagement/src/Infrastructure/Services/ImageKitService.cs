using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.ImageKit.Dtos;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public class ImageKitService(IConfiguration configuration) : IImageKitService
{
    public ImageKitAuthResponseDto GetAuth()
    {
        var privateKey = configuration["ImageKit:PrivateKey"];
        var publicKey = configuration["ImageKit:PublicKey"];

        if (string.IsNullOrEmpty(privateKey) || string.IsNullOrEmpty(publicKey))
        {
            throw new Exception("ImageKit configuration is missing.");
        }

        var token = Guid.NewGuid().ToString();
        var expire = DateTimeOffset.UtcNow.AddMinutes(55).ToUnixTimeSeconds();
        var signature = GenerateSignature(token, expire.ToString(), privateKey);

        return new ImageKitAuthResponseDto
        {
            Signature = signature,
            Token = token,
            Expire = expire,
            PublicKey = publicKey ?? ""
        };
    }

    public async Task DeleteFileAsync(string fileId)
    {
        if (string.IsNullOrWhiteSpace(fileId))
            throw new Exception("fileId is required");

        var privateKey = configuration["ImageKit:PrivateKey"];
        if (string.IsNullOrEmpty(privateKey))
            throw new Exception("ImageKit configuration is missing.");

        var requestUrl = $"https://api.imagekit.io/v1/files/{fileId}";
        using var httpClient = new HttpClient();
        var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{privateKey}:"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await httpClient.DeleteAsync(requestUrl);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to delete ImageKit file: {content}");
        }
    }

    private static string GenerateSignature(string token, string expire, string privateKey)
    {
        var data = token + expire;
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(privateKey));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
