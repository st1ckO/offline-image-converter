using System.Globalization;
using System.Text;

namespace PrintShopImageConverter.Tests;

internal static class TestPdfFactory
{
    public static string Write(string path, params string[] pageContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (pageContent.Length == 0)
        {
            throw new ArgumentException("At least one page is required.", nameof(pageContent));
        }

        var pageObjectStart = 3;
        var contentObjectStart = pageObjectStart + pageContent.Length;
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(' ', Enumerable.Range(pageObjectStart, pageContent.Length).Select(number => $"{number} 0 R"))}] /Count {pageContent.Length} >>"
        };

        for (var index = 0; index < pageContent.Length; index++)
        {
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 72 36] /Resources << >> /Contents {contentObjectStart + index} 0 R >>");
        }

        foreach (var content in pageContent)
        {
            var contentLength = Encoding.ASCII.GetByteCount(content);
            objects.Add($"<< /Length {contentLength} >>\nstream\n{content}\nendstream");
        }

        using var pdf = new MemoryStream();
        WriteAscii(pdf, "%PDF-1.4\n");
        var offsets = new List<long>();
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(pdf.Position);
            WriteAscii(pdf, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = pdf.Position;
        WriteAscii(pdf, $"xref\n0 {objects.Count + 1}\n");
        WriteAscii(pdf, "0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            WriteAscii(pdf, $"{offset.ToString("0000000000", CultureInfo.InvariantCulture)} 00000 n \n");
        }

        WriteAscii(pdf, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, pdf.ToArray());
        return path;
    }

    private static void WriteAscii(Stream stream, string value) =>
        stream.Write(Encoding.ASCII.GetBytes(value));
}
