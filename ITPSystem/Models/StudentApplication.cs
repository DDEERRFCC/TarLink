using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("studentapplication")]
    public class StudentApplication
    {
        [Key]
        public int application_id { get; set; }

        // ===== Audit Fields (handled by DB) =====
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }

        // ===== Academic Info =====
        [Required]
        [StringLength(20)]
        public string number_ic { get; set; } = string.Empty;

        [Required]
        [StringLength(45)]
        public string studentID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Student ID is required")]
        [StringLength(255)]
        public string studentName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(255)]
        public string studentEmail { get; set; } = string.Empty;

        public DateTime? contactDate { get; set; }

        [Required]
        [Range(1, 2)]
        public byte? level { get; set; }  // 1 = Diploma, 2 = Degree

        [StringLength(1)]
        public string gender { get; set; } = "O";

        [Column(TypeName = "decimal(3,2)")]
        public decimal? CGPA { get; set; }

        [Required(ErrorMessage = "Cohort is required")]
        public int cohortId { get; set; }

        [Required, StringLength(4)]
        public string? programme { get; set; }

        [Required]
        public int? groupNo { get; set; }

        // ===== Company =====
        [StringLength(255)]
        public string? comName { get; set; }

        [StringLength(500)]
        public string? comAddress { get; set; }

        [StringLength(255)]
        public string? comSupervisor { get; set; }

        [StringLength(255)]
        public string? comSupervisorEmail { get; set; }

        [StringLength(255)]
        public string? comSupervisorContact { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? allowance { get; set; }

        // ===== Supervisor =====
        [StringLength(255)]
        public string? ucSupervisor { get; set; }

        [StringLength(255)]
        public string? ucSupervisorEmail { get; set; }

        [StringLength(255)]
        public string? ucSupervisorContact { get; set; }

        // ===== Personal / Contact =====
        [StringLength(255)]
        public string? personalEmail { get; set; }

        [StringLength(500)]
        public string? tempAddress { get; set; }

        public string? permanentAddress { get; set; }

        [StringLength(45)]
        public string? permanentContact { get; set; }

        [Required]
        public bool ownTransport { get; set; } = false;

        public string? healthRemark { get; set; }

        // ===== Technical Knowledge =====
        [StringLength(50)]
        public string? programmingKnowledge { get; set; }

        [StringLength(50)]
        public string? databaseKnowledge { get; set; }

        [StringLength(50)]
        public string? networkingKnowledge { get; set; }

        public byte? templateVersion { get; set; } = 1;

        // ===== Application Status =====
        [Column(TypeName = "ENUM('pending','approved','rejected','withdrawn')")]
        public string? applyStatus { get; set; } = "pending";

        public string? remark { get; set; }

        // ===== Documents =====
        [StringLength(255)]
        public string? formAcceptance { get; set; }

        [StringLength(255)]
        public string? formAcknowledgement { get; set; }

        [StringLength(255)]
        public string? letterIdentity { get; set; }

        [StringLength(255)]
        public string? otherEvidence { get; set; }

        [StringLength(255)]
        public string? doVerifier { get; set; }

        [StringLength(255)]
        public string? doVerifierEmail { get; set; }

        public bool isAgreed { get; set; } = false;

        // ===== Navigation =====
        [ForeignKey("cohortId")]
        public Cohort? Cohort { get; set; }
    }
}
