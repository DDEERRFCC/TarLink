using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("cohort")]
    public class Cohort
    {
        [Key]
        public int cohort_id { get; set; }

        [StringLength(100)]
        public string? description { get; set; }

        public DateTime? startDate { get; set; }

        public DateTime? endDate { get; set; }

        public byte? level { get; set; }

        public bool isActive { get; set; }

        // ===== Report Due Dates =====
        public DateTime? report1DueDate { get; set; }
        public DateTime? report2DueDate { get; set; }
        public DateTime? report3DueDate { get; set; }
        public DateTime? report4DueDate { get; set; }
        public DateTime? report5DueDate { get; set; }
        public DateTime? finalReportDueDate { get; set; }

        // ===== Exam & Evaluation =====
        public DateTime? examStartDate { get; set; }
        public DateTime? examEndDate { get; set; }
        public DateTime? companyEvaluationDate { get; set; }

        // ===== Report Months =====
        [StringLength(45)]
        public string? reportMonth1 { get; set; }

        [StringLength(45)]
        public string? reportMonth2 { get; set; }

        [StringLength(45)]
        public string? reportMonth3 { get; set; }

        [StringLength(45)]
        public string? reportMonth4 { get; set; }

        [StringLength(45)]
        public string? reportMonth5 { get; set; }

        [StringLength(45)]
        public string? reportMonth6 { get; set; }

        // ===== Administration =====
        [StringLength(45)]
        public string? campus { get; set; }

        [StringLength(45)]
        public string? faculty { get; set; }

        [StringLength(250)]
        public string? personInCharge { get; set; }

        [StringLength(250)]
        public string? pidEmail { get; set; }

        // ===== Navigation =====
        public ICollection<StudentApplication> StudentApplications { get; set; }
            = new List<StudentApplication>();
    }
}
