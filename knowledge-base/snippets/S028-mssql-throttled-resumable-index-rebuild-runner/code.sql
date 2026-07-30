/* ============================================================
   S028 -- Consolidated Fragmentation-Gated, Logged, Throttled,
   Resumable Index Rebuild Runner (extends & completes S027)

   Deploys the FULL D027 + D028 design in one pass, confirmed
   necessary because the real production TaskIndexRebuild script
   (grounded 2026-07-29, inbox/rebuild-index-db/script-rebuild.sql)
   still has none of it applied. Populates the schedule table with
   the actual 194 unique (table, index) candidates extracted
   programmatically from that real script (195 total ALTER INDEX
   statements, 1 confirmed duplicate: StoreLocation.PK_StoreLocation
   on the old @day=4 and @day=6 branches -- eliminated here by the
   schedule table's own primary key).
   ============================================================ */

-- 1) Config-driven schedule: one row per unique table/index pair.
--    A duplicate entry is now a PK violation, not a silent bug.
IF OBJECT_ID('dbo.IndexMaintenanceSchedule', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.IndexMaintenanceSchedule (
        SchemaName      sysname      NOT NULL,
        TableName       sysname      NOT NULL,
        IndexName       sysname      NOT NULL,
        PreferredDow    tinyint      NULL,        -- 1..7, NULL = any day
        IsActive        bit          NOT NULL DEFAULT (1),
        CONSTRAINT PK_IndexMaintenanceSchedule PRIMARY KEY (SchemaName, TableName, IndexName)
    );
END
GO

-- 2) Append-only per-run log, RunId-correlated (answers the P022
--    audit-attribution question) and shaped for future SIEM tailing.
IF OBJECT_ID('dbo.IndexMaintenanceLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.IndexMaintenanceLog (
        LogId               bigint IDENTITY PRIMARY KEY,
        RunId               uniqueidentifier NOT NULL,
        SchemaName          sysname          NOT NULL,
        TableName           sysname          NOT NULL,
        IndexName           sysname          NOT NULL,
        FragmentPercent     decimal(5,2)     NOT NULL,
        StartedAt           datetime2(3)     NOT NULL,
        CompletedAt         datetime2(3)     NULL,
        SessionId           int              NOT NULL,
        Status              varchar(20)      NOT NULL DEFAULT ('Running'),
        AbortedByLowPriority bit             NOT NULL DEFAULT (0),
        WasResumable         bit             NOT NULL DEFAULT (0)
    );
    CREATE INDEX IX_IndexMaintenanceLog_RunId ON dbo.IndexMaintenanceLog(RunId, StartedAt);
END
GO

-- 3) Populate the schedule with the VERIFIED real index inventory,
--    extracted programmatically from inbox/rebuild-index-db/script-rebuild.sql.
--    195 total ALTER INDEX statements in source; 194 unique (table,index)
--    pairs after removing the confirmed PK_StoreLocation duplicate.
--    Safe to re-run: skipped if already populated.
IF NOT EXISTS (SELECT 1 FROM dbo.IndexMaintenanceSchedule)
BEGIN
INSERT INTO dbo.IndexMaintenanceSchedule (SchemaName, TableName, IndexName, PreferredDow, IsActive) VALUES
(N'dbo', N'OrderPromotion', N'Idx_SourceOrderId', NULL, 1),
(N'dbo', N'PackageTb', N'Idx_SourceOrderId_SourceSubOrderId', NULL, 1),
(N'dbo', N'SubOrder', N'idx_SourceOrderId_SourceSubOrderId', NULL, 1),
(N'dbo', N'ProcessOrder', N'idx_SourceOrderId_SourceSubOrderId_Status', NULL, 1),
(N'dbo', N'PackageInfo', N'Idx_SourceOrderId_SourceSubOrderId', NULL, 1),
(N'dbo', N'OrderPromotion', N'IX_OrderPromotion_AmountId', NULL, 1),
(N'dbo', N'OrderItemTax', N'IX_OrderItemTax_OrderItemPaymentModelId', NULL, 1),
(N'dbo', N'SubOrderFeeTax', N'IX_SubOrderFeeTax_SubOrderFeePaymentModelId', NULL, 1),
(N'dbo', N'ProcessOrder', N'idx_Status_CreatedDate', NULL, 1),
(N'dbo', N'SubOrderItemTax', N'IX_SubOrderItemTax_SubOrderItemPayModelId', NULL, 1),
(N'dbo', N'ProcessOrder', N'PK_ProcessOrder', NULL, 1),
(N'dbo', N'SubOrderPromotion', N'PK_SubOrderPromotion', NULL, 1),
(N'dbo', N'SubOrderItemPay', N'IX_SubOrderItemPay_SubOrderItemModelId', NULL, 1),
(N'dbo', N'OrderItemPromotionsTb', N'PK_OrderItemPromotionsTb', NULL, 1),
(N'dbo', N'SubOrderFeeAmount', N'IX_SubOrderFeeAmount_PaidId', NULL, 1),
(N'dbo', N'SubOrderItemAmt', N'PK_SubOrderItemAmt', NULL, 1),
(N'dbo', N'OrderRemark', N'PK_OrderRemark', NULL, 1),
(N'dbo', N'OrderItemAmoutDetail', N'PK_OrderItemAmoutDetail', NULL, 1),
(N'dbo', N'SubOrderItemTax', N'PK_SubOrderItemTax', NULL, 1),
(N'dbo', N'OrderItem', N'idx_OrderNumber', NULL, 1),
(N'dbo', N'SubOrderAddress', N'IX_SubOrderAddress_SubOrderModelId', NULL, 1),
(N'dbo', N'OrderFeePayment', N'IX_OrderFeePayment_OrderFeeModelId', NULL, 1),
(N'dbo', N'BackgroudSeviceSetting', N'PK_BackgroudSeviceSetting', NULL, 1),
(N'dbo', N'SysRunningNumberTypes', N'PK_SysRunningNumberTypes', NULL, 1),
(N'dbo', N'SysFulfillmentTypeTb', N'PK_SysFulfillmentTypeTb', NULL, 1),
(N'dbo', N'SysOrderFulfillmentTypeTb', N'PK_SysOrderFulfillmentTypeTb', NULL, 1),
(N'dbo', N'SysChannelTb', N'PK_SysChannelTb', NULL, 1),
(N'dbo', N'SysDeliverySubTypeTb', N'PK_SysDeliverySubTypeTb', NULL, 1),
(N'dbo', N'SubOrderFeeModel', N'nci_wi_SubOrderFeeModel_2D91AB9181C7C9095966BDA8CC319325', NULL, 1),
(N'dbo', N'SubOrder', N'idx_SourceSubOrderId', NULL, 1),
(N'dbo', N'SubOrder', N'idx_SourceSubOrderId_OrderDate', NULL, 1),
(N'dbo', N'SubOrderItem', N'idx_OrderNumber_SubOrderNumber', NULL, 1),
(N'dbo', N'OrderReference', N'Idx_OrderReference_RefSourceOrderId', NULL, 1),
(N'dbo', N'AllowedStatusSetting', N'idx_FromStatus_ToStatus_Bu_Channel', NULL, 1),
(N'dbo', N'OrderRemark', N'IX_OrderRemark_OrderModelId', NULL, 1),
(N'dbo', N'ItemOtherInfo', N'PK_ItemOtherInfo', NULL, 1),
(N'dbo', N'SubOrderPromotionAmount', N'PK_SubOrderPromotionAmount', NULL, 1),
(N'dbo', N'Order', N'PK_Order', NULL, 1),
(N'dbo', N'SubOrderFeeTax', N'PK_SubOrderFeeTax', NULL, 1),
(N'dbo', N'SubOrderItemAmt', N'IX_SubOrderItemAmt_NormalId', NULL, 1),
(N'dbo', N'SubOrderItemDeliveryWindow', N'IX_SubOrderItemDeliveryWindow_SubOrderItemFulFillmentModelId', NULL, 1),
(N'dbo', N'OrderItemDeliveryWindow', N'IX_OrderItemDeliveryWindow_OrderItemFulFillmentModelId', NULL, 1),
(N'dbo', N'SubOrderPromotion', N'IX_SubOrderPromotion_AmountId', NULL, 1),
(N'dbo', N'OrderFeeAmountDtl', N'PK_OrderFeeAmountDtl', NULL, 1),
(N'dbo', N'SubOrderFeeModel', N'IX_SubOrderFeeModel_AmountId', NULL, 1),
(N'dbo', N'OrderItemAmout', N'IX_OrderItemAmout_RetailPriceId', NULL, 1),
(N'dbo', N'OrderFeeAmount', N'IX_OrderFeeAmount_PaidId', NULL, 1),
(N'dbo', N'SuOrderRemark', N'PK_SuOrderRemark', NULL, 1),
(N'dbo', N'SubOrderItemRemark', N'PK_SubOrderItemRemark', NULL, 1),
(N'dbo', N'__EFMigrationsHistory', N'PK___EFMigrationsHistory', NULL, 1),
(N'dbo', N'SysRunningNumbers', N'PK_SysRunningNumbers', NULL, 1),
(N'dbo', N'SysStatusTb', N'PK_SysStatusTb', NULL, 1),
(N'dbo', N'BuTbl', N'PK_BuTbl', NULL, 1),
(N'dbo', N'SubOrderItemFulFillmentServiceType', N'IX_SubOrderItemFulFillmentServiceType_SubOrderItemFulFillmentModelId', NULL, 1),
(N'dbo', N'SysDeliveryTypeTb', N'PK_SysDeliveryTypeTb', NULL, 1),
(N'dbo', N'fm_log_incre', N'Idx_sourceOrderId', NULL, 1),
(N'dbo', N'PackageTb', N'PK_PackageTb', NULL, 1),
(N'dbo', N'SubOrderItem', N'idx_SourceItemId_SourceItemNumber_Barcode', NULL, 1),
(N'dbo', N'OrderPayment', N'idx_PaymentDate_SourcePaymentId', NULL, 1),
(N'dbo', N'SubOrderItem', N'idx_SourceOrderId_SourceSubOrderId_SourceItemId_SourceItemNumber', NULL, 1),
(N'dbo', N'OrderAddress', N'idx_SourceOrderId_AddrType', NULL, 1),
(N'dbo', N'SubOrderItem', N'idx_TrackNo', NULL, 1),
(N'dbo', N'SubOrderItemPromotionsTb', N'IX_SubOrderItemPromotionsTb_SubOrderItemModelId', NULL, 1),
(N'dbo', N'OrderItemTax', N'IX_OrderItemTax_OrderItemAmountDtlModelId', NULL, 1),
(N'dbo', N'OrderItem', N'idx_SourceOrderId_Barcode', NULL, 1),
(N'dbo', N'ProcessOrder', N'Idx_ProcessOrder_OrderId', NULL, 1),
(N'dbo', N'SubOrderFeeAmountDtl', N'PK_SubOrderFeeAmountDtl', NULL, 1),
(N'dbo', N'SubOrderFeeModel', N'PK_SubOrderFeeModel', NULL, 1),
(N'dbo', N'SubOrderItemAmt', N'IX_SubOrderItemAmt_PaidId', NULL, 1),
(N'dbo', N'OrderFeeTax', N'IX_OrderFeeTax_OrderFeePaymentModelId', NULL, 1),
(N'dbo', N'SubOrder', N'idx_SubOrderNumber', NULL, 1),
(N'dbo', N'SubOrderFeeAmount', N'IX_SubOrderFeeAmount_NormalId', NULL, 1),
(N'dbo', N'SubOrderItemPromotionsTb', N'PK_SubOrderItemPromotionsTb', NULL, 1),
(N'dbo', N'SubOrderItem', N'PK_SubOrderItem', NULL, 1),
(N'dbo', N'OrderCustomer', N'PK_OrderCustomer', NULL, 1),
(N'dbo', N'OrderPayment', N'PK_OrderPayment', NULL, 1),
(N'dbo', N'OrderItemAmout', N'IX_OrderItemAmout_PaidId', NULL, 1),
(N'dbo', N'OrderPayment', N'IX_OrderPayment_OrderModelId', NULL, 1),
(N'dbo', N'StoreLocation', N'idx_SourceBu_SourceLoc', NULL, 1),
(N'dbo', N'SysRunningNumbers', N'IX_SysRunningNumbers_RunningNumberTypeId', NULL, 1),
(N'dbo', N'PromotionItemConditionTb', N'PK_PromotionItemConditionTb', NULL, 1),
(N'dbo', N'DeliveryWindow', N'PK_DeliveryWindow', NULL, 1),
(N'dbo', N'SubOrderItemFulFillmentServiceType', N'PK_SubOrderItemFulFillmentServiceType', NULL, 1),
(N'dbo', N'SysFulfillmentOptionTb', N'PK_SysFulfillmentOptionTb', NULL, 1),
(N'dbo', N'OrderStaging', N'idx_SourceOrderId_SourceSubOrderId', NULL, 1),
(N'dbo', N'OrderPromotionAmount', N'PK_OrderPromotionAmount', NULL, 1),
(N'dbo', N'OrderStaging', N'PK_OrderStaging', NULL, 1),
(N'dbo', N'Order', N'idx_SourceOrderId_OrderDate', NULL, 1),
(N'dbo', N'OrderPayment', N'nci_wi_OrderPayment_06697C222BEEF07ACC2C24AC875C4A14', NULL, 1),
(N'dbo', N'SubOrderAddress', N'idx_SourceOrderId_SourceSubOrderId_AddrType', NULL, 1),
(N'dbo', N'OrderPromotion', N'IX_OrderPromotion_OrderModelId', NULL, 1),
(N'dbo', N'SubOrderItem', N'idx_SourceOrderId', NULL, 1),
(N'dbo', N'OrderItem', N'idx_SourceOrderId', NULL, 1),
(N'dbo', N'OrderItem', N'idx_SourceOrderId_SourceItemId_SourceItemNumber', NULL, 1),
(N'dbo', N'SubOrderFeeAmount', N'PK_SubOrderFeeAmount', NULL, 1),
(N'dbo', N'SubOrderFeePayment', N'PK_SubOrderFeePayment', NULL, 1),
(N'dbo', N'SubOrder', N'idx_OrderNumber', NULL, 1),
(N'dbo', N'OrderCustomer', N'IX_OrderCustomer_OrderModelId', NULL, 1),
(N'dbo', N'OrderFeeTax', N'IX_OrderFeeTax_OrderFeeAmountDtlModelId', NULL, 1),
(N'dbo', N'OrderItemPayment', N'PK_OrderItemPayment', NULL, 1),
(N'dbo', N'Order', N'idx_OrderNumber', NULL, 1),
(N'dbo', N'OrderFeePayment', N'PK_OrderFeePayment', NULL, 1),
(N'dbo', N'OrderItem', N'idx_ItemLineNumber', NULL, 1),
(N'dbo', N'OrderFeeTax', N'PK_OrderFeeTax', NULL, 1),
(N'dbo', N'OrderItemRemark', N'PK_OrderItemRemark', NULL, 1),
(N'dbo', N'OrderFeeModel', N'IX_OrderFeeModel_AmountId', NULL, 1),
(N'dbo', N'OrderItemDeliveryWindow', N'PK_OrderItemDeliveryWindow', NULL, 1),
(N'dbo', N'StoreLocation', N'PK_StoreLocation', NULL, 1),
(N'dbo', N'Functions', N'PK_Functions', NULL, 1),
(N'dbo', N'ProcessOrderItem', N'PK_ProcessOrderItem', NULL, 1),
(N'dbo', N'SubOrderItemPromotion', N'PK_SubOrderItemPromotion', NULL, 1),
(N'dbo', N'SysFulfillmentPriorityTb', N'PK_SysFulfillmentPriorityTb', NULL, 1),
(N'dbo', N'OrderPromotion', N'PK_OrderPromotion', NULL, 1),
(N'dbo', N'OrderFeeModel', N'Idx_SourceOrderId', NULL, 1),
(N'dbo', N'SubOrder', N'idx_SourceOrderId_SourceSubOrderId_Status', NULL, 1),
(N'dbo', N'SubOrderItem', N'idx_SourceOrderId_SourceSubOrderId_SourceItemId_SourceItemNumber_FulFillmentId', NULL, 1),
(N'dbo', N'SubOrderItem', N'idx_SourceSubOrderId', NULL, 1),
(N'dbo', N'OrderItem', N'idx_SourceItemId_SourceItemNumber_Barcode', NULL, 1),
(N'dbo', N'SubOrderItemFulFillment', N'Idx_SourceBU_SourceLoc', NULL, 1),
(N'dbo', N'SubOrderItem', N'IX_SubOrderItem_SubOrderModelId', NULL, 1),
(N'dbo', N'OrderItemRemark', N'IX_OrderItemRemark_OrderItemModelId', NULL, 1),
(N'dbo', N'SubOrderItemTax', N'IX_SubOrderItemTax_SubOrderItemAmtDtlModelId', NULL, 1),
(N'dbo', N'SubOrderItem', N'IX_SubOrderItem_AmountId', NULL, 1),
(N'dbo', N'SubOrder', N'idx_OrderNumber_SubOrderNumber', NULL, 1),
(N'dbo', N'SuOrderRemark', N'IX_SuOrderRemark_SubOrderModelId', NULL, 1),
(N'dbo', N'SubOrderItem', N'IX_SubOrderItem_PromotionId', NULL, 1),
(N'dbo', N'OrderFeeAmount', N'PK_OrderFeeAmount', NULL, 1),
(N'dbo', N'Order', N'Idx_CreatedDate', NULL, 1),
(N'dbo', N'SubOrderItemAmtDtl', N'PK_SubOrderItemAmtDtl', NULL, 1),
(N'dbo', N'OrderFeeModel', N'IX_OrderFeeModel_OrderModelId', NULL, 1),
(N'dbo', N'OrderItemTax', N'PK_OrderItemTax', NULL, 1),
(N'dbo', N'OrderItem', N'IX_OrderItem_PromotionId', NULL, 1),
(N'dbo', N'OrderItemAmout', N'IX_OrderItemAmout_NormalId', NULL, 1),
(N'dbo', N'OrderItemAmout', N'PK_OrderItemAmout', NULL, 1),
(N'dbo', N'StateProcessMonitor', N'PK_StateProcessMonitor', NULL, 1),
(N'dbo', N'SysFunctionConfigTb', N'PK_SysFunctionConfigTb', NULL, 1),
(N'dbo', N'OrderItemPromotion', N'PK_OrderItemPromotion', NULL, 1),
(N'dbo', N'HttpLogs', N'Idx_EventName_SubEventName_SourceOrderId_SourceSubOrderId', NULL, 1),
(N'dbo', N'PromotionItemTb', N'idx_SourceOrderId_PromotionId_Barcode', NULL, 1),
(N'dbo', N'OrderItemFulFillmentServiceType', N'PK_OrderItemFulFillmentServiceType', NULL, 1),
(N'dbo', N'fm_log_incre', N'Idx_syncFlag', NULL, 1),
(N'dbo', N'SubOrderItem', N'idx_SourceOrderId_SourceSubOrderId_Barcode', NULL, 1),
(N'dbo', N'Order', N'Idx_SourceOrderId_Bu', NULL, 1),
(N'dbo', N'SubOrderItem', N'idx_SourceOrderId_SourceSubOrderId', NULL, 1),
(N'dbo', N'SubOrderItem', N'Idx_Status', NULL, 1),
(N'dbo', N'SubOrderItem', N'idx_FulFillmentId_Brand_Cat2', NULL, 1),
(N'dbo', N'OrderItemPromotionsTb', N'IX_OrderItemPromotionsTb_OrderItemModelId', NULL, 1),
(N'dbo', N'PackageInfo', N'PK_PackageInfo', NULL, 1),
(N'dbo', N'SubOrderItem', N'idx_ItemLineNumber', NULL, 1),
(N'dbo', N'SubOrder', N'PK_SubOrder', NULL, 1),
(N'dbo', N'SubOrderItem', N'IX_SubOrderItem_FulFillmentId', NULL, 1),
(N'dbo', N'fm_log_incre', N'Idx_triggerDate', NULL, 1),
(N'dbo', N'SubOrderPromotion', N'IX_SubOrderPromotion_SubOrderModelId', NULL, 1),
(N'dbo', N'OrderPaymentTransaction', N'IX_OrderPaymentTransaction_OrderPaymentModelId', NULL, 1),
(N'dbo', N'OrderItem', N'idx_TrackNo', NULL, 1),
(N'dbo', N'SubOrderFeePayment', N'IX_SubOrderFeePayment_SubOrderFeeModelId', NULL, 1),
(N'dbo', N'SubOrderItemFulFillment', N'PK_SubOrderItemFulFillment', NULL, 1),
(N'dbo', N'OrderFeeModel', N'PK_OrderFeeModel', NULL, 1),
(N'dbo', N'OrderItem', N'IX_OrderItem_OrderModelId', NULL, 1),
(N'dbo', N'OrderFeeAmount', N'IX_OrderFeeAmount_NormalId', NULL, 1),
(N'dbo', N'OrderAddress', N'IX_OrderAddress_OrderModelId', NULL, 1),
(N'dbo', N'SubOrderAddress', N'PK_SubOrderAddress', NULL, 1),
(N'dbo', N'SubOrderItemBarcode', N'PK_SubOrderItemBarcode', NULL, 1),
(N'dbo', N'SysFunctionTb', N'PK_SysFunctionTb', NULL, 1),
(N'dbo', N'OrderItemBarcode', N'PK_OrderItemBarcode', NULL, 1),
(N'dbo', N'PromotionItemTb', N'idx_SourceOrderId_SourceSubOrderId', NULL, 1),
(N'dbo', N'OrderItemFulFillmentServiceType', N'IX_OrderItemFulFillmentServiceType_OrderItemFulFillmentModelId', NULL, 1),
(N'dbo', N'OrderStaging', N'idx_SourceOrderId', NULL, 1),
(N'dbo', N'ItemOtherInfo', N'Idx_SourceItemId_SourceItemNumber', NULL, 1),
(N'dbo', N'SubOrder', N'idx_SourceOrderId', NULL, 1),
(N'dbo', N'Order', N'idx_SourceOrderId', NULL, 1),
(N'dbo', N'AllowedStatusSetting', N'Idx_Bu_Channel', NULL, 1),
(N'dbo', N'SubOrderItem', N'idx_SourceOrderId_SourceSubOrderId_FulFillmentId', NULL, 1),
(N'dbo', N'OrderItem', N'idx_Barcode', NULL, 1),
(N'dbo', N'AllowedStatusSetting', N'PK_AllowedStatusSetting', NULL, 1),
(N'dbo', N'SubOrderFeeTax', N'IX_SubOrderFeeTax_SubOrderFeeAmountDtlModelId', NULL, 1),
(N'dbo', N'SubOrderItemAmt', N'IX_SubOrderItemAmt_RetailPriceId', NULL, 1),
(N'dbo', N'SubOrderItemRemark', N'IX_SubOrderItemRemark_SubOrderItemModelId', NULL, 1),
(N'dbo', N'OrderReference', N'PK_OrderReference', NULL, 1),
(N'dbo', N'OrderItemPayment', N'IX_OrderItemPayment_OrderItemModelId', NULL, 1),
(N'dbo', N'MessageLogs', N'Idx_CreatedDate', NULL, 1),
(N'dbo', N'SubOrderItemDeliveryWindow', N'PK_SubOrderItemDeliveryWindow', NULL, 1),
(N'dbo', N'SubOrderFeeModel', N'IX_SubOrderFeeModel_SubOrderModelId', NULL, 1),
(N'dbo', N'SubOrderItemPay', N'PK_SubOrderItemPay', NULL, 1),
(N'dbo', N'OrderItemFulFillment', N'PK_OrderItemFulFillment', NULL, 1),
(N'dbo', N'OrderItem', N'IX_OrderItem_FulFillmentId', NULL, 1),
(N'dbo', N'OrderItem', N'IX_OrderItem_AmountId', NULL, 1),
(N'dbo', N'OrderItem', N'PK_OrderItem', NULL, 1),
(N'dbo', N'OrderPaymentTransaction', N'PK_OrderPaymentTransaction', NULL, 1),
(N'dbo', N'OrderAddress', N'PK_OrderAddress', NULL, 1),
(N'dbo', N'SubOrderItemBarcode', N'IX_SubOrderItemBarcode_SubOrderItemModelId', NULL, 1),
(N'dbo', N'OrderProcessConditionTb', N'PK_OrderProcessConditionTb', NULL, 1),
(N'dbo', N'OrderItemBarcode', N'IX_OrderItemBarcode_OrderItemModelId', NULL, 1),
(N'dbo', N'PromotionItemTb', N'PK_PromotionItemTb', NULL, 1);
END
GO

-- 4) The combined, deployment-ready procedure: fragmentation gate +
--    RunId logging (D027/S027) + WAIT_AT_LOW_PRIORITY throttling +
--    inter-rebuild pacing + off-peak window guard + RESUMABLE (D028).
CREATE OR ALTER PROCEDURE dbo.RebuildIndexOptimized
    @FragmentationThreshold    decimal(5,2) = 30.0,   -- % logical fragmentation
    @MinPageCount              int          = 1000,    -- skip trivially small indexes
    @LargeIndexPageCount       int          = 500000,  -- >= this many pages => RESUMABLE = ON
    @LowPriorityMaxWaitMinutes int          = 2,        -- WAIT_AT_LOW_PRIORITY ceiling per rebuild
    @InterRebuildDelaySeconds  int          = 15,       -- pacing delay between rebuilds
    @WindowEnd                 time         = '02:00'   -- hard stop; remaining candidates roll to next run
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RunId     uniqueidentifier = NEWID();
    DECLARE @Today     tinyint          = DATEPART(dw, GETDATE());
    DECLARE @SessionId int              = @@SPID;

    -- Tags this session so the audit trail's session_id can be cross-checked
    -- against this specific run (the P022 verification pattern).
    EXEC sp_set_session_context @key = N'MaintenanceRunId', @value = @RunId;

    DECLARE @SchemaName sysname, @TableName sysname, @IndexName sysname, @Frag decimal(5,2), @Pages bigint;

    DECLARE candidates CURSOR LOCAL FAST_FORWARD FOR
        SELECT s.SchemaName, s.TableName, s.IndexName, ps.avg_fragmentation_in_percent, ps.page_count
        FROM dbo.IndexMaintenanceSchedule AS s
        CROSS APPLY sys.dm_db_index_physical_stats(
                        DB_ID(), OBJECT_ID(QUOTENAME(s.SchemaName) + N'.' + QUOTENAME(s.TableName)),
                        NULL, NULL, 'LIMITED') AS ps
        JOIN sys.indexes AS i
            ON i.object_id = ps.object_id AND i.index_id = ps.index_id AND i.name = s.IndexName
        WHERE s.IsActive = 1
          AND (s.PreferredDow IS NULL OR s.PreferredDow = @Today)
          AND ps.avg_fragmentation_in_percent >= @FragmentationThreshold
          AND ps.page_count >= @MinPageCount
        ORDER BY ps.avg_fragmentation_in_percent DESC;  -- worst fragmentation first, in case the window closes early

    OPEN candidates;
    FETCH NEXT FROM candidates INTO @SchemaName, @TableName, @IndexName, @Frag, @Pages;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Off-peak guard: stop enqueuing new rebuilds once the maintenance window
        -- closes. Untouched candidates simply remain fragmented and are picked up
        -- by the *next* scheduled run -- the fragmentation gate itself gives this
        -- "resume" semantics, no separate checkpoint bookkeeping required.
        IF CAST(GETDATE() AS time) > @WindowEnd
        BEGIN
            PRINT 'Maintenance window closed -- remaining candidates deferred to next run.';
            BREAK;
        END

        DECLARE @LogId bigint;
        DECLARE @UseResumable bit = CASE WHEN @Pages >= @LargeIndexPageCount THEN 1 ELSE 0 END;

        INSERT INTO dbo.IndexMaintenanceLog
            (RunId, SchemaName, TableName, IndexName, FragmentPercent, StartedAt, SessionId, WasResumable)
        VALUES (@RunId, @SchemaName, @TableName, @IndexName, @Frag, SYSUTCDATETIME(), @SessionId, @UseResumable);
        SET @LogId = SCOPE_IDENTITY();

        DECLARE @Sql nvarchar(max) =
            N'ALTER INDEX ' + QUOTENAME(@IndexName) +
            N' ON ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) +
            N' REBUILD WITH (ONLINE = ON (WAIT_AT_LOW_PRIORITY (MAX_DURATION = ' +
            CAST(@LowPriorityMaxWaitMinutes AS nvarchar(10)) + N' MINUTES, ABORT_AFTER_WAIT = SELF))' +
            CASE WHEN @UseResumable = 1 THEN N', RESUMABLE = ON' ELSE N'' END + N');';

        BEGIN TRY
            EXEC sp_executesql @Sql;
            UPDATE dbo.IndexMaintenanceLog
               SET CompletedAt = SYSUTCDATETIME(), Status = 'Completed'
             WHERE LogId = @LogId;
        END TRY
        BEGIN CATCH
            -- ABORT_AFTER_WAIT = SELF raises an error when the low-priority wait
            -- ceiling is exceeded -- expected, benign "deferred, not failed": the
            -- index stays fragmented and will be retried later, instead of forcing
            -- a blocking Sch-M wait onto live OLTP transactions.
            DECLARE @IsLowPriorityAbort bit =
                CASE WHEN ERROR_NUMBER() IN (1222, 49920) THEN 1 ELSE 0 END;

            UPDATE dbo.IndexMaintenanceLog
               SET CompletedAt = SYSUTCDATETIME(),
                   Status = CASE WHEN @IsLowPriorityAbort = 1 THEN 'Deferred: low-priority wait exceeded'
                                 ELSE 'Failed: ' + LEFT(ERROR_MESSAGE(), 150) END,
                   AbortedByLowPriority = @IsLowPriorityAbort
             WHERE LogId = @LogId;
        END CATCH

        -- Pace successive rebuilds so log-flush/IO from one operation settles
        -- before the next begins, instead of saturating IO back-to-back.
        -- WAITFOR DELAY only accepts a string literal or a local variable --
        -- not an inline expression -- so the delay string must be built first.
        IF @InterRebuildDelaySeconds > 0
        BEGIN
            DECLARE @DelaySeconds char(8) = '00:00:' + RIGHT('0' + CAST(@InterRebuildDelaySeconds AS varchar(2)), 2);
            WAITFOR DELAY @DelaySeconds;
        END

        FETCH NEXT FROM candidates INTO @SchemaName, @TableName, @IndexName, @Frag, @Pages;
    END

    CLOSE candidates;
    DEALLOCATE candidates;
END
GO

-- 5) Companion procedure for the low end of the fragmentation range.
--    REORGANIZE is always online and holds only short per-page locks
--    (no Sch-M wait like REBUILD), so it doesn't need WAIT_AT_LOW_PRIORITY
--    or RESUMABLE -- those are REBUILD-only concepts. Reuses the same
--    schedule/log tables as RebuildIndexOptimized so both procedures
--    share one audit trail.
CREATE OR ALTER PROCEDURE dbo.ReorganizeIndexOptimized
    @MinFragmentationThreshold decimal(5,2) = 5.0,    -- below this: not worth reorganizing
    @MaxFragmentationThreshold decimal(5,2) = 30.0,   -- at/above this: REBUILD is more effective instead
    @MinPageCount               int         = 1000,    -- skip trivially small indexes
    @InterRebuildDelaySeconds   int         = 5,        -- REORGANIZE is lighter; shorter pacing is fine
    @WindowEnd                  time        = '03:00'   -- hard stop; remaining candidates roll to next run
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RunId     uniqueidentifier = NEWID();
    DECLARE @Today     tinyint          = DATEPART(dw, GETDATE());
    DECLARE @SessionId int              = @@SPID;

    EXEC sp_set_session_context @key = N'MaintenanceRunId', @value = @RunId;

    DECLARE @SchemaName sysname, @TableName sysname, @IndexName sysname, @Frag decimal(5,2), @Pages bigint;

    -- Deliberately excludes anything at/above @MaxFragmentationThreshold:
    -- that band belongs to RebuildIndexOptimized, not here. Run both
    -- procedures back to back (or as separate job steps) to cover the
    -- full range.
    DECLARE candidates CURSOR LOCAL FAST_FORWARD FOR
        SELECT s.SchemaName, s.TableName, s.IndexName, ps.avg_fragmentation_in_percent, ps.page_count
        FROM dbo.IndexMaintenanceSchedule AS s
        CROSS APPLY sys.dm_db_index_physical_stats(
                        DB_ID(), OBJECT_ID(QUOTENAME(s.SchemaName) + N'.' + QUOTENAME(s.TableName)),
                        NULL, NULL, 'LIMITED') AS ps
        JOIN sys.indexes AS i
            ON i.object_id = ps.object_id AND i.index_id = ps.index_id AND i.name = s.IndexName
        WHERE s.IsActive = 1
          AND (s.PreferredDow IS NULL OR s.PreferredDow = @Today)
          AND ps.avg_fragmentation_in_percent >= @MinFragmentationThreshold
          AND ps.avg_fragmentation_in_percent <  @MaxFragmentationThreshold
          AND ps.page_count >= @MinPageCount
        ORDER BY ps.avg_fragmentation_in_percent DESC;

    OPEN candidates;
    FETCH NEXT FROM candidates INTO @SchemaName, @TableName, @IndexName, @Frag, @Pages;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF CAST(GETDATE() AS time) > @WindowEnd
        BEGIN
            PRINT 'Maintenance window closed -- remaining candidates deferred to next run.';
            BREAK;
        END

        DECLARE @LogId bigint;

        INSERT INTO dbo.IndexMaintenanceLog
            (RunId, SchemaName, TableName, IndexName, FragmentPercent, StartedAt, SessionId, WasResumable)
        VALUES (@RunId, @SchemaName, @TableName, @IndexName, @Frag, SYSUTCDATETIME(), @SessionId, 0);
        SET @LogId = SCOPE_IDENTITY();

        DECLARE @Sql nvarchar(max) =
            N'ALTER INDEX ' + QUOTENAME(@IndexName) +
            N' ON ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) +
            N' REORGANIZE;';

        BEGIN TRY
            EXEC sp_executesql @Sql;
            UPDATE dbo.IndexMaintenanceLog
               SET CompletedAt = SYSUTCDATETIME(), Status = 'Completed'
             WHERE LogId = @LogId;
        END TRY
        BEGIN CATCH
            UPDATE dbo.IndexMaintenanceLog
               SET CompletedAt = SYSUTCDATETIME(),
                   Status = 'Failed: ' + LEFT(ERROR_MESSAGE(), 150)
             WHERE LogId = @LogId;
        END CATCH

        IF @InterRebuildDelaySeconds > 0
        BEGIN
            DECLARE @DelaySeconds char(8) = '00:00:' + RIGHT('0' + CAST(@InterRebuildDelaySeconds AS varchar(2)), 2);
            WAITFOR DELAY @DelaySeconds;
        END

        FETCH NEXT FROM candidates INTO @SchemaName, @TableName, @IndexName, @Frag, @Pages;
    END

    CLOSE candidates;
    DEALLOCATE candidates;
END
GO

-- Verification query for any future P022-style "suspicious audit row":
-- was a maintenance run active at that timestamp?
-- SELECT * FROM dbo.IndexMaintenanceLog
-- WHERE '2026-07-29T15:50:07' BETWEEN StartedAt AND ISNULL(CompletedAt, SYSUTCDATETIME())
--   AND SchemaName = 'dbo' AND TableName = 'SubOrderItem';

-- Monitoring query: candidates repeatedly deferred by low-priority wait timeout --
-- signals a table that never has an idle moment and may need a widened window or
-- a manually scheduled exception rather than assuming it will eventually succeed.
-- SELECT SchemaName, TableName, IndexName, COUNT(*) AS DeferredCount
-- FROM dbo.IndexMaintenanceLog
-- WHERE AbortedByLowPriority = 1 AND StartedAt >= DATEADD(day, -14, SYSUTCDATETIME())
-- GROUP BY SchemaName, TableName, IndexName
-- HAVING COUNT(*) >= 3
-- ORDER BY DeferredCount DESC;
