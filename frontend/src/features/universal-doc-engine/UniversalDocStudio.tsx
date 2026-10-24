import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { StudioApi, type ConfigurationSection, type StudioBootstrap, type StudioDraft, type StudioField, type StudioMapping, type StudioSession } from './studioApi';
import './studio.css';
import { MultiSheetEditor, ValidationRuleEditor } from './WorkflowConfigurationEditor';
import { SchemaEditor, ManualSampleEditor } from './ExampleTemplateEditor';

const steps = ['Session & template', 'Mapping', 'Demo extraction', 'Data review', 'Human approval', 'Excel export'];
function save(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob); const link = document.createElement('a'); link.href = url; link.download = filename;
  link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
}
function MappingEditor({ session, busy, apply }: { session: StudioSession; busy: boolean; apply: (mapping: StudioMapping) => void }) {
  const [mapping, setMapping] = useState<StudioMapping>(() => session.mapping || {
    sheet: session.sheets[0] || 'Document', startRow: 6, headers: { number: 'B1', total: 'B3' },
    columns: { description: 1, quantity: 2, price: 3, amount: 4 },
  });
  return <form onSubmit={event => { event.preventDefault(); apply(mapping); }}>
    <div className="uds-grid">
      <label>Worksheet<select aria-label="Mapping worksheet" value={mapping.sheet} onChange={e => setMapping({ ...mapping, sheet: e.target.value })}>
        {session.sheets.map(sheet => <option key={sheet}>{sheet}</option>)}
      </select></label>
      <label>Prototype row<input aria-label="Prototype row" type="number" min="1" max="1000" value={mapping.startRow} onChange={e => setMapping({ ...mapping, startRow: Number(e.target.value) })} /></label>
      {Object.entries(mapping.headers).map(([key, cell]) => <label key={key}>Header: {key}<input aria-label={`Mapping ${key}`} value={cell} maxLength={12}
        onChange={e => setMapping({ ...mapping, headers: { ...mapping.headers, [key]: e.target.value } })} /></label>)}
      {Object.entries(mapping.columns).map(([key, column]) => <label key={key}>Column: {key}<input aria-label={`Mapping ${key}`} type="number" min="1" max="32" value={column}
        onChange={e => setMapping({ ...mapping, columns: { ...mapping.columns, [key]: Number(e.target.value) } })} /></label>)}
    </div>
    <button disabled={busy} type="submit">Apply mapping correction</button>
    <p className="uds-muted">Applying a correction clears confirmation and any earlier document approval. Confirmation remains a separate action.</p>
  </form>;
}
function FieldEditor({ field, busy, edit, displayName, onDirtyChange }: { onDirtyChange: (id: string, dirty: boolean) => void; displayName?: string; field: StudioField; busy: boolean; edit: (field: StudioField, value: string) => void }) {
  const [value, setValue] = useState(field.value ?? '');
  const editId = `${field.scope}:${field.row}:${field.key}`;
  useEffect(() => {
    onDirtyChange(editId, value !== (field.value ?? ''));
    return () => onDirtyChange(editId, false);
  }, [editId, value, field.value, onDirtyChange]);
  const label = `${field.scope} ${field.row === null ? '' : `row ${field.row + 1} `}${displayName || field.key}`;
  return <tr>
    <th scope="row">{label}<small>{field.type} · {field.source} · {field.review}</small></th>
    <td><form className="uds-inline" onSubmit={e => { e.preventDefault(); edit(field, value); }}>
      <input aria-label={label} value={value} maxLength={2000} onChange={e => setValue(e.target.value)} />
      <button disabled={busy} type="submit" aria-label={`Save ${label}`}>Save</button>
    </form></td>
    <td><span>{field.sourceId || 'Manual edit / no source claim'}{field.page ? ` · page ${field.page}` : ''}</span>
      {field.region && <small>Region {field.region.x}, {field.region.y}, {field.region.width}, {field.region.height}</small>}
      <small>Original observations: {field.rawObservations.join(' | ') || 'Not observed'}</small>
    </td>
  </tr>;
}
export default function UniversalDocStudio() {
  const { user, logout } = useAuth();
  const [api] = useState(() => new StudioApi()); const [bootstrap, setBootstrap] = useState<StudioBootstrap | null>(null);
  const [session, setSession] = useState<StudioSession | null>(null); const [workflow, setWorkflow] = useState('invoice');
  const [file, setFile] = useState<File | null>(null); const [step, setStep] = useState(0);
  const [drafts, setDrafts] = useState<StudioDraft[]>([]);
  const [savedDraft, setSavedDraft] = useState<{ sessionId: string; version: number; draft: StudioDraft } | null>(null);
  const [mode, setMode] = useState('demo');
  const [example, setExample] = useState<File | null>(null);
  const [sourceFile, setSourceFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [notice, setNotice] = useState('');
  const [configurationEdits, setConfigurationEdits] = useState<Partial<Record<ConfigurationSection, boolean>>>({});
  const reportConfigurationEdit = useCallback((section: ConfigurationSection, dirty: boolean) => {
    setConfigurationEdits(previous => previous[section] === dirty ? previous : { ...previous, [section]: dirty });
  }, []);
  const [fieldEdits, setFieldEdits] = useState<Record<string, boolean>>({});
  const reportFieldEdit = useCallback((id: string, dirty: boolean) => {
    setFieldEdits(previous => previous[id] === dirty ? previous : { ...previous, [id]: dirty });
  }, []);
  const hasUnsavedFields = Object.values(fieldEdits).some(Boolean);
  const hasUnsavedConfiguration = Object.values(configurationEdits).some(Boolean) || hasUnsavedFields;
  function changeStep(next: number) {
    if (busy) return;
    if (hasUnsavedConfiguration && next !== step) { setError('Apply or discard unsaved edits before changing steps. Server confirmation covers saved configuration only.'); return; }
    setStep(next);
  }
  useEffect(() => {
    const abort = new AbortController();
    void api.bootstrap(abort.signal).then(async bootstrap => {
      if (abort.signal.aborted) return;
      setBootstrap(bootstrap);
      const availableDrafts = bootstrap.draftsEnabled ? await api.drafts() : [];
      if (abort.signal.aborted) return;
      setDrafts(availableDrafts);
      const id = sessionStorage.getItem('universal-doc-session');
      if (id) {
        try {
          const restored = await api.read(id);
          if (!abort.signal.aborted) {
            setSession(restored);
            try {
              const link = JSON.parse(sessionStorage.getItem('universal-doc-draft-link') || 'null') as { sessionId?: string; draftId?: string; revision?: number } | null;
              const draft = availableDrafts.find(d => d.id === link?.draftId);
              if (link?.sessionId === restored.id && draft && Number.isSafeInteger(link.revision) && link.revision! > 0)
                setSavedDraft({ sessionId: restored.id, version: -1, draft: { ...draft, revision: link.revision! } });
            } catch { /* Convenience metadata never restores content or Saved authority. */ }
          }
        }
        catch { sessionStorage.removeItem('universal-doc-session'); }
      }
    }).catch((reason: unknown) => { if (!abort.signal.aborted) setError(reason instanceof Error ? reason.message : 'Unable to open Studio'); });
    return () => abort.abort();
  }, [api]);
  useEffect(() => { if (session) sessionStorage.setItem('universal-doc-session', session.id); }, [session]);
  useEffect(() => { if (savedDraft) sessionStorage.setItem('universal-doc-draft-link', JSON.stringify({ sessionId: savedDraft.sessionId, draftId: savedDraft.draft.id, revision: savedDraft.draft.revision })); }, [savedDraft]);
  async function run(operation: () => Promise<void>) {
    setBusy(true); setError(''); setNotice('');
    try { await operation(); } catch (reason) { setSavedDraft(previous => previous ? { ...previous, version: -1 } : null); setError(reason instanceof Error ? reason.message : 'Studio request failed'); }
    finally { setBusy(false); }
  }
  function act(action: string, data: Record<string, unknown> = {}, method = 'POST') {
    const section = ({ schema: 'schema', 'multisheet-mapping': 'mapping', 'validation-rules': 'rules', 'manual-sample': 'sample' } as Record<string, ConfigurationSection>)[action];
    if ((hasUnsavedFields && action !== 'fields') || Object.entries(configurationEdits).some(([name, dirty]) => dirty && name !== section)) {
      setError('Apply or discard the other unsaved configuration edits first. Server confirmation covers saved configuration only.'); return;
    }
    if (session) void run(async () => { setSession(await api.act(session, action, data, method)); setNotice('Server state updated.'); });
  }
  return <main className="uds">
    <header className="uds-top"><div><p className="uds-eyebrow">SHOEDOCX · INTERNAL DEMO</p><h1>Universal Document Studio</h1>
      <p>Review a template, normalize synthetic observations, and export after human approval.</p></div>
      <div><span>{user?.fullName}</span><a href="/">Legacy workspace</a><button onClick={logout}>Sign out</button></div>
    </header>
    <p className="uds-banner">Demo extraction and private document intake. No OCR recognition or AI calls. Sessions expire after 20 minutes and disappear when the backend restarts.</p>
    {bootstrap?.draftsEnabled && <section className="uds-panel" aria-label="Durable drafts">
      <h2>My drafts</h2><p>Explicit save of server-applied state only. Maximum two private drafts, retained for at most seven days. Resuming creates a new session and requires fresh confirmation, review and approval.</p>
      <button disabled={busy} onClick={() => void run(async () => { setDrafts(await api.drafts()); })}>Refresh my drafts</button>
      {session?.workflow === 'example-template' && <>
        <p role="status">{savedDraft?.sessionId === session.id && savedDraft.version === session.version && !hasUnsavedConfiguration ? `Draft saved at ${new Date(savedDraft.draft.updatedAt).toLocaleString()}` : 'Current work is not saved as a durable draft. Apply form edits and save explicitly.'}</p>
        <button disabled={busy || hasUnsavedConfiguration || !session.schemaDraft} onClick={() => void run(async () => {
          const result = await api.saveDraft(session, savedDraft?.sessionId === session.id ? savedDraft.draft : undefined);
          setSession(result.session); setSavedDraft({ sessionId: result.session.id, version: result.session.version, draft: result.draft });
          setDrafts(await api.drafts()); setNotice('Draft saved durably.');
        })}>Save draft</button>
        {savedDraft?.sessionId === session.id && <button disabled={busy || hasUnsavedConfiguration} onClick={() => void run(async () => {
          const result = await api.saveDraft(session); setSession(result.session);
          setSavedDraft({ sessionId: result.session.id, version: result.session.version, draft: result.draft });
          setDrafts(await api.drafts()); setNotice('A separate new draft was saved.');
        })}>Save as a new draft</button>}
      </>}
      {drafts.map(draft => <article key={draft.id} className="uds-state"><strong>Example workflow draft</strong><p>{draft.workflow} · Revision {draft.revision} · Updated {new Date(draft.updatedAt).toLocaleString()} · Expires {new Date(draft.expiresAt).toLocaleString()}</p>
        {session && savedDraft?.sessionId === session.id && savedDraft.draft.id === draft.id && savedDraft.draft.revision !== draft.revision && <>
          <p>Another request updated this draft. Resume that version, or choose to replace it with this session on the next save.</p>
          <button disabled={busy} onClick={() => setSavedDraft({ sessionId: session.id, version: -1, draft })}>Replace this draft on next save</button>
        </>}
        <button disabled={busy || hasUnsavedConfiguration} aria-label={`Resume draft ${draft.id}`} onClick={() => void run(async () => {
          const restored = await api.resumeDraft(draft); setSession(restored); setSavedDraft({ sessionId: restored.id, version: restored.version, draft }); setStep(1);
          setNotice('Draft resumed in a new session. Confirm mapping and repeat human review and approval before export.');
        })}>Resume in new session</button>
        <button disabled={busy} aria-label={`Delete draft ${draft.id}`} onClick={() => void run(async () => { await api.deleteDraft(draft); setDrafts(await api.drafts()); if (savedDraft?.draft.id === draft.id) setSavedDraft(null); setNotice('Draft deleted.'); })}>Delete draft</button>
      </article>)}
    </section>}
    <nav aria-label="Studio steps" className="uds-steps">{steps.map((name, index) => <button key={name} aria-current={step === index ? 'step' : undefined}
      onClick={() => changeStep(index)}><span>{index + 1}</span>{name}</button>)}</nav>
    {error && <div role="alert" className="uds-error">{error}{(error.includes('409') || error.includes('DRAFT_STORAGE_UNAVAILABLE') || error.includes('DRAFT_PAYLOAD_LIMIT')) && session && <button disabled={busy} onClick={() => void run(async () => { setSession(await api.read(session.id)); })}>Reload server state</button>}</div>}
    {hasUnsavedConfiguration && <p role="status" className="uds-error">Unsaved configuration edits are not confirmed server state. Apply or discard them before continuing.</p>}
    {notice && <p role="status" className="uds-success">{notice}</p>}
    {session && <aside className="uds-state">Server mapping: <strong>{session.mappingState}</strong> · Review: <strong>{session.reviewState}</strong> · Session version {session.version}
      {' · '}Profile {session.profileVersion} · Data revision {session.dataRevision ?? '—'} · Expires {new Date(session.expiresAt).toLocaleTimeString()}
      <button disabled={busy} onClick={() => void run(async () => { await api.remove(session); sessionStorage.removeItem('universal-doc-session'); setSession(null); setStep(0); })}>Discard session</button></aside>}
    <section className="uds-panel" aria-busy={busy}>
      <h2>{step + 1}. {steps[step]}</h2>
      {step === 0 && <>
        {!session && <label>Template mode<select aria-label="Template mode" value={mode} onChange={e => setMode(e.target.value)}><option value="demo">Synthetic Demo</option><option value="example">Example-Based Template</option></select></label>}
        {!session ? <div className="uds-inline"><label>Supported workflow<select aria-label="Supported workflow" value={workflow} onChange={e => setWorkflow(e.target.value)}>
          {(bootstrap?.workflows || []).map(name => <option key={name}>{name}</option>)}</select></label>
          <button disabled={busy || !bootstrap} onClick={() => void run(async () => { setSession(await api.create(mode === 'example' ? 'example-template' : workflow)); })}>Create private session</button></div>
          : <p>Current workflow: {session.workflow}. Upload a supported template, or start with the synthetic sample.</p>}
        <button disabled={busy || !bootstrap} onClick={() => void run(async () => save(await api.sample(session?.workflow === 'example-template' ? workflow : session?.workflow || workflow), 'synthetic-template.xlsx'))}>Download synthetic template</button>
        {session && <form className="uds-inline" onSubmit={e => { e.preventDefault(); if (file) void run(async () => { setSession(await api.upload(session, file)); setStep(1); }); }}>
          <label>Excel template (.xlsx, maximum 2 MiB)<input type="file" aria-label="Excel template" accept=".xlsx" onChange={e => setFile(e.target.files?.[0] || null)} /></label>
          <button disabled={busy || !file} type="submit">Upload and analyze</button></form>}
      </>}
      {step > 0 && !session && <p>Create a private backend session in Step 1 first.</p>}
      {step === 1 && session && <>
        <p>Detected worksheets: {session.sheets.join(', ') || 'Upload a template first'}. Suggestions never constitute confirmation.</p>
        <ul>{session.suggestions.map(s => <li key={`${s.scope}:${s.key}`}><strong>{s.scope}.{s.key}</strong> — {s.status}: {s.candidates.map(c => `${c.sheet}!${c.cell || `column ${c.column}, row ${c.prototypeRow ?? '?'}`}`).join('; ') || 'No candidate'}</li>)}</ul>
        {session.schemaDraft && <><SchemaEditor key={session.schemaDraft.revision} session={session} busy={busy} act={act} onDirtyChange={reportConfigurationEdit} />
          <form onSubmit={e => { e.preventDefault(); if (example) void run(async () => { setSession(await api.upload(session, example, 'example')); }); }}>
            <label>Optional filled example<input aria-label="Filled example" type="file" accept=".xlsx" onChange={e => setExample(e.target.files?.[0] || null)} /></label><button disabled={busy || hasUnsavedConfiguration || !example} type="submit">Compare filled example</button>
          </form><p>One to five contiguous example rows, with verified formulas and footer relocation. Examples never confirm a schema.</p>
          {session.schemaDraft.correspondence && <ul aria-label="Example correspondence">{session.schemaDraft.correspondence.sheets.map(s => <li key={s.worksheet}>{s.worksheet}: {s.exampleRowCount} example rows ({s.firstExampleRow}–{s.lastExampleRow}), prototype {s.prototypeRow} · {s.status}. {s.footerEvidence}</li>)}</ul>}
          {session.mapping && <><MultiSheetEditor key={`mapping:${session.profileVersion}`} session={session} busy={busy} act={act} onDirtyChange={reportConfigurationEdit} /><ValidationRuleEditor key={`rules:${session.profileVersion}`} session={session} busy={busy} act={act} onDirtyChange={reportConfigurationEdit} /></>}</>}
        {!session.schemaDraft && session.sheets.length > 0 && <MappingEditor key={`${session.profileVersion}:${session.mappingRevision}`} session={session} busy={busy}
          apply={mapping => act('mapping', { ...mapping }, 'PATCH')} />}
        <button disabled={busy || hasUnsavedConfiguration || session.mappingState !== 'DraftSuggested' || (!!session.schemaDraft && session.schemaDraft.state !== 'ReadyForConfirmation')} onClick={() => act('mapping/confirm')}>Confirm mapping as current user</button>
      </>}
      {step === 2 && session?.schemaDraft && <ManualSampleEditor key={`${session.profileVersion}:${session.dataRevision}`} session={session} busy={busy} act={act} onDirtyChange={reportConfigurationEdit} />}
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
          {session.fields.map(field => <FieldEditor key={`${session.dataRevision}:${field.scope}:${field.row}:${field.key}`} field={field} busy={busy} onDirtyChange={reportFieldEdit} displayName={session.schemaDraft?.fields.find(f => f.scope === field.scope && f.key === field.key)?.label}
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
