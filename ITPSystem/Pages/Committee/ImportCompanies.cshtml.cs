using ClosedXML.Excel;
using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

public class CommitteeImportCompaniesModel : CommitteePageModelBase
{
    private static readonly string[] RequiredHeaders =
    {
        "reg_no",
        "name",
        "address_line"
    };

    private readonly ApplicationDbContext _db;

    public CommitteeImportCompaniesModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public IFormFile? UploadFile { get; set; }

    [TempData]
    public string? Message { get; set; }

    public List<ImportError> Errors { get; private set; } = new();
    public int ImportedCompanyCount { get; private set; }
    public int ImportedBranchCount { get; private set; }
    public int FailedCount { get; private set; }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        return Page();
    }

    public IActionResult OnPostImport()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        if (UploadFile == null || UploadFile.Length == 0)
        {
            Message = "Please choose a file.";
            return Page();
        }

        var ext = Path.GetExtension(UploadFile.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".csv")
        {
            Message = "Unsupported file type. Please upload .xlsx or .csv.";
            return Page();
        }

        List<ImportCompanyRow> rows;
        try
        {
            rows = ext == ".xlsx" ? ReadXlsx(UploadFile) : ReadCsv(UploadFile);
        }
        catch (Exception ex)
        {
            Message = "Failed to read file: " + ex.Message;
            return Page();
        }

        if (!rows.Any())
        {
            Message = "No data rows found in the uploaded file.";
            return Page();
        }

        var createdCompanies = new Dictionary<string, Company>(StringComparer.OrdinalIgnoreCase);
        var createdBranchKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNo = i + 2;
            var row = rows[i];
            NormalizeRow(row);

            var rowError = ValidateRow(row);
            if (rowError != null)
            {
                Errors.Add(new ImportError(rowNo, rowError));
                continue;
            }

            try
            {
                var company = ResolveCompany(row, createdCompanies);
                var branchKey = BuildBranchKey(company.regNo, row);

                if (!createdBranchKeys.Add(branchKey))
                {
                    Errors.Add(new ImportError(rowNo, $"Duplicate branch row in file for company {company.regNo}."));
                    continue;
                }

                var branchExists = _db.CompanyBranches.Any(b =>
                    b.company_id == company.company_id &&
                    (b.address_line ?? string.Empty) == (row.address_line ?? string.Empty) &&
                    (b.city ?? string.Empty) == (row.city ?? string.Empty) &&
                    (b.state ?? string.Empty) == (row.state ?? string.Empty) &&
                    (b.postcode ?? string.Empty) == (row.postcode ?? string.Empty) &&
                    (b.country ?? string.Empty) == (row.country ?? string.Empty));

                if (branchExists)
                {
                    Errors.Add(new ImportError(rowNo, $"Branch already exists for company {company.regNo}."));
                    continue;
                }

                var branch = new CompanyBranch
                {
                    company_id = company.company_id,
                    address_line = NullIfWhiteSpace(row.address_line),
                    city = NullIfWhiteSpace(row.city),
                    state = NullIfWhiteSpace(row.state),
                    postcode = NullIfWhiteSpace(row.postcode),
                    country = NullIfWhiteSpace(row.country),
                    totalNoOfStaff = row.total_no_of_staff,
                    vacancyLevel = NullIfWhiteSpace(row.vacancy_level),
                    status = row.status ?? 0,
                    visibility = row.visibility ?? 1,
                    lastVisit = row.last_visit,
                    lastContact = row.last_contact,
                    is_hq = row.is_hq ?? false,
                    created_at = DateTime.Now,
                    updated_at = DateTime.Now
                };

                _db.CompanyBranches.Add(branch);
                _db.SaveChanges();
                ImportedBranchCount++;
            }
            catch (Exception ex)
            {
                Errors.Add(new ImportError(rowNo, ex.InnerException?.Message ?? ex.Message));
            }
        }

        FailedCount = Errors.Count;
        Message = $"Import complete. Companies created: {ImportedCompanyCount}, Branches created: {ImportedBranchCount}, Failed: {FailedCount}.";
        return Page();
    }

    private Company ResolveCompany(ImportCompanyRow row, Dictionary<string, Company> createdCompanies)
    {
        if (createdCompanies.TryGetValue(row.reg_no!, out var cached))
        {
            return cached;
        }

        var existing = _db.Companies.FirstOrDefault(c => c.regNo == row.reg_no);
        if (existing != null)
        {
            createdCompanies[row.reg_no!] = existing;
            return existing;
        }

        var company = new Company
        {
            regNo = row.reg_no!,
            name = row.name!,
            industryInvolved = NullIfWhiteSpace(row.industry_involved),
            productsAndServices = NullIfWhiteSpace(row.products_and_services),
            companyBackground = NullIfWhiteSpace(row.company_background),
            website = NullIfWhiteSpace(row.website),
            remark = NullIfWhiteSpace(row.remark),
            created_at = DateTime.Now,
            updated_at = DateTime.Now
        };

        _db.Companies.Add(company);
        _db.SaveChanges();
        ImportedCompanyCount++;
        createdCompanies[row.reg_no!] = company;
        return company;
    }

    private static string BuildBranchKey(string regNo, ImportCompanyRow row)
    {
        return string.Join("|",
            regNo.Trim().ToLowerInvariant(),
            (row.address_line ?? string.Empty).Trim().ToLowerInvariant(),
            (row.city ?? string.Empty).Trim().ToLowerInvariant(),
            (row.state ?? string.Empty).Trim().ToLowerInvariant(),
            (row.postcode ?? string.Empty).Trim().ToLowerInvariant(),
            (row.country ?? string.Empty).Trim().ToLowerInvariant());
    }

    private List<ImportCompanyRow> ReadCsv(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new InvalidOperationException("CSV header row is missing.");
        }

        var headers = ParseCsvLine(headerLine).Select(x => x.Trim()).ToList();
        EnsureRequiredHeaders(headers);

        var map = headers
            .Select((name, index) => new { key = NormalizeHeader(name), index })
            .GroupBy(x => x.key)
            .Select(g => g.First())
            .ToDictionary(x => x.key, x => x.index, StringComparer.OrdinalIgnoreCase);

        var rows = new List<ImportCompanyRow>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = ParseCsvLine(line);
            rows.Add(MapRow(cells, map));
        }

        return rows;
    }

    private List<ImportCompanyRow> ReadXlsx(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws == null)
        {
            throw new InvalidOperationException("Worksheet is missing.");
        }

        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        if (lastCol == 0 || lastRow == 0)
        {
            return new List<ImportCompanyRow>();
        }

        var headers = new List<string>();
        for (var c = 1; c <= lastCol; c++)
        {
            headers.Add(ws.Cell(1, c).GetString().Trim());
        }
        EnsureRequiredHeaders(headers);

        var map = headers
            .Select((name, index) => new { key = NormalizeHeader(name), index = index + 1 })
            .GroupBy(x => x.key)
            .Select(g => g.First())
            .ToDictionary(x => x.key, x => x.index, StringComparer.OrdinalIgnoreCase);

        var rows = new List<ImportCompanyRow>();
        for (var r = 2; r <= lastRow; r++)
        {
            var isEmpty = true;
            for (var c = 1; c <= lastCol; c++)
            {
                if (!string.IsNullOrWhiteSpace(ws.Cell(r, c).GetString()))
                {
                    isEmpty = false;
                    break;
                }
            }

            if (isEmpty)
            {
                continue;
            }

            rows.Add(new ImportCompanyRow
            {
                reg_no = GetCell(ws, r, map, "reg_no"),
                name = GetCell(ws, r, map, "name"),
                industry_involved = GetCell(ws, r, map, "industry_involved"),
                products_and_services = GetCell(ws, r, map, "products_and_services"),
                company_background = GetCell(ws, r, map, "company_background"),
                website = GetCell(ws, r, map, "website"),
                remark = GetCell(ws, r, map, "remark"),
                address_line = GetCell(ws, r, map, "address_line"),
                city = GetCell(ws, r, map, "city"),
                state = GetCell(ws, r, map, "state"),
                postcode = GetCell(ws, r, map, "postcode"),
                country = GetCell(ws, r, map, "country"),
                total_no_of_staff_raw = GetCell(ws, r, map, "total_no_of_staff"),
                vacancy_level = GetCell(ws, r, map, "vacancy_level"),
                status_raw = GetCell(ws, r, map, "status"),
                visibility_raw = GetCell(ws, r, map, "visibility"),
                last_visit_raw = GetCell(ws, r, map, "last_visit"),
                last_contact_raw = GetCell(ws, r, map, "last_contact"),
                is_hq_raw = GetCell(ws, r, map, "is_hq")
            });
        }

        return rows;
    }

    private static ImportCompanyRow MapRow(IReadOnlyList<string> cells, Dictionary<string, int> map)
    {
        return new ImportCompanyRow
        {
            reg_no = GetCell(cells, map, "reg_no"),
            name = GetCell(cells, map, "name"),
            industry_involved = GetCell(cells, map, "industry_involved"),
            products_and_services = GetCell(cells, map, "products_and_services"),
            company_background = GetCell(cells, map, "company_background"),
            website = GetCell(cells, map, "website"),
            remark = GetCell(cells, map, "remark"),
            address_line = GetCell(cells, map, "address_line"),
            city = GetCell(cells, map, "city"),
            state = GetCell(cells, map, "state"),
            postcode = GetCell(cells, map, "postcode"),
            country = GetCell(cells, map, "country"),
            total_no_of_staff_raw = GetCell(cells, map, "total_no_of_staff"),
            vacancy_level = GetCell(cells, map, "vacancy_level"),
            status_raw = GetCell(cells, map, "status"),
            visibility_raw = GetCell(cells, map, "visibility"),
            last_visit_raw = GetCell(cells, map, "last_visit"),
            last_contact_raw = GetCell(cells, map, "last_contact"),
            is_hq_raw = GetCell(cells, map, "is_hq")
        };
    }

    private static string GetCell(IReadOnlyList<string> cells, Dictionary<string, int> map, string key)
    {
        return map.TryGetValue(NormalizeHeader(key), out var index) && index >= 0 && index < cells.Count
            ? cells[index].Trim()
            : string.Empty;
    }

    private static string GetCell(IXLWorksheet ws, int row, Dictionary<string, int> map, string key)
    {
        return map.TryGetValue(NormalizeHeader(key), out var col)
            ? ws.Cell(row, col).GetString().Trim()
            : string.Empty;
    }

    private static void EnsureRequiredHeaders(IEnumerable<string> headers)
    {
        var normalized = headers.Select(NormalizeHeader).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = RequiredHeaders.Where(h => !normalized.Contains(NormalizeHeader(h))).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException("Missing required column(s): " + string.Join(", ", missing));
        }
    }

    private static string NormalizeHeader(string header)
    {
        return (header ?? string.Empty).Trim().Replace(" ", "_").ToLowerInvariant();
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

    private static void NormalizeRow(ImportCompanyRow row)
    {
        row.reg_no = NullIfWhiteSpace(row.reg_no);
        row.name = NullIfWhiteSpace(row.name);
        row.industry_involved = NullIfWhiteSpace(row.industry_involved);
        row.products_and_services = NullIfWhiteSpace(row.products_and_services);
        row.company_background = NullIfWhiteSpace(row.company_background);
        row.website = NullIfWhiteSpace(row.website);
        row.remark = NullIfWhiteSpace(row.remark);
        row.address_line = NullIfWhiteSpace(row.address_line);
        row.city = NullIfWhiteSpace(row.city);
        row.state = NullIfWhiteSpace(row.state);
        row.postcode = NullIfWhiteSpace(row.postcode);
        row.country = NullIfWhiteSpace(row.country);
        row.vacancy_level = NullIfWhiteSpace(row.vacancy_level);

        if (int.TryParse(row.total_no_of_staff_raw, out var staff))
        {
            row.total_no_of_staff = staff;
        }

        if (byte.TryParse(row.status_raw, out var status))
        {
            row.status = status;
        }

        if (byte.TryParse(row.visibility_raw, out var visibility))
        {
            row.visibility = visibility;
        }

        if (DateTime.TryParse(row.last_visit_raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastVisit))
        {
            row.last_visit = lastVisit.Date;
        }

        if (DateTime.TryParse(row.last_contact_raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastContact))
        {
            row.last_contact = lastContact.Date;
        }

        if (!string.IsNullOrWhiteSpace(row.is_hq_raw))
        {
            var raw = row.is_hq_raw.Trim().ToLowerInvariant();
            row.is_hq = raw is "1" or "true" or "yes" or "y";
        }
    }

    private static string? ValidateRow(ImportCompanyRow row)
    {
        if (string.IsNullOrWhiteSpace(row.reg_no))
        {
            return "reg_no is required.";
        }

        if (string.IsNullOrWhiteSpace(row.name))
        {
            return "name is required.";
        }

        if (string.IsNullOrWhiteSpace(row.address_line))
        {
            return "address_line is required.";
        }

        if (row.status.HasValue && row.status.Value > 3)
        {
            return "status must be 0, 1, 2, or 3.";
        }

        if (row.visibility.HasValue && row.visibility.Value > 1)
        {
            return "visibility must be 0 or 1.";
        }

        if (row.total_no_of_staff_raw != null && row.total_no_of_staff == null)
        {
            return "total_no_of_staff must be a whole number.";
        }

        if (row.last_visit_raw != null && row.last_visit == null)
        {
            return "last_visit must be a valid date.";
        }

        if (row.last_contact_raw != null && row.last_contact == null)
        {
            return "last_contact must be a valid date.";
        }

        return null;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public sealed class ImportCompanyRow
    {
        public string? reg_no { get; set; }
        public string? name { get; set; }
        public string? industry_involved { get; set; }
        public string? products_and_services { get; set; }
        public string? company_background { get; set; }
        public string? website { get; set; }
        public string? remark { get; set; }
        public string? address_line { get; set; }
        public string? city { get; set; }
        public string? state { get; set; }
        public string? postcode { get; set; }
        public string? country { get; set; }
        public string? total_no_of_staff_raw { get; set; }
        public int? total_no_of_staff { get; set; }
        public string? vacancy_level { get; set; }
        public string? status_raw { get; set; }
        public byte? status { get; set; }
        public string? visibility_raw { get; set; }
        public byte? visibility { get; set; }
        public string? last_visit_raw { get; set; }
        public DateTime? last_visit { get; set; }
        public string? last_contact_raw { get; set; }
        public DateTime? last_contact { get; set; }
        public string? is_hq_raw { get; set; }
        public bool? is_hq { get; set; }
    }

    public sealed record ImportError(int RowNumber, string MessageText);
}
