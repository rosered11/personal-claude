// =========================================================================
// S022 — FMSUpdateAdapter: ResolveShipmentProvider private helper
// Decision: D022 | Problem: P017
// =========================================================================

// ---- 1. PRIVATE HELPER (add inside FMSUpdateAdapter class) ----

/// <summary>
/// Resolves ShipmentProvider using explicit 3-tier priority:
///   Priority 1 — Package.ThirdPartyLogistic  (package-level carrier assignment, highest)
///   Priority 2 — Item.ThirdPartyLogistic      (item-level carrier from WMS)
///   Priority 3 — Item.FulFillment.Carrier     (fulfillment carrier fallback)
/// Returns defaultCarrierCode if all three tiers are empty/null.
/// </summary>
private string ResolveShipmentProvider(
    PackageTbMessage package,
    SubOrderItemMessage item,
    string defaultCarrierCode)
{
    // Priority 1: Package (highest — explicitly assigned at pack step)
    if (package != null && !string.IsNullOrWhiteSpace(package.ThirdPartyLogistic))
        return package.ThirdPartyLogistic;

    // Priority 2: SubOrderItem carrier from WMS
    if (!string.IsNullOrWhiteSpace(item.ThirdPartyLogistic))
        return item.ThirdPartyLogistic;

    // Priority 3: FulFillment Carrier (ACL fallback)
    if (!string.IsNullOrWhiteSpace(item.FulFillment?.Carrier))
        return item.FulFillment.Carrier;

    // Default: channel-configured carrier mapping (may be empty string — caller accepts that)
    return defaultCarrierCode ?? string.Empty;
}


// ---- 2. REFACTORED INNER LOOP BODY (unified for both weighted-item and normal-item branches) ----
//
// Replace the ShipmentProvider assignment and separate package block in BOTH branches with:

PackageTbMessage activePackage = isMultiplePackage
    ? packages.FirstOrDefault(w => w.Qty > 0)
    : null;

addShipItem.ShipmentProvider = ResolveShipmentProvider(activePackage, item, defaultCarrierCode);

if (activePackage != null)
{
    addShipItem.TrackingNumber = string.IsNullOrEmpty(activePackage.TrackingNo)
        ? string.Empty
        : activePackage.TrackingNo;
    addShipItem.TrackingUrl = string.IsNullOrEmpty(activePackage.TrackingUrl)
        ? string.Empty
        : activePackage.TrackingUrl;
    activePackage.Qty--;
}

// NOTE: Remove the separate `if (isMultiplePackage) { ... }` blocks from both branches.
// NOTE: Remove the `thirdPartyLogistic` variable — its merged logic is now explicit in ResolveShipmentProvider.
//       If TrackNo/TrackURL from item is still needed outside the package block, keep those assignments
//       but do NOT merge ThirdPartyLogistic into a pre-computed variable anymore.
