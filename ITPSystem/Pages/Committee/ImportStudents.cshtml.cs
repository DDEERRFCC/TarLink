using ClosedXML.Excel;
using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

public class CommitteeImportStudentsModel : CommitteePageModelBase
{
    private static readonly string[] RequiredHeaders =
    {
        "number_ic",
        "studentID",
        "studentName",
        "studentEmail",
        "cohortId",
        "level",
        "programme",
        "groupNo"
    };

    private readonly ApplicationDbContext _db;

    public CommitteeImportStudentsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public IFormFile? UploadFile { get; set; }

    [TempData]
    public string? Message { get; set; }

    public List<ImportError> Errors { get; private set; } = new();
    public int ImportedCount { get; private set; }
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

        List<ImportStudentRow> rows;
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

        var seenStudentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenIc = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNo = i + 2; // Row 1 is header
            var row = rows[i];
            NormalizeRow(row);

            var rowError = ValidateRow(row);
            if (rowError != null)
            {
                Errors.Add(new ImportError(rowNo, rowError));
                continue;
            }

            if (!seenStudentIds.Add(row.studentID))
            {
                Errors.Add(new ImportError(rowNo, $"Duplicate studentID in file: {row.studentID}"));
                continue;
            }
            if (!seenEmails.Add(row.studentEmail))
            {
                Errors.Add(new ImportError(rowNo, $"Duplicate studentEmail in file: {row.studentEmail}"));
                continue;
            }
            if (!seenIc.Add(row.number_ic))
            {
                Errors.Add(new ImportError(rowNo, $"Duplicate number_ic in file: {row.number_ic}"));
                continue;
            }

            if (!_db.Cohorts.Any(c => c.cohort_id == row.cohortId))
            {
                Errors.Add(new ImportError(rowNo, $"cohortId not found: {row.cohortId}"));
                continue;
            }

            if (_db.StudentApplications.Any(s =>
                s.studentID == row.studentID ||
                s.studentEmail == row.studentEmail ||
                s.number_ic == row.number_ic))
            {
                Errors.Add(new ImportError(rowNo, "Student already exists (studentID/email/IC)."));
                continue;
            }

            if (_db.SysUsers.Any(u =>
                u.username == row.studentID ||
                u.email == row.studentEmail ||
                u.ic_number == row.number_ic))
            {
                Errors.Add(new ImportError(rowNo, "Account already exists (username/email/IC)."));
                continue;
            }

            using var tx = _db.Database.BeginTransaction();
            try
            {
                var now = DateTime.Now;
                var app = new StudentApplication
                {
                    number_ic = row.number_ic,
                    studentID = row.studentID,
                    studentName = row.studentName,
                    studentEmail = row.studentEmail,
                    cohortId = row.cohortId,
                    level = row.level,
                    programme = row.programme,
                    groupNo = row.groupNo,
                    applyStatus = NormalizeStatus(row.applyStatus),
                    created_at = now,
                    updated_at = now,
                    gender = "O",
                    ownTransport = false,
                    ucSupervisor = NullIfWhiteSpace(row.ucSupervisor),
                    ucSupervisorEmail = NullIfWhiteSpace(row.ucSupervisorEmail),
                    ucSupervisorContact = NullIfWhiteSpace(row.ucSupervisorContact)
                };

                _db.StudentApplications.Add(app);
                _db.SaveChanges();

                var user = new SysUser
                {
                    email = app.studentEmail,
                    username = app.studentID,
                    password = app.number_ic,
                    role = "student",
                    ic_number = app.number_ic,
                    application_id = app.application_id,
                    is_active = true,
                    is_locked = false,
                    created_at = now,
                    updated_at = now
                };

                _db.SysUsers.Add(user);
                _db.SaveChanges();

                tx.Commit();
                ImportedCount++;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Errors.Add(new ImportError(rowNo, ex.InnerException?.Message ?? ex.Message));
            }
        }

        FailedCount = Errors.Count;
        Message = $"Import complete. Success: {ImportedCount}, Failed: {FailedCount}.";
        return Page();
    }

    private List<ImportStudentRow> ReadCsv(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new InvalidOperationException("CSV header row is missing.");
        }

        var headers = headerLine.Split(',').Select(x => x.Trim()).ToList();
        EnsureRequiredHeaders(headers);

        var map = headers
            .Select((name, index) => new { key = NormalizeHeader(name), index })
            .GroupBy(x => x.key)
            .Select(g => g.First())
            .ToDictionary(x => x.key, x => x.index, StringComparer.OrdinalIgnoreCase);

        var rows = new List<ImportStudentRow>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = line.Split(',');
            rows.Add(new ImportStudentRow
            {
                number_ic = GetCell(cells, map, "number_ic"),
                studentID = GetCell(cells, map, "studentID"),
                studentName = GetCell(cells, map, "studentName"),
                studentEmail = GetCell(cells, map, "studentEmail"),
                cohortIdRaw = GetCell(cells, map, "cohortId"),
                levelRaw = GetCell(cells, map, "level"),
                programme = GetCell(cells, map, "programme"),
                groupNoRaw = GetCell(cells, map, "groupNo"),
                applyStatus = GetCell(cells, map, "applyStatus"),
                ucSupervisor = GetCell(cells, map, "ucSupervisor"),
                ucSupervisorEmail = GetCell(cells, map, "ucSupervisorEmail"),
                ucSupervisorContact = GetCell(cells, map, "ucSupervisorContact")
            });
        }

        return rows;
    }

    private List<ImportStudentRow> ReadXlsx(IFormFile file)
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
            return new List<ImportStudentRow>();
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

        var rows = new List<ImportStudentRow>();
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

            rows.Add(new ImportStudentRow
            {
                number_ic = GetCell(ws, r, map, "number_ic"),
                studentID = GetCell(ws, r, map, "studentID"),
                studentName = GetCell(ws, r, map, "studentName"),
                studentEmail = GetCell(ws, r, map, "studentEmail"),
                cohortIdRaw = GetCell(ws, r, map, "cohortId"),
                levelRaw = GetCell(ws, r, map, "level"),
                programme = GetCell(ws, r, map, "programme"),
                groupNoRaw = GetCell(ws, r, map, "groupNo"),
                applyStatus = GetCell(ws, r, map, "applyStatus"),
                ucSupervisor = GetCell(ws, r, map, "ucSupervisor"),
                ucSupervisorEmail = GetCell(ws, r, map, "ucSupervisorEmail"),
                ucSupervisorContact = GetCell(ws, r, map, "ucSupervisorContact")
            });
        }

        return rows;
    }

    private static string GetCell(string[] cells, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(NormalizeHeader(key), out var index))
        {
            return string.Empty;
        }

        return index >= 0 && index < cells.Length ? cells[index].Trim() : string.Empty;
    }

    private static string GetCell(IXLWorksheet ws, int row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(NormalizeHeader(key), out var col))
        {
            return string.Empty;
        }

        return ws.Cell(row, col).GetString().Trim();
    }

    private static void EnsureRequiredHeaders(List<string> headers)
    {
        var normalized = headers.Select(NormalizeHeader).ToList();
        var missing = RequiredHeaders
            .Where(required => !normalized.Contains(NormalizeHeader(required), StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missing.Any())
        {
            throw new InvalidOperationException("Missing required columns: " + string.Join(", ", missing));
        }
    }

    private void NormalizeRow(ImportStudentRow row)
    {
        row.number_ic = (row.number_ic ?? string.Empty).Trim();
        row.studentID = (row.studentID ?? string.Empty).Trim();
        row.studentName = (row.studentName ?? string.Empty).Trim();
        row.studentEmail = (row.studentEmail ?? string.Empty).Trim();
        row.programme = (row.programme ?? string.Empty).Trim().ToUpperInvariant();
        row.applyStatus = string.IsNullOrWhiteSpace(row.applyStatus) ? "pending" : row.applyStatus.Trim();

        if (int.TryParse(row.cohortIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cohortId))
        {
            row.cohortId = cohortId;
        }
        if (byte.TryParse(row.levelRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level))
        {
            row.level = level;
        }
        if (int.TryParse(row.groupNoRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var groupNo))
        {
            row.groupNo = groupNo;
        }
    }

    private static string? ValidateRow(ImportStudentRow row)
    {
        if (string.IsNullOrWhiteSpace(row.number_ic)) return "number_ic is required.";
        if (string.IsNullOrWhiteSpace(row.studentID)) return "studentID is required.";
        if (string.IsNullOrWhiteSpace(row.studentName)) return "studentName is required.";
        if (string.IsNullOrWhiteSpace(row.studentEmail)) return "studentEmail is required.";
        if (string.IsNullOrWhiteSpace(row.programme)) return "programme is required.";
        if (row.cohortId <= 0) return "cohortId must be a valid integer.";
        if (row.groupNo <= 0) return "groupNo must be a valid integer.";
        if (row.level <= 0) return "level must be a positive integer.";
        if (!row.studentEmail.Contains('@')) return "studentEmail is invalid.";
        if (row.programme.Length > 4) return "programme max length is 4.";
        return null;
    }

    private static string NormalizeHeader(string header)
    {
        var chars = header
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return new string(chars);
    }

    private static string NormalizeStatus(string? rawStatus)
    {
        var value = (rawStatus ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "approved" => "approved",
            "rejected" => "rejected",
            "withdrawn" => "withdrawn",
            _ => "pending"
        };
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public class ImportStudentRow
    {
        public string number_ic { get; set; } = string.Empty;
        public string studentID { get; set; } = string.Empty;
        public string studentName { get; set; } = string.Empty;
        public string studentEmail { get; set; } = string.Empty;
        public string cohortIdRaw { get; set; } = string.Empty;
        public string levelRaw { get; set; } = string.Empty;
        public string programme { get; set; } = string.Empty;
        public string groupNoRaw { get; set; } = string.Empty;
        public string? applyStatus { get; set; }
        public string? ucSupervisor { get; set; }
        public string? ucSupervisorEmail { get; set; }
        public string? ucSupervisorContact { get; set; }

        public int cohortId { get; set; }
        public byte level { get; set; }
        public int groupNo { get; set; }
    }

    public record ImportError(int RowNo, string Error);
}
