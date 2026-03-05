using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("studentcv")]
    public class StudentCv
    {
        [Key]
        public int cv_id { get; set; }

        public int application_id { get; set; }

        [Required]
        [StringLength(500)]
        public string file_path { get; set; } = string.Empty;

        [StringLength(255)]
        public string? file_name { get; set; }

        public DateTime uploaded_at { get; set; }

        [ForeignKey("application_id")]
        public StudentApplication? StudentApplication { get; set; }
    }
}
