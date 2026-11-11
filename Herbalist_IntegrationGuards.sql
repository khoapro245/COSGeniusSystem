/* Follow-up for an EXISTING canonical schema. Does not replay Canonical.sql.
   Review enabled triggers and reconcile historical discrepant dispenses first.
   No inventory update. Run only after backup and review, never automatically. */
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 IF OBJECT_ID('dbo.HerbRequisitionPrescription','U') IS NULL
  THROW 51020, 'Canonical multi-prescription schema is required.', 1;
 IF EXISTS(SELECT 1 FROM dbo.DispenseDetail d JOIN dbo.HerbOutboundDetail o
 ON o.HerbOutboundDetailID=d.HerbOutboundDetailID
 WHERE o.ReceivedQuantity IS NULL OR o.ReceivedQuantity<>o.IssuedQuantity)
  THROW 51021, 'Historical allocations use discrepant receipts; reconcile before applying.', 1;
 ALTER TABLE dbo.DispenseDetail ALTER COLUMN QuantityTaken decimal(18,3) NOT NULL;
 ALTER TABLE dbo.Batch ALTER COLUMN CurrentQuantity decimal(18,6) NOT NULL;
 ALTER TABLE dbo.Batch ALTER COLUMN OriginalQuantity decimal(18,6) NOT NULL;
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
  > CASE WHEN o.ReceivedQuantity=o.IssuedQuantity THEN o.IssuedQuantity ELSE 0 END
 ) THROW 51012,''Combined dispense allocations exceed valid received quantity.'',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.MedicineDispense m ON m.DispenseID=i.DispenseID WHERE m.HerbOutboundID IS NOT NULL AND i.HerbOutboundDetailID IS NULL)
 THROW 51013,''Canonical dispense details require an outbound source.'',1;
END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_Herb_StockUnitImmutable
 ON dbo.Herb AFTER UPDATE AS
 BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.HerbID=i.HerbID
 WHERE i.Unit<>d.Unit AND EXISTS(SELECT 1 FROM dbo.Batch b WITH(UPDLOCK,HOLDLOCK) WHERE b.HerbID=i.HerbID))
 THROW 51022,''Cannot reinterpret existing batch quantities by changing the herb stock unit.'',1;
 END;');
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
