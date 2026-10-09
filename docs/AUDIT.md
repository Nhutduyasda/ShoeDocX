# Documentation audit

Source baseline: `40881ac`. Audited on 9 October 2026 before rewriting the README. “Implemented” below means source and UI routes exist; it does not establish production readiness.

| Area | Finding | Evidence |
| --- | --- | --- |
| OCR | Implemented: OpenAI image extraction, multiple documents/images, review, reconciliation and reasoned manual confirmation. No Gemini provider or simulated no-key fallback in the current service. | `OcrExtractionService.cs`, `OcrController.cs`, `OcrUploadModal.tsx`, `BatchOcrModal.tsx`, OCR tests |
| Documents | Implemented: INV/PKL previews, template-based Excel generation, product-specific packing, process-specific output, saved-record re-export. Template drawings/stamps are removed, so exact preservation is not promised. | `ExcelImportExportService.cs`, `ShipmentsController.cs`, `DocumentTemplateConfig.cs` |
| Master data | Implemented: catalog, contract/partner folders, prices, packing settings, Excel preview/import/export and row errors. | `ProductMastersController.cs`, `MasterDataFoldersController.cs`, `ProductMasterPage.tsx` |
| Dispatch | Implemented: source selection, merge preview/export, 2–10 logical split allocations, per-style/process and aggregate quantity guards, consecutive numbering, ZIP, persistence and audit. Mixed processes can generate more physical documents than logical allocations. | `ShipmentDispatchService.cs`, `SequenceService.cs`, dispatch components, `ShipmentDispatchTests.cs` |
| Packing-aware split | Partial: split balance and warning logic assumes 12 pairs; final packing uses master data. Do not advertise arbitrary-carton automatic allocation. | `splitAllocation.ts`, `ValidateSplitAsync`, `CalculatePklBreakdown` |
| History / drafts | Implemented: history loading, guarded editing and re-export; browser drafts isolated by user/backend with conflict checks. Drafts do not sync between devices. | `ShipmentPage.tsx`, `invoiceDraftStore.ts`, frontend tests |
| Access | Implemented: Identity, cookie JWT, departmental API authorization, revocation/security-stamp checks, warehouse handoff, cleared-record guards, business/unlock audits and tenant query filters. No independent security certification established. | `Program.cs`, `AuthController.cs`, warehouse/shipment controllers, `AppDbContext.cs` |
| Other modules | Source/UI exist for customs comparison/archive/settlement, BOM, material management and planning. Kept secondary to the document workflow. | customs/BOM/material controllers, services and pages |
| AI template analysis | Experimental designation for this portfolio: service and UI exist, with demo credit replenishment. A paid billing system and reliable arbitrary-template support are not established. | `TemplateAiParserService.cs`, `TemplatesController.cs`, `TemplateConfigModal.tsx` |
| Roadmap | Core dispatch work already exists despite older roadmap labels. Threshold suggestions at 1,500/5,000 pairs were not found in inspected UI. Real-image end-to-end acceptance and deployment remain unverified. | `ROADMAP_SHIPMENT_DISPATCH_RULES.md`, dispatch source and tests |
| Stack / setup | .NET 8, EF Core 8 SQL Server runtime; SQLite is used in tests and retained historical migrations. React 19, Ant Design 6, Vite 8, TypeScript 6. Compose requires external SQL Server and JWT configuration. Swagger is Development-only. | project/package manifests, `Program.cs`, Compose, Dockerfiles, Vite/Nginx configs |
| Assets / license | No genuine UI screenshots or root license found. Existing hero is generic framework artwork. Business spreadsheets and database backups are unsuitable public preview assets. | tracked asset/file inventory |

## Preservation decisions

Keep the original roadmap and frontend draft-test guide. Move corrected setup, OCR configuration, template mapping and storage-maintenance guidance to `SETUP.md` and `ARCHITECTURE.md`. Replace outdated versions, credential examples, production language and unsupported test counts. Do not copy business templates or records into public assets.

## Validation

Final execution results and limitations are recorded in [VALIDATION.md](VALIDATION.md).
