# Office Wizard — pending upload / unsaved navigation fix

Baseline: `79f05046e219c1aaa4dd0b300b61135b7be89b5a`, branch `feat/shoedocx-shadcn-ui-v2`. No tracked local changes existed before work. Pre-existing untracked `ShoeDocX-Gate3A/` was preserved.

## Reproduced cause

1. Choosing a file only updates browser state; it does not upload it. The original combined `unsaved = formsDirty || template || example` correctly blocked navigation, but showed two generic messages without identifying the missing upload/comparison action. Baseline normal template upload followed by step 2 PASSED; no claim is made that every successful normal template upload was broken.
2. A real failure was reproduced with the authenticated full API: example upload returned HTTP 200, then an intentionally aborted preview GET rejected `refresh()`. `setExample(null)` was after that await, so it never ran. The server had accepted the example and advanced its version, while the client retained a pending example and blocked navigation.
3. Editors own local state and report dirty via effects. Their React keys now include the acknowledged server version so a successful Apply rebases normalized editor state; failed mutations never advance that key. This protects synchronization without simply clearing the parent's dirty map.

## Fix

- Template and example handlers validate matching session ID and an advanced safe integer version before clearing the pending file/input.
- Upload acknowledgement is independent of preview retrieval. A preview failure preserves the acknowledged session/version, clears the already-uploaded pending file and gives an explicit preview/retry message. It never claims a failed upload succeeded.
- One actionable pending banner identifies template, example, schema, mapping, rules or data. Navigation stays blocked, with scroll/focus on the corresponding upload/Apply action. The duplicate generic warning and per-editor duplicate warning were removed.
- Selection and server acknowledgement have distinct labels. Upload is a full-width primary action. Selecting another file clears an old success notice; filename-qualified server acknowledgement remains visible.
- Editor mutations, draft revision binding, cookie/CSRF, actor/tenant resolution and server export approval contracts are unchanged. No backend or studioApi.ts changes.

## Executed regression coverage

`frontend/tests/ui-v2/office-pending.spec.ts` has eight checks:

| Case | Evidence | Result |
|---|---|---|
| A | Selected file blocks next; specific upload instruction; focus; single pending warning | PASS |
| B | Real HTTP 200 clears pending file and native input; step 2 opens without false warning | PASS |
| B protocol fault | A deliberately invalid version acknowledgement retains the file and reports no success | PASS |
| C | Real rejected XLSX retains the selected file and retry action; no fake success | PASS |
| D | Pending example names the comparison action; explicit discard permits navigation | PASS |
| E–F | Schema/mapping changes block next; actual successful Apply permits subsequent step | PASS |
| F preview fault | Real example mutation succeeds, preview GET is deliberately aborted; no false pending state | PASS |
| G | Save/resume retains data and expected revision; does not grant mapping confirmation/approval/export | PASS |

Full API suite: **20/20 PASS**, including the eight new cases. Real Identity and a fresh isolated synthetic SQL database were used; authentication and CSRF were not mocked. Only deliberate transport/response faults were injected in the two named fault cases. The valid XLSX was generated with openpyxl; invalid input was synthetic bytes.

Existing Office suite: **five journeys PASS**, including upload → structure → mapping, 20-row/multi-sheet entry, SQL draft recovery across an actual test-host restart, fresh review/approval and cross-owner/tenant denial. **11 XLSX independently reopened PASS**.

Frontend TypeScript/Vite build: **PASS**. Existing regression/model/navigation/security tests: **48/48 PASS**. No new package dependencies.

The tests create/drop only dedicated disposable databases and their own sessions. The live `ShoeDocXLocalTest` database and the user's existing drafts, keys and credentials were untouched. No merge or deployment.

## Actual screenshots

- Pending template: [before](office-pending/before-pending-template.png) / [after](office-pending/after-pending-template.png).
- Acknowledged example + failed preview: [before](office-pending/before-example-preview-failure.png) / [after](office-pending/after-example-preview-failure.png).

These are actual browser screenshots with synthetic authenticated users. They are not mockups.
