# Architecture and document generation

[Back to README](../README.md)

## Responsibilities and boundaries

- `frontend/src/pages` provides overview, products, shipments/history, warehouse, customs settlement, BOM, materials and planning. OCR and dispatch components sit alongside these pages.
- `frontend/src/api/client.ts` uses Axios with cookies and `/api` by default. Vite proxies development traffic; Nginx proxies container traffic.
- `backend/ShoeExportInvoice.Api/Controllers` exposes authenticated business endpoints. `Program.cs` registers Identity, cookie-aware JWT validation, authorization, rate-limit policies and services.
- `Services/ShipmentDispatchService.cs` builds merge previews, validates split allocation, reserves sequences, generates files and persists dispatch results inside serializable transactions with retry execution.
- `Services/OcrExtractionService.cs` sends image content to OpenAI and parses/enriches structured results. Uploaded slips should therefore be suitable for transfer to the configured external provider.
- `Services/ExcelImportExportService.cs` implements catalog Excel import/export, packing/document previews and workbook generation. `TemplateService` selects templates/configuration.
- `Data/AppDbContext.cs` uses EF Core with tenant query filters and integrity constraints. Runtime is SQL Server; tests frequently use SQLite in memory.

## Dispatch API map

All paths below are under `/api/shipments`, with `Admin,Xnk` authorization on dispatch operations.

| Operation | Method and suffix |
| --- | --- |
| Merge preview | `POST merge-preview` |
| Merge output | `POST merge-export` |
| Split validation | `POST split-validate` |
| Split ZIP output | `POST split-export-zip` |
| Packing preview | `POST preview-pkl` |
| Document preview | `POST preview-document` |
| Current editor export | `POST export-excel` |
| Saved-record export | `GET {id}/export-excel` |

The original roadmap proposes different names; this table reflects controller attributes. Dispatch groups styles with production process, validates compatible source contracts and rejects missing master data. Mixed processes produce separate physical workbooks. Split validation checks each source item and total; a 12-pair remainder is a warning rather than a quantity-integrity exception.

Warehouse batch sources are linked to generated shipments through `ShipmentSourceBatch`. OCR document identifiers and manual confirmations are included in dispatch audit details; these are a different traceability path from persisted warehouse-batch relations.

## Workbook behavior

Generation opens a selected workbook rather than recreating every sheet from scratch. It writes configured headers and rows, adjusts rows/totals and calculates packing data. Template drawings, legacy drawings and stamps are stripped. Formatting reuse is intentional, but complete preservation is not guaranteed.

Defaults from `Models/Templates/DocumentTemplateConfig.cs`:

| INV header | Cell |
| --- | --- |
| Invoice number / date / contract | `J4` / `J5` / `J6` |
| Delivery / payment / destination | `J7` / `J8` / `J9` |
| Buyer name / address | `D4` / `D5` |

INV rows start at 13: sequence `B`, code `C`, description `D`, quantity `E`, unit `F`, CMT/DAP prices `G/H`, amounts `I/J`.

PKL defaults start at row 12: carton range `A`, code `B`, description `C`, quantity `D`, unit `E`, cartons `F`, net weight `G`, gross weight `H`. These are configurable schema defaults; actual insertion and total-row handling are implemented in the export service. The master-data sheet is refreshed for the shipment rather than blindly retaining another partner's catalog.

Packing uses product-specific pairs per carton to separate full cartons and remainders and build cumulative ranges. The current weight calculation uses a 3.2 kg reference per full-carton equivalent plus 0.1 kg carton weight with gross rounding. These are implemented business assumptions, not measured physical weights.

## Access and consistency

Identity handles passwords and account lockout. JWTs can be read from an HttpOnly, SameSite=Strict cookie. Validation checks active users, security stamps, departmental claims and revoked token identifiers. Controllers enforce departmental permissions; frontend navigation is an additional interface restriction.

Cleared/locked shipments cannot be edited/deleted through normal actions. Unlock and sequence overrides have dedicated authorization and audit paths. Tenant query filters and uniqueness constraints support isolation and integrity; they are not a substitute for an independent security audit.

Browser invoice drafts are local persistence, partitioned by user and backend. They retain delayed-save edits, detect storage failures and compare saved-record baselines. They do not synchronize across devices.

## Storage and test boundaries

SQL Server stores users, catalog, shipments, source relations, templates/configuration metadata, customs and planning records. Customs files and template workbooks live on disk. Generated exports are returned as bytes; saved-record re-export regenerates from stored data rather than proving archival retention of the original workbook bytes.

SQLite-based tests and fake OCR/Excel services verify selected rules. Full SQL Server transaction behavior, real vision extraction, browser interaction and container deployment require separate acceptance checks. See [VALIDATION.md](VALIDATION.md).
