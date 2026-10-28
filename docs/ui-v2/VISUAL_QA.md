# Visual QA

Screenshots are generated from rendered browser pages under `.audit-run/ui-v2/screenshots/`. They are not fabricated. The directory is ignored by Git; the dedicated CI uploads sanitized UI test evidence.

Baseline authenticated screenshots were unavailable before implementation. Do not present new screenshots as an old/new pixel comparison. Review against DESIGN_SYSTEM.md.

At the initial checkpoint, login was captured at all five required viewport sizes. Authenticated screenshots, business modals, real 20-row Office layouts and exports require the SQL-backed verification run. Unsupported or unexecuted pages remain NOT TESTED in the parity matrix.
