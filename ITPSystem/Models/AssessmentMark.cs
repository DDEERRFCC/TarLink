using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("assessmentmark")]
    public class AssessmentMark
    {
        [Key]
        public long mark_id { get; set; }

        public int application_id { get; set; }
        public int supervisor_user_id { get; set; }

        [StringLength(120)]
        public string rubric_item { get; set; } = string.Empty;

        [Column(TypeName = "decimal(5,2)")]
        public decimal score { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal max_score { get; set; }

        public string? remarks { get; set; }
        public DateTime created_at { get; set; } = DateTime.UtcNow;
    }
}
