using Microsoft.EntityFrameworkCore;
using ITPSystem.Models;

namespace ITPSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Add these DbSets
        public DbSet<StudentApplication> StudentApplications { get; set; }
        public DbSet<SysUser> SysUsers { get; set; }
        public DbSet<Cohort> Cohorts { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<CompanyBranch> CompanyBranches { get; set; }
        public DbSet<BlacklistCompany> BlacklistCompanies { get; set; }
        public DbSet<Person> Persons { get; set; }
        public DbSet<AssessmentMark> AssessmentMarks { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<DocumentReview> DocumentReviews { get; set; }
        public DbSet<ProgressReport> ProgressReports { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<StudentCv> StudentCvs { get; set; }
        public DbSet<CompanyRequest> CompanyRequests { get; set; }
        public DbSet<UcSupervisor> UcSupervisors { get; set; }
        public DbSet<AppointmentLetterTemplate> AppointmentLetterTemplates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Company>()
                .HasMany(c => c.Branches)
                .WithOne(a => a.Company)
                .HasForeignKey(a => a.company_id)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AppointmentLetterTemplate>()
                .HasOne(t => t.Cohort)
                .WithMany()
                .HasForeignKey(t => t.cohort_id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AppointmentLetterTemplate>()
                .Property(t => t.created_at)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAdd();
        }
    }

}
