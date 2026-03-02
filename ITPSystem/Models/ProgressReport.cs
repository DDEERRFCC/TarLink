using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("progressreport")]
    public class ProgressReport
    {
        [Key]
        public long report_id { get; set; }

        public DateTime? timeStamp { get; set; }
        public DateTime? lastUpdate { get; set; }
        public long? applicantId { get; set; }
        public int? cohortId { get; set; }

        [StringLength(50)]
        public string? month { get; set; }

        public DateTime? dueDate { get; set; }

        public byte? status { get; set; } // 0=pending, 1=approved, 2=rejected

        public string? remark { get; set; }
    }
}
