# Tailwind 4 compatibility migration

Accepted comparison baseline: main `329b684` / Tailwind 3.4.17 with reviewed selector-parser 6.1.5. Baseline install/build, 13 invoice-draft tests and 19 Office-model tests passed; npm audit reported zero advisories.

Official guidance reviewed: https://tailwindcss.com/docs/upgrade-guide and https://tailwindcss.com/docs/preflight . React 19, Vite 8 and TypeScript 6 remain installed; the frontend build is the compatibility probe.

Proposed branch uses Tailwind 4.3.3 with its Vite plugin. `index.css` imports theme and utilities explicitly, omitting `preflight.css`. Legacy JS token extensions are loaded with `@config`. The old PostCSS Tailwind plugin and autoprefixer are removed from the active pipeline. Explicit borders/box-sizing/button defaults are scoped to new UI rather than globally resetting legacy widgets.

Utilities and retained custom CSS still require browser review on each business page. Border defaults, shadows, ring semantics, form controls and document print CSS are particular review points. A successful build is not merge acceptance. No dependency overrides/advisory suppressions were added.

Ant Design cascade integration follows https://ant.design/docs/react/compatible-style/ : StyleProvider enables the antd layer, ordered before utilities. This restores intended utility overrides without enabling Preflight. The direct cssinjs dependency matches Ant Design's installed compatible 2.1.2 version.
