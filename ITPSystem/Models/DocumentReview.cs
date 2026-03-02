using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("documentreview")]
    public class DocumentReview
    {
        [Key]
        public long review_id { get; set; }

        public int application_id { get; set; }
        public int reviewed_by { get; set; }

        [StringLength(60)]
        public string document_type { get; set; } = string.Empty;

        [Column(TypeName = "ENUM('pending','approved','rejected')")]
        public string status { get; set; } = "pending";

        public string? remarks { get; set; }
        public DateTime reviewed_at { get; set; } = DateTime.UtcNow;
    }
}
