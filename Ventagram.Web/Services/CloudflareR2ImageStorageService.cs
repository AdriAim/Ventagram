using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Ventagram.Services;

public sealed class CloudflareR2ImageStorageService
{
    private static readonly HashSet<string> AllowedVideoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4",
        "video/webm",
        "video/quicktime"
    };

    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CloudflareR2ImageStorageService> _logger;
    private readonly Lazy<AmazonS3Client> _client;
    private readonly Lazy<byte[]> _watermarkBytes;

    public CloudflareR2ImageStorageService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<CloudflareR2ImageStorageService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        _client = new Lazy<AmazonS3Client>(CreateClient);
        _watermarkBytes = new Lazy<byte[]>(LoadWatermarkBytes);
    }

    public async Task<List<string>> UploadPublicationImagesAsync(IReadOnlyList<IFormFile> files, CancellationToken cancellationToken = default)
    {
        var options = GetOptions();
        ValidateConfiguration(options);

        var urls = new List<string>();
        foreach (var file in files.Where(x => x.Length > 0).Take(11))
        {
            var url = await ProcessAndUploadAsync(file, options, cancellationToken);
            urls.Add(url);
        }

        return urls;
    }

    public async Task<string> UploadPublicationVideoAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("El video esta vacio.");
        }

        var options = GetOptions();
        ValidateConfiguration(options);

        var contentType = NormalizeVideoContentType(file);
        if (!AllowedVideoContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException("El video debe estar en formato MP4, WEBM o MOV.");
        }

        if (file.Length > options.MaxVideoBytes)
        {
            throw new InvalidOperationException($"El video supera el limite de {options.MaxVideoBytes / (1024 * 1024)} MB.");
        }

        await using var inputStream = file.OpenReadStream();
        var extension = ResolveVideoExtension(file.FileName, contentType);
        var key = BuildObjectKey(options, extension);
        var request = new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = key,
            InputStream = inputStream,
            ContentType = contentType,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };
        request.Headers.CacheControl = "public, max-age=31536000, immutable";

        await _client.Value.PutObjectAsync(request, cancellationToken);

        return BuildPublicUrl(options.PublicBaseUrl, key);
    }

    public async Task DeletePublicObjectsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        var options = GetOptions();
        ValidateConfiguration(options);

        var keys = urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => TryExtractManagedObjectKey(url!, options.PublicBaseUrl))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var key in keys)
        {
            try
            {
                await _client.Value.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = options.Bucket,
                    Key = key
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo borrar el objeto {ObjectKey} de R2.", key);
            }
        }
    }

    private async Task<string> ProcessAndUploadAsync(IFormFile file, R2Options options, CancellationToken cancellationToken)
    {
        await using var inputStream = file.OpenReadStream();
        using var image = await Image.LoadAsync(inputStream, cancellationToken);

        if (Math.Max(image.Width, image.Height) > options.MaxImageSide)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(options.MaxImageSide, options.MaxImageSide),
                Sampler = KnownResamplers.Lanczos3
            }));
        }

        ApplyWatermark(image, options);
        var webpQuality = ResolveWebpQuality(file.Length, options);

        await using var output = new MemoryStream();
        await image.SaveAsWebpAsync(output, new WebpEncoder
        {
            Quality = webpQuality
        }, cancellationToken);

        _logger.LogInformation(
            "Compressed publication image {FileName} from {OriginalBytes} bytes to {CompressedBytes} bytes using q={Quality} and {Width}x{Height}.",
            file.FileName,
            file.Length,
            output.Length,
            webpQuality,
            image.Width,
            image.Height);

        output.Position = 0;
        var key = BuildObjectKey(options, ".webp");
        var request = new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = key,
            InputStream = output,
            ContentType = "image/webp",
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };
        request.Headers.CacheControl = "public, max-age=31536000, immutable";

        await _client.Value.PutObjectAsync(request, cancellationToken);

        return BuildPublicUrl(options.PublicBaseUrl, key);
    }

    private void ApplyWatermark(Image image, R2Options options)
    {
        var watermarkBytes = _watermarkBytes.Value;
        using var watermark = Image.Load(watermarkBytes);

        var targetWidth = Math.Clamp((int)(image.Width * options.WatermarkScale), 96, 260);
        var targetHeight = (int)Math.Round(watermark.Height * (targetWidth / (double)watermark.Width));
        watermark.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Stretch,
            Size = new Size(targetWidth, targetHeight),
            Sampler = KnownResamplers.Lanczos3
        }));

        var margin = Math.Max(12, image.Width / 40);
        var x = Math.Max(margin, image.Width - watermark.Width - margin);
        var y = Math.Max(margin, image.Height - watermark.Height - margin);
        image.Mutate(ctx => ctx.DrawImage(watermark, new Point(x, y), options.WatermarkOpacity));
    }

    private AmazonS3Client CreateClient()
    {
        var options = GetOptions();
        ValidateConfiguration(options);

        var config = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = options.Region
        };

        return new AmazonS3Client(options.AccessKeyId, options.SecretAccessKey, config);
    }

    private byte[] LoadWatermarkBytes()
    {
        var watermarkPath = Path.Combine(_environment.WebRootPath, "images", "marcaagua.png");
        if (File.Exists(watermarkPath))
        {
            return File.ReadAllBytes(watermarkPath);
        }

        throw new FileNotFoundException("No se encontró wwwroot/images/marcaagua.png para la marca de agua.");
    }

    private R2Options GetOptions()
    {
        var section = _configuration.GetSection("Cloudflare:R2");
        var serviceUrl = section["ServiceUrl"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(serviceUrl) && !string.IsNullOrWhiteSpace(section["AccountId"]))
        {
            serviceUrl = $"https://{section["AccountId"]}.r2.cloudflarestorage.com";
        }

        return new R2Options
        {
            AccountId = section["AccountId"] ?? string.Empty,
            AccessKeyId = section["AccessKeyId"] ?? string.Empty,
            SecretAccessKey = section["SecretAccessKey"] ?? string.Empty,
            Bucket = section["Bucket"] ?? string.Empty,
            PublicBaseUrl = section["PublicBaseUrl"] ?? string.Empty,
            ServiceUrl = serviceUrl,
            Prefix = section["Prefix"] ?? "publications",
            Region = section["Region"] ?? "auto",
            MaxImageSide = section.GetValue("MaxImageSide", 1600),
            WebpQuality = section.GetValue("WebpQuality", 82),
            WatermarkScale = section.GetValue("WatermarkScale", 0.14f),
            WatermarkOpacity = section.GetValue("WatermarkOpacity", 0.45f),
            MaxVideoBytes = section.GetValue("MaxVideoBytes", 80 * 1024 * 1024)
        };
    }

    private static void ValidateConfiguration(R2Options options)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ServiceUrl) && string.IsNullOrWhiteSpace(options.AccountId)) missing.Add("ServiceUrl/AccountId");
        if (string.IsNullOrWhiteSpace(options.AccessKeyId)) missing.Add("AccessKeyId");
        if (string.IsNullOrWhiteSpace(options.SecretAccessKey)) missing.Add("SecretAccessKey");
        if (string.IsNullOrWhiteSpace(options.Bucket)) missing.Add("Bucket");
        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl)) missing.Add("PublicBaseUrl");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Faltan configurar Cloudflare:R2: {string.Join(", ", missing)}. No se puede continuar con la subida.");
        }
    }

    private static string BuildObjectKey(R2Options options, string extension)
    {
        return $"{options.Prefix.Trim('/')}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
    }

    private static string BuildPublicUrl(string publicBaseUrl, string key)
    {
        return $"{publicBaseUrl.TrimEnd('/')}/{key.TrimStart('/')}";
    }

    private static string? TryExtractManagedObjectKey(string url, string publicBaseUrl)
    {
        var normalizedBaseUrl = publicBaseUrl.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalizedBaseUrl))
        {
            return null;
        }

        var normalizedUrl = url.Trim();
        if (!normalizedUrl.StartsWith(normalizedBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var key = normalizedUrl[normalizedBaseUrl.Length..].TrimStart('/');
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }

    private static int ResolveWebpQuality(long originalBytes, R2Options options)
    {
        if (originalBytes <= 0)
        {
            return options.WebpQuality;
        }

        if (originalBytes <= 400_000)
        {
            return Math.Clamp(options.WebpQuality + 6, 70, 92);
        }

        if (originalBytes <= 1_200_000)
        {
            return Math.Clamp(options.WebpQuality + 2, 65, 90);
        }

        if (originalBytes <= 3_000_000)
        {
            return Math.Clamp(options.WebpQuality, 62, 88);
        }

        if (originalBytes <= 7_000_000)
        {
            return Math.Clamp(options.WebpQuality - 6, 58, 84);
        }

        return Math.Clamp(options.WebpQuality - 12, 52, 80);
    }

    private static string NormalizeVideoContentType(IFormFile file)
    {
        var contentType = file.ContentType?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            return contentType;
        }

        return ContentTypeProvider.TryGetContentType(file.FileName, out var inferred)
            ? inferred
            : "application/octet-stream";
    }

    private static string ResolveVideoExtension(string? fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.ToLowerInvariant();
        }

        return contentType.ToLowerInvariant() switch
        {
            "video/webm" => ".webm",
            "video/quicktime" => ".mov",
            _ => ".mp4"
        };
    }

    private sealed class R2Options
    {
        public string AccountId { get; set; } = string.Empty;
        public string AccessKeyId { get; set; } = string.Empty;
        public string SecretAccessKey { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
        public string PublicBaseUrl { get; set; } = string.Empty;
        public string ServiceUrl { get; set; } = string.Empty;
        public string Prefix { get; set; } = "publications";
        public string Region { get; set; } = "auto";
        public int MaxImageSide { get; set; } = 1600;
        public int WebpQuality { get; set; } = 82;
        public float WatermarkScale { get; set; } = 0.14f;
        public float WatermarkOpacity { get; set; } = 0.45f;
        public int MaxVideoBytes { get; set; } = 80 * 1024 * 1024;
    }
}
