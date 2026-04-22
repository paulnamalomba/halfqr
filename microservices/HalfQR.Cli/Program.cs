using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HalfQR.Contracts.Enums;
using HalfQR.Contracts.Models;
using HalfQR.Contracts.Requests;
using HalfQR.QrEngine.Hashing;
using HalfQR.QrEngine.PayloadEncoding;
using HalfQR.QrEngine.Rendering;
using SkiaSharp;
using ZXing;

var exitCode = await MainAsync(args);
return exitCode;

static async Task<int> MainAsync(string[] args)
{
	if (args.Length == 0 || IsHelpCommand(args[0]))
	{
		PrintHelp();
		return 0;
	}

	if (!string.Equals(args[0], "verify-render", StringComparison.OrdinalIgnoreCase))
	{
		Console.Error.WriteLine($"Unknown command '{args[0]}'.");
		Console.Error.WriteLine();
		PrintHelp();
		return 1;
	}

	try
	{
		var options = ParseOptions(args[1..]);
		var verificationOptions = ResolveVerificationOptions(options);
		var summary = await RunVerificationAsync(verificationOptions);

		PrintSummary(summary);
		return summary.Passed ? 0 : 2;
	}
	catch (Exception exception)
	{
		Console.Error.WriteLine(exception.Message);
		return 1;
	}
}

static bool IsHelpCommand(string value)
	=> value is "-h" or "--help" or "help";

static Dictionary<string, string> ParseOptions(string[] args)
{
	var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	for (var index = 0; index < args.Length; index += 1)
	{
		var key = args[index];

		if (!key.StartsWith("--", StringComparison.Ordinal))
		{
			throw new InvalidOperationException($"Unexpected argument '{key}'. Options must use the --name value format.");
		}

		if ((index + 1) >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
		{
			throw new InvalidOperationException($"Option '{key}' requires a value.");
		}

		options[key] = args[index + 1];
		index += 1;
	}

	return options;
}

static VerificationOptions ResolveVerificationOptions(IReadOnlyDictionary<string, string> options)
{
	var targetUrl = RequireOption(options, "--url");

	if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var absoluteTargetUrl) || absoluteTargetUrl.Scheme is not ("http" or "https"))
	{
		throw new InvalidOperationException("--url must be a valid absolute http or https URL.");
	}

	var contentType = ParseEnumOption(options, new[] { "--type", "--content-type" }, QrContentType.Link);

	if (!IsUrlBackedContentType(contentType))
	{
		throw new InvalidOperationException("verify-render currently supports URL-backed types only: Link, App, Social, Pdf, Image, and Video.");
	}

	var presetId = GetOption(options, "--preset");
	var logoFilePath = GetOption(options, "--logo-file");

	if (!string.IsNullOrWhiteSpace(presetId) && !string.Equals(presetId, "none", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(logoFilePath))
	{
		throw new InvalidOperationException("Use either --preset or --logo-file, not both.");
	}

	var hasPreset = !string.IsNullOrWhiteSpace(presetId) && !string.Equals(presetId, "none", StringComparison.OrdinalIgnoreCase);
	var logoSizePercent = ParseIntOption(options, "--logo-size", hasPreset ? 12 : 18, 12, 24);
	var backdropPaddingPercent = ParseIntOption(options, "--backdrop-padding", 40, 10, 80);

	return new VerificationOptions(
		TargetUrl: absoluteTargetUrl.ToString(),
		ContentType: contentType,
		SizePx: ParseIntOption(options, "--size", 1024, 256, 4096),
		ErrorCorrectionLevel: ParseEnumOption(options, new[] { "--ecc" }, QrErrorCorrectionLevel.H),
		Pattern: ParseEnumOption(options, new[] { "--pattern" }, QrDataPattern.Square),
		GradientMode: ParseEnumOption(options, new[] { "--gradient" }, QrGradientMode.Linear),
		GradientStart: GetOption(options, "--gradient-start") ?? "#000000",
		GradientEnd: GetOption(options, "--gradient-end") ?? "#1F61C0",
		GradientRotation: ParseIntOption(options, "--gradient-rotation", 135, 0, 360),
		DarkColor: GetOption(options, "--dark") ?? "#000000",
		LightColor: GetOption(options, "--light") ?? "#FFFFFF",
		PresetId: hasPreset ? presetId : null,
		LogoFilePath: string.IsNullOrWhiteSpace(logoFilePath) ? null : ResolveInputPath(logoFilePath),
		LogoSizePercent: logoSizePercent,
		BackdropPaddingPercent: backdropPaddingPercent,
		OutputDirectory: GetOption(options, "--output-dir"));
}

static async Task<VerificationSummary> RunVerificationAsync(VerificationOptions options)
{
	var repositoryRoot = FindRepositoryRoot();
	var loadedLogo = await LoadLogoAsync(options, repositoryRoot);
	var request = BuildRequest(options, loadedLogo?.Options);
	var renderService = new QrRenderService(new QrPayloadEncoder(), new Sha256HashService());

	var draft = await renderService.RenderDraftAsync(request, CancellationToken.None);
	var finalArtifacts = await renderService.RenderAsync(request, CancellationToken.None);
	var draftRasterizedPng = QrArtifactRasterizer.RasterizeSvgToPng(draft.SvgMarkup, request.Output.SizePx);
	var draftDecodedPayload = DecodeQrPayload(draftRasterizedPng);
	var finalDecodedPayload = DecodeQrPayload(finalArtifacts.PngBytes);
	var outputDirectory = ResolveOutputDirectory(repositoryRoot, options.OutputDirectory);
	var moduleCount = ExtractModuleCount(draft.SvgMarkup);
	var expectedPayload = draft.EncodedPayload;

	Directory.CreateDirectory(outputDirectory);

	await File.WriteAllTextAsync(Path.Combine(outputDirectory, "request.json"), SerializeIndented(request));
	await File.WriteAllTextAsync(Path.Combine(outputDirectory, "draft.svg"), draft.SvgMarkup);
	await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "draft-rasterized.png"), draftRasterizedPng);
	await File.WriteAllTextAsync(Path.Combine(outputDirectory, "final.svg"), finalArtifacts.SvgMarkup);
	await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "final.png"), finalArtifacts.PngBytes);

	var draftVerification = new VerificationArtifact(
		Label: "Draft SVG",
		DecodedPayload: draftDecodedPayload,
		MatchesEncodedPayload: string.Equals(draftDecodedPayload, expectedPayload, StringComparison.Ordinal));

	var finalVerification = new VerificationArtifact(
		Label: "Final PNG",
		DecodedPayload: finalDecodedPayload,
		MatchesEncodedPayload: string.Equals(finalDecodedPayload, expectedPayload, StringComparison.Ordinal));

	var summary = new VerificationSummary(
		Passed: draftVerification.MatchesEncodedPayload && finalVerification.MatchesEncodedPayload,
		OutputDirectory: outputDirectory,
		RequestedUrl: options.TargetUrl,
		EncodedPayload: expectedPayload,
		ResolvedTargetUrl: draft.ResolvedTargetUrl,
		ContentType: request.ContentType,
		ModuleCount: moduleCount,
		LogoLabel: loadedLogo?.Label ?? "none",
		Draft: draftVerification,
		Final: finalVerification);

	await File.WriteAllTextAsync(Path.Combine(outputDirectory, "summary.json"), SerializeIndented(summary));
	return summary;
}

static SubmitRenderJobRequest BuildRequest(VerificationOptions options, QrLogoOptions? logo)
{
	var gradientMode = options.GradientMode;

	return new SubmitRenderJobRequest
	{
		ContentType = options.ContentType,
		TargetUrl = options.TargetUrl,
		ErrorCorrectionLevel = options.ErrorCorrectionLevel,
		Output = new QrOutputOptions
		{
			Format = "png",
			SizePx = options.SizePx,
		},
		Finder = new QrFinderOptions
		{
			BorderShape = QrFinderShape.Rounded,
			CenterShape = QrFinderShape.Circle,
		},
		Colors = new QrColorOptions
		{
			Dark = options.DarkColor,
			Light = options.LightColor,
		},
		Data = new QrDataOptions
		{
			Pattern = options.Pattern,
			GradientMode = gradientMode,
			GradientStart = gradientMode == QrGradientMode.Linear ? options.GradientStart : null,
			GradientEnd = gradientMode == QrGradientMode.Linear ? options.GradientEnd : null,
			GradientRotation = options.GradientRotation,
		},
		Logo = logo,
	};
}

static async Task<LoadedLogo?> LoadLogoAsync(VerificationOptions options, string repositoryRoot)
{
	if (!string.IsNullOrWhiteSpace(options.PresetId))
	{
		var presetPath = ResolvePresetAssetPath(repositoryRoot, options.PresetId);
		return await LoadLogoFromFileAsync($"preset:{options.PresetId}", presetPath, options.LogoSizePercent, options.BackdropPaddingPercent);
	}

	if (!string.IsNullOrWhiteSpace(options.LogoFilePath))
	{
		return await LoadLogoFromFileAsync(Path.GetFileName(options.LogoFilePath), options.LogoFilePath, options.LogoSizePercent, options.BackdropPaddingPercent);
	}

	return null;
}

static async Task<LoadedLogo> LoadLogoFromFileAsync(string label, string path, int logoSizePercent, int backdropPaddingPercent)
{
	if (!File.Exists(path))
	{
		throw new InvalidOperationException($"Logo file was not found: {path}");
	}

	var extension = Path.GetExtension(path).ToLowerInvariant();

	return extension switch
	{
		".svg" => new LoadedLogo(
			Label: label,
			Options: new QrLogoOptions
			{
				SourceType = QrLogoSourceType.Svg,
				Svg = await File.ReadAllTextAsync(path),
				SizePercent = logoSizePercent,
				RemoveBackground = false,
				BackdropPaddingPercent = backdropPaddingPercent,
			}),
		".png" => new LoadedLogo(
			Label: label,
			Options: new QrLogoOptions
			{
				SourceType = QrLogoSourceType.Png,
				ContentBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(path)),
				ContentType = "image/png",
				SizePercent = logoSizePercent,
				RemoveBackground = false,
				BackdropPaddingPercent = backdropPaddingPercent,
			}),
		".jpg" or ".jpeg" => new LoadedLogo(
			Label: label,
			Options: new QrLogoOptions
			{
				SourceType = QrLogoSourceType.Jpeg,
				ContentBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(path)),
				ContentType = "image/jpeg",
				SizePercent = logoSizePercent,
				RemoveBackground = false,
				BackdropPaddingPercent = backdropPaddingPercent,
			}),
		_ => throw new InvalidOperationException("Logos must be SVG, PNG, or JPEG files."),
	};
}

static string? DecodeQrPayload(byte[] pngBytes)
{
	using var bitmap = SKBitmap.Decode(pngBytes);

	if (bitmap is null)
	{
		return null;
	}

	var rawPixels = new byte[bitmap.Width * bitmap.Height * 4];
	var offset = 0;

	for (var y = 0; y < bitmap.Height; y += 1)
	{
		for (var x = 0; x < bitmap.Width; x += 1)
		{
			var pixel = bitmap.GetPixel(x, y);
			rawPixels[offset] = pixel.Red;
			rawPixels[offset + 1] = pixel.Green;
			rawPixels[offset + 2] = pixel.Blue;
			rawPixels[offset + 3] = pixel.Alpha;
			offset += 4;
		}
	}

	var reader = new BarcodeReaderGeneric();
	reader.Options.TryHarder = true;
	reader.Options.PossibleFormats = [BarcodeFormat.QR_CODE];

	var result = reader.Decode(rawPixels, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.RGBA32);
	return result?.Text;
}

static int? ExtractModuleCount(string svgMarkup)
{
	var match = Regex.Match(svgMarkup, "viewBox=\"0 0 (?<width>\\d+(?:\\.\\d+)?) (?<height>\\d+(?:\\.\\d+)?)\"", RegexOptions.CultureInvariant);

	if (!match.Success)
	{
		return null;
	}

	if (!double.TryParse(match.Groups["width"].Value, out var width) || !double.TryParse(match.Groups["height"].Value, out var height))
	{
		return null;
	}

	return Math.Abs(width - height) < double.Epsilon ? (int)Math.Round(width) : null;
}

static string SerializeIndented<T>(T value)
	=> JsonSerializer.Serialize(value, new JsonSerializerOptions
	{
		WriteIndented = true,
		Converters =
		{
			new JsonStringEnumConverter(),
		},
	});

static string ResolvePresetAssetPath(string repositoryRoot, string presetId)
	=> presetId.Trim().ToLowerInvariant() switch
	{
		"scan-me" => Path.Combine(repositoryRoot, "webapp", "public", "assets", "qr-watermarks", "scan-me-logo_in-qr-code.svg"),
		"link" => Path.Combine(repositoryRoot, "webapp", "public", "assets", "qr-watermarks", "link-logo_in-qr-code.svg"),
		"menu" => Path.Combine(repositoryRoot, "webapp", "public", "assets", "qr-watermarks", "menu-logo_in-qr-code.svg"),
		"whatsapp" => Path.Combine(repositoryRoot, "webapp", "public", "assets", "qr-watermarks", "whatsapp-logo_in-qr-code.svg"),
		_ => throw new InvalidOperationException($"Unknown preset '{presetId}'. Use scan-me, link, menu, or whatsapp."),
	};

static string ResolveOutputDirectory(string repositoryRoot, string? configuredOutputDirectory)
{
	if (!string.IsNullOrWhiteSpace(configuredOutputDirectory))
	{
		return ResolveInputPath(configuredOutputDirectory);
	}

	return Path.Combine(
		repositoryRoot,
		".artifacts",
		"render-verify",
		DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"));
}

static string ResolveInputPath(string path)
	=> Path.IsPathRooted(path)
		? Path.GetFullPath(path)
		: Path.GetFullPath(path, Directory.GetCurrentDirectory());

static string FindRepositoryRoot()
{
	var candidates = new[]
	{
		Directory.GetCurrentDirectory(),
		AppContext.BaseDirectory,
	};

	foreach (var candidate in candidates)
	{
		var resolvedRoot = TryFindRepositoryRoot(candidate);

		if (resolvedRoot is not null)
		{
			return resolvedRoot;
		}
	}

	throw new InvalidOperationException("Unable to locate the HalfQR repository root from the current working directory or the CLI binary path.");
}

static string? TryFindRepositoryRoot(string startPath)
{
	var directory = new DirectoryInfo(startPath);

	while (directory is not null)
	{
		if (File.Exists(Path.Combine(directory.FullName, "HalfQR.sln")))
		{
			return directory.FullName;
		}

		directory = directory.Parent;
	}

	return null;
}

static T ParseEnumOption<T>(IReadOnlyDictionary<string, string> options, IEnumerable<string> keys, T fallback)
	where T : struct, Enum
{
	foreach (var key in keys)
	{
		var value = GetOption(options, key);

		if (string.IsNullOrWhiteSpace(value))
		{
			continue;
		}

		if (Enum.TryParse<T>(value, ignoreCase: true, out var parsed))
		{
			return parsed;
		}

		throw new InvalidOperationException($"Option '{key}' has an unsupported value '{value}'.");
	}

	return fallback;
}

static int ParseIntOption(IReadOnlyDictionary<string, string> options, string key, int fallback, int min, int max)
{
	var value = GetOption(options, key);

	if (string.IsNullOrWhiteSpace(value))
	{
		return fallback;
	}

	if (!int.TryParse(value, out var parsed) || parsed < min || parsed > max)
	{
		throw new InvalidOperationException($"Option '{key}' must be an integer from {min} to {max}.");
	}

	return parsed;
}

static string RequireOption(IReadOnlyDictionary<string, string> options, string key)
	=> GetOption(options, key) ?? throw new InvalidOperationException($"Missing required option '{key}'.");

static string? GetOption(IReadOnlyDictionary<string, string> options, string key)
	=> options.TryGetValue(key, out var value) ? value : null;

static bool IsUrlBackedContentType(QrContentType contentType)
	=> contentType is QrContentType.Link or QrContentType.App or QrContentType.Social or QrContentType.Pdf or QrContentType.Image or QrContentType.Video;

static void PrintSummary(VerificationSummary summary)
{
	Console.WriteLine($"Output directory: {summary.OutputDirectory}");
	Console.WriteLine($"Requested URL: {summary.RequestedUrl}");
	Console.WriteLine($"Resolved payload: {summary.EncodedPayload}");
	Console.WriteLine($"Content type: {summary.ContentType}");
	Console.WriteLine($"Logo: {summary.LogoLabel}");
	Console.WriteLine($"Module count: {(summary.ModuleCount is int count ? count : -1)}");
	Console.WriteLine($"Draft SVG decode: {(summary.Draft.MatchesEncodedPayload ? "PASS" : "FAIL")} {FormatDecodedPayload(summary.Draft.DecodedPayload)}");
	Console.WriteLine($"Final PNG decode: {(summary.Final.MatchesEncodedPayload ? "PASS" : "FAIL")} {FormatDecodedPayload(summary.Final.DecodedPayload)}");
	Console.WriteLine(summary.Passed ? "Overall result: PASS" : "Overall result: FAIL");
}

static string FormatDecodedPayload(string? decodedPayload)
	=> string.IsNullOrWhiteSpace(decodedPayload)
		? "(no payload decoded)"
		: decodedPayload;

static void PrintHelp()
{
	Console.WriteLine("HalfQR CLI");
	Console.WriteLine();
	Console.WriteLine("Commands:");
	Console.WriteLine("  verify-render --url <absolute-url> [options]");
	Console.WriteLine();
	Console.WriteLine("Options:");
	Console.WriteLine("  --type <Link|App|Social|Pdf|Image|Video>   URL-backed content type. Default: Link");
	Console.WriteLine("  --preset <scan-me|link|menu|whatsapp>      Use a curated preset logo");
	Console.WriteLine("  --logo-file <path>                         Use an SVG, PNG, or JPEG logo from disk");
	Console.WriteLine("  --size <256-4096>                          Output PNG size. Default: 1024");
	Console.WriteLine("  --ecc <L|M|Q|H>                            Error correction level. Default: H");
	Console.WriteLine("  --pattern <Square|Dotted>                  Data module pattern. Default: Square");
	Console.WriteLine("  --gradient <None|Linear>                   Data gradient mode. Default: Linear");
	Console.WriteLine("  --gradient-start <hex>                     Gradient start colour. Default: #000000");
	Console.WriteLine("  --gradient-end <hex>                       Gradient end colour. Default: #1F61C0");
	Console.WriteLine("  --gradient-rotation <0-360>                Gradient rotation. Default: 135");
	Console.WriteLine("  --dark <hex>                               Dark QR colour. Default: #000000");
	Console.WriteLine("  --light <hex>                              Light QR colour. Default: #FFFFFF");
	Console.WriteLine("  --logo-size <12-24>                        Center logo size percent. Default: 18 or 12 for presets");
	Console.WriteLine("  --backdrop-padding <10-80>                 Center logo backdrop padding. Default: 40");
	Console.WriteLine("  --output-dir <path>                        Directory for request, SVG, PNG, and summary artifacts");
	Console.WriteLine();
	Console.WriteLine("Example:");
	Console.WriteLine("  dotnet run --project microservices/HalfQR.Cli -- verify-render --type Pdf --url https://example.com/menu.pdf --preset menu");
}

file sealed record VerificationOptions(
	string TargetUrl,
	QrContentType ContentType,
	int SizePx,
	QrErrorCorrectionLevel ErrorCorrectionLevel,
	QrDataPattern Pattern,
	QrGradientMode GradientMode,
	string GradientStart,
	string GradientEnd,
	int GradientRotation,
	string DarkColor,
	string LightColor,
	string? PresetId,
	string? LogoFilePath,
	int LogoSizePercent,
	int BackdropPaddingPercent,
	string? OutputDirectory);

file sealed record LoadedLogo(string Label, QrLogoOptions Options);

file sealed record VerificationArtifact(string Label, string? DecodedPayload, bool MatchesEncodedPayload);

file sealed record VerificationSummary(
	bool Passed,
	string OutputDirectory,
	string RequestedUrl,
	string EncodedPayload,
	string ResolvedTargetUrl,
	QrContentType ContentType,
	int? ModuleCount,
	string LogoLabel,
	VerificationArtifact Draft,
	VerificationArtifact Final);
