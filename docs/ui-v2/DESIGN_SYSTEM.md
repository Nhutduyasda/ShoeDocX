# ShoeDocX UI V2 design system

Light neutral workspace, white surfaces, blue `#2563eb` actions, slate text, 1px borders, 7px controls and 10–12px cards. Vietnamese is the primary language. No invented metrics or notification counts.

The new shell owns responsive sidebar, breadcrumbs, destination search, account/help menus, and a skip link. All business destinations are filtered by the existing department helpers. Navigation callbacks still pass through `requestInvoiceDraftLeave`; shipment refs and OCR entry remain in App.tsx.

Radix Dialog supplies focus trapping, Escape and focus restoration for mobile navigation/search. Radix Dropdown provides keyboard menus. Native labeled form controls preserve browser/password-manager semantics. Lucide supplies icons. Existing AuthContext/API clients remain authoritative.

Office uses persistent steps, editing surface and contextual server/save status. Mobile stacks content and confines overflow to tables. Preview remains informational; browser code never executes workbook formulas. Server version, review, approval and export gates remain unchanged.

Ant Design tables, editable grids, dialogs and complex business forms remain compatibility widgets. UI V2 spacing/typography surrounds them; a full component-by-component shadcn migration is not complete. Dark mode is deferred until document previews and every retained widget can be verified.

## Upstream provenance

- Repository: https://github.com/satnaing/shadcn-admin
- Immutable commit: `e16c87f213a5ba5e45964e9b67c792105ec74d26`
- Copied/adapted: `src/components/ui/button.tsx` → `frontend/src/components/ui/button.tsx` (local utility import).
- Reviewed patterns: `src/components/layout/app-sidebar.tsx`, UI sidebar, dialogs, dropdowns, cards, README, package manifest and LICENSE.
- New shell and business integrations are ShoeDocX-specific; no upstream demo auth, Clerk, fake users, router, Query or dashboard data imported.
- MIT copyright and complete notice: `frontend/SHADCN_ADMIN_LICENSE` (Copyright 2024 Sat Naing).

## Dependency decisions

Add Radix dialog/dropdown/slot, CVA, clsx, tailwind-merge and lucide-react for actual used components. Add Playwright solely for verification. Keep Ant Design and existing state/routing. Do not install the upstream dependency graph. Keep parser/nested as dev security fixtures so accepted selector regression tests remain runnable; they are not the active Tailwind pipeline.
