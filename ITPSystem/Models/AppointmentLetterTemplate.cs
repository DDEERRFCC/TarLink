using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("appointmentlettertemplate")]
    public class AppointmentLetterTemplate
    {
        [Key]
        public int template_id { get; set; }

        public int cohort_id { get; set; }

        [StringLength(100)]
        public string? template_name { get; set; }

        public string? content { get; set; }

        [StringLength(500)]
        public string? company_acceptance_letter_path { get; set; }

        [StringLength(500)]
        public string? indemnity_letter_path { get; set; }

        [StringLength(500)]
        public string? parent_acknowledgement_form_path { get; set; }

        [StringLength(500)]
        public string? company_supervisor_evaluation_form_path { get; set; }

        [StringLength(500)]
        public string? progress_report_template_path { get; set; }

        [StringLength(500)]
        public string? final_report_template_path { get; set; }

        [StringLength(500)]
        public string? student_support_letter_path { get; set; }

        [StringLength(500)]
        public string? appointment_confirmation_letter_path { get; set; }

        [StringLength(500)]
        public string? warning_letter_path { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime created_at { get; set; }

        [ForeignKey(nameof(cohort_id))]
        public Cohort? Cohort { get; set; }
    }
}
