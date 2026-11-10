using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;
namespace COSGeniusSystem.Data;
public partial class ApplicationDbContext
{
 public DbSet<HerbRequisition> HerbRequisitions => Set<HerbRequisition>();
 public DbSet<HerbRequisitionPrescription> HerbRequisitionPrescriptions => Set<HerbRequisitionPrescription>();
 public DbSet<HerbOutboundDetail> HerbOutboundDetails => Set<HerbOutboundDetail>();
 public DbSet<HerbRequisitionDetail> HerbRequisitionDetails => Set<HerbRequisitionDetail>();
 public DbSet<HerbOutbound> HerbOutbounds => Set<HerbOutbound>();
 public DbSet<WarehouseStorageLocation> WarehouseStorageLocations => Set<WarehouseStorageLocation>();
 public DbSet<HerbReceiptDiscrepancy> HerbReceiptDiscrepancies => Set<HerbReceiptDiscrepancy>();
 partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
 {
  modelBuilder.Entity<HerbOutboundDemand>(e=>{
   e.ToTable("HerbOutboundDemand");e.HasKey(x=>x.Id);e.Property(x=>x.QuantityGrams).HasPrecision(13,3);
   e.HasOne(x=>x.OutboundDetail).WithMany().HasForeignKey(x=>x.OutboundDetailId).OnDelete(DeleteBehavior.NoAction);
   e.HasOne<HerbRequisitionDetail>().WithMany().HasForeignKey(x=>x.RequisitionDetailId).OnDelete(DeleteBehavior.NoAction);
  });
  modelBuilder.Entity<Invoice>().ToTable("Invoice",t=>t.UseSqlOutputClause(false));
  modelBuilder.Entity<InvoiceDetail>().ToTable("InvoiceDetail",t=>t.UseSqlOutputClause(false));
  modelBuilder.Entity<Batch>().Property(x=>x.CurrentQuantity).HasPrecision(15,5);
  modelBuilder.Entity<Batch>().Property(x=>x.OriginalQuantity).HasPrecision(15,5);
  modelBuilder.Entity<Batch>().HasOne(x=>x.StorageLocation).WithMany().HasForeignKey(x=>x.StorageLocationId).OnDelete(DeleteBehavior.NoAction);
  modelBuilder.Entity<DispenseDetail>().ToTable("DispenseDetail",t=>t.UseSqlOutputClause(false));
  modelBuilder.Entity<DispenseDetail>().Property(x=>x.HerbOutboundDetailId).HasColumnName("HerbOutboundDetailID");
  modelBuilder.Entity<DispenseDetail>().Property(x=>x.QuantityTaken).HasPrecision(18,3);
  // Completion inserts must support the enabled MedicineDispense triggers.
  modelBuilder.Entity<MedicineDispense>().ToTable("MedicineDispense",t=>t.UseSqlOutputClause(false));
  modelBuilder.Entity<MedicineDispense>().Property(x=>x.HerbOutboundId).HasColumnName("HerbOutboundID");
  modelBuilder.Entity<WarehouseStorageLocation>(e=>{e.ToTable("WarehouseStorageLocation");e.HasKey(x=>x.Id); e.Property(x=>x.Code).HasMaxLength(100);});
  modelBuilder.Entity<HerbRequisition>(e=>{
   e.ToTable("HerbRequisition");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("HerbRequisitionID");
   e.Property(x=>x.CreatedBy).HasColumnName("RequestedBy"); e.Property(x=>x.CreatedAt).HasColumnName("RequestedAt");
   e.Property(x=>x.RejectionReason).HasColumnName("ClosureReason");e.Property(x=>x.RowVersion).IsRowVersion();
  });
  modelBuilder.Entity<HerbRequisitionPrescription>(e=>{
   e.ToTable("HerbRequisitionPrescription");e.HasKey(x=>x.Id);
   e.HasAlternateKey(x=>new { x.RequestId, x.PrescriptionId });
   e.HasIndex(x=>x.PrescriptionId).IsUnique().HasFilter("[IsActive] = 1");
   e.HasOne(x=>x.Request).WithMany(x=>x.Prescriptions).HasForeignKey(x=>x.RequestId).OnDelete(DeleteBehavior.NoAction);
   e.HasOne(x=>x.Prescription).WithMany().HasForeignKey(x=>x.PrescriptionId).OnDelete(DeleteBehavior.NoAction);
  });
  modelBuilder.Entity<HerbRequisitionDetail>(e=>{
   e.ToTable("HerbRequisitionDetail");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("HerbRequisitionDetailID");
   e.Property(x=>x.RequestId).HasColumnName("HerbRequisitionID");e.Property(x=>x.RequestedQuantity).HasPrecision(13,3);
   e.HasOne(x=>x.Request).WithMany(x=>x.Details).HasForeignKey(x=>x.RequestId).OnDelete(DeleteBehavior.NoAction);
   // Lines depend on the request/prescription link, not only on the header.
   // Match the database constraint so EF inserts links before their lines.
   e.HasOne<HerbRequisitionPrescription>().WithMany()
    .HasForeignKey(x=>new { x.RequestId, x.PrescriptionId })
    .HasPrincipalKey(x=>new { x.RequestId, x.PrescriptionId })
    .HasConstraintName("FK_HL_ReqLinePrescriptionV2").OnDelete(DeleteBehavior.NoAction);
  });
  modelBuilder.Entity<HerbOutbound>(e=>{
   e.ToTable("HerbOutbound");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("HerbOutboundID");
   e.Property(x=>x.RequestId).HasColumnName("HerbRequisitionID");
  });
  modelBuilder.Entity<HerbOutboundDetail>(e=>{
   // Receipt updates must work with the database's enabled outbound triggers.
   e.ToTable("HerbOutboundDetail",t=>t.UseSqlOutputClause(false));e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("HerbOutboundDetailID");
   e.Property(x=>x.RequestId).HasColumnName("HerbRequisitionID");e.Property(x=>x.OutboundId).HasColumnName("HerbOutboundID");
   e.Property(x=>x.QuantityGrams).HasColumnName("IssuedQuantity").HasPrecision(13,3);e.Property(x=>x.ReceivedQuantity).HasPrecision(13,3);
   e.HasOne(x=>x.Request).WithMany(x=>x.Allocations).HasForeignKey(x=>x.RequestId).OnDelete(DeleteBehavior.NoAction);
   e.HasOne(x=>x.Outbound).WithMany(x=>x.Details).HasForeignKey(x=>x.OutboundId).OnDelete(DeleteBehavior.NoAction);
   e.HasOne(x=>x.Batch).WithMany().HasForeignKey(x=>x.BatchId).OnDelete(DeleteBehavior.NoAction);
  });
  modelBuilder.Entity<HerbReceiptDiscrepancy>(e=>{
   e.ToTable("HerbReceiptDiscrepancy");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("HerbReceiptDiscrepancyID");
   e.Property(x=>x.OutboundId).HasColumnName("HerbOutboundID");
   e.HasMany(x=>x.Details).WithOne().HasForeignKey(x=>x.ReportId).OnDelete(DeleteBehavior.NoAction);
  });
  modelBuilder.Entity<HerbReceiptDiscrepancyDetail>(e=>{
   e.ToTable("HerbReceiptDiscrepancyDetail");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("HerbReceiptDiscrepancyDetailID");
   e.Property(x=>x.ReportId).HasColumnName("HerbReceiptDiscrepancyID");e.Property(x=>x.OutboundId).HasColumnName("HerbOutboundID");e.Property(x=>x.OutboundDetailId).HasColumnName("HerbOutboundDetailID");
   e.Property(x=>x.ExpectedQuantity).HasPrecision(13,3);e.Property(x=>x.ReceivedQuantity).HasPrecision(13,3);
   e.Property(x=>x.DifferenceQuantity).HasPrecision(14,3).HasComputedColumnSql("[ReceivedQuantity]-[ExpectedQuantity]",true);
  });
 }
}




