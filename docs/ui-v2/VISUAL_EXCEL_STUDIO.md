# Visual Excel Studio — implementation and verification

Continues PR #21 from `10f5b9049a5ce3a5cb55d07d7f4208132a31aac6`. The remote branch matched that commit when fetched. Existing untracked `ShoeDocX-Gate3A/` and `outputs/` were preserved. Backend, SQL, API contracts and exporter are unchanged.

## Working interactions

- Server-discovered field cards select the mapping target. Actual uploaded OOXML provides sheet tabs, coordinates, raw cell values, merged-range indications and formula markers. Click an existing non-formula/unmerged cell or use arrows and Enter. Jump-to-cell and 40-row windows bound rendering; each sheet retains its real coordinates.
- Step 2 supports schema destinations, type, required/omitted status and actual candidate evidence. Step 3 supports independent sheet mappings and shared prototype rows, with the existing maximum of three data sheets. Original detailed editors remain available under explicit expanders, including scope/name corrections, candidate selection and sheet removal. No field keys or revisions are manufactured.
- Mapping cards and markers distinguish pending local edits from server-applied positions; cards identify server-confirmed positions. Candidate counts describe ambiguity, without invented confidence scores. Apply uses the existing schema/multisheet endpoints; confirmation is still a separate server operation. Failed requests retain local changes and block navigation.
- The manual grid accepts tab/newline paste into text, numeric, date and Boolean fields using their existing string wire formats. Paste is atomic and bounded to 20 rows, available columns and 2,000 characters per value. Strings retain leading zeros. Nothing evaluates formulas or guesses locale decimals. Apply and Undo remain explicit. Tab/Shift+Tab, Enter/Shift+Enter, up/down, and Alt+left/right support keyboard entry.
- Server issues resolve field/check/rule identifiers to actual controls where evidence permits; “Sửa…” opens step 4 and focuses the affected cell. Inline invalid state and Vietnamese messages remain subordinate to actual server validation. The same submit action retries validation.
- A successful upload offers “Xem cấu trúc Excel”. Existing acknowledgment verification, precise pending banner, preview-failure handling and acknowledged-version editor rebasing remain intact. Draft status distinguishes pending changes, active server requests, acknowledged state and durable saves. Existing SQL expiration, ownership, conflict and reapproval behavior is retained.

## Preview and dependency/security decision

Selected approach A: a custom read-only semantic HTML cell canvas, with existing shadcn Button and React 19.2.8. A full editor such as ReactGrid would introduce editing/state and dependency scope beyond this bounded view, without replacing the need for an OOXML reader. No new dependencies, licenses, lockfile changes or third-party uploads. Existing shadcn MIT attribution remains intact.

`workbookPreview.ts` uses browser-native `DecompressionStream('deflate-raw')`, `DOMParser` and Web Crypto. Tested in installed Edge and in Chromium CI. Browsers without raw DEFLATE support fall back honestly to the original coordinate editors. The reader accepts at most 2 MiB compressed, 128 ZIP members, 8 MiB declared expanded archive content, 2 MiB per member, four sheets and 2,000 cells inside A1:AF1000. Actual streamed expansion is capped before buffering beyond each member budget. Encrypted/multi-disk/unsupported ZIP methods, duplicate/traversal entries, macros, external-link parts, embedded objects, connections and XML DTD/entities are rejected. Only workbook relationships, shared strings and worksheet XML are inflated. External workbook relationships are rejected; worksheet hyperlinks are never followed or rendered as links. ZIP64 is unsupported. Preview is informational, not an independent ZIP authenticity checker.

React renders all values as text; no HTML injection, macro execution, formula interpreter, external fetch, or source-to-business-data auto-import exists. Before display the SHA-256 must match the acknowledged server schema template hash. This only binds visual evidence; it grants no server authority. Workbook evidence lives in component memory, not browser storage or durable SQL. After refresh/resume, reselect the exact original file to restore visualization without mutation. The unchanged API does not expose template bytes.

Unsupported visual features are labeled: Excel formatting, dates/number formats, charts, images, print dimensions, hidden-row/column visibility, and recalculation. Raw saved cell values are shown; formulas have a marker and only their cached value, if present. Merged cells are indicated by their range rather than reproduced pixel-perfectly. The preview may be stricter than server-supported XLSX; the original editors remain usable.

Production Office chunk: approximately 58.5 kB / 17.6 kB gzip after the change. The existing application main-chunk size warning remains. `npm audit --audit-level=low`: zero advisories. React build passes without new peer dependencies. Legacy Ant Design React 19/deprecation warnings remain outside these new components.

## Executed local evidence

- TypeScript/Vite production build PASS.
- Existing model/navigation/security regressions plus issue-to-control resolution: 49 tests PASS.
- Full unchanged API + isolated synthetic SQL browser suite: 23 checks PASS, including eight upload-pending regressions and the new visual journey. No API success response mocks are used in the new journey. The two inherited tests deliberately inject upload acknowledgment/preview faults.
- Existing Gate 5D Office acceptance: five journeys PASS, actual backend restart, SQL draft recovery, cross-owner/tenant denial, reconfirm/review/approve/export. Eleven resulting XLSX files independently reopened with openpyxl PASS.
- New browser reader tests use a real synthetic openpyxl fixture with leading zeros, two sheets, a formula without cache, merged cells, AF80 and hostile-looking text. Malformed/oversized/encrypted ZIP rejection PASS.
- New visual API journey checks actual cell evidence, header pointer mapping, detail keyboard mapping, dirty guard, server revision advance, mapping confirmation gate, 20-row paste, leading zeros and error-control focus. Canvas/page bounds and screenshots cover widths 1440, 1366, 1024, 390 and 360.

Current exact-HEAD Windows/Ubuntu workflow results are reported on PR #21. The existing workflow checks out the PR head SHA and runs both frontend and isolated SQL jobs. Newly generated visual screenshots are included in its sanitized artifact whitelist. Do not infer a CI result from older commits.

## Limits and verdict

VISUAL WORKSPACE PARTIAL: working visual mapping and data entry, with explicitly limited workbook fidelity; assistive-technology acceptance remains outstanding. MAPPING PARITY PASS, DATA ENTRY PARITY PASS, SECURITY REGRESSION PASS within the executed synthetic scope. MERGE NOT READY: broader inherited legacy widget/business acceptance and full accessibility review are still incomplete. No claim is made to rerun the complete Gate 6B production release/certificate/disk-fault matrix for this frontend change. No merge, deployment or production modification.

## Actual screenshots

The before image comes from the immutable baseline source and the same synthetic workbook/real API. The after images show the implemented canvas; no mock data responses.

- [Before: coordinate mapping at 1366 px](visual-excel/before-mapping-1366.png).
- [After: visual canvas at 1366 px](visual-excel/after-canvas-1366.png).
- [After: mobile mapping at 390 px](visual-excel/after-mapping-390.png).

See [Vietnamese testing guide](VISUAL_EXCEL_TESTING_VI.md), [feature matrix](FEATURE_PARITY_MATRIX.md), and the visual screenshots in `visual-excel/`.
