using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Runtime.Versioning;
using System.Text;
using ITPSystem.Models;

namespace ITPSystem.Helpers;

public static class ParentAcknowledgementFormWordTemplateBuilder
{
    private const string TemplateFileName = "FOCS_StudF02 Parent Acknowledgement Form (09.11.2022).docx";
    private const string LetterDatePlaceholder = "<DATE>";
    private const string StartDatePlaceholder = "<START DATE>";
    private const string EndDatePlaceholder = "<END DATE>";
    private const string EncodedLetterDatePlaceholder = "&lt;DATE&gt;";
    private const string EncodedStartDatePlaceholder = "&lt;START DATE&gt;";
    private const string EncodedEndDatePlaceholder = "&lt;END DATE&gt;";
    private const string TrainingRangePattern = @"Industrial Training\s*\(\s*(?:&lt;START DATE&gt;|<START DATE>)\s*to\s*(?:&lt;END DATE&gt;|<END DATE>)\s*\)";
    private const string ReturnDatePrefixPlaceholder = "DD-MM-";
    private const string ReturnDateSuffixPlaceholder = "YYYY";

    public const string GeneratedFileName = "GeneratedParentAcknowledgementForm.pdf";

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
            throw new FileNotFoundException("Parent acknowledgement form template was not found.", templatePath);
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "ITPSystem", "ParentAcknowledgementForm");
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

        var letterDate = DateTime.Today.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        var internshipStartDate = FormatInternshipDate(student?.Cohort?.startDate);
        var internshipEndDate = FormatInternshipDate(student?.Cohort?.endDate);
        var returnByDate = GetReturnByDate(student?.Cohort?.startDate);

        var wordXmlEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase)
                && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var entry in wordXmlEntries)
        {
            UpdateXmlEntry(entry, letterDate, internshipStartDate, internshipEndDate, returnByDate);
        }
    }

    private static void UpdateXmlEntry(
        ZipArchiveEntry entry,
        string letterDate,
        string internshipStartDate,
        string internshipEndDate,
        string returnByDate)
    {
        var archive = entry.Archive;
        var entryFullName = entry.FullName;

        string xml;
        using (var entryStream = entry.Open())
        using (var reader = new StreamReader(entryStream, Encoding.UTF8))
        {
            xml = reader.ReadToEnd();
        }

        xml = ReplaceTrainingRange(xml, internshipStartDate, internshipEndDate);
        xml = xml.Replace(EncodedLetterDatePlaceholder, EscapeXml(letterDate), StringComparison.Ordinal);
        xml = xml.Replace(LetterDatePlaceholder, EscapeXml(letterDate), StringComparison.Ordinal);
        xml = xml.Replace(EncodedStartDatePlaceholder, EscapeXml(internshipStartDate), StringComparison.Ordinal);
        xml = xml.Replace(StartDatePlaceholder, EscapeXml(internshipStartDate), StringComparison.Ordinal);
        xml = xml.Replace(EncodedEndDatePlaceholder, EscapeXml(internshipEndDate), StringComparison.Ordinal);
        xml = xml.Replace(EndDatePlaceholder, EscapeXml(internshipEndDate), StringComparison.Ordinal);
        xml = ReplaceReturnDateText(xml, returnByDate);

        entry.Delete();
        var updatedEntry = archive.CreateEntry(entryFullName, CompressionLevel.Optimal);
        using var updatedStream = updatedEntry.Open();
        using var writer = new StreamWriter(updatedStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(xml);
    }

    private static string ReplaceReturnDateText(string xml, string returnByDate)
    {
        var splitPattern = $"{ReturnDatePrefixPlaceholder}</w:t>";
        if (xml.Contains(splitPattern, StringComparison.Ordinal) &&
            xml.Contains($">{ReturnDateSuffixPlaceholder}</w:t>", StringComparison.Ordinal))
        {
            return xml
                .Replace(ReturnDatePrefixPlaceholder, returnByDate[..6], StringComparison.Ordinal)
                .Replace($">{ReturnDateSuffixPlaceholder}</w:t>", $">{returnByDate[6..]}</w:t>", StringComparison.Ordinal);
        }

        return xml.Replace($"{ReturnDatePrefixPlaceholder}{ReturnDateSuffixPlaceholder}", returnByDate, StringComparison.Ordinal);
    }

    private static string ReplaceTrainingRange(string xml, string internshipStartDate, string internshipEndDate)
    {
        var replacement = EscapeXml($"Industrial Training ({internshipStartDate} to {internshipEndDate})");
        return Regex.Replace(
            xml,
            TrainingRangePattern,
            replacement,
            RegexOptions.CultureInvariant);
    }

    private static string FormatInternshipDate(DateTime? date)
    {
        return date.HasValue
            ? date.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
            : "______________";
    }

    private static string GetReturnByDate(DateTime? cohortStartDate)
    {
        return cohortStartDate.HasValue
            ? cohortStartDate.Value.Date.AddDays(-1).ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)
            : "DD-MM-YYYY";
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
                "Failed to convert the parent acknowledgement form to PDF. Make sure Microsoft Word is installed on this machine.",
                threadException);
        }

        if (!File.Exists(targetPdfPath))
        {
            throw new InvalidOperationException("Word did not produce the PDF file.");
        }
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
