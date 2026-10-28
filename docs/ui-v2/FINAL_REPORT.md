# UI V2 engineering report

**PARTIAL / NOT READY.** Keep Draft PR [#21](https://github.com/Nhutduyasda/ShoeDocX/pull/21). No merge/deployment performed.

## Provenance and scope

Starting main: `329b684ae9daf71879e2198fff65faf073b11f05`. Branch: `feat/shoedocx-shadcn-ui-v2`. Implementation checkpoint: `b1efd49e4e65e845f0ddd3ed99ce200725683403`. The PR's current head and exact-checkout CI run identify subsequent documentation-only commits.

Upstream Shadcn Admin: `e16c87f213a5ba5e45964e9b67c792105ec74d26`. Adapted Button source with complete MIT notice; reviewed sidebar/dialog/menu patterns. No upstream authentication, Clerk, fake data, router or query/state infrastructure imported.

New presentation: login; responsive shell/sidebar/header; account/help/search; real workspace cards; shared business page/table/modal tokens; Product Master mobile form; Invoice header reflow; Office upload and six-step three-region workspace; readable server checks/preview; explicit mapping/data/approval/save states; accessible draft-delete confirmation; skeleton/error boundary.

Warehouse, shipment, history, OCR, products, materials, BOM, planning and customs retain their business components/API clients. Complex Ant Design widgets remain compatibility areas. This is not a completed component-by-component replacement of every legacy dialog/grid.

## Executed evidence

| Check | Result |
|---|---|
| Baseline build and model tests | PASS — 32 tests |
| Feature regression/navigation/security-fixture tests | PASS — 48 tests |
| Feature TypeScript and Vite production build | PASS |
| Full API browser suite | PASS — 12 checks; actual synthetic SQL/Identity, no auth/API mocks |
| Responsive | PASS — major destinations at 1440×900, 1366×768, 1024×768, 390×844, 360×800; main-page overflow assertions |
| Product Master | PASS — required validation, real create/persisted list, mobile modal/footer |
| Materials | PASS — real create/search/edit/persisted list |
| Office six steps | PASS — 5 journeys including ambiguous mapping, validation rejection/correction, 2/3 mapped sheets, 20-row mobile entry |
| SQL draft recovery | PASS — actual backend restart, fresh login, resume/reconfirm/review/approve/export; cross-owner/tenant denial |
| Draft delete | PASS — confirmation cancel then actual authenticated/versioned delete |
| XLSX independent reopen | PASS — 11 workbooks, values/types/leading zeros/formulas/styles/sheet order/reference sheet |
| npm audit before/after | PASS — zero advisories; no overrides/advisory suppression |
| Lint | Completed with existing legacy warnings; no claim of warning-free source |
| Backend source diff | Empty |

Selected real screenshots: [VISUAL_QA.md](VISUAL_QA.md). Complete exercised/untested boundaries: [FEATURE_PARITY_MATRIX.md](FEATURE_PARITY_MATRIX.md).

Initial commit's four Windows/Ubuntu jobs passed: [run 40640404310](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40640404310). Follow-up exact-head runs are linked from [PR checks](https://github.com/Nhutduyasda/ShoeDocX/pull/21/checks). Only a completed run on the current SHA is a final CI verdict.

## Remaining acceptance gaps

- Complete all legacy CRUD/batch/import/export/reconciliation/print journeys and per-modal validation/error states listed in the parity matrix.
- Verify Fast-Grid keyboard work and all non-Admin browser role journeys; unit role equivalence is already tested.
- Finish per-widget shadcn migration/visual signoff of retained Ant Design compatibility areas.
- Native browser 200% zoom, screen-reader review and complete WCAG 2.2 AA assessment. The current 720×450 reflow check is not native zoom certification.
- Existing Ant Design deprecation/ref/form warnings and large bundle warning remain. Dark mode is deferred until preview/widget readability can be verified.

## Data and security handling

No backend source, SQL migrations, deployment/Docker files, production keys or existing User Secrets were modified. No paid AI/OCR calls were made. New full-API runs create and drop only their own UUID `gate5c_ui_v2_full_*` database. Earlier exploratory `ShoeDocXUiV2Synthetic_*` databases were left intact rather than broadly deleted. `ShoeDocXLocalTest` was not used for QA. Private API logs/keyring are excluded from CI artifacts.
