# Verified feature matrix

[Back to README](../README.md) · [Audit](AUDIT.md)

“Implemented” means source and UI/API support were found. It does not mean every scenario has passed browser acceptance testing.

| Status | Capability | Evidence / limits |
| --- | --- | --- |
| Implemented | Image upload/paste and multi-image OCR | `OcrUploadModal`, `BatchOcrModal`, `OcrController`, `OcrExtractionService`; OpenAI key required |
| Implemented | Multiple detected documents and quantity review | Separate document parsing, reported/calculated totals, review steps and mismatch confirmation audit |
| Implemented | Catalog and partner/contract management | Product/folder controllers and pages; prices, carton specifications and Excel import preview with row errors |
| Implemented | INV/PKL preview and template Excel export | Shipment controller and Excel service; configured cell mapping and refreshed master-data sheet |
| Implemented | Merge selection and export | Merge modal and backend service; same-contract checks, style/process grouping; mixed processes can produce ZIP |
| Implemented | Split matrix and ZIP | Split modal and service; 2–10 logical allocations, item/process reconciliation, numbering, persistence and audit |
| Partial | Automatic packing-aware split | Auto-balance defaults to 12 pairs and backend split remainder warnings use 12; final Excel packing uses product master data |
| Implemented | History, re-edit/re-export and browser drafts | Shipment page, lock guards and draft store; browser-local storage, no cross-device sync |
| Implemented | Departments and business protection | Identity/JWT, controller authorization, warehouse submission, clearance guards and audited overrides |
| Partial | Account provisioning | Bootstrap administrator exists; no general registration/user-management API was found in the audited controllers |
| Implemented in source | Customs, settlement, BOM and materials/planning | Controllers/services/pages exist; supplementary to the portfolio's document focus |
| Experimental / unverified breadth | AI template analysis and credits | Parser/configuration UI exist; demo credit replenishment is not a verified commercial billing system |
| Planned; not found in inspected code | Threshold-based dispatch suggestions | Roadmap's 1,500/5,000-pair recommendations were not found in OCR/dispatch UI |
| Awaiting verification | Real-image end-to-end acceptance and deployment | No complete browser/API/SQL Server/container run or production deployment evidence established |

## Reading the original roadmap

[ROADMAP_SHIPMENT_DISPATCH_RULES.md](../ROADMAP_SHIPMENT_DISPATCH_RULES.md) is preserved as the original requirement narrative. Its Phase 1/2 dispatch services and interface are now represented in source. OCR dispatch integration and audit/source relations also exist; threshold suggestion behavior and the roadmap's real-document acceptance scenarios remain unverified.

Do not treat its estimates, historical labels, proposed endpoint names or claimed percentage improvements as current validated results. Current endpoints are listed in [ARCHITECTURE.md](ARCHITECTURE.md).
