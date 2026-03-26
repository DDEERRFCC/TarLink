using System.IO.Compression;
using System.Text;
using ITPSystem.Models;

namespace ITPSystem.Helpers;

public static class IndemnityLetterWordTemplateBuilder
{
    private const string TemplateFileName = "FOCS_StudF01 Indemnity Letter (09.11.2022).docx";
    private const string DocumentXmlPath = "word/document.xml";
    private const string CourseTitlePlaceholder = "&lt;course code and title&gt;";

    public const string GeneratedFileName = "GeneratedIndemnityLetter.docx";

    public static byte[] Build(StudentApplication? student, string webRootPath)
    {
        var templatePath = Path.Combine(webRootPath, "documents", TemplateFileName);
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("Indemnity letter template was not found.", templatePath);
        }

        var output = new MemoryStream();
        using (var templateStream = File.OpenRead(templatePath))
        {
            templateStream.CopyTo(output);
        }

        output.Position = 0;

        using (var archive = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true))
        {
            var documentEntry = archive.GetEntry(DocumentXmlPath)
                ?? throw new InvalidOperationException("The indemnity letter template is missing word/document.xml.");

            string xml;
            using (var entryStream = documentEntry.Open())
            using (var reader = new StreamReader(entryStream, Encoding.UTF8))
            {
                xml = reader.ReadToEnd();
            }

            var replacement = EscapeXml(GetCourseCodeAndTitle(student));
            xml = xml.Replace(CourseTitlePlaceholder, replacement, StringComparison.Ordinal);

            documentEntry.Delete();
            var updatedEntry = archive.CreateEntry(DocumentXmlPath, CompressionLevel.Optimal);
            using var updatedStream = updatedEntry.Open();
            using var writer = new StreamWriter(updatedStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(xml);
        }

        return output.ToArray();
    }

    private static string GetCourseCodeAndTitle(StudentApplication? student)
    {
        var programme = (student?.programme ?? string.Empty).Trim().ToUpperInvariant();
        var level = student?.level;

        return (programme, level) switch
        {
            ("RSD", 1) => "RSD Diploma in Software Engineering",
            ("RIT", 1) => "RIT Diploma in Information Technology",
            ("RSD", 2) => "RSD Bachelor of Software Engineering (Honours)",
            ("RIT", 2) => "RIT Bachelor of Information Technology (Honours)",
            ("RSD", _) => "RSD Software Engineering",
            ("RIT", _) => "RIT Information Technology",
            _ when !string.IsNullOrWhiteSpace(programme) => programme,
            _ => "the relevant course"
        };
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }
}
