using ITPSystem.Models;
using System.Text;

namespace ITPSystem.Helpers;

public static class CompanyAcceptanceLetterDocumentBuilder
{
    private const double PageWidth = 595;
    private const double LeftMargin = 50;
    private const double RightMargin = 50;
    private const double ContentWidth = PageWidth - LeftMargin - RightMargin;

    private enum LineStyle
    {
        Normal,
        Bold,
        Italic,
        BoldUnderline,
        RedBoldUnderline,
        BoldItalic
    }

    private sealed record StyledLine(string Text, LineStyle Style);

    public const string GeneratedFileName = "GeneratedCompanyAcceptanceLetter.pdf";

    public static byte[] BuildPdf(StudentApplication? student)
    {
        var lines = BuildLetterLines(student);
        var wrappedLines = WrapLines(lines, ContentWidth).ToList();
        return BuildSimplePdf(wrappedLines);
    }

    private static List<StyledLine> BuildLetterLines(StudentApplication? student)
    {
        var companyName = SafeValue(student?.comName, 33);
        var companyAddress = SafeValue(student?.comAddress, 33);
        var studentName = SafeValue(student?.studentName, 61);
        var nric = SafeValue(student?.number_ic, 22);
        var studentId = SafeValue(student?.studentID, 19);
        var startDate = student?.Cohort?.startDate?.ToString("dd-MM-yyyy") ?? Blank(10);
        var endDate = student?.Cohort?.endDate?.ToString("dd-MM-yyyy") ?? Blank(10);
        var allowance = student?.allowance?.ToString("0.00") ?? Blank(31);

        var lines = new List<StyledLine>
        {
            new("FOCS_EmpF02: Company Acceptance Letter", LineStyle.RedBoldUnderline),
            new("(Please send reply through the student within 5 working days after the interview)", LineStyle.BoldItalic),
            new("", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("_________________________________", LineStyle.Normal),
            new("(Company Name & Address)", LineStyle.Italic),
            new("_________________________________", LineStyle.Normal),
            new(companyName, LineStyle.Normal),
            new(companyAddress, LineStyle.Normal),
            new("", LineStyle.Normal),
            new("Date  (dd/mm/yyyy): ________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("Industrial Training Programme Coordinator", LineStyle.Normal),
            new("Tunku Abdul Rahman University of Management and Technology", LineStyle.Normal),
            new("Kuala Lumpur", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("To Whom It May Concern:", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("Industrial Training Programme", LineStyle.BoldUnderline),
            new("", LineStyle.Normal),
            new("With reference to the above, we wish to inform you that:", LineStyle.Normal),
            new("", LineStyle.Normal),
            new($"1. We are able to accept {studentName},", LineStyle.Bold),
            new($"    NRIC {nric}, Student ID {studentId} for practical training in our organisation from {startDate} to {endDate}.", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("2. The student will report to ________________________________ of _________________________", LineStyle.Normal),
            new("    (company supervisor name)                                    (department name)", LineStyle.Normal),
            new("3. Nature of work(s) (Please tick (v) whichever apply):", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("[   ] Computer Science & Mathematics based: (Computer Science/Management Mathematics, etc)", LineStyle.Normal),
            new("\t_______________________________________________________________________________", LineStyle.Normal),
            new("\t_______________________________________________________________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("[   ] ICT based: (Programming/Networking/ Technical/System Support/ Internet Security/Games", LineStyle.Normal),
            new("      Technology, etc)", LineStyle.Normal),
            new("\t_______________________________________________________________________________", LineStyle.Normal),
            new("\t_______________________________________________________________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("[Please indicate the programming languages/databases used, if relevant]", LineStyle.Normal),
            new("\t_______________________________________________________________________________", LineStyle.Normal),
            new("\t_______________________________________________________________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("[   ] Other related tasks (not applicable for sales and marketing)", LineStyle.Normal),
            new("\t_______________________________________________________________________________", LineStyle.Normal),
            new("\t_______________________________________________________________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new($"4. Allowance per month\t\t: {allowance}", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("5. Working Days (eg. Monday-Friday): _______________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("6. Working Hours (eg.9am - 5pm)\t: _______________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("7. Travelling required?\t\t: [  ] No    [  ] Yes, Location: __________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("8. Travelling allowance (if any)\t: _______________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("9. Accommodation provided?\t\t: [  ] No    [  ] Yes, Address: ___________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("10. Location of training\t\t: ________________________________________________", LineStyle.Normal),
            new("      _______________________________________________________________________________", LineStyle.Normal),
            new(" (if different from the company address)", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("11. Other job requirements / conditions: _______________________________________________", LineStyle.Normal),
            new("      _______________________________________________________________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("We fully understand that we are not allowed to request, use or borrow any form of resources", LineStyle.Normal),
            new("(e.g. Laptop, PC, Software, etc) which belong to the students, to be used for performing any", LineStyle.Normal),
            new("organisational related tasks, whether at the office, customer's place or home.", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("Yours sincerely", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("_________________________________________", LineStyle.Normal),
            new("Signature (Person-in-charge of industrial training)", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("\tName\t: ____________________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("\tDesignation\t: ____________________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("\tEmail\t: ____________________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("\tTel No.\t : ____________________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("\tFax No\t : ____________________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("\tWebsite\t : ____________________________________", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("I hereby agree to accept the above offer,", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("", LineStyle.Normal),
            new("__________________", LineStyle.Normal),
            new("Student's Signature", LineStyle.Normal),
            new($"Name : {studentName}", LineStyle.Normal),
            new("Date  (dd/mm/yyyy):", LineStyle.Normal)
        };

        return lines;
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
        const int fontBoldItalicObjectId = 3;
        const int fontItalicObjectId = 4;
        const int pagesObjectId = 5;
        var pageObjectIds = new List<int>();
        var contentObjectIds = new List<int>();
        var nextObjectId = 6;

        for (var i = 0; i < pages.Count; i++)
        {
            pageObjectIds.Add(nextObjectId++);
            contentObjectIds.Add(nextObjectId++);
        }

        var catalogObjectId = nextObjectId;
        var objects = new Dictionary<int, string>
        {
            [fontRomanObjectId] = "<< /Type /Font /Subtype /Type1 /BaseFont /Times-Roman >>",
            [fontBoldObjectId] = "<< /Type /Font /Subtype /Type1 /BaseFont /Times-Bold >>",
            [fontBoldItalicObjectId] = "<< /Type /Font /Subtype /Type1 /BaseFont /Times-BoldItalic >>",
            [fontItalicObjectId] = "<< /Type /Font /Subtype /Type1 /BaseFont /Times-Italic >>"
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
                $"/Resources << /Font << /F1 {fontRomanObjectId} 0 R /F2 {fontBoldObjectId} 0 R /F3 {fontBoldItalicObjectId} 0 R /F4 {fontItalicObjectId} 0 R >> >> /Contents {contentId} 0 R >>";
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
        var underlineSegments = new List<(double Y, double Width, bool IsRed)>();
        var stampFieldLineIndices = new List<int>();

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

            var style = pageLines[i].Style;
            var text = pageLines[i].Text;
            var textWidth = EstimateTextWidth(text, style);
            var y = 790 - (i * 13);

            if (IsStampFieldLine(text))
            {
                stampFieldLineIndices.Add(i);
            }

            if (style == LineStyle.BoldUnderline || style == LineStyle.RedBoldUnderline)
            {
                sb.AppendLine(style == LineStyle.RedBoldUnderline ? "/F2 12 Tf" : "/F2 11 Tf");
                sb.AppendLine(style == LineStyle.RedBoldUnderline ? "1 0 0 rg" : "0 0 0 rg");
                underlineSegments.Add((Y: y - 1.5, Width: textWidth, IsRed: style == LineStyle.RedBoldUnderline));
            }
            else if (style == LineStyle.Bold)
            {
                sb.AppendLine("/F2 11 Tf");
                sb.AppendLine("0 0 0 rg");
            }
            else if (style == LineStyle.BoldItalic)
            {
                sb.AppendLine("/F3 11 Tf");
                sb.AppendLine("0 0 0 rg");
            }
            else if (style == LineStyle.Italic)
            {
                sb.AppendLine("/F4 11 Tf");
                sb.AppendLine("0 0 0 rg");
            }
            else
            {
                sb.AppendLine("/F1 11 Tf");
                sb.AppendLine("0 0 0 rg");
            }

            sb.Append('(')
              .Append(EscapePdfText(text))
              .AppendLine(") Tj");
        }

        sb.AppendLine("ET");

        if (underlineSegments.Count > 0)
        {
            sb.AppendLine("0.8 w");
            foreach (var segment in underlineSegments)
            {
                sb.AppendLine(segment.IsRed ? "1 0 0 RG" : "0 0 0 RG");
                var right = LeftMargin + segment.Width;
                sb.AppendLine($"{LeftMargin:0.###} {segment.Y:0.###} m {right:0.###} {segment.Y:0.###} l S");
            }
        }

        if (stampFieldLineIndices.Count > 0)
        {
            var first = stampFieldLineIndices.Min();
            var last = stampFieldLineIndices.Max();
            var topY = 790 - (first * 13) + 3;
            var bottomY = 790 - (last * 13) - 3;
            var height = topY - bottomY;
            var boxX = PageWidth - RightMargin - 110;
            var boxWidth = 110d;

            sb.AppendLine("0 0 0 RG");
            sb.AppendLine("0.8 w");
            sb.AppendLine($"{boxX:0.###} {bottomY:0.###} {boxWidth:0.###} {height:0.###} re S");

            sb.AppendLine("BT");
            sb.AppendLine("/F1 11 Tf");
            sb.AppendLine($"{boxX + 12:0.###} {topY - 14:0.###} Td");
            sb.AppendLine("(Company Stamp) Tj");
            sb.AppendLine("ET");
        }

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

    private static bool IsStampFieldLine(string text)
    {
        var value = (text ?? string.Empty).TrimStart();
        return value.StartsWith("Name", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Designation", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Email", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Tel No.", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Fax No", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Website", StringComparison.OrdinalIgnoreCase);
    }

    private static double EstimateTextWidth(string text, LineStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var factor = style switch
        {
            LineStyle.RedBoldUnderline => 6.1,
            LineStyle.BoldUnderline or LineStyle.Bold => 5.6,
            LineStyle.BoldItalic => 5.45,
            LineStyle.Italic => 5.35,
            _ => 5.3
        };
        var width = text.Length * factor;
        return width;
    }

    private static string SafeValue(string? value, int blankLength)
    {
        return string.IsNullOrWhiteSpace(value) ? Blank(blankLength) : value.Trim();
    }

    private static string Blank(int length) => new('_', length);
}
