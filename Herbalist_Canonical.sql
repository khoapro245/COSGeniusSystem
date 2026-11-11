/* SQL Server: run against the database used by this application, after backup.
   One-time upgrade from repository database.sql (2026-11-10).
   NO stock deduction/replay. NO new goods-receipt/balance tables.
   Existing Batch positions must be assigned using the location API before issue.
   Legacy completed HerbalistRequest data has no actual per-order allocation:
   this script deliberately aborts rather than fabricate allocation history.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 IF OBJECT_ID('dbo.HerbRequisitionPrescription','U') IS NOT NULL
   THROW 51000, 'Canonical migration already applied; do not run twice.', 1;
 IF OBJECT_ID('dbo.HerbRequisition','U') IS NULL
   THROW 51000, 'Apply the repository base database.sql first.', 1;
 DECLARE @legacy nvarchar(max)=N'';
 SELECT @legacy=@legacy+N'IF EXISTS(SELECT 1 FROM dbo.'+QUOTENAME(name)+N') THROW 51001, ''Legacy '+name+N' contains data. Reconcile warehouse, receipt and actual dispense history before dropping it.'',1;'
 FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND name IN ('HerbalistRequest','HerbalistRequestPrescription','HerbalistRequestAllocation');
 EXEC sp_executesql @legacy;

 ALTER TABLE dbo.HerbRequisitionDetail DROP CONSTRAINT FK_HL_HerbRequisitionDetail_Req;
 ALTER TABLE dbo.HerbOutbound DROP CONSTRAINT FK_HL_HerbOutbound_Req;
 ALTER TABLE dbo.HerbOutboundDetail DROP CONSTRAINT FK_HL_HerbOutboundDetail_Header;
 ALTER TABLE dbo.HerbOutboundDetail DROP CONSTRAINT FK_HL_HerbOutboundDetail_ReqLine;
 ALTER TABLE dbo.MedicineDispense DROP CONSTRAINT FK_HL_MedicineDispense_Outbound;
 DROP INDEX UX_HL_OneEffectiveRequisition ON dbo.HerbRequisition;
 ALTER TABLE dbo.HerbRequisition DROP CONSTRAINT CK_HL_HerbRequisition_State, CK_HL_HerbRequisition_Closure;
 ALTER TABLE dbo.HerbRequisition ALTER COLUMN PrescriptionID int NULL;
 ALTER TABLE dbo.HerbOutbound ALTER COLUMN PrescriptionID int NULL;
 ALTER TABLE dbo.HerbOutboundDetail ALTER COLUMN PrescriptionID int NULL;
 ALTER TABLE dbo.HerbOutboundDetail ALTER COLUMN HerbRequisitionDetailID int NULL;
 ALTER TABLE dbo.HerbRequisition ADD ReceivedBy int NULL, ReceivedAt datetime2(3) NULL, IssuedBy int NULL, IssuedAt datetime2(3) NULL;
 EXEC(N'ALTER TABLE dbo.HerbRequisition ADD CONSTRAINT FK_RequisitionReceivedByV2 FOREIGN KEY(ReceivedBy) REFERENCES dbo.[User](UserID);');
 EXEC(N'ALTER TABLE dbo.HerbRequisition ADD CONSTRAINT FK_RequisitionIssuedByV2 FOREIGN KEY(IssuedBy) REFERENCES dbo.[User](UserID);');
 ALTER TABLE dbo.HerbRequisition ADD CONSTRAINT CK_HL_RequisitionStateV2 CHECK(Status IN ('REQUESTED','ISSUED','RECEIVED','DISCREPANCY','COMPLETED','CANCELLED','REJECTED'));
 ALTER TABLE dbo.HerbRequisition ADD CONSTRAINT CK_HL_RequisitionClosureV2 CHECK(
  (Status IN ('CANCELLED','REJECTED') AND ClosedBy IS NOT NULL AND ClosedAt IS NOT NULL AND NULLIF(LTRIM(RTRIM(ClosureReason)),'') IS NOT NULL)
  OR (Status NOT IN ('CANCELLED','REJECTED') AND ClosedBy IS NULL AND ClosedAt IS NULL AND ClosureReason IS NULL));
 CREATE TABLE dbo.HerbRequisitionPrescription (
  Id int IDENTITY PRIMARY KEY, RequestId int NOT NULL REFERENCES dbo.HerbRequisition(HerbRequisitionID),
  PrescriptionId int NOT NULL REFERENCES dbo.HerbalPrescription(PrescriptionID), IsActive bit NOT NULL DEFAULT 1,
  CompletedBy int NULL REFERENCES dbo.[User](UserID), CompletedAt datetime2(3) NULL,
  CONSTRAINT UQ_HerbRequisitionPrescription UNIQUE(RequestId,PrescriptionId)
 );
 CREATE UNIQUE INDEX UX_HerbRequisitionPrescription_Active ON dbo.HerbRequisitionPrescription(PrescriptionId) WHERE IsActive=1;
 INSERT dbo.HerbRequisitionPrescription(RequestId,PrescriptionId,IsActive)
 SELECT HerbRequisitionID,PrescriptionID,CASE WHEN Status IN ('CANCELLED','REJECTED') THEN 0 ELSE 1 END FROM dbo.HerbRequisition WHERE PrescriptionID IS NOT NULL;
 ALTER TABLE dbo.HerbRequisitionDetail ADD CONSTRAINT FK_HL_ReqLinePrescriptionV2 FOREIGN KEY(HerbRequisitionID,PrescriptionID) REFERENCES dbo.HerbRequisitionPrescription(RequestId,PrescriptionId);
 CREATE UNIQUE INDEX UX_HL_ReqWarehouseV2 ON dbo.HerbRequisition(HerbRequisitionID,WarehouseID);
 ALTER TABLE dbo.HerbOutbound ADD CONSTRAINT FK_HL_OutboundReqV2 FOREIGN KEY(HerbRequisitionID,WarehouseID) REFERENCES dbo.HerbRequisition(HerbRequisitionID,WarehouseID);
 CREATE UNIQUE INDEX UX_HL_OutboundScopeV2 ON dbo.HerbOutbound(HerbOutboundID,HerbRequisitionID,WarehouseID);
 ALTER TABLE dbo.HerbOutboundDetail ADD CONSTRAINT FK_HL_OutboundHeaderV2 FOREIGN KEY(HerbOutboundID,HerbRequisitionID,WarehouseID) REFERENCES dbo.HerbOutbound(HerbOutboundID,HerbRequisitionID,WarehouseID);
 ALTER TABLE dbo.HerbOutboundDetail ADD CONSTRAINT FK_HL_OutboundRequestV2 FOREIGN KEY(HerbRequisitionID) REFERENCES dbo.HerbRequisition(HerbRequisitionID);
 ALTER TABLE dbo.MedicineDispense ADD CONSTRAINT FK_HL_DispenseOutboundV2 FOREIGN KEY(HerbOutboundID) REFERENCES dbo.HerbOutbound(HerbOutboundID);
 CREATE TABLE dbo.WarehouseStorageLocation (
  Id int IDENTITY PRIMARY KEY, WarehouseId int NOT NULL REFERENCES dbo.Warehouse(WarehouseID),
  Code nvarchar(100) NOT NULL, IsActive bit NOT NULL DEFAULT 1,
  CONSTRAINT UQ_WarehouseStorageLocation UNIQUE(WarehouseId,Code),
  CONSTRAINT UQ_WarehouseStorageLocationScope UNIQUE(Id,WarehouseId),
  CONSTRAINT CK_WarehouseStorageLocationCode CHECK(LEN(LTRIM(RTRIM(Code)))>0)
 );
 ALTER TABLE dbo.Batch ADD StorageLocationId int NULL;
 -- NULL is only for pre-upgrade batches whose physical position is not yet known.
 -- No stock balance is stored on a location. One Batch -> one location.
 EXEC(N'ALTER TABLE dbo.Batch ADD CONSTRAINT FK_BatchStorageLocation FOREIGN KEY(StorageLocationId,WarehouseID) REFERENCES dbo.WarehouseStorageLocation(Id,WarehouseId);');
 ALTER TABLE dbo.Batch ALTER COLUMN CurrentQuantity decimal(15,5) NOT NULL;
 ALTER TABLE dbo.Batch ALTER COLUMN OriginalQuantity decimal(15,5) NOT NULL;
 ALTER TABLE dbo.Batch ADD CONSTRAINT CK_BatchNonnegativeV2 CHECK(CurrentQuantity>=0);
 ALTER TABLE dbo.HerbOutboundDetail ADD ReceivedQuantity decimal(13,3) NULL, ReceivedBy int NULL REFERENCES dbo.[User](UserID), ReceivedAt datetime2(3) NULL,
  StorageLocationId int NULL, LocationSnapshot nvarchar(100) NULL;
 EXEC(N'ALTER TABLE dbo.HerbOutboundDetail ADD CONSTRAINT CK_ReceivedQuantityV2 CHECK(ReceivedQuantity IS NULL OR ReceivedQuantity>=0);');
 EXEC(N'ALTER TABLE dbo.HerbOutboundDetail ADD CONSTRAINT FK_OutboundStorageLocation FOREIGN KEY(StorageLocationId,WarehouseID) REFERENCES dbo.WarehouseStorageLocation(Id,WarehouseId);');
 ALTER TABLE dbo.DispenseDetail ALTER COLUMN QuantityTaken decimal(18,3) NOT NULL;
 -- Do not reinterpret historical workflow units. Abort if source units are ambiguous.
 IF EXISTS(SELECT 1 FROM dbo.HerbRequisitionDetail WHERE UnitSnapshot NOT IN ('g',N'gam'))
   THROW 51002, 'Existing requisitions use units other than grams. Reconcile quantities before migration.',1;
 -- Historical received quantities are not guessed; backfill only from signed discrepancy records.
 EXEC(N'UPDATE d SET ReceivedQuantity=x.ReceivedQuantity,ReceivedBy=r.ReportedBy,ReceivedAt=r.ReportedAt
 FROM dbo.HerbOutboundDetail d JOIN dbo.HerbReceiptDiscrepancyDetail x ON x.HerbOutboundDetailID=d.HerbOutboundDetailID
 JOIN dbo.HerbReceiptDiscrepancy r ON r.HerbReceiptDiscrepancyID=x.HerbReceiptDiscrepancyID;');
 EXEC(N'UPDATE q SET IssuedBy=o.IssuedBy,IssuedAt=o.IssuedAt FROM dbo.HerbRequisition q JOIN dbo.HerbOutbound o ON o.HerbRequisitionID=q.HerbRequisitionID;');
 -- The existing default label denotes already-completed legacy dispense records.
 UPDATE dbo.MedicineDispense SET Status=N'COMPLETED' WHERE Status=N'Đã hoàn thành';
 -- Empty duplicate tables only; guard above prevents erasing unconverted records.
 DROP TABLE IF EXISTS dbo.HerbalistRequestAllocation;
 DROP TABLE IF EXISTS dbo.HerbalistRequestPrescription;
 DROP TABLE IF EXISTS dbo.HerbalistRequest;
 CREATE TABLE dbo.HerbOutboundDemand (
  Id int IDENTITY PRIMARY KEY,
  OutboundDetailId int NOT NULL REFERENCES dbo.HerbOutboundDetail(HerbOutboundDetailID),
  RequisitionDetailId int NOT NULL REFERENCES dbo.HerbRequisitionDetail(HerbRequisitionDetailID),
  QuantityGrams decimal(13,3) NOT NULL CHECK(QuantityGrams>0),
  CONSTRAINT UQ_HerbOutboundDemand UNIQUE(OutboundDetailId,RequisitionDetailId)
 );
 INSERT dbo.HerbOutboundDemand(OutboundDetailId,RequisitionDetailId,QuantityGrams)
 SELECT HerbOutboundDetailID,HerbRequisitionDetailID,IssuedQuantity FROM dbo.HerbOutboundDetail WHERE HerbRequisitionDetailID IS NOT NULL;
 UPDATE l SET CompletedBy=d.DispensedBy,CompletedAt=d.DispenseDate
 FROM dbo.HerbRequisitionPrescription l JOIN dbo.MedicineDispense d ON d.PrescriptionID=l.PrescriptionId
 WHERE d.Status=N'COMPLETED';
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_InvoiceDetail_CompletedMedicine
ON dbo.InvoiceDetail AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.PrescriptionID IS NOT NULL AND NOT EXISTS(
  SELECT 1 FROM dbo.MedicineDispense d WHERE d.PrescriptionID=i.PrescriptionID AND d.Status=N''COMPLETED''))
 THROW 51010,''Prescription must be dispensed completely before medication billing.'',1;
END;');

 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_Invoice_PaymentCompletedMedicine
ON dbo.Invoice AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.InvoiceDetail l ON l.InvoiceID=i.InvoiceID
 WHERE l.PrescriptionID IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.MedicineDispense d WHERE d.PrescriptionID=l.PrescriptionID AND d.Status=N''COMPLETED''))
 THROW 51010,''Prescription must be dispensed completely before medication payment.'',1;
END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_DispenseDetail_ReceiptBudget
ON dbo.DispenseDetail AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 -- Lock source rows in the transaction before reading aggregate allocations.
 DECLARE @locked int;
 SELECT @locked=COUNT(*) FROM dbo.HerbOutboundDetail o WITH(UPDLOCK,HOLDLOCK)
 WHERE o.HerbOutboundDetailID IN(SELECT HerbOutboundDetailID FROM inserted WHERE HerbOutboundDetailID IS NOT NULL);
 IF EXISTS(
  SELECT 1 FROM inserted i JOIN dbo.MedicineDispense m ON m.DispenseID=i.DispenseID
  JOIN dbo.HerbOutboundDetail o ON o.HerbOutboundDetailID=i.HerbOutboundDetailID
  WHERE i.QuantityTaken<=0 OR i.BatchID<>o.BatchID OR i.HerbID<>o.HerbID
  OR m.WarehouseID<>o.WarehouseID OR m.HerbOutboundID IS NULL OR m.HerbOutboundID<>o.HerbOutboundID
  OR NOT EXISTS(SELECT 1 FROM dbo.HerbRequisitionPrescription p WHERE p.RequestId=o.HerbRequisitionID AND p.PrescriptionId=m.PrescriptionID AND p.IsActive=1)
 ) THROW 51011,''Dispense source must belong to the same effective requisition, herb, warehouse and outbound.'',1;
 IF EXISTS(
  SELECT 1 FROM dbo.HerbOutboundDetail o WHERE o.HerbOutboundDetailID IN(SELECT HerbOutboundDetailID FROM inserted)
  AND (SELECT SUM(d.QuantityTaken) FROM dbo.DispenseDetail d WITH(HOLDLOCK) WHERE d.HerbOutboundDetailID=o.HerbOutboundDetailID)
  > CASE WHEN o.ReceivedQuantity IS NULL THEN 0 WHEN o.ReceivedQuantity<o.IssuedQuantity THEN o.ReceivedQuantity ELSE o.IssuedQuantity END
 ) THROW 51012,''Combined dispense allocations exceed valid received quantity.'',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.MedicineDispense m ON m.DispenseID=i.DispenseID WHERE m.HerbOutboundID IS NOT NULL AND i.HerbOutboundDetailID IS NULL)
 THROW 51013,''Canonical dispense details require an outbound source.'',1;
END;');
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;



