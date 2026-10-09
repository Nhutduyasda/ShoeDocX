# Screenshot capture checklist

`workflow.svg` is an original conceptual illustration. No genuine product screenshots were found; the frontend hero is generic artwork.

| Proposed filename | Real screen to capture | Caption |
| --- | --- | --- |
| `overview.webp` | Overview with synthetic records | A shared view of shipment preparation |
| `ocr-upload.webp` | Image upload | Start from a synthetic warehouse slip |
| `ocr-review.webp` | Extraction and discrepancy review | Verify quantities before applying rows |
| `shipment-editor.webp` | Editor and packing preview | Review product and packing data |
| `dispatch.webp` | Merge preview or split matrix | Reconcile allocations against source quantities |
| `document-preview.webp` | INV/PKL preview | Review documents before export |

1. Start the real app against a dedicated demo SQL Server database. Disable development-user seeding; create a unique demo administrator.
2. Use fictitious company, buyer, contract, product and price data. Replace default business identities in demo configuration and use a sanitized workbook, including headers, footers, stamps and metadata.
3. Use synthetic slip images. Do not fake API responses or generate interface mockups to fill the gallery.
4. Capture at a consistent viewport, approximately 1440 × 900. Inspect account menus, notifications, tables, filenames and browser chrome for private information.
5. Save optimized WebP or PNG here, ideally below 300 KB each. Add relative links and descriptive alt text to the README only after privacy inspection.

Missing input: a clean demo database, sanitized workbook and synthetic slip images. No confidential documents need to be supplied.
