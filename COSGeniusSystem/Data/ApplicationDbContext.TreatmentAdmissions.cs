using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Data;

public partial class ApplicationDbContext
{
    public DbSet<TreatmentAdmission> TreatmentAdmissions => Set<TreatmentAdmission>();

    private static void ConfigureTreatmentAdmissions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TreatmentAdmission>(e =>
        {
            e.ToTable("TreatmentAdmission");
            e.HasKey(x => x.AdmissionId);
            e.Property(x => x.RequestFingerprint).HasMaxLength(64).IsUnicode(false);
            e.Property(x => x.Shift).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.RegistrationKey, x.DoctorId }).IsUnique();
            e.HasIndex(x => new { x.DoctorId, x.ScheduledDate });
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AcceptedBy).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ClosedBy).OnDelete(DeleteBehavior.NoAction);
            e.HasMany(x => x.Sessions).WithOne(x => x.Admission).HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
