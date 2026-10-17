import { useState } from 'react';
import type { LearnedField, StudioSession } from './studioApi';
type Act = (action: string, data?: Record<string, unknown>, method?: string) => void;
export function SchemaEditor({ session, busy, act }: { session: StudioSession; busy: boolean; act: Act }) {
  const [fields, setFields] = useState<LearnedField[]>(session.schemaDraft?.fields ?? []);
  const update = (index: number, patch: Partial<LearnedField>) => setFields(fields.map((f, i) => i === index ? { ...f, ...patch } : f));
  return <form onSubmit={e => { e.preventDefault(); act('schema', { fields: fields.map(({ key, label, scope, type, required, omit, candidate, worksheet, cell, column, prototypeRow }) => ({ key, label, scope, type, required, omit, candidate, worksheet, cell, column, prototypeRow })) }, 'PATCH'); }}>
    <p>Schema: {session.schemaDraft?.state} · Revision {session.schemaDraft?.revision}. One data worksheet and one prototype row. Reference sheets are preserved. Numeric identifiers default to Text; select Decimal after checking the meaning.</p>
    <ul>{session.sheetSummaries.map(s => <li key={s.name}>{s.name}: {s.usedCells} used cells · {s.tables} table candidates · prototype rows {s.prototypeRows.join(', ') || 'none'} · footer rows {s.footerRows.join(', ') || 'none'} · merges {s.mergedRanges.join(', ') || 'none'}</li>)}</ul>
    {fields.length === 0 && <p role="alert">No supported candidates. Use bold table headers, one materialized prototype and adjacent header value slots.</p>}
    {fields.map((f, i) => <fieldset key={i}><legend>{f.label} — {f.status}</legend>
      <label>Field label<input aria-label={`Field label ${i + 1}`} value={f.label} maxLength={256} onChange={e => update(i, { label: e.target.value })} /></label>
      <label>Field key<input aria-label={`Field key ${i + 1}`} value={f.key} maxLength={64} onChange={e => update(i, { key: e.target.value })} /></label>
      <label>Scope<select aria-label={`Field scope ${i + 1}`} value={f.scope} onChange={e => update(i, { scope: e.target.value })}><option>Header</option><option>LineItem</option></select></label>
      <label>Type<select aria-label={`Field type ${i + 1}`} value={f.type} onChange={e => update(i, { type: e.target.value })}>{['Text', 'Decimal', 'Date', 'Boolean'].map(t => <option key={t}>{t}</option>)}</select></label>
      <label>Destination<select aria-label={`Field destination ${i + 1}`} value={f.candidate ?? ''} onChange={e => update(i, { candidate: e.target.value === '' ? null : Number(e.target.value) })}><option value="">Unresolved — choose evidence</option>{f.candidates.map((c, n) => <option key={n} value={n}>{c.worksheet}!{c.cell ?? `column ${c.column}, row ${c.prototypeRow}`} · label {c.labelCell}</option>)}</select></label>
      <details><summary>Correct scope or choose another existing template cell</summary>
        <label>Explicit worksheet<select aria-label={`Explicit worksheet ${i + 1}`} value={f.worksheet ?? ''} onChange={e => update(i, { worksheet: e.target.value || undefined })}><option value="">Use candidate above</option>{session.sheets.map(s => <option key={s}>{s}</option>)}</select></label>
        {f.scope === 'Header' ? <label>Header cell<input aria-label={`Explicit cell ${i + 1}`} value={f.cell ?? ''} maxLength={12} onChange={e => update(i, { cell: e.target.value })} /></label> : <><label>Column<input aria-label={`Explicit column ${i + 1}`} type="number" min={1} max={32} value={f.column ?? ''} onChange={e => update(i, { column: Number(e.target.value) })} /></label><label>Prototype row<input aria-label={`Explicit row ${i + 1}`} type="number" min={1} max={1000} value={f.prototypeRow ?? ''} onChange={e => update(i, { prototypeRow: Number(e.target.value) })} /></label></>}
      </details>
      <label><input type="checkbox" aria-label={`Field required ${i + 1}`} checked={f.required} onChange={e => update(i, { required: e.target.checked })} />Required</label>
      <label><input type="checkbox" aria-label={`Field omit ${i + 1}`} checked={f.omit} onChange={e => update(i, { omit: e.target.checked })} />Explicitly omit optional field</label>
      <p>{f.rationale}</p><ul>{f.candidates.map((c, n) => <li key={n}>{c.worksheet}!{c.labelCell}: {c.matchedLabel} — {c.explanation}</li>)}</ul>
    </fieldset>)}
    <button disabled={busy || fields.length === 0} type="submit">Apply schema corrections</button><p>Changes clear confirmation, review and approval. Scope changes require compatible destination evidence.</p>
  </form>;
}
export function ManualSampleEditor({ session, busy, act }: { session: StudioSession; busy: boolean; act: Act }) {
  const fields = session.schemaDraft?.fields.filter(f => !f.omit) ?? [];
  const [rows, setRows] = useState(1); const [values, setValues] = useState<Record<string, string>>({});
  const key = (f: LearnedField, row: number) => `${f.scope}:${row}:${f.key}`;
  return <form onSubmit={e => { e.preventDefault(); act('manual-sample', {
    header: Object.fromEntries(fields.filter(f => f.scope === 'Header').map(f => [f.key, values[key(f, 0)] ?? ''])),
    rows: Array.from({ length: rows }, (_, row) => Object.fromEntries(fields.filter(f => f.scope === 'LineItem').map(f => [f.key, values[key(f, row)] ?? '']))),
  }); }}>
    <p>Manual or synthetic values only. Examples provide layout evidence, never recognized source data. No arithmetic rules are inferred; check printed totals yourself.</p>
    <label>Sample rows<input aria-label="Sample rows" type="number" min={1} max={20} value={rows} onChange={e => setRows(Math.max(1, Math.min(20, Number(e.target.value) || 1)))} /></label>
    {fields.flatMap(f => Array.from({ length: f.scope === 'Header' ? 1 : rows }, (_, row) => <label key={key(f, row)}>{f.label} ({f.type}){f.scope === 'LineItem' ? ` row ${row + 1}` : ''}<input aria-label={`Sample ${f.key} ${row + 1}`} value={values[key(f, row)] ?? ''} maxLength={2000} onChange={e => setValues({ ...values, [key(f, row)]: e.target.value })} /></label>))}
    <button disabled={busy || session.mappingState !== 'UserConfirmed'} type="submit">Validate manual sample</button>
  </form>;
}
