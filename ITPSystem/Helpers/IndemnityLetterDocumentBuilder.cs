using ITPSystem.Models;
using System.Text;

namespace ITPSystem.Helpers;

public static class IndemnityLetterDocumentBuilder
{
    private enum LineStyle
    {
        Normal,
        Bold
    }

    private sealed record StyledLine(string Text, LineStyle Style);

    private const double PageWidth = 595;
    private const double LeftMargin = 50;
    private const double RightMargin = 50;
    private const double ContentWidth = PageWidth - LeftMargin - RightMargin;
    private const double LineHeight = 13;

    public const string GeneratedFileName = "GeneratedIndemnityLetter.pdf";

    public static byte[] BuildPdf(StudentApplication? student)
    {
        var lines = BuildLines(student);
        var wrapped = WrapLines(lines, ContentWidth).ToList();
        return BuildSimplePdf(wrapped);
    }

    private static List<StyledLine> BuildLines(StudentApplication? student)
    {
        var companyName = SafeValue(student?.comName, 64);
        var startDate = student?.Cohort?.startDate?.ToString("dd-MM-yyyy") ?? Blank(16);
        var endDate = student?.Cohort?.endDate?.ToString("dd-MM-yyyy") ?? Blank(16);
        var studentName = SafeValue(student?.studentName, 31);
        var mykad = SafeValue(student?.number_ic, 30);
        var studentId = SafeValue(student?.studentID, 20);
        var programme = SafeValue(student?.programme, 40);

        return new List<StyledLine>
        {
            new("FOCS_StudF01: Instructions/Advice to Students Undergoing Industrial Training and Letter of Indemnity", LineStyle.Bold),
            new("", LineStyle.Normal),
            new("Instructions/Advice to Students Undergoing Industrial Training", LineStyle.Bold),
            new("Faculty of Computing and Information Technology", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("Practical training is an integral part of your Diploma and Degree Programmes in the University. It will provide you with an opportunity to establish contact with the profession of your choice and may lead to employment after your graduation.", LineStyle.Normal),
            new("For your own good and to uphold the University image you are required to follow the rules set out below:", LineStyle.Normal),
            new("1. As a trainee member of staff, you are naturally under orders of the company that you are attached to. You must at all times observe strict discipline on matters in connection with your job and your behaviour must be above criticism.", LineStyle.Normal),
            new("2. Your services should be used as much as possible by the company so that you will not be left idle. Even if your training supervisor is too busy to give you exact instructions and tasks, you should take the initiative to try and make yourself useful, and try to learn as much as possible.", LineStyle.Normal),
            new("3. Since you are new to your work, every care should be exercised to safeguard against accidents. Do not operate any equipment unless you are sure of or have been taught how to operate it. Permission to operate any equipment must be obtained from the supervisory personnel.", LineStyle.Normal),
            new("4. Should any accident occur during the training, inform the Department of Students Affair and your TAR UMT supervisor within 14 days. We will help to make claims from University group personal accident insurance.", LineStyle.Normal),
            new("5. You are not allowed to take out from the company premises any software, hardware, stationery or any information pertaining to company's work, unless permission has been given by the authorised personnel of the company.", LineStyle.Normal),
            new("6. You are prohibited from leaking secrets, or providing any information related to the business of the company or its clients or any other information acquired during or after the training period, to outside parties.", LineStyle.Normal),
            new("7. You are prohibited from destroying or misusing any property belonging to the company.", LineStyle.Normal),
            new("8. You are not allowed to use any company's facilities e.g. photocopying machine, fax, or printers for personal use. You are required to keep all information pertaining to the company in strict confidence, which you are entrusted with.", LineStyle.Normal),
            new("9. If a student is found to have violated these regulations, or to have neglected his or her duties, or to have breached discipline, appropriate action can be taken against him or her by the University.", LineStyle.Normal),
            new("10. Should you have any problems relating to your training, you should contact the Faculty of Computing and Information Technology Office, your Programme Leader or your TAR UMT Supervisor.", LineStyle.Normal),
            new("11. You are not allowed to terminate your training early. You must consult the Associate Dean, Programme Leader or your TAR UMT Supervisor before applying for leave from the company for essential matters.", LineStyle.Normal),
            new("12. All trainees are advised to be punctual and to wear proper attire at all times in the place of work.", LineStyle.Normal),
            new("13. The Faculty will request for a confidential report on your training from the company concerned after you have completed the training. Your training will be graded according to the report submitted by your training supervisor and your progress reports submitted to your TAR UMT Supervisor.", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("Letter of Indemnity", LineStyle.Bold),
            new("Date: _______________ (dd/mm/yyyy)", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("Dean", LineStyle.Normal),
            new("Faculty of Computing and Information Technology", LineStyle.Normal),
            new("Tunku Abdul Rahman University of Management and Technology", LineStyle.Normal),
            new("Jalan Genting Kelang", LineStyle.Normal),
            new("53300 Kuala Lumpur", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("Re: Industrial Training", LineStyle.Bold),
            new("I hereby undertake at all times to comply with the rules and regulations given to me during my Industrial Training at", LineStyle.Normal),
            new($"{companyName} from {startDate} to {endDate}.", LineStyle.Normal),
            new("(start date)    (end date)", LineStyle.Normal),
            new("If in the course of such training, I shall have the misfortune to suffer any accidental injury whether or not due solely to personal negligence, I hereby declare that the University and the company concerned shall not be responsible for the same.", LineStyle.Normal),
            new("Should any other person suffer such accidental injury during the course of and arising out of such practical training as a direct or indirect result of any act or omission on my part, I hereby undertake full responsibility for the same and keep the University and company indemnified from any claims made against it by reason of such accidental injury having been suffered.", LineStyle.Normal),
            new("I also confirm that I will at all times during the course of such training uphold the good name of the University and I will not attempt to terminate the training earlier.", LineStyle.Normal),
            new("I understand that I will fail the industrial training programme and hence the <course code and title> if I were to withdraw from or be absent during the training without any valid reasons.", LineStyle.Normal),
            new("I hereby consent that Tunku Abdul Rahman University of Management and Technology (TAR UMT) discloses any or all my personal data to prospective companies for the sole purpose of industrial training placement. I hereby release TAR UMT from all liabilities on account of such disclosure above.", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("Yours faithfully,", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("Signature: _________________________", LineStyle.Normal),
            new($"Name: {studentName}", LineStyle.Normal),
            new($"MyKad No: {mykad}    Student Reg No: {studentId}", LineStyle.Normal),
            new($"Programme: {programme}", LineStyle.Normal)
        };
    }

    private static byte[] BuildSimplePdf(IReadOnlyList<StyledLine> lines)
    {
        var normalized = (lines.Count == 0 ? new[] { new StyledLine(string.Empty, LineStyle.Normal) } : lines)
            .Select(line => new StyledLine(NormalizePdfLine(line.Text), line.Style))
            .ToList();

        const int linesPerPage = 54;
        var pages = new List<List<StyledLine>>();
        for (var i = 0; i < normalized.Count; i += linesPerPage)
        {
            pages.Add(normalized.Skip(i).Take(linesPerPage).ToList());
        }

        if (pages.Count == 0)
        {
            pages.Add(new List<StyledLine> { new(string.Empty, LineStyle.Normal) });
        }

        const int fontRomanObjectId = 1;
        const int fontBoldObjectId = 2;
        const int pagesObjectId = 3;
        var pageObjectIds = new List<int>();
        var contentObjectIds = new List<int>();
        var nextObjectId = 4;

        for (var i = 0; i < pages.Count; i++)
        {
            pageObjectIds.Add(nextObjectId++);
            contentObjectIds.Add(nextObjectId++);
        }

        var catalogObjectId = nextObjectId;
        var objects = new Dictionary<int, string>
        {
            [fontRomanObjectId] = "<< /Type /Font /Subtype /Type1 /BaseFont /Times-Roman >>",
            [fontBoldObjectId] = "<< /Type /Font /Subtype /Type1 /BaseFont /Times-Bold >>"
        };

        var kids = string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"));
        objects[pagesObjectId] = $"<< /Type /Pages /Count {pages.Count} /Kids [ {kids} ] >>";

        for (var i = 0; i < pages.Count; i++)
        {
            var pageId = pageObjectIds[i];
            var contentId = contentObjectIds[i];
            var stream = BuildPageContent(pages[i], i + 1, pages.Count);
            var streamLength = Encoding.ASCII.GetByteCount(stream);

            objects[contentId] = $"<< /Length {streamLength} >>\nstream\n{stream}\nendstream";
            objects[pageId] =
                $"<< /Type /Page /Parent {pagesObjectId} 0 R /MediaBox [0 0 595 842] " +
                $"/Resources << /Font << /F1 {fontRomanObjectId} 0 R /F2 {fontBoldObjectId} 0 R >> >> /Contents {contentId} 0 R >>";
        }

        objects[catalogObjectId] = $"<< /Type /Catalog /Pages {pagesObjectId} 0 R >>";

        using var ms = new MemoryStream();
        var offsets = new int[catalogObjectId + 1];

        void Write(string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            ms.Write(bytes, 0, bytes.Length);
        }

        Write("%PDF-1.4\n%ITPSYS\n");

        for (var id = 1; id <= catalogObjectId; id++)
        {
            offsets[id] = (int)ms.Position;
            Write($"{id} 0 obj\n{objects[id]}\nendobj\n");
        }

        var xrefPosition = (int)ms.Position;
        Write($"xref\n0 {catalogObjectId + 1}\n");
        Write("0000000000 65535 f \n");
        for (var id = 1; id <= catalogObjectId; id++)
        {
            Write($"{offsets[id]:D10} 00000 n \n");
        }

        Write($"trailer\n<< /Size {catalogObjectId + 1} /Root {catalogObjectId} 0 R >>\n");
        Write($"startxref\n{xrefPosition}\n%%EOF");
        return ms.ToArray();
    }

    private static string BuildPageContent(IReadOnlyList<StyledLine> pageLines, int pageNumber, int totalPages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BT");
        sb.AppendLine("/F1 11 Tf");
        sb.AppendLine("13 TL");
        sb.AppendLine($"{LeftMargin:0.###} 790 Td");

        for (var i = 0; i < pageLines.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine("T*");
            }

            sb.AppendLine(pageLines[i].Style == LineStyle.Bold ? "/F2 11 Tf" : "/F1 11 Tf");
            sb.Append('(')
              .Append(EscapePdfText(pageLines[i].Text))
              .AppendLine(") Tj");
        }

        sb.AppendLine("ET");

        sb.AppendLine("BT");
        sb.AppendLine("/F1 10 Tf");
        sb.AppendLine("270 20 Td");
        sb.Append('(')
          .Append(EscapePdfText($"Page {pageNumber} of {totalPages}"))
          .AppendLine(") Tj");
        sb.AppendLine("ET");
        return sb.ToString();
    }

    private static IEnumerable<StyledLine> WrapLines(IEnumerable<StyledLine> lines, double maxWidth)
    {
        foreach (var originalLine in lines)
        {
            var line = originalLine.Text ?? string.Empty;
            if (EstimateTextWidth(line, originalLine.Style) <= maxWidth)
            {
                yield return originalLine;
                continue;
            }

            var remaining = line;
            while (!string.IsNullOrEmpty(remaining) && EstimateTextWidth(remaining, originalLine.Style) > maxWidth)
            {
                var splitIndex = FindWrapIndex(remaining, maxWidth, originalLine.Style);
                if (splitIndex <= 0)
                {
                    splitIndex = Math.Min(1, remaining.Length);
                }

                yield return new StyledLine(remaining[..splitIndex].TrimEnd(), originalLine.Style);
                remaining = remaining[splitIndex..].TrimStart();
            }

            if (remaining.Length > 0)
            {
                yield return new StyledLine(remaining, originalLine.Style);
            }
        }
    }

    private static int FindWrapIndex(string text, double maxWidth, LineStyle style)
    {
        var bestSpace = -1;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] == ' ')
            {
                bestSpace = i;
            }

            var candidate = text[..i];
            if (EstimateTextWidth(candidate, style) > maxWidth)
            {
                return bestSpace > 0 ? bestSpace : Math.Max(1, i - 1);
            }
        }

        return text.Length;
    }

    private static string NormalizePdfLine(string line)
    {
        var source = ExpandTabs(line ?? string.Empty, 8);
        var chars = source.Select(ch =>
        {
            if (ch < 32)
            {
                return ' ';
            }

            return ch <= 126 ? ch : '?';
        });

        return new string(chars.ToArray());
    }

    private static string ExpandTabs(string input, int tabSize)
    {
        var sb = new StringBuilder();
        var column = 0;

        foreach (var ch in input)
        {
            if (ch == '\t')
            {
                var spaces = tabSize - (column % tabSize);
                sb.Append(' ', spaces);
                column += spaces;
                continue;
            }

            if (ch == '\r' || ch == '\n')
            {
                continue;
            }

            sb.Append(ch);
            column++;
        }

        return sb.ToString();
    }

    private static string EscapePdfText(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }

    private static double EstimateTextWidth(string text, LineStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var factor = style == LineStyle.Bold ? 5.6 : 5.3;
        return text.Length * factor;
    }

    private static string SafeValue(string? value, int blankLength)
    {
        return string.IsNullOrWhiteSpace(value) ? Blank(blankLength) : value.Trim();
    }

    private static string Blank(int length) => new('_', length);
}
