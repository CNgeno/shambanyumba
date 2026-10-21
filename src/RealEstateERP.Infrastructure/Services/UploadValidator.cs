namespace RealEstateERP.Infrastructure.Services;

/// <summary>Checks extension AND file signature, so a renamed .exe can't pass as a .pdf.</summary>
public static class UploadValidator
{
    private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
        [".png"] = [[0x89, 0x50, 0x4E, 0x47]],
        [".pdf"] = [[0x25, 0x50, 0x44, 0x46]]
    };

    public static IReadOnlyCollection<string> AllowedExtensions => Signatures.Keys;

    public static string? Validate(Stream content, string fileName, long length, int maxSizeMb)
    {
        if (length <= 0) return "The file is empty.";
        if (length > maxSizeMb * 1024L * 1024L) return $"The file is larger than {maxSizeMb} MB.";

        var ext = Path.GetExtension(fileName);
        if (!Signatures.TryGetValue(ext, out var signatures))
            return "Upload a JPG, PNG or PDF file.";

        if (!content.CanSeek) return "The file could not be read.";
        var header = new byte[8];
        var read = content.Read(header, 0, header.Length);
        content.Position = 0;

        var matches = signatures.Any(sig => read >= sig.Length && header.AsSpan(0, sig.Length).SequenceEqual(sig));
        return matches ? null : "The file content doesn't match its type. Upload a real JPG, PNG or PDF.";
    }
}
