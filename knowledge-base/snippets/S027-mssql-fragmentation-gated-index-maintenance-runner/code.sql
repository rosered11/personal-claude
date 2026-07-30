/* ============================================================
   S027 -- Fragmentation-Gated, Logged Index Maintenance Runner
   Replaces TaskIndexRebuild's hardcoded day-of-week branches
   with a config-driven, fragmentation-gated schedule plus a
   RunId-correlated log for audit-trail attribution (P022/D027).
   ============================================================ */

-- 1) One row per table/index pair -- duplicates are now a PK violation,
--    not a silent copy-paste bug (fixes the discovered PK_StoreLocation
--    double-entry on @day=4 and @day=6).
CREATE TABLE dbo.IndexMaintenanceSchedule (
    SchemaName      sysname      NOT NULL,
    TableName       sysname      NOT NULL,
    IndexName       sysname      NOT NULL,
    PreferredDow    tinyint      NULL,        -- 1..7, NULL = any day
    IsActive        bit          NOT NULL DEFAULT (1),
    CONSTRAINT PK_IndexMaintenanceSchedule PRIMARY KEY (SchemaName, TableName, IndexName)
);

-- 2) Append-only per-run log, shaped so it can later be tailed by a
--    SIEM/event pipeline (deferred Lens B) without a redesign.
CREATE TABLE dbo.IndexMaintenanceLog (
    LogId           bigint IDENTITY PRIMARY KEY,
    RunId           uniqueidentifier NOT NULL,
    SchemaName      sysname          NOT NULL,
    TableName       sysname          NOT NULL,
    IndexName       sysname          NOT NULL,
    FragmentPercent decimal(5,2)     NOT NULL,
    StartedAt       datetime2(3)     NOT NULL,
    CompletedAt     datetime2(3)     NULL,
    SessionId       int              NOT NULL,
    Status          varchar(20)      NOT NULL DEFAULT ('Running')
);
CREATE INDEX IX_IndexMaintenanceLog_RunId ON dbo.IndexMaintenanceLog(RunId, StartedAt);
GO

CREATE OR ALTER PROCEDURE dbo.TaskIndexRebuild
    @FragmentationThreshold decimal(5,2) = 10.0,   -- % logical fragmentation
    @MinPageCount           int          = 1000    -- skip trivially small indexes
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RunId     uniqueidentifier = NEWID();
    DECLARE @Today     tinyint          = DATEPART(dw, GETDATE());
    DECLARE @SessionId int              = @@SPID;

    -- Tag this session so the audit trail's session_id can be cross-checked
    -- against this specific run (the verification step identified in P022).
    EXEC sp_set_session_context @key = N'MaintenanceRunId', @value = @RunId;

    DECLARE @SchemaName sysname, @TableName sysname, @IndexName sysname, @Frag decimal(5,2);

    DECLARE candidates CURSOR LOCAL FAST_FORWARD FOR
        SELECT s.SchemaName, s.TableName, s.IndexName, ps.avg_fragmentation_in_percent
        FROM dbo.IndexMaintenanceSchedule AS s
        CROSS APPLY sys.dm_db_index_physical_stats(
                        DB_ID(), OBJECT_ID(QUOTENAME(s.SchemaName) + N'.' + QUOTENAME(s.TableName)),
                        NULL, NULL, 'LIMITED') AS ps
        JOIN sys.indexes AS i
            ON i.object_id = ps.object_id AND i.index_id = ps.index_id AND i.name = s.IndexName
        WHERE s.IsActive = 1
          AND (s.PreferredDow IS NULL OR s.PreferredDow = @Today)
          AND ps.avg_fragmentation_in_percent >= @FragmentationThreshold
          AND ps.page_count >= @MinPageCount;

    OPEN candidates;
    FETCH NEXT FROM candidates INTO @SchemaName, @TableName, @IndexName, @Frag;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @LogId bigint;
        INSERT INTO dbo.IndexMaintenanceLog (RunId, SchemaName, TableName, IndexName, FragmentPercent, StartedAt, SessionId)
        VALUES (@RunId, @SchemaName, @TableName, @IndexName, @Frag, SYSUTCDATETIME(), @SessionId);
        SET @LogId = SCOPE_IDENTITY();

        DECLARE @Sql nvarchar(max) = N'ALTER INDEX ' + QUOTENAME(@IndexName) +
                                      N' ON ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) +
                                      N' REBUILD WITH (ONLINE = ON);';
        BEGIN TRY
            EXEC sp_executesql @Sql;
            UPDATE dbo.IndexMaintenanceLog SET CompletedAt = SYSUTCDATETIME(), Status = 'Completed' WHERE LogId = @LogId;
        END TRY
        BEGIN CATCH
            UPDATE dbo.IndexMaintenanceLog
               SET CompletedAt = SYSUTCDATETIME(), Status = 'Failed: ' + LEFT(ERROR_MESSAGE(), 150)
             WHERE LogId = @LogId;
            -- Intentionally continue to the next index rather than aborting the whole run.
        END CATCH

        FETCH NEXT FROM candidates INTO @SchemaName, @TableName, @IndexName, @Frag;
    END

    CLOSE candidates;
    DEALLOCATE candidates;
END
GO

-- Verification query used to answer the P022 question for any future
-- "suspicious" audit row: was a maintenance run active at that timestamp?
-- SELECT * FROM dbo.IndexMaintenanceLog
-- WHERE '2026-07-22T13:46:32' BETWEEN StartedAt AND ISNULL(CompletedAt, SYSUTCDATETIME())
--   AND SchemaName = 'dbo' AND TableName = 'SubOrderItem';
