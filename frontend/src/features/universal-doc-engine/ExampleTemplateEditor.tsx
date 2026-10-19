import { useEffect, useState } from 'react';
import type { ConfigurationDirtyChange, LearnedField, StudioSession } from './studioApi';
type Act = (action: string, data?: Record<string, unknown>, method?: string) => void;
export function SchemaEditor({ session, busy, act, onDirtyChange }: { session: StudioSession; busy: boolean; act: Act; onDirtyChange?: ConfigurationDirtyChange }) {
  const [fields, setFields] = useState<LearnedField[]>(session.schemaDraft?.fields ?? []);
  const dirty = JSON.stringify(fields) !== JSON.stringify(session.schemaDraft?.fields ?? []);
  useEffect(() => { onDirtyChange?.('schema', dirty); return () => onDirtyChange?.('schema', false); }, [dirty, onDirtyChange]);
  const update = (index: number, patch: Partial<LearnedField>) => setFields(fields.map((f, i) => i === index ? { ...f, ...patch } : f));
  return <form onSubmit={e => { e.preventDefault(); act('schema', { fields: fields.map(({ key, label, scope, type, required, omit, candidate, worksheet, cell, column, prototypeRow }) => ({ key, label, scope, type, required, omit, candidate, worksheet, cell, column, prototypeRow })) }, 'PATCH'); }}>
    {dirty && <p role="status">Unsaved schema edits. Apply or discard before confirmation or leaving this step.</p>}
    <button type="button" disabled={busy || !dirty} onClick={() => setFields(session.schemaDraft?.fields ?? [])}>Discard schema edits</button>
    <p>Field keys are generated automatically; you can leave them unchanged. Edits are unsaved until Apply. Confirmation uses the last applied server revision.</p>
    <p>Schema: {session.schemaDraft?.state} · Revision {session.schemaDraft?.revision}. Up to three data worksheets with one prototype row each. Reference sheets are preserved. Numeric identifiers default to Text; select Decimal after checking the meaning.</p>
    <ul>{session.sheetSummaries.map(s => <li key={s.name}>{s.name}: {s.usedCells} used cells · {s.tables} table candidates · prototype rows {s.prototypeRows.join(', ') || 'none'} · footer rows {s.footerRows.join(', ') || 'none'} · merges {s.mergedRanges.join(', ') || 'none'}</li>)}</ul>
    {fields.length === 0 && <p role="alert">No supported candidates. Use bold table headers, one materialized prototype and adjacent header value slots.</p>}
    {fields.map((f, i) => <fieldset key={i}><legend>{f.label} — {f.status}</legend>
      <label>Field label<input disabled={busy} aria-label={`Field label ${i + 1}`} value={f.label} maxLength={256} onChange={e => update(i, { label: e.target.value })} /></label>
      <label>Field key<input disabled={busy} aria-label={`Field key ${i + 1}`} value={f.key} maxLength={64} onChange={e => update(i, { key: e.target.value })} /></label>
      <label>Scope<select disabled={busy} aria-label={`Field scope ${i + 1}`} value={f.scope} onChange={e => update(i, { scope: e.target.value })}><option>Header</option><option>LineItem</option></select></label>
      <label>Type<select disabled={busy} aria-label={`Field type ${i + 1}`} value={f.type} onChange={e => update(i, { type: e.target.value })}>{['Text', 'Decimal', 'Date', 'Boolean'].map(t => <option key={t}>{t}</option>)}</select></label>
      <label>Destination<select disabled={busy} aria-label={`Field destination ${i + 1}`} value={f.candidate ?? ''} onChange={e => update(i, { candidate: e.target.value === '' ? null : Number(e.target.value) })}><option value="">Unresolved — choose evidence</option>{f.candidates.map((c, n) => <option key={n} value={n}>{c.worksheet}!{c.cell ?? `column ${c.column}, row ${c.prototypeRow}`} · label {c.labelCell}</option>)}</select></label>
      <details><summary>Correct scope or choose another existing template cell</summary>
        <label>Explicit worksheet<select disabled={busy} aria-label={`Explicit worksheet ${i + 1}`} value={f.worksheet ?? ''} onChange={e => update(i, { worksheet: e.target.value || undefined })}><option value="">Use candidate above</option>{session.sheets.map(s => <option key={s}>{s}</option>)}</select></label>
        {f.scope === 'Header' ? <label>Header cell<input disabled={busy} aria-label={`Explicit cell ${i + 1}`} value={f.cell ?? ''} maxLength={12} onChange={e => update(i, { cell: e.target.value })} /></label> : <><label>Column<input disabled={busy} aria-label={`Explicit column ${i + 1}`} type="number" min={1} max={32} value={f.column ?? ''} onChange={e => update(i, { column: Number(e.target.value) })} /></label><label>Prototype row<input disabled={busy} aria-label={`Explicit row ${i + 1}`} type="number" min={1} max={1000} value={f.prototypeRow ?? ''} onChange={e => update(i, { prototypeRow: Number(e.target.value) })} /></label></>}
      </details>
      <label><input disabled={busy} type="checkbox" aria-label={`Field required ${i + 1}`} checked={f.required} onChange={e => update(i, { required: e.target.checked })} />Required</label>
      <label><input disabled={busy} type="checkbox" aria-label={`Field omit ${i + 1}`} checked={f.omit} onChange={e => update(i, { omit: e.target.checked })} />Explicitly omit optional field</label>
      <p>{f.rationale}</p><ul>{f.candidates.map((c, n) => <li key={n}>{c.worksheet}!{c.labelCell}: {c.matchedLabel} — {c.explanation}</li>)}</ul>
    </fieldset>)}
    <button disabled={busy || fields.length === 0} type="submit">Apply schema corrections</button><p>Changes clear confirmation, review and approval. Scope changes require compatible destination evidence.</p>
  </form>;
}
export function ManualSampleEditor({ session, busy, act }: { session: StudioSession; busy: boolean; act: Act }) {
  const fields = session.schemaDraft?.fields.filter(f => !f.omit) ?? [];
  const [rows, setRows] = useState(() => Math.max(1, ...session.fields.filter(f => f.row !== null).map(f => f.row! + 1)));
  const [values, setValues] = useState<Record<string, string>>(() => Object.fromEntries(session.fields.map(f => [`${f.scope}:${f.row ?? 0}:${f.key}`, f.value ?? ''])));
  const key = (f: LearnedField, row: number) => `${f.scope}:${row}:${f.key}`;
  return <form onSubmit={e => { e.preventDefault(); act('manual-sample', {
    header: Object.fromEntries(fields.filter(f => f.scope === 'Header').map(f => [f.key, values[key(f, 0)] ?? ''])),
    rows: Array.from({ length: rows }, (_, row) => Object.fromEntries(fields.filter(f => f.scope === 'LineItem').map(f => [f.key, values[key(f, row)] ?? '']))),
  }); }}>
    <p>Manual or synthetic values only. Examples provide layout evidence, never recognized source data. Arithmetic rules are configured explicitly; compare entered values against the source.</p>
    <label>Sample rows<input disabled={busy} aria-label="Sample row…3179 tokens truncated…">{session.schemaDraft.correspondence.sheets.map(s => <li key={s.worksheet}>{s.worksheet}: {s.exampleRowCount} example rows ({s.firstExampleRow}–{s.lastExampleRow}), prototype {s.prototypeRow} · {s.status}. {s.footerEvidence}</li>)}</ul>}
          {session.mapping && <><MultiSheetEditor key={`mapping:${session.profileVersion}`} session={session} busy={busy} act={act} onDirtyChange={reportConfigurationEdit} /><ValidationRuleEditor key={`rules:${session.profileVersion}`} session={session} busy={busy} act={act} onDirtyChange={reportConfigurationEdit} /></>}</>}
        {!session.schemaDraft && session.sheets.length > 0 && <MappingEditor key={`${session.profileVersion}:${session.mappingRevision}`} session={session} busy={busy}
          apply={mapping => act('mapping', { ...mapping }, 'PATCH')} />}
        <button disabled={busy || hasUnsavedConfiguration || session.mappingState !== 'DraftSuggested' || (!!session.schemaDraft && session.schemaDraft.state !== 'ReadyForConfirmation')} onClick={() => act('mapping/confirm')}>Confirm mapping as current user</button>
      </>}
      {step === 2 && session?.schemaDraft && <ManualSampleEditor key={session.profileVersion} session={session} busy={busy} act={act} />}
      {step === 2 && session && !session.schemaDraft && <><h3>Demo Extraction</h3><p>Load the bounded server-owned synthetic fixture through Gate 3B. This does not recognize an image or PDF.</p>
        <button disabled={busy || session.mappingState !== 'UserConfirmed'} onClick={() => act('extract-demo')}>Run Demo Extraction</button>
        <hr /><h3>Document Upload</h3>
        <p>Admission only — OCR recognition is not yet available. Uploading or removing a source clears extracted data, review and approval. Demo Extraction always uses its separate synthetic fixture.</p>
        <p className="uds-muted">PNG: 8-bit RGB/RGBA, no metadata or animation. JPEG: baseline single scan. WebP: simple lossy key frame. PDF: strict single blank-page subset only; ordinary content PDFs are currently unsupported. Maximum 1 MiB per file, 4 sources, 2 MiB shared with the template. Images: at most 262,144 pixels.</p>
        <form className="uds-inline" onSubmit={e => { e.preventDefault(); if (sourceFile) void run(async () => {
          setSession(await api.uploadSource(session, sourceFile)); setNotice('Source admitted. Not recognized. Previous extraction and approval cleared.');
        }); }}>
          <label>Local source document<input aria-label="Source document" type="file" accept=".pdf,.png,.jpg,.jpeg,.webp" onChange={e => setSourceFile(e.target.files?.[0] || null)} /></label>
          <button disabled={busy || !sourceFile} type="submit">Upload source</button>
        </form>
        <div aria-label="Server source metadata" className="uds-sources">{session.sources.length === 0 ? <p>No source documents admitted.</p> : session.sources.map(source => <article key={source.sourceId}>
          <h4>{source.mediaType} · {source.byteLength} bytes</h4><p><strong>Admitted — not recognized</strong></p>
          <dl><dt>Server source ID</dt><dd>{source.sourceId}</dd><dt>SHA-256 of actual bytes</dt><dd>{source.sha256}</dd>
            <dt>Verified metadata</dt><dd>{source.width !== null ? `${source.width} × ${source.height} pixels` : 'Dimensions unavailable'} · {source.pageCount !== null ? `${source.pageCount} page` : 'Page count unavailable'}</dd>
            <dt>Uploaded</dt><dd>{new Date(source.uploadedAt).toLocaleString()}</dd></dl>
          <button disabled={busy} onClick={() => void run(async () => { setSession(await api.deleteSource(session, source.sourceId)); setNotice('Source removed. Previous extraction and approval cleared.'); })}>Remove source</button>
        </article>)}</div></>}
      {step === 3 && session && <><p>Edits are sent to the backend and invalidate earlier approval. Original synthetic observation evidence remains visible separately.</p>
        <div className="uds-scroll"><table><thead><tr><th>Field</th><th>Current server value</th><th>Source evidence</th></tr></thead><tbody>
          {session.fields.map(field => <FieldEditor key={`${session.dataRevision}:${field.scope}:${field.row}:${field.key}`} field={field} busy={busy} displayName={session.schemaDraft?.fields.find(f => f.scope === field.scope && f.key === field.key)?.label}
            edit={(f, value) => act('fields', { scope: f.scope, key: f.key, row: f.row, value }, 'PATCH')} />)}
        </tbody></table></div>
        <h3>Validation results</h3><ul aria-label="Validation results">{session.checks?.map(c => <li key={c.id}>{c.id}: {c.outcome === 'NotEvaluated' ? 'Not evaluated' : c.outcome}{c.required ? ' · Required' : ''}{session.issues.some(i => i.field === c.id) ? ' · See issue below' : ''}</li>)}</ul>
        <h3>Validation and extraction issues</h3>{session.issues.length ? <ul>{session.issues.map((issue, index) => <li key={index}>{issue.severity === 'Warning' ? 'Warning · ' : ''}{issue.message || issue.code} · {issue.field} {issue.rowPosition !== null ? `row ${issue.rowPosition + 1}` : ''}</li>)}</ul> : <p>No current issues reported.</p>}
      </>}
      {step === 4 && session && <><p>Complete review and approval are separate authenticated backend transitions. Neither extraction nor mapping confirmation approves document data.</p>
        <button disabled={busy || !session.canReview} onClick={() => act('review')}>Complete human review</button>
        <button disabled={busy || !session.canApprove} onClick={() => act('approve')}>Approve document</button>
        {!session.canApprove && <p>Your server-validated role does not have approval authority.</p>}</>}
      {step === 5 && session && <><p>The backend rechecks mapping, current validation, human approval, ownership, expiry and concurrency before returning a workbook.</p>
        <button disabled={busy || !session.canApprove} onClick={() => void run(async () => { const blob = await api.export(session); save(blob, 'synthetic-document.xlsx'); setSession(await api.read(session.id)); setNotice('Excel workbook exported.'); })}>Export Excel</button></>}
      <footer className="uds-inline"><button disabled={step === 0} onClick={() => changeStep(step - 1)}>Previous step</button><button disabled={step === 5} onClick={() => changeStep(step + 1)}>Next step</button></footer>
    </section>
  </main>;
}
