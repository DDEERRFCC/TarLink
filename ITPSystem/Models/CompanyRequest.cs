using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("companyrequest")]
    public class CompanyRequest
    {
        [Key]
        public int request_id { get; set; }

        public int? requested_by { get; set; }

        [Required]
        [StringLength(250)]
        public string company_name { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string address1 { get; set; } = string.Empty;

        [StringLength(150)]
        public string? contact_name { get; set; }

        [StringLength(255)]
        public string? contact_email { get; set; }

        [StringLength(30)]
        public string? contact_phone { get; set; }

        public string status { get; set; } = "pending";

        public string? decision_remark { get; set; }

        public int? reviewed_by { get; set; }

        public DateTime? reviewed_at { get; set; }

        public DateTime requested_at { get; set; }

        public DateTime updated_at { get; set; }
    }
}
