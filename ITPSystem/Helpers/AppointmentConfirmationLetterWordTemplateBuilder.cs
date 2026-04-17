using System.IO.Compression;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using ITPSystem.Models;

namespace ITPSystem.Helpers;

public static class AppointmentConfirmationLetterWordTemplateBuilder
{
    private const string TemplateFileName = "DownloadAppointmentLetter.docx";
    private const string StudentNamePlaceholder = "<Student Name>";
    private const string GenderPlaceholder = "<M>";
    private const string IcNumberPlaceholder = "<IC Number>";
    private const string SupervisorNamePlaceholder = "<Supervisor Name>";
    private const string SupervisorEmailPlaceholder = "<Supervisor Email>";
    private const string EncodedStudentNamePlaceholder = "&lt;Student Name&gt;";
    private const string EncodedGenderPlaceholder = "&lt;M&gt;";
    private const string EncodedIcNumberPlaceholder = "&lt;IC Number&gt;";
    private const string EncodedSupervisorNamePlaceholder = "&lt;Supervisor Name&gt;";
    private const string EncodedSupervisorEmailPlaceholder = "&lt;Supervisor Email&gt;";

    public const string GeneratedFileName = "GeneratedAppointmentConfirmationLetter.pdf";

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
            throw new FileNotFoundException("Appointment confirmation letter template was not found.", templatePath);
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "ITPSystem", "AppointmentConfirmationLetter");
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

        var studentName = EscapeXml((student?.studentName ?? string.Empty).Trim());
        var gender = EscapeXml(GetGenderValue(student?.gender));
        var icNumber = EscapeXml((student?.number_ic ?? string.Empty).Trim());
        var supervisorName = EscapeXml((student?.ucSupervisor ?? string.Empty).Trim());
        var supervisorEmail = EscapeXml((student?.ucSupervisorEmail ?? string.Empty).Trim());

        var wordXmlEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase)
                && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var entry in wordXmlEntries)
        {
            var entryFullName = entry.FullName;

            string xml;
            using (var entryStream = entry.Open())
            using (var reader = new StreamReader(entryStream, Encoding.UTF8))
            {
                xml = reader.ReadToEnd();
            }

            xml = xml.Replace(EncodedStudentNamePlaceholder, studentName, StringComparison.Ordinal);
            xml = xml.Replace(StudentNamePlaceholder, studentName, StringComparison.Ordinal);
            xml = xml.Replace(EncodedGenderPlaceholder, gender, StringComparison.Ordinal);
            xml = xml.Replace(GenderPlaceholder, gender, StringComparison.Ordinal);
            xml = xml.Replace(EncodedIcNumberPlaceholder, icNumber, StringComparison.Ordinal);
            xml = xml.Replace(IcNumberPlaceholder, icNumber, StringComparison.Ordinal);
            xml = xml.Replace(EncodedSupervisorNamePlaceholder, supervisorName, StringComparison.Ordinal);
            xml = xml.Replace(SupervisorNamePlaceholder, supervisorName, StringComparison.Ordinal);
            xml = xml.Replace(EncodedSupervisorEmailPlaceholder, supervisorEmail, StringComparison.Ordinal);
            xml = xml.Replace(SupervisorEmailPlaceholder, supervisorEmail, StringComparison.Ordinal);

            entry.Delete();
            var updatedEntry = archive.CreateEntry(entryFullName, CompressionLevel.Optimal);
            using var updatedStream = updatedEntry.Open();
            using var writer = new StreamWriter(updatedStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(xml);
        }
    }

    private static string GetGenderValue(string? gender)
    {
        var trimmed = (gender ?? string.Empty).Trim().ToUpperInvariant();
        return trimmed switch
        {
            "M" => "M",
            "F" => "F",
            "O" => "O",
            _ => string.Empty
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
                "Failed to convert the appointment confirmation letter to PDF. Make sure Microsoft Word is installed on this machine.",
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
