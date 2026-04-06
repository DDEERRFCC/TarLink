using System.IO.Compression;
using System.Reflection;
using System.Globalization;
using System.Text;
using System.Runtime.Versioning;
using ITPSystem.Models;

namespace ITPSystem.Helpers;

public static class IndemnityLetterWordTemplateBuilder
{
    private const string TemplateFileName = "FOCS_StudF01 Indemnity Letter (09.11.2022).docx";
    private const string DocumentXmlPath = "word/document.xml";
    private const string CourseTitlePlaceholder = "&lt;course code and title&gt;";
    private const string LetterDatePlaceholder = "Date: _______________";
    private const string CompanyPlaceholder = "________________________________________________________________";
    private const string StartDatePlaceholderPart1 = "from _______________________";
    private const string StartDatePlaceholderPart2 = "_______";
    private const string StartDatePlaceholderPart3 = "__";
    private const string EndDatePlaceholder = "to ______________________________.";
    private const string DateHintParagraphAnchor = "(start date)";

    public const string GeneratedFileName = "GeneratedIndemnityLetter.pdf";

    public static byte[] Build(StudentApplication? student, string webRootPath)
    {
        var templatePath = Path.Combine(webRootPath, "documents", "templates", TemplateFileName);
        return BuildFromTemplatePath(student, templatePath);
    }

    public static byte[] BuildFromTemplatePath(StudentApplication? student, string templatePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Microsoft Word PDF export is only supported on Windows.");
        }

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("Indemnity letter template was not found.", templatePath);
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "ITPSystem", "IndemnityLetter");
        Directory.CreateDirectory(tempDirectory);

        var tempDocxPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.docx");
        var tempPdfPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.pdf");

        try
        {
            File.Copy(templatePath, tempDocxPath, overwrite: true);
            ReplaceTemplatePlaceholders(tempDocxPath, student);
            ExportWordDocumentToPdf(tempDocxPath, tempPdfPath);
            return File.ReadAllBytes(tempPdfPath);
        }
        finally
        {
            TryDeleteFile(tempDocxPath);
            TryDeleteFile(tempPdfPath);
        }
    }

    private static void ReplaceTemplatePlaceholders(string documentPath, StudentApplication? student)
    {
        using var fileStream = new FileStream(documentPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Update, leaveOpen: false);

        var documentEntry = archive.GetEntry(DocumentXmlPath)
            ?? throw new InvalidOperationException("The indemnity letter template is missing word/document.xml.");

        string xml;
        using (var entryStream = documentEntry.Open())
        using (var reader = new StreamReader(entryStream, Encoding.UTF8))
        {
            xml = reader.ReadToEnd();
        }

        var courseTitle = EscapeXml(GetCourseCodeAndTitle(student));
        var companyName = GetCompanyName(student?.comName);
        var startDate = FormatInternshipDate(student?.Cohort?.startDate);
        var endDate = FormatInternshipDate(student?.Cohort?.endDate);
        var letterDate = EscapeXml(DateTime.Today.ToString("dd/MM/yyyy"));

        xml = xml.Replace(CourseTitlePlaceholder, courseTitle, StringComparison.Ordinal);
        xml = xml.Replace(LetterDatePlaceholder, $"Date: {letterDate}", StringComparison.Ordinal);
        xml = xml.Replace(CompanyPlaceholder, EscapeXml(companyName), StringComparison.Ordinal);
        xml = ReplaceStartDate(xml, startDate);
        xml = xml.Replace(EndDatePlaceholder, $"to {EscapeXml(endDate)}.", StringComparison.Ordinal);
        xml = RemoveParagraphContaining(xml, DateHintParagraphAnchor);

        documentEntry.Delete();
        var updatedEntry = archive.CreateEntry(DocumentXmlPath, CompressionLevel.Optimal);
        using var updatedStream = updatedEntry.Open();
        using var writer = new StreamWriter(updatedStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(xml);
    }

    [SupportedOSPlatform("windows")]
    private static void ExportWordDocumentToPdf(string sourceDocxPath, string targetPdfPath)
    {
        Exception? threadException = null;

        var thread = new Thread(() =>
        {
            object? wordApp = null;
            object? documents = null;
            object? document = null;

            try
            {
                var wordType = Type.GetTypeFromProgID("Word.Application")
                    ?? throw new InvalidOperationException("Microsoft Word is not installed or is not registered correctly.");

                wordApp = Activator.CreateInstance(wordType)
                    ?? throw new InvalidOperationException("Unable to start Microsoft Word.");

                SetProperty(wordApp, "Visible", false);
                SetProperty(wordApp, "DisplayAlerts", 0);

                documents = GetProperty(wordApp, "Documents");
                document = InvokeMethod(documents, "Open", sourceDocxPath, Missing.Value, true, Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value, false);
                InvokeMethod(document, "ExportAsFixedFormat", targetPdfPath, 17);
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
            finally
            {
                if (document != null)
                {
                    TryInvokeMethod(document, "Close", false);
                    ReleaseComObject(document);
                }

                if (documents != null)
                {
                    ReleaseComObject(documents);
                }

                if (wordApp != null)
                {
                    TryInvokeMethod(wordApp, "Quit", false);
                    ReleaseComObject(wordApp);
                }
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException != null)
        {
            throw new InvalidOperationException(
                "Failed to convert the indemnity letter to PDF. Make sure Microsoft Word is installed on this machine.",
                threadException);
        }

        if (!File.Exists(targetPdfPath))
        {
            throw new InvalidOperationException("Word did not produce the PDF file.");
        }
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

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static string FitToPlaceholder(string? value, int length)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return new string('_', length);
        }

        return trimmed.Length > length
            ? trimmed[..length]
            : trimmed.PadRight(length, '_');
    }

    private static string GetCompanyName(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        return trimmed.Length > 32
            ? trimmed[..32]
            : trimmed;
    }

    private static string FormatInternshipDate(DateTime? date)
    {
        return date.HasValue
            ? date.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
            : new string('_', 15);
    }

    private static string ReplaceStartDate(string xml, string startDate)
    {
        var fullPlaceholder = string.Concat(
            StartDatePlaceholderPart1,
            "</w:t></w:r><w:r><w:rPr><w:sz w:val=\"22\"/><w:szCs w:val=\"22\"/></w:rPr><w:t>",
            StartDatePlaceholderPart2,
            "</w:t></w:r><w:r w:rsidRPr=\"008918D4\"><w:rPr><w:sz w:val=\"22\"/><w:szCs w:val=\"22\"/></w:rPr><w:t>",
            StartDatePlaceholderPart3);
        var replacement = EscapeXml($"from {startDate}");

        if (xml.Contains(fullPlaceholder, StringComparison.Ordinal))
        {
            return xml.Replace(fullPlaceholder, replacement, StringComparison.Ordinal);
        }

        return xml
            .Replace(StartDatePlaceholderPart1, replacement, StringComparison.Ordinal)
            .Replace(StartDatePlaceholderPart2, string.Empty, StringComparison.Ordinal)
            .Replace(StartDatePlaceholderPart3, string.Empty, StringComparison.Ordinal);
    }

    private static string RemoveParagraphContaining(string xml, string anchor)
    {
        var anchorIndex = xml.IndexOf(anchor, StringComparison.Ordinal);
        if (anchorIndex < 0)
        {
            return xml;
        }

        var paragraphStart = xml.LastIndexOf("<w:p ", anchorIndex, StringComparison.Ordinal);
        if (paragraphStart < 0)
        {
            return xml;
        }

        var paragraphEnd = xml.IndexOf("</w:p>", anchorIndex, StringComparison.Ordinal);
        if (paragraphEnd < 0)
        {
            return xml;
        }

        paragraphEnd += "</w:p>".Length;

        return string.Concat(
            xml.AsSpan(0, paragraphStart),
            xml.AsSpan(paragraphEnd));
    }

    private static object GetProperty(object instance, string propertyName)
    {
        return instance.GetType().InvokeMember(
            propertyName,
            BindingFlags.GetProperty,
            binder: null,
            target: instance,
            args: null)!;
    }

    private static void SetProperty(object instance, string propertyName, object? value)
    {
        instance.GetType().InvokeMember(
            propertyName,
            BindingFlags.SetProperty,
            binder: null,
            target: instance,
            args: [value]);
    }

    private static object InvokeMethod(object instance, string methodName, params object?[]? args)
    {
        return instance.GetType().InvokeMember(
            methodName,
            BindingFlags.InvokeMethod,
            binder: null,
            target: instance,
            args: args)!;
    }

    private static void TryInvokeMethod(object instance, string methodName, params object?[]? args)
    {
        try
        {
            instance.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod,
                binder: null,
                target: instance,
                args: args);
        }
        catch
        {
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ReleaseComObject(object instance)
    {
        try
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(instance);
        }
        catch
        {
        }
    }
}
