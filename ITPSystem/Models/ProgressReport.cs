using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("progressreport")]
    public class ProgressReport
    {
        [Key]
        public long report_id { get; set; }

        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }

        public int applicantId { get; set; }
        public int cohortId { get; set; }

        [Column(TypeName = "ENUM('progress','final')")]
        [StringLength(10)]
        public string reportType { get; set; } = "progress";

        public byte? reportNo { get; set; } // 1..6 for progress, NULL for final

        public DateTime dueDate { get; set; }

        public byte status { get; set; } // 0=pending,1=submitted,2=approved,3=rejected
        public string? remark { get; set; }
        [StringLength(500)]
        public string? file_path { get; set; }
    }
}
