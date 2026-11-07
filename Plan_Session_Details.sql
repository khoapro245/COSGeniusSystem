-- Run before deploying the updated API. Planned sessions are prescribing details,
-- separate from actual TreatmentSessions and their scheduling/status lifecycle.
IF COL_LENGTH('dbo.TreatmentPlan', 'SessionDetailsJson') IS NULL
BEGIN
    ALTER TABLE dbo.TreatmentPlan ADD SessionDetailsJson nvarchar(max) NULL;
END;
