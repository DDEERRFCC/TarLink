using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;

public class CommitteeImportCompaniesModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeImportCompaniesModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public IFormFile? CsvFile { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        return Page();
    }

    public IActionResult OnPost()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        if (CsvFile == null || CsvFile.Length == 0)
        {
            StatusMessage = "Please select a CSV file.";
            return RedirectToPage();
        }

        var imported = 0;
        var skipped = 0;

        using var stream = CsvFile.OpenReadStream();
        using var reader = new StreamReader(stream);

        var lineNo = 0;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            lineNo++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (lineNo == 1 && line.Contains("name", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cols = ParseCsvLine(line);
            if (cols.Count < 2)
            {
                skipped++;
                continue;
            }

            var name = cols[0].Trim();
            var address1 = cols[1].Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address1))
            {
                skipped++;
                continue;
            }

            var exists = _db.Companies.Any(c =>
                c.name.ToLower() == name.ToLower() &&
                (c.address1 ?? string.Empty).ToLower() == address1.ToLower());
            if (exists)
            {
                skipped++;
                continue;
            }

            var company = new Company
            {
                created_at = DateTime.Now,
                lastUpdate = DateTime.Now,
                name = name,
                address1 = address1,
                address2 = GetCol(cols, 2),
                address3 = GetCol(cols, 3),
                regNo = GetCol(cols, 4),
                vacancyLevel = GetCol(cols, 5),
                website = GetCol(cols, 6),
                remark = GetCol(cols, 7),
                status = ParseByte(GetCol(cols, 8), 1),
                visibility = ParseByte(GetCol(cols, 9), 1)
            };

            _db.Companies.Add(company);
            imported++;
        }

        _db.SaveChanges();
        TempData["StatusMessage"] = $"Import completed. Imported: {imported}, Skipped: {skipped}.";
        return RedirectToPage("/Committee/Companies");
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString());
        return result;
    }

    private static string? GetCol(IReadOnlyList<string> cols, int idx)
    {
        if (idx >= cols.Count)
        {
            return null;
        }

        var value = cols[idx].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static byte ParseByte(string? raw, byte fallback)
    {
        return byte.TryParse(raw, out var parsed) ? parsed : fallback;
    }
}
