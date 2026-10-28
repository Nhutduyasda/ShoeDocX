# Visual QA — actual rendered evidence

Selected screenshots captured with synthetic authenticated users are committed for review:

| Surface | Evidence |
|---|---|
| Login desktop | [Screenshot](screenshots/login-desktop.png) |
| Workspace desktop | [Screenshot](screenshots/workspace-desktop.png) |
| Product form, 360px | [Screenshot](screenshots/product-mobile.png) |
| Office review desktop | [Screenshot](screenshots/office-review-desktop.png) |

The generated set under `.audit-run/ui-v2/screenshots/` includes every major business destination at 1440×900, 1366×768, 1024×768, 390×844 and 360×800; Product Master modal at desktop/390/360; Office's six populated steps; and the technical Studio at all five sizes. CI uploads these sanitized screenshots, never private API logs/keyring or user credentials.

The full-API browser suite asserts main-page overflow constraints, real API 5xx and runtime errors. The Product Master form now stacks on mobile and its submit footer remains in the viewport. The Invoice partner header's mobile overflow and CSS layer precedence were corrected. Successful and not-evaluated Office checks are distinguished, with full details available without displacing the review controls.

This is layout/smoke coverage with real API calls, plus the explicit CRUD and Office journeys in FEATURE_PARITY_MATRIX.md. It is not proof of every legacy operation or a WCAG certification. Baseline authenticated screenshots were not captured before implementation; these are new-design screenshots, not a claimed old/new pixel comparison.
