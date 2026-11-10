/* Run AFTER assigning the actual rack/bin for every existing Batch through
   PUT /api/v1/warehouse/storage-locations/batches/{batchId}.
   This finalizes the mandatory one-Batch/one-location model. */
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 IF EXISTS(SELECT 1 FROM dbo.Batch WITH(UPDLOCK,HOLDLOCK) WHERE StorageLocationId IS NULL)
   THROW 51020,'Assign actual storage locations for all batches before finalization.',1;
 ALTER TABLE dbo.Batch ALTER COLUMN StorageLocationId int NOT NULL;
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
