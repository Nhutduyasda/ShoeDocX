# UI V2 engineering checkpoint

Status: **PARTIAL / NOT READY**. Keep the pull request Draft. No merge or deployment authorized.

Starting main: `329b684ae9daf71879e2198fff65faf073b11f05`. Feature branch: `feat/shoedocx-shadcn-ui-v2`. Exact reviewed SHA is the PR head / CI checkout SHA; subsequent documentation will link actual CI runs.

Implemented: shadcn/Radix shell, role-filtered navigation and command search, account/help menus, responsive mobile drawer, labeled native login, workspace action cards, explicit dashboard load errors, shared presentation styles, Office upload and three-region six-step workspace, server/save status, error boundary and skeleton. Backend/API/auth/session/approval logic is unchanged.

Local verification so far:

- Baseline build + 32 tests, zero npm audit advisories.
- Feature build PASS; 48 regression/navigation/security-fixture tests PASS.
- Full unchanged backend with a new synthetic SQL database: 8 browser checks PASS, including login at five viewports, keyboard/reflow, authenticated navigation, mobile menu and Office empty states.
- Existing Office journeys: 5 PASS with real Identity, SQL draft restart, cross-owner/tenant denial, validation/approval/export gates, 20-row mobile entry.
- 11 XLSX independently reopened with openpyxl: PASS (values, types, leading zeros, formulas, styles, sheet order and reference sheet).
- Dependency audit after migration: zero advisories.

Limits: retained Ant Design complex widgets still need per-widget migration/visual signoff; CRUD/batch/import/export parity for every legacy page and modal is not yet proven. A rendered page is not a full functional verdict. Native 200% browser zoom, full WCAG review, all legacy keyboard grids and per-role authenticated business coverage remain open. Some pre-existing Ant Design deprecation/ref warnings and lint warnings remain. Large bundle warning remains.

Dedicated Windows 2022 / Ubuntu 24.04 CI is proposed in `ui-v2-verification.yml`, independent of historical Gate 6B release blockers. Only actual completed CI runs may be called PASS. No existing database/secrets were changed by full API QA; it creates a separate `ShoeDocXUiV2Synthetic_*` database and process-only keys. Private API logs/keyring are excluded from artifacts.
