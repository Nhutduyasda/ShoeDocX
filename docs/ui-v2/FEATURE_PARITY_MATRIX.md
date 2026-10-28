# Feature parity matrix

PASS is limited to the executed behavior in each row. Rendering/viewport coverage does not imply complete CRUD/export acceptance.

| Screen / workflow | Executed behavior | Result | Still required |
|---|---|---|---|
| Sign-in | Real Identity login, required fields, invalid credentials, password reveal, five viewports, keyboard/reflow | PASS | Full assistive-technology review |
| Application shell | Every allowed Admin destination, mobile drawer/Escape, role-helper equivalence for ten string/numeric departments | PASS | Browser journeys for each non-Admin role |
| Dashboard | Real synthetic API data, destination cards, role filtering source/test | PASS | Accounting-specific data/error scenarios |
| Warehouse | Real API load/render and five viewport overflow checks | PASS | Batch save/submit/edit/delete, full Fast-Grid keyboard parity NOT TESTED |
| Shipment / Invoice / PKL | Real load/render, five viewport overflow checks, 13 invoice draft model/guard regressions | PASS | Save/merge/split/print/export end-to-end NOT TESTED |
| Shipment history | Real load/render, five viewport checks | PASS | Filtering, paging, selection, historical reopen/export NOT TESTED |
| OCR modal | Existing ref/entry handlers retained in App.tsx | NOT TESTED | Manual open/review/close; paid OCR calls remain prohibited |
| Product Master | Required validation, create via real API, visible persisted record, modal at 1440/390/360, five page viewports | PASS | Import, bulk move/unit, folders and all edit/export paths NOT TESTED |
| Materials | Real create/search/edit/persisted list, five viewport page checks | PASS | Delete and all numeric/error boundaries NOT TESTED |
| BOM Calculator | Real load/render and five viewport checks | PASS | Calculate/save/update and result export NOT TESTED |
| BOM Management | Real load/render and five viewport checks; icon buttons labeled | PASS | Definition create/edit/delete and material rows NOT TESTED |
| Material Planning | Real load/render and five viewport checks | PASS | Approve/issue/cancel/download workflow NOT TESTED |
| Customs Settlement | Real load/render and five viewport checks | PASS | Sync/archive/reconciliation/export workflows NOT TESTED |
| Universal Document Studio technical UI | Authenticated entry and five viewport snapshots | PASS | Full technical-mode session journeys NOT TESTED in this UI suite |
| Office step 1 | Actual XLSX upload, example matching, invalid workbook rejection; component size/extension/removal tests | PASS | — |
| Office steps 2–3 | Ambiguous fields, explicit correction, 2/3-sheet mapping, rules, confirm, unsaved guard | PASS | — |
| Office step 4 | Real validation rejection/correction, leading zeros, exact types, 20-row mobile editor | PASS | — |
| Office step 5 | Review/approval gates and invalidation; successful checks collapsible with details retained | PASS | — |
| Office step 6 | Server export plus 11 independent XLSX reopen checks | PASS | — |
| Durable Office drafts | Real SQL save, actual process restart, fresh login, resume/reconfirm/review/approve/export, ownership/tenant denial | PASS | — |
| Draft deletion | Accessible confirmation, cancel then authenticated/versioned delete | PASS | — |
| Shared legacy dialogs/grids | Product modal and retained Ant Design controls share tokens/layers | NOT TESTED | Complete per-widget accessibility, keyboard, import/export parity |

Evidence: `frontend/tests/ui-v2/`, `frontend/tests/universalDocGate5D.e2e.mjs`, generated `results.json`, `office-summary.json`, `workbook-qa.json`; selected screenshots in `screenshots/`. Keep Draft until the untested protected flows are verified.


## Visual Excel Studio continuation

| Behavior | Result | Evidence / limit |
|---|---|---|
| Actual XLSX values, tabs, coordinates, merge/formula indications | PASS | Real openpyxl fixture and authenticated upload; raw values only |
| Header pointer and detail keyboard mapping | PASS | Real schema/multisheet acknowledgments and version advances |
| Ambiguity, type/required corrections, three-sheet limit | PASS | Candidate evidence and original detailed editors retained; existing 2/3-sheet export journeys |
| 20-row paste, leading-zero strings, keyboard and Undo/Apply | PASS | New visual API journey plus existing Office tests |
| Error-to-control focus | PASS | Actual missing required header response, focus recovery; bounded check/rule ID resolution model test |
| Pending-upload/navigation regression | PASS | All eight original pending cases retained |
| Five viewport widths | PASS | 1440/1366/1024/390/360; region-confined scrolling |
| Durable preview values across restart | PARTIAL | SQL business draft survives; original file must be reselected locally and hash-matched |
| Full Excel fidelity / assistive technologies | NOT TESTED | Unsupported formatting/images/print/recalculation explicit; manual accessibility review outstanding |

See `VISUAL_EXCEL_STUDIO.md` for the actual tested scope and security review. Visual workspace PARTIAL; mapping/data/security parity PASS within synthetic coverage; NOT READY to merge.
