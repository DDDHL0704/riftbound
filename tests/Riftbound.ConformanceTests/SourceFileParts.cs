namespace Riftbound.ConformanceTests;

internal static class SourceFileParts
{
    internal static string ReadAllText(string primaryPath)
    {
        var primary = File.ReadAllText(primaryPath);
        var parts = Path.Combine(Path.GetDirectoryName(primaryPath)!, Path.GetFileNameWithoutExtension(primaryPath));
        if (!Directory.Exists(parts)) return primary;

        // Source guards inspect the entire extracted implementation, including
        // future parts added to the same domain directory.
        return string.Join("\n", new[] { primary }.Concat(Directory.EnumerateFiles(parts, "*.cs")
            .OrderBy(path => path, StringComparer.Ordinal).Select(File.ReadAllText)));
    }
}
