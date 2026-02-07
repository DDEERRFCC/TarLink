using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("studentapplication")]
    public class StudentApplication
    {
        [Key]
        public long application_id { get; set; }

        public DateTime? timeStamp { get; set; }

        [StringLength(45)]
        public string? studentID { get; set; }

        [StringLength(255)]
        public string? studentName { get; set; }

        [StringLength(1)]
        public string? gender { get; set; }

        [StringLength(255)]
        public string? studentEmail { get; set; }

        public double? CGPA { get; set; }

        // Add other properties if needed
    }
}
