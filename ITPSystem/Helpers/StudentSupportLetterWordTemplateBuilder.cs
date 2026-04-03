using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using ITPSystem.Models;

namespace ITPSystem.Helpers;

public static class StudentSupportLetterWordTemplateBuilder
{
    private const string DiplomaTemplateFileName = "FOCS Student support Letter - diploma (18.9.2024).docx";
    private const string BachelorTemplateFileName = "FOCS Student support Letter - Bachelor (18.9.2024).docx";
    private const string StudentNamePlaceholder = "<STUD NAME>";
    private const string StudentIdPlaceholder = "<STUD ID>";
    private const string StudentProgrammePlaceholder = "<STUD PROGRAMME>";
    private const string StartDatePlaceholder = "<START DATE>";
    private const string EndDatePlaceholder = "<END DATE>";
    private const string EncodedStudentNamePlaceholder = "&lt;STUD NAME&gt;";
    private const string EncodedStudentIdPlaceholder = "&lt;STUD ID&gt;";
    private const string EncodedStudentProgrammePlaceholder = "&lt;STUD PROGRAMME&gt;";
    private const string EncodedStartDatePlaceholder = "&lt;START DATE&gt;";
    private const string EncodedEndDatePlaceholder = "&lt;END DATE&gt;";

    public const string GeneratedFileName = "GeneratedStudentSupportLetter.pdf";

    public static byte[] Build(StudentApplication? student, string webRootPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Microsoft Word PDF export is only supported on Windows.");
        }

        var templateFileName = GetTemplateFileName(student?.level);
        var templatePath = Path.Combine(webRootPath, "documents", "templates", templateFileName);
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("Student support letter template was not found.", templatePath);
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "ITPSystem", "StudentSupportLetter");
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

    private static string GetTemplateFileName(byte? level)
    {
        return level == 2
            ? BachelorTemplateFileName
            : DiplomaTemplateFileName;
    }

    private static void ReplaceTemplatePlaceholders(string documentPath, StudentApplication? student)
    {
        using var fileStream = new FileStream(documentPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Update, leaveOpen: false);

        var studentName = EscapeXml((student?.studentName ?? string.Empty).Trim());
        var studentId = EscapeXml((student?.studentID ?? string.Empty).Trim());
        var studentProgramme = EscapeXml(GetStudentProgramme(student));
        var startDate = EscapeXml(FormatInternshipDate(student?.Cohort?.startDate));
        var endDate = EscapeXml(FormatInternshipDate(student?.Cohort?.endDate));

        var wordXmlEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase)
                && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var entry in wordXmlEntries)
        {
            string xml;
            using (var entryStream = entry.Open())
            using (var reader = new StreamReader(entryStream, Encoding.UTF8))
            {
                xml = reader.ReadToEnd();
            }

            xml = xml.Replace(EncodedStudentNamePlaceholder, studentName, StringComparison.Ordinal);
            xml = xml.Replace(StudentNamePlaceholder, studentName, StringComparison.Ordinal);
            xml = xml.Replace(EncodedStudentIdPlaceholder, studentId, StringComparison.Ordinal);
            xml = xml.Replace(StudentIdPlaceholder, studentId, StringComparison.Ordinal);
            xml = xml.Replace(EncodedStudentProgrammePlaceholder, studentProgramme, StringComparison.Ordinal);
            xml = xml.Replace(StudentProgrammePlaceholder, studentProgramme, StringComparison.Ordinal);
            xml = xml.Replace(EncodedStartDatePlaceholder, startDate, StringComparison.Ordinal);
            xml = xml.Replace(StartDatePlaceholder, startDate, StringComparison.Ordinal);
            xml = xml.Replace(EncodedEndDatePlaceholder, endDate, StringComparison.Ordinal);
            xml = xml.Replace(EndDatePlaceholder, endDate, StringComparison.Ordinal);

            entry.Delete();
            var updatedEntry = archive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
            using var updatedStream = updatedEntry.Open();
            using var writer = new StreamWriter(updatedStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(xml);
        }
    }

    private static string GetStudentProgramme(StudentApplication? student)
    {
        var programme = (student?.programme ?? string.Empty).Trim().ToUpperInvariant();
        var level = student?.level;

        return (programme, level) switch
        {
            ("RSD", 1) => "Diploma in Software Engineering",
            ("RIT", 1) => "Diploma in Information Technology",
            ("RSD", 2) => "Bachelor of Software Engineering (Honours)",
            ("RIT", 2) => "Bachelor of Information Technology (Honours)",
            _ when !string.IsNullOrWhiteSpace(programme) => programme,
            _ => string.Empty
        };
    }

    private static string FormatInternshipDate(DateTime? date)
    {
        return date.HasValue
            ? date.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
            : "______________";
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
                "Failed to convert the student support letter to PDF. Make sure Microsoft Word is installed on this machine.",
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
