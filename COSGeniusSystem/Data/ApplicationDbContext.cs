using COSGeniusSystem.Models;

namespace COSGeniusSystem.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditDetail> AuditDetails { get; set; }

    public virtual DbSet<Batch> Batches { get; set; }

    public virtual DbSet<DispenseDetail> DispenseDetails { get; set; }

    public virtual DbSet<Disposal> Disposals { get; set; }

    public virtual DbSet<DisposalDetail> DisposalDetails { get; set; }

    public virtual DbSet<Doctor> Doctors { get; set; }

    public virtual DbSet<ExamSheet> ExamSheets { get; set; }

    public virtual DbSet<FormulaDetail> FormulaDetails { get; set; }

    public virtual DbSet<GoodsReceipt> GoodsReceipts { get; set; }

    public virtual DbSet<Herb> Herbs { get; set; }

    public virtual DbSet<ImportRequest> ImportRequests { get; set; }

    public virtual DbSet<ImportRequestDetail> ImportRequestDetails { get; set; }

    public virtual DbSet<InventoryAudit> InventoryAudits { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceDetail> InvoiceDetails { get; set; }

    public virtual DbSet<MedicalRecord> MedicalRecords { get; set; }

    public virtual DbSet<MedicineDispense> MedicineDispenses { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Patient> Patients { get; set; }

    public virtual DbSet<PatientContact> PatientContacts { get; set; }

    public virtual DbSet<Podetail> Podetails { get; set; }

    public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public virtual DbSet<ReceiptDetail> ReceiptDetails { get; set; }

    public virtual DbSet<ReceiptHistory> ReceiptHistories { get; set; }

    public virtual DbSet<RemedyFormula> RemedyFormulas { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Service> Services { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<TreatmentProtocol> TreatmentProtocols { get; set; }

    public virtual DbSet<UsageHistory> UsageHistories { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditDetail>(entity =>
        {
            entity.HasKey(e => e.AuditDetailId).HasName("PK__AuditDet__1E1C703DAB62BCCE");

            entity.ToTable("AuditDetail");

            entity.Property(e => e.AuditDetailId).HasColumnName("AuditDetailID");
            entity.Property(e => e.ActualQuantity).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.AuditId).HasColumnName("AuditID");
            entity.Property(e => e.BatchId).HasColumnName("BatchID");
            entity.Property(e => e.Discrepancy)
                .HasComputedColumnSql("([ActualQuantity]-[SystemQuantity])", false)
                .HasColumnType("decimal(13, 2)");
            entity.Property(e => e.Reason).HasMaxLength(255);
            entity.Property(e => e.SystemQuantity).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.Audit).WithMany(p => p.AuditDetails)
                .HasForeignKey(d => d.AuditId)
                .HasConstraintName("FK_AuditDetail_Audit");

            entity.HasOne(d => d.Batch).WithMany(p => p.AuditDetails)
                .HasForeignKey(d => d.BatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AuditDetail_Batch");
        });

        modelBuilder.Entity<Batch>(entity =>
        {
            entity.HasKey(e => e.BatchId).HasName("PK__Batch__5D55CE38A4FE232B");

            entity.ToTable("Batch");

            entity.HasIndex(e => new { e.HerbId, e.ExpiryDate }, "IX_Batch_Herb_ExpiryDate");

            entity.HasIndex(e => e.BatchNumber, "UQ__Batch__F869ED6D63EFBD99").IsUnique();

            entity.Property(e => e.BatchId).HasColumnName("BatchID");
            entity.Property(e => e.BatchNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CurrentQuantity).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.HerbId).HasColumnName("HerbID");
            entity.Property(e => e.ImportDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.OriginalQuantity).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Đạt chuẩn");
            entity.Property(e => e.UnitCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.Herb).WithMany(p => p.Batches)
                .HasForeignKey(d => d.HerbId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Batch_Herb");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.Batches)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Batch_Warehouse");
        });

        modelBuilder.Entity<DispenseDetail>(entity =>
        {
            entity.HasKey(e => e.DispenseDetailId).HasName("PK__Dispense__991E2D4BB797A4DE");

            entity.ToTable("DispenseDetail");

            entity.HasIndex(e => e.BatchId, "IX_DispenseDetail_BatchID");

            entity.Property(e => e.DispenseDetailId).HasColumnName("DispenseDetailID");
            entity.Property(e => e.BatchId).HasColumnName("BatchID");
            entity.Property(e => e.DispenseId).HasColumnName("DispenseID");
            entity.Property(e => e.HerbId).HasColumnName("HerbID");
            entity.Property(e => e.QuantityTaken).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.Batch).WithMany(p => p.DispenseDetails)
                .HasForeignKey(d => d.BatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DispenseDetail_Batch");

            entity.HasOne(d => d.Dispense).WithMany(p => p.DispenseDetails)
                .HasForeignKey(d => d.DispenseId)
                .HasConstraintName("FK_DispenseDetail_Dispense");

            entity.HasOne(d => d.Herb).WithMany(p => p.DispenseDetails)
                .HasForeignKey(d => d.HerbId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DispenseDetail_Herb");
        });

        modelBuilder.Entity<Disposal>(entity =>
        {
            entity.HasKey(e => e.DisposalId).HasName("PK__Disposal__206044C3500067ED");

            entity.ToTable("Disposal");

            entity.HasIndex(e => e.DisposalCode, "UQ__Disposal__68D7C9735CCB340D").IsUnique();

            entity.Property(e => e.DisposalId).HasColumnName("DisposalID");
            entity.Property(e => e.DisposalCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DisposalDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Chờ phê duyệt");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Disposals)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Disposal_User");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.Disposals)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Disposal_Warehouse");
        });

        modelBuilder.Entity<DisposalDetail>(entity =>
        {
            entity.HasKey(e => e.DisposalDetailId).HasName("PK__Disposal__DFE713E677DDC18F");

            entity.ToTable("DisposalDetail");

            entity.Property(e => e.DisposalDetailId).HasColumnName("DisposalDetailID");
            entity.Property(e => e.BatchId).HasColumnName("BatchID");
            entity.Property(e => e.DisposalId).HasColumnName("DisposalID");
            entity.Property(e => e.DisposalQuantity).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Notes).HasMaxLength(255);

            entity.HasOne(d => d.Batch).WithMany(p => p.DisposalDetails)
                .HasForeignKey(d => d.BatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DisposalDetail_Batch");

            entity.HasOne(d => d.Disposal).WithMany(p => p.DisposalDetails)
                .HasForeignKey(d => d.DisposalId)
                .HasConstraintName("FK_DisposalDetail_Disposal");
        });

        modelBuilder.Entity<Doctor>(entity =>
        {
            entity.HasKey(e => e.DoctorId).HasName("PK__Doctor__2DC00EDFF582BC3C");

            entity.ToTable("Doctor");

            entity.HasIndex(e => e.UserId, "UQ__Doctor__1788CCAD1B186363").IsUnique();

            entity.Property(e => e.DoctorId).HasColumnName("DoctorID");
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LicenseNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Specialization).HasMaxLength(150);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithOne(p => p.Doctor)
                .HasForeignKey<Doctor>(d => d.UserId)
                .HasConstraintName("FK_Doctor_User");
        });

        modelBuilder.Entity<ExamSheet>(entity =>
        {
            entity.HasKey(e => e.ExamSheetId).HasName("PK__ExamShee__31F9A9581FDCAC59");

            entity.ToTable("ExamSheet");

            entity.HasIndex(e => e.RecordId, "IX_ExamSheet_RecordID");

            entity.Property(e => e.ExamSheetId).HasColumnName("ExamSheetID");
            entity.Property(e => e.DoctorId).HasColumnName("DoctorID");
            entity.Property(e => e.ExamDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.FinalDiagnosis).HasMaxLength(500);
            entity.Property(e => e.PulseDiagnosis).HasMaxLength(255);
            entity.Property(e => e.RecordId).HasColumnName("RecordID");
            entity.Property(e => e.TongueDiagnosis).HasMaxLength(255);

            entity.HasOne(d => d.Doctor).WithMany(p => p.ExamSheets)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ExamSheet_Doctor");

            entity.HasOne(d => d.Record).WithMany(p => p.ExamSheets)
                .HasForeignKey(d => d.RecordId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ExamSheet_Record");
        });

        modelBuilder.Entity<FormulaDetail>(entity =>
        {
            entity.HasKey(e => e.FormulaDetailId).HasName("PK__FormulaD__DC9CD7F03E6B94FA");

            entity.ToTable("FormulaDetail");

            entity.Property(e => e.FormulaDetailId).HasColumnName("FormulaDetailID");
            entity.Property(e => e.DosagePerDose).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.FormulaId).HasColumnName("FormulaID");
            entity.Property(e => e.HerbId).HasColumnName("HerbID");
            entity.Property(e => e.Note).HasMaxLength(255);
            entity.Property(e => e.RoleInFormula).HasMaxLength(50);

            entity.HasOne(d => d.Formula).WithMany(p => p.FormulaDetails)
                .HasForeignKey(d => d.FormulaId)
                .HasConstraintName("FK_FormulaDetail_Formula");

            entity.HasOne(d => d.Herb).WithMany(p => p.FormulaDetails)
                .HasForeignKey(d => d.HerbId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FormulaDetail_Herb");
        });

        modelBuilder.Entity<GoodsReceipt>(entity =>
        {
            entity.HasKey(e => e.ReceiptId).HasName("PK__GoodsRec__CC08C400402F8E47");

            entity.ToTable("GoodsReceipt");

            entity.HasIndex(e => e.ReceiptCode, "UQ__GoodsRec__1AB76D002590D01C").IsUnique();

            entity.Property(e => e.ReceiptId).HasColumnName("ReceiptID");
            entity.Property(e => e.Note).HasMaxLength(255);
            entity.Property(e => e.Poid).HasColumnName("POID");
            entity.Property(e => e.ReceiptCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ReceiptDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TotalCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.Po).WithMany(p => p.GoodsReceipts)
                .HasForeignKey(d => d.Poid)
                .HasConstraintName("FK_GoodsReceipt_PO");

            entity.HasOne(d => d.ReceivedByNavigation).WithMany(p => p.GoodsReceipts)
                .HasForeignKey(d => d.ReceivedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GoodsReceipt_User");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.GoodsReceipts)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GoodsReceipt_Warehouse");
        });

        modelBuilder.Entity<Herb>(entity =>
        {
            entity.HasKey(e => e.HerbId).HasName("PK__Herb__086A0758896E06DB");

            entity.ToTable("Herb");

            entity.HasIndex(e => e.HerbCode, "UQ__Herb__A7FAC81B12CCE445").IsUnique();

            entity.Property(e => e.HerbId).HasColumnName("HerbID");
            entity.Property(e => e.CommonName).HasMaxLength(150);
            entity.Property(e => e.HerbCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MinThreshold).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.ScientificName).HasMaxLength(200);
            entity.Property(e => e.StandardPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TasteProperties).HasMaxLength(255);
            entity.Property(e => e.Unit).HasMaxLength(30);
        });

        modelBuilder.Entity<ImportRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__ImportRe__33A8519A62E3CF35");

            entity.ToTable("ImportRequest");

            entity.HasIndex(e => e.RequestCode, "UQ__ImportRe__CBAB82F61111396E").IsUnique();

            entity.Property(e => e.RequestId).HasColumnName("RequestID");
            entity.Property(e => e.Reason).HasMaxLength(255);
            entity.Property(e => e.RequestCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RequestDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Chờ duyệt");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.ImportRequests)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ImportRequest_User");
        });

        modelBuilder.Entity<ImportRequestDetail>(entity =>
        {
            entity.HasKey(e => e.RequestDetailId).HasName("PK__ImportRe__DC528B70B0CC50E0");

            entity.ToTable("ImportRequestDetail");

            entity.Property(e => e.RequestDetailId).HasColumnName("RequestDetailID");
            entity.Property(e => e.HerbId).HasColumnName("HerbID");
            entity.Property(e => e.Note).HasMaxLength(255);
            entity.Property(e => e.RequestId).HasColumnName("RequestID");
            entity.Property(e => e.RequestedQuantity).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.Herb).WithMany(p => p.ImportRequestDetails)
                .HasForeignKey(d => d.HerbId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ImportRequestDetail_Herb");

            entity.HasOne(d => d.Request).WithMany(p => p.ImportRequestDetails)
                .HasForeignKey(d => d.RequestId)
                .HasConstraintName("FK_ImportRequestDetail_Request");
        });

        modelBuilder.Entity<InventoryAudit>(entity =>
        {
            entity.HasKey(e => e.AuditId).HasName("PK__Inventor__A17F23B886181D25");

            entity.ToTable("InventoryAudit");

            entity.HasIndex(e => e.AuditCode, "UQ__Inventor__EAFB17CEA5C5ED89").IsUnique();

            entity.Property(e => e.AuditId).HasColumnName("AuditID");
            entity.Property(e => e.AuditCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.AuditDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Đang kiểm kê");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.AuditedByNavigation).WithMany(p => p.InventoryAudits)
                .HasForeignKey(d => d.AuditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryAudit_User");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.InventoryAudits)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryAudit_Warehouse");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Invoice__D796AAD5A9A943D8");

            entity.ToTable("Invoice");

            entity.HasIndex(e => e.ExamSheetId, "IX_Invoice_ExamSheetID");

            entity.HasIndex(e => e.InvoiceCode, "UQ__Invoice__0D9D7FF35ACA6B70").IsUnique();

            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.CashierId).HasColumnName("CashierID");
            entity.Property(e => e.Discount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ExamSheetId).HasColumnName("ExamSheetID");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InvoiceCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IssuedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50)
                .HasDefaultValue("Tiền mặt");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(50)
                .HasDefaultValue("Chưa thanh toán");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Cashier).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.CashierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invoice_Cashier");

            entity.HasOne(d => d.ExamSheet).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.ExamSheetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invoice_ExamSheet");
        });

        modelBuilder.Entity<InvoiceDetail>(entity =>
        {
            entity.HasKey(e => e.InvoiceDetailId).HasName("PK__InvoiceD__1F1578F167D12318");

            entity.ToTable("InvoiceDetail");

            entity.Property(e => e.InvoiceDetailId).HasColumnName("InvoiceDetailID");
            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.ItemName).HasMaxLength(150);
            entity.Property(e => e.ItemType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Quantity).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ReferenceId).HasColumnName("ReferenceID");
            entity.Property(e => e.SubTotal)
                .HasComputedColumnSql("([Quantity]*[UnitPrice])", false)
                .HasColumnType("decimal(29, 4)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceDetails)
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("FK_InvoiceDetail_Invoice");
        });

        modelBuilder.Entity<MedicalRecord>(entity =>
        {
            entity.HasKey(e => e.RecordId).HasName("PK__MedicalR__FBDF78C9F4FA2F15");

            entity.ToTable("MedicalRecord");

            entity.Property(e => e.RecordId).HasColumnName("RecordID");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.InitialDiagnosis).HasMaxLength(500);
            entity.Property(e => e.PatientId).HasColumnName("PatientID");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Đang theo dõi");

            entity.HasOne(d => d.Patient).WithMany(p => p.MedicalRecords)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MedicalRecord_Patient");
        });

        modelBuilder.Entity<MedicineDispense>(entity =>
        {
            entity.HasKey(e => e.DispenseId).HasName("PK__Medicine__26DF670A51035B13");

            entity.ToTable("MedicineDispense");

            entity.HasIndex(e => e.FormulaId, "IX_MedicineDispense_FormulaID");

            entity.HasIndex(e => e.DispenseCode, "UQ__Medicine__126E0A48B9E5C8B4").IsUnique();

            entity.Property(e => e.DispenseId).HasColumnName("DispenseID");
            entity.Property(e => e.DispenseCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DispenseDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.FormulaId).HasColumnName("FormulaID");
            entity.Property(e => e.Note).HasMaxLength(255);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Đã hoàn thành");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.DispensedByNavigation).WithMany(p => p.MedicineDispenses)
                .HasForeignKey(d => d.DispensedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MedicineDispense_User");

            entity.HasOne(d => d.Formula).WithMany(p => p.MedicineDispenses)
                .HasForeignKey(d => d.FormulaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MedicineDispense_Formula");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.MedicineDispenses)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MedicineDispense_Warehouse");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E3279BC1919");

            entity.ToTable("Notification");

            entity.Property(e => e.NotificationId).HasColumnName("NotificationID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Notification_User");
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(e => e.PatientId).HasName("PK__Patient__970EC34686FD9E92");

            entity.ToTable("Patient");

            entity.Property(e => e.PatientId).HasColumnName("PatientID");
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Ethnicity).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.IdentityCard)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.IdentityCardIssuedPlace).HasMaxLength(255);
            entity.Property(e => e.Nationality).HasMaxLength(100);
            entity.Property(e => e.Occupation).HasMaxLength(150);
            entity.Property(e => e.PatientCode)
                .HasMaxLength(22)
                .IsUnicode(false)
                .HasComputedColumnSql("('BN'+CONVERT([varchar](20),[PatientID]))", true);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PhotoUrl).HasMaxLength(500);
            entity.Property(e => e.Province).HasMaxLength(100);
            entity.Property(e => e.Ward).HasMaxLength(100);
            entity.Property(e => e.Workplace).HasMaxLength(255);
        });

        modelBuilder.Entity<PatientContact>(entity =>
        {
            entity.HasKey(e => e.ContactId).HasName("PK__PatientC__5C6625BB39DDE3F1");

            entity.ToTable("PatientContact");

            entity.HasIndex(e => e.PatientId, "IX_PatientContact_PatientID");

            entity.Property(e => e.ContactId).HasColumnName("ContactID");
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.PatientId).HasColumnName("PatientID");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Relationship).HasMaxLength(50);

            entity.HasOne(d => d.Patient).WithMany(p => p.PatientContacts)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PatientContact_Patient");
        });

        modelBuilder.Entity<Podetail>(entity =>
        {
            entity.HasKey(e => e.PodetailId).HasName("PK__PODetail__4EB47B5E22280B4B");

            entity.ToTable("PODetail");

            entity.Property(e => e.PodetailId).HasColumnName("PODetailID");
            entity.Property(e => e.HerbId).HasColumnName("HerbID");
            entity.Property(e => e.LineTotal)
                .HasComputedColumnSql("([OrderQuantity]*[UnitPrice])", false)
                .HasColumnType("decimal(31, 4)");
            entity.Property(e => e.OrderQuantity).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Poid).HasColumnName("POID");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Herb).WithMany(p => p.Podetails)
                .HasForeignKey(d => d.HerbId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PODetail_Herb");

            entity.HasOne(d => d.Po).WithMany(p => p.Podetails)
                .HasForeignKey(d => d.Poid)
                .HasConstraintName("FK_PODetail_PO");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.Poid).HasName("PK__Purchase__5F02A2F47F712579");

            entity.ToTable("PurchaseOrder");

            entity.HasIndex(e => e.Pocode, "UQ__Purchase__40ACF5B85D1CCAD9").IsUnique();

            entity.Property(e => e.Poid).HasColumnName("POID");
            entity.Property(e => e.OrderDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Pocode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("POCode");
            entity.Property(e => e.RequestId).HasColumnName("RequestID");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Mới tạo");
            entity.Property(e => e.SupplierId).HasColumnName("SupplierID");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseOrder_Creator");

            entity.HasOne(d => d.Request).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.RequestId)
                .HasConstraintName("FK_PurchaseOrder_Request");

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseOrder_Supplier");
        });

        modelBuilder.Entity<ReceiptDetail>(entity =>
        {
            entity.HasKey(e => e.ReceiptDetailId).HasName("PK__ReceiptD__82FADEDB2D061BB2");

            entity.ToTable("ReceiptDetail");

            entity.Property(e => e.ReceiptDetailId).HasColumnName("ReceiptDetailID");
            entity.Property(e => e.BatchId).HasColumnName("BatchID");
            entity.Property(e => e.LineTotal)
                .HasComputedColumnSql("([Quantity]*[UnitPrice])", false)
                .HasColumnType("decimal(31, 4)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.ReceiptId).HasColumnName("ReceiptID");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Batch).WithMany(p => p.ReceiptDetails)
                .HasForeignKey(d => d.BatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReceiptDetail_Batch");

            entity.HasOne(d => d.Receipt).WithMany(p => p.ReceiptDetails)
                .HasForeignKey(d => d.ReceiptId)
                .HasConstraintName("FK_ReceiptDetail_Receipt");
        });

        modelBuilder.Entity<ReceiptHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__ReceiptH__4D7B4ADDC11785B3");

            entity.ToTable("ReceiptHistory");

            entity.Property(e => e.HistoryId).HasColumnName("HistoryID");
            entity.Property(e => e.BalanceAfter).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.BatchId).HasColumnName("BatchID");
            entity.Property(e => e.ChangeDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.QuantityChanged).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.ReceiptId).HasColumnName("ReceiptID");

            entity.HasOne(d => d.Batch).WithMany(p => p.ReceiptHistories)
                .HasForeignKey(d => d.BatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReceiptHistory_Batch");

            entity.HasOne(d => d.Receipt).WithMany(p => p.ReceiptHistories)
                .HasForeignKey(d => d.ReceiptId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReceiptHistory_Receipt");
        });

        modelBuilder.Entity<RemedyFormula>(entity =>
        {
            entity.HasKey(e => e.FormulaId).HasName("PK__RemedyFo__2274564543758D58");

            entity.ToTable("RemedyFormula");

            entity.Property(e => e.FormulaId).HasColumnName("FormulaID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DosageDays).HasDefaultValue(1);
            entity.Property(e => e.ExamSheetId).HasColumnName("ExamSheetID");
            entity.Property(e => e.FormulaName).HasMaxLength(150);
            entity.Property(e => e.TotalDoses).HasDefaultValue(1);

            entity.HasOne(d => d.ExamSheet).WithMany(p => p.RemedyFormulas)
                .HasForeignKey(d => d.ExamSheetId)
                .HasConstraintName("FK_RemedyFormula_ExamSheet");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Role__8AFACE3AC2F1042A");

            entity.ToTable("Role");

            entity.HasIndex(e => e.RoleCode, "UQ__Role__D62CB59C5FEC503E").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.RoleCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RoleName).HasMaxLength(100);
        });

        modelBuilder.Entity<Service>(entity =>
        {
            entity.HasKey(e => e.ServiceId).HasName("PK__Service__C51BB0EA828335D1");

            entity.ToTable("Service");

            entity.HasIndex(e => e.ServiceCode, "UQ__Service__A01D74C948D336FE").IsUnique();

            entity.Property(e => e.ServiceId).HasColumnName("ServiceID");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ServiceCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ServiceName).HasMaxLength(150);
            entity.Property(e => e.Unit).HasMaxLength(50);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("PK__Supplier__4BE6669471D9491D");

            entity.ToTable("Supplier");

            entity.HasIndex(e => e.SupplierCode, "UQ__Supplier__44BE981B88DC8699").IsUnique();

            entity.Property(e => e.SupplierId).HasColumnName("SupplierID");
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SupplierCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SupplierName).HasMaxLength(200);
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TreatmentProtocol>(entity =>
        {
            entity.HasKey(e => e.ProtocolId).HasName("PK__Treatmen__C1131837F07CA261");

            entity.ToTable("TreatmentProtocol");

            entity.Property(e => e.ProtocolId).HasColumnName("ProtocolID");
            entity.Property(e => e.DoctorId).HasColumnName("DoctorID");
            entity.Property(e => e.ProtocolName).HasMaxLength(200);
            entity.Property(e => e.RecordId).HasColumnName("RecordID");

            entity.HasOne(d => d.Doctor).WithMany(p => p.TreatmentProtocols)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentProtocol_Doctor");

            entity.HasOne(d => d.Record).WithMany(p => p.TreatmentProtocols)
                .HasForeignKey(d => d.RecordId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentProtocol_Record");
        });

        modelBuilder.Entity<UsageHistory>(entity =>
        {
            entity.HasKey(e => e.UsageId).HasName("PK__UsageHis__29B197C0C7D02719");

            entity.ToTable("UsageHistory");

            entity.Property(e => e.UsageId).HasColumnName("UsageID");
            entity.Property(e => e.ExamSheetId).HasColumnName("ExamSheetID");
            entity.Property(e => e.ExecutionTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.ResultNote).HasMaxLength(500);
            entity.Property(e => e.ServiceId).HasColumnName("ServiceID");

            entity.HasOne(d => d.ExamSheet).WithMany(p => p.UsageHistories)
                .HasForeignKey(d => d.ExamSheetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UsageHistory_ExamSheet");

            entity.HasOne(d => d.PerformedByNavigation).WithMany(p => p.UsageHistories)
                .HasForeignKey(d => d.PerformedBy)
                .HasConstraintName("FK_UsageHistory_Doctor");

            entity.HasOne(d => d.Service).WithMany(p => p.UsageHistories)
                .HasForeignKey(d => d.ServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UsageHistory_Service");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__User__1788CCACBDED65D1");

            entity.ToTable("User");

            entity.HasIndex(e => e.Username, "UQ__User__536C85E4A8CEAE1C").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId }).HasName("PK__UserRole__AF27604FDEF7A3F7");

            entity.ToTable("UserRole");

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK_UserRole_Role");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserRole_User");
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(e => e.WarehouseId).HasName("PK__Warehous__2608AFD98F636906");

            entity.ToTable("Warehouse");

            entity.HasIndex(e => e.WarehouseCode, "UQ__Warehous__1686A0568604EC20").IsUnique();

            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Location).HasMaxLength(255);
            entity.Property(e => e.WarehouseCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.WarehouseName).HasMaxLength(150);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
