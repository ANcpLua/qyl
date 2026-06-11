using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Qyl.Frankenstein;

internal static class ImageMetadataReader
{
    public static ImageInfo Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 24 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4e &&
            bytes[3] == 0x47)
        {
            return new ImageInfo(ReadBigEndianInt32(bytes, 16), ReadBigEndianInt32(bytes, 20), "png");
        }

        if (bytes.Length >= 30 &&
            Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" &&
            Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP")
        {
            return ReadWebP(bytes);
        }

        throw new FrankensteinException($"unsupported image format: {path}");
    }

    private static ImageInfo ReadWebP(byte[] bytes)
    {
        var offset = 12;
        while (offset + 8 <= bytes.Length)
        {
            var chunk = Encoding.ASCII.GetString(bytes, offset, 4);
            var size = ReadLittleEndianInt32(bytes, offset + 4);
            var data = offset + 8;

            if (data + size > bytes.Length)
            {
                break;
            }

            if (chunk == "VP8X" && size >= 10)
            {
                var width = 1 + ReadLittleEndian24(bytes, data + 4);
                var height = 1 + ReadLittleEndian24(bytes, data + 7);
                return new ImageInfo(width, height, "webp");
            }

            if (chunk == "VP8L" && size >= 5)
            {
                var bits = bytes[data + 1] |
                           (bytes[data + 2] << 8) |
                           (bytes[data + 3] << 16) |
                           (bytes[data + 4] << 24);
                var width = 1 + (bits & 0x3fff);
                var height = 1 + ((bits >> 14) & 0x3fff);
                return new ImageInfo(width, height, "webp");
            }

            if (chunk == "VP8 " && size >= 10)
            {
                var frameHeader = data + 3;
                if (bytes[frameHeader] == 0x9d && bytes[frameHeader + 1] == 0x01 && bytes[frameHeader + 2] == 0x2a)
                {
                    var width = ReadLittleEndianInt16(bytes, frameHeader + 3) & 0x3fff;
                    var height = ReadLittleEndianInt16(bytes, frameHeader + 5) & 0x3fff;
                    return new ImageInfo(width, height, "webp");
                }
            }

            offset = data + size + (size % 2);
        }

        throw new FrankensteinException("unable to read WebP dimensions.");
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) |
        (bytes[offset + 1] << 16) |
        (bytes[offset + 2] << 8) |
        bytes[offset + 3];

    private static int ReadLittleEndianInt32(byte[] bytes, int offset) =>
        bytes[offset] |
        (bytes[offset + 1] << 8) |
        (bytes[offset + 2] << 16) |
        (bytes[offset + 3] << 24);

    private static int ReadLittleEndianInt16(byte[] bytes, int offset) =>
        bytes[offset] | (bytes[offset + 1] << 8);

    private static int ReadLittleEndian24(byte[] bytes, int offset) =>
        bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16);
}

internal static class AtlasOccupancyInspector
{
    public static IReadOnlyList<RowEvidence> ReadRows(string spritePath, AtlasContract atlas)
    {
        if (!CommandRunner.Exists("magick"))
        {
            return [];
        }

        var rows = new List<RowEvidence>();
        for (var row = 0; row < atlas.Rows; row++)
        {
            var nonEmptyFrames = 0;
            for (var column = 0; column < atlas.Columns; column++)
            {
                var crop = $"{atlas.CellWidth}x{atlas.CellHeight}+{column * atlas.CellWidth}+{row * atlas.CellHeight}";
                var result = CommandRunner.Run("magick", [
                    spritePath,
                    "-alpha",
                    "extract",
                    "-crop",
                    crop,
                    "-format",
                    "%[fx:mean]",
                    "info:"
                ]);

                if (result.ExitCode is not 0)
                {
                    return [];
                }

                if (double.TryParse(result.Output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var mean) && mean > 0.0001)
                {
                    nonEmptyFrames++;
                }
            }

            rows.Add(new RowEvidence(row, nonEmptyFrames, ToolAvailable: true));
        }

        return rows;
    }
}

internal static class CommandRunner
{
    public static bool Exists(string executable)
    {
        var result = Run(executable, ["-version"], timeoutMilliseconds: 2_000);
        return result.ExitCode is 0;
    }

    public static CommandResult Run(string executable, IReadOnlyList<string> arguments, int timeoutMilliseconds = 10_000)
    {
        using var process = new Process();
        process.StartInfo.FileName = executable;
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new CommandResult(127, string.Empty, $"{executable} not found");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            return new CommandResult(124, output, "process timed out");
        }

        return new CommandResult(process.ExitCode, output, error);
    }
}

internal sealed record CommandResult(int ExitCode, string Output, string Error);
