import { useEffect, useState } from 'react';
import type { ConfigurationDirtyChange, StudioMapping, StudioRule, StudioSession } from './studioApi';
type Act = (action: string, data?: Record<string, unknown>, method?: string) => void;
export function MultiSheetEditor({ session, busy, act, onDirtyChange }: { session: StudioSession; busy: boolean; act: Act; onDirtyChange?: ConfigurationDirtyChange }) {
  const [sheets, setSheets] = useState<StudioMapping[]>(session.mappings ?? []);
  const dirty = JSON.stringify(sheets) !== JSON.stringify(session.mappings ?? []);
  useEffect(() => { onDirtyChange?.('mapping', dirty); return () => onDirtyChange?.('mapping', false); }, [dirty, onDirtyChange]);
  const fields = session.schemaDraft?.fields.filter(f => !f.omit) ?? [];
  const update = (index: number, patch: Partial<StudioMapping>) => setSheets(sheets.map((s, i) => i === index ? { ...s, ...patch } : s));
  return <form onSubmit={e => { e.preventDefault(); act('multisheet-mapping', { sheets: sheets.map(s => ({ sheet: s.sheet, prototypeRow: s.startRow, headers: s.headers, columns: s.columns })) }, 'PATCH'); }}>
    <h3>Selected data worksheets</h3>
    {dirty && <p role="status">Unsaved worksheet mapping edits. Apply or discard before confirmation or leaving this step.</p>}
    <button type="button" disabled={busy || !dirty} onClick={() => setSheets(session.mappings ?? [])}>Discard worksheet mapping edits</button><p>All selected sheets use the same ordered rows. Up to three data sheets; reference sheets remain unchanged. Each active field needs at least one destination. Select the same field explicitly to reuse it across sheets.</p>
    {sheets.map((sheet, index) => <fieldset key={index}><legend>Data worksheet {index + 1}</legend>
      <label>Worksheet<select disabled={busy} aria-label={`Mapped worksheet ${index + 1}`} value={sheet.sheet} onChange={e => update(index, { sheet: e.target.value })}>{session.sheets.map(s => <option key={s}>{s}</option>)}</select></label>
      <label>Prototype row<input disabled={busy} aria-label={`Mapped prototype ${index + 1}`} type="number" min={1} max={1000} value={sheet.startRow} onChange={e => update(index, { startRow: Number(e.target.value) })} /></label>
      {fields.map(field => {
        const header = field.scope === 'Header'; const destinations = header ? sheet.headers : sheet.columns;
        const shared = sheets.filter(s => (header ? s.headers : s.columns)[field.key] !== undefined).length > 1;
        return <label key={`${field.scope}:${field.key}`}>{field.label} ({field.key}) {shared ? '· Shared source field' : ''}
          <input disabled={busy} aria-label={`Map ${sheet.sheet} ${field.key}`} type={header ? 'text' : 'number'} min={header ? undefined : 1} max={header ? undefined : 32} maxLength={header ? 12 : undefined}
            placeholder={header ? 'Header cell, or leave blank' : 'Column, or leave blank'} value={destinations[field.key] ?? ''}
            onChange={e => { const next = { ...destinations }; if (!e.target.value) delete next[field.key]; else next[field.key] = header ? e.target.value : Number(e.target.value); update(index, header ? { headers: next as Record<string, string> } : { columns: next as Record<string, number> }); }} />
        </label>;
      })}
      <button type="button" disabled={busy} onClick={() => setSheets(sheets.filter((_, i) => i !== index))}>Remove worksheet</button>
    </fieldset>)}
    <button type="button" disabled={busy || sheets.length >= 3} onClick={() => setSheets([...sheets, { sheet: session.sheets.find(s => !sheets.some(m => m.sheet === s)) ?? session.sheets[0], startRow: 1, headers: {}, columns: {} }])}>Add data worksheet</button>
    <button disabled={busy || !sheets.length} type="submit">Apply complete worksheet mapping</button>
  </form>;
}
export function ValidationRuleEditor({ session, busy, act, onDirtyChange }: { session: StudioSession; busy: boolean; act: Act; onDirtyChange?: ConfigurationDirtyChange }) {
  const [rules, setRules] = useState<StudioRule[]>(session.validationRules ?? []);
  const dirty = JSON.stringify(rules) !== JSON.stringify(session.validationRules ?? []);
  useEffect(() => { onDirtyChange?.('rules', dirty); return () => onDirtyChange?.('rules', false); }, [dirty, onDirtyChange]);
  const decimals = session.schemaDraft?.fields.filter(f => !f.omit && f.type === 'Decimal') ?? [];
  const update = (index: number, patch: Partial<StudioRule>) => setRules(rules.map((r, i) => i === index ? { ...r, ...patch } : r));
  const options = (scope: string) => <><option value="">Choose a Decimal field</option>{decimals.filter(f => f.scope === scope).map(f => <option key={f.key} value={f.key}>{f.label} ({f.key})</option>)}</>;
  return <form onSubmit={e => { e.preventDefault(); act('validation-rules', { rules }, 'PATCH'); }}>
    <h3>Business consistency rules</h3>
    {dirty && <p role="status">Unsaved validation rule edits. Apply or discard before confirmation or leaving this step.</p>}
    <button type="button" disabled={busy || !dirty} onClick={() => setRules(session.validationRules ?? [])}>Discard validation rule edits</button><p>Up to eight checks. Checks compare entered printed values; they do not establish source accuracy or replace amounts. Policy changes require renewed confirmation, review and approval. Tolerance is between 0 and 1.</p>
    {rules.map((rule, index) => <fieldset key={index}><legend>Rule {index + 1}</legend>
      <label>Rule name<input disabled={busy} aria-label={`Rule name ${index + 1}`} value={rule.id} maxLength={64} onChange={e => update(index, { id: e.target.value })} /></label>
      <label>Rule type<select disabled={busy} aria-label={`Rule type ${index + 1}`} value={rule.kind} onChange={e => update(index, { kind: e.target.value, target: '', operator: e.target.value === 'Arithmetic' ? 'Multiply' : null, fieldB: e.target.value === 'Arithmetic' ? '' : null })}><option value="Arithmetic">Row arithmetic</option><option value="CrossFoot">Column total</option></select></label>
      <label>Source field A<select disabled={busy} aria-label={`Rule A ${index + 1}`} value={rule.fieldA} onChange={e => update(index, { fieldA: e.target.value })}>{options('LineItem')}</select></label>
      {rule.kind === 'Arithmetic' && <><label>Operator<select disabled={busy} aria-label={`Rule operator ${index + 1}`} value={rule.operator ?? 'Multiply'} onChange={e => update(index, { operator: e.target.value })}>{['Add', 'Subtract', 'Multiply', 'Divide'].map(o => <option key={o}>{o}</option>)}</select></label>
        <label>Source field B<select disabled={busy} aria-label={`Rule B ${index + 1}`} value={rule.fieldB ?? ''} onChange={e => update(index, { fieldB: e.target.value })}>{options('LineItem')}</select></label></>}
      <label>Printed {rule.kind === 'Arithmetic' ? 'row result' : 'document total'}<select disabled={busy} aria-label={`Rule target ${index + 1}`} value={rule.target} onChange={e => update(index, { target: e.target.value })}>{options(rule.kind === 'Arithmetic' ? 'LineItem' : 'Header')}</select></label>
      <label>Tolerance<input disabled={busy} aria-label={`Rule tolerance ${index + 1}`} type="number" min={0} max={1} step="any" value={rule.tolerance} onChange={e => update(index, { tolerance: Number(e.target.value) })} /></label>
      <label><input disabled={busy} aria-label={`Rule required ${index + 1}`} type="checkbox" checked={rule.required} onChange={e => update(index, { required: e.target.checked })} />Required check</label>
      <label>Severity<select disabled={busy} aria-label={`Rule severity ${index + 1}`} value={rule.severity} onChange={e => update(index, { severity: e.target.value })}><option value="BlockingError">Blocking error</option><option value="Warning">Warning (review still required)</option></select></label>
      <button type="button" disabled={busy} onClick={() => setRules(rules.filter((_, i) => i !== index))}>Remove rule</button>
      <button type="button" disabled={busy || index === 0} onClick={() => { const next = [...rules]; [next[index - 1], next[index]] = [next[index], next[index - 1]]; setRules(next); }}>Move rule up</button>
    </fieldset>)}
    <button type="button" disabled={busy || rules.length >= 8} onClick={() => setRules([...rules, { id: Array.from({ length: 9 }, (_, i) => `rule${i + 1}`).find(id => !rules.some(rule => rule.id === id))!, kind: 'Arithmetic', fieldA: '', operator: 'Multiply', fieldB: '', target: '', tolerance: 0, required: true, severity: 'BlockingError' }])}>Add validation rule</button>
    <button type="submit" disabled={busy}>Apply validation rules</button>
  </form>;
}
