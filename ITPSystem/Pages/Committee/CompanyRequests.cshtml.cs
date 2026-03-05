using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

public class CommitteeCompanyRequestsModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeCompanyRequestsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<CompanyRequest> PendingRequests { get; private set; } = new();
    public List<CompanyRequest> HistoryRequests { get; private set; } = new();

    [BindProperty]
    public DecisionInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class DecisionInput
    {
        [Required]
        public int request_id { get; set; }

        [Required]
        public string status { get; set; } = "approved";

        public string? decision_remark { get; set; }
    }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadRequests();
        return Page();
    }

    public IActionResult OnPostDecide()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        if (!ModelState.IsValid)
        {
            LoadRequests();
            return Page();
        }

        var request = _db.CompanyRequests.FirstOrDefault(r => r.request_id == Input.request_id);
        if (request == null)
        {
            StatusMessage = "Request not found.";
            return RedirectToPage();
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        request.status = NormalizeStatus(Input.status);
        request.decision_remark = string.IsNullOrWhiteSpace(Input.decision_remark) ? null : Input.decision_remark.Trim();
        request.reviewed_by = userId.Value;
        request.reviewed_at = DateTime.Now;
        request.updated_at = DateTime.Now;

        if (string.Equals(request.status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            UpsertApprovedCompany(request);
        }

        _db.SaveChanges();
        StatusMessage = "Request updated successfully.";
        return RedirectToPage();
    }

    private void LoadRequests()
    {
        var all = _db.CompanyRequests.AsNoTracking()
            .OrderByDescending(r => r.requested_at)
            .ToList();

        PendingRequests = all
            .Where(r => string.Equals(r.status, "pending", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.requested_at)
            .ToList();

        HistoryRequests = all
            .Where(r => !string.Equals(r.status, "pending", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.reviewed_at ?? r.updated_at)
            .ToList();
    }

    private int? GetCurrentUserId()
    {
        var userIdText = HttpContext.Session.GetString("UserID");
        return int.TryParse(userIdText, out var userId) ? userId : null;
    }

    private void UpsertApprovedCompany(CompanyRequest request)
    {
        var companyName = (request.company_name ?? string.Empty).Trim();
        var address = (request.address1 ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        var nameLower = companyName.ToLower();
        var addressLower = address.ToLower();

        var existing = _db.Companies.FirstOrDefault(c =>
            c.name.ToLower() == nameLower &&
            (c.address1 ?? string.Empty).ToLower() == addressLower);

        if (existing == null)
        {
            _db.Companies.Add(new Company
            {
                created_at = DateTime.Now,
                lastUpdate = DateTime.Now,
                name = companyName,
                address1 = address,
                status = 1,
                visibility = 1
            });
            return;
        }

        existing.status = 1;
        existing.visibility = 1;
    }

    private static string NormalizeStatus(string? rawStatus)
    {
        var value = (rawStatus ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "approved" => "approved",
            "rejected" => "rejected",
            "cancelled" => "cancelled",
            _ => "pending"
        };
    }

}
