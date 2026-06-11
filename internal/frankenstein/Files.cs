using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Qyl.Frankenstein;

internal static class JsonFile
{
    public static JsonObject ReadObject(string path)
    {
        var node = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8));
        return node as JsonObject ?? throw new FrankensteinException($"JSON root must be an object: {path}");
    }

    public static void WriteObject(string path, JsonObject json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json.ToJsonString(JsonFormatter.Options) + Environment.NewLine, TextEncodings.Utf8NoBom);
    }
}

internal static class TextEncodings
{
    public static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}

internal static class FileHasher
{
    public static string HashFile(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }
}

internal static class DirectoryHasher
{
    public static string HashDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new FrankensteinException($"directory not found: {path}");
        }

        var builder = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                     .OrderBy(file => Path.GetRelativePath(path, file), StringComparer.Ordinal))
        {
            var relativePath = Path.GetRelativePath(path, file).Replace(Path.DirectorySeparatorChar, '/');
            builder.Append(relativePath).Append('\n');
            builder.Append(FileHasher.HashFile(file)).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
