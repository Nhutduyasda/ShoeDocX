export type ConfigurationSection = 'schema' | 'mapping' | 'rules' | 'sample';
export type ConfigurationDirtyChange = (section: ConfigurationSection, dirty: boolean) => void;
export interface StudioRule { id: string; kind: string; fieldA: string; operator: string | null; fieldB: string | null; target: string; tolerance: number; required: boolean; severity: string }
export interface LearnedField {
  key: string; label: string; scope: string; type: string; required: boolean; omit: boolean; candidate: number | null; status: string; rationale: string;
  worksheet?: string; cell?: string; column?: number; prototypeRow?: number;
  candidates: { worksheet: string; labelCell: string; matchedLabel: string; cell: string | null; column: number | null; prototypeRow: number | null; explanation: string }[];
}
export interface StudioField {
  scope: string; key: string; row: number | null; type: string; value: string | null;
  source: string; review: string; sourceId: string | null; page: number | null;
  region: { x: number; y: number; width: number; height: number } | null; rawObservations: string[];
}
export interface StudioMapping { sheet: string; startRow: number; headers: Record<string, string>; columns: Record<string, number> }
export interface StudioSource {
  sourceId: string; sha256: string; byteLength: number; mediaType: string; uploadedAt: string;
  width: number | null; height: number | null; pageCount: number | null; status: string;
}
export interface StudioSession {
  mappings: StudioMapping[]; validationRules: StudioRule[]; checks: { id: string; outcome: string; required: boolean }[];
  id: string; version: number; expiresAt: string; workflow: string; mappingState: string;
  mappingRevision: number | null; profileVersion: number; dataRevision: number | null; reviewState: string;
  sheets: string[]; mapping: StudioMapping | null; canReview: boolean; canApprove: boolean;
  suggestions: { scope: string; key: string; status: string; candidates: { sheet: string; labelCell: string; cell: string | null; column: number | null; prototypeRow: number | null }[] }[];
  fields: StudioField[]; issues: { code: string; field: string | null; rowId: string | null; rowPosition: number | null; message?: string; severity?: string }[];
  schemaDraft: { revision: number; state: string; templateSha256: string; exampleSha256: string | null; fields: LearnedField[]; correspondence?: { supported: boolean; sheets: { worksheet: string; prototypeRow: number; firstExampleRow: number; lastExampleRow: number; exampleRowCount: number; status: string; footerEvidence: string }[]; problems: string[] } } | null;
  sheetSummaries: { name: string; usedCells: number; tables: number; prototypeRows: number[]; footerRows: number[]; mergedRanges: string[] }[];
  sources: StudioSource[];
}
export interface StudioBootstrap { draftsEnabled?: boolean; csrfToken: string; workflows: string[]; canReview: boolean; canApprove: boolean }

export interface StudioDraft { id: string; workflow: string; revision: number; createdAt: string; updatedAt: string; expiresAt: string }
export class StudioApi {
  private csrf = '';
  private readonly base = `${(import.meta.env.VITE_API_URL || '/api').replace(/\/$/, '')}/universal-doc`;
  private async request(path: string, method = 'GET', body?: unknown, signal?: AbortSignal): Promise<Response> {
    const form = body instanceof FormData;
    const response = await fetch(this.base + path, {
      method, credentials: 'include', signal,
      headers: method === 'GET' ? {} : { 'X-Studio-CSRF': this.csrf, ...(!form ? { 'Content-Type': 'application/json' } : {}) },
      body: body === undefined ? undefined : form ? body : JSON.stringify(body),
    });
    if (!response.ok) {
      if (response.status === 401) window.dispatchEvent(new Event('auth:unauthorized'));
      const error = await response.json().catch(() => ({})) as { code?: string; title?: string; details?: string[] };
      const code = error.code || error.title || 'STUDIO_REQUEST_FAILED';
      const messages: Record<string, string> = {
        DRAFT_REVISION_CONFLICT: 'This draft changed. Refresh My drafts before choosing to save or resume again.',
        DRAFT_EXPIRED: 'This draft has expired and cannot be resumed.',
        DRAFT_NOT_FOUND: 'This private draft is unavailable or has been deleted.',
        DRAFT_CONFIGURATION_UNAVAILABLE: 'Durable drafts are unavailable. Ask the administrator to check protected storage configuration.',
        DRAFT_STORAGE_UNAVAILABLE: 'The durable save did not complete. Reload server state and My drafts before retrying.',
        DRAFT_CAPACITY_REACHED: 'Draft storage is full. Delete an old draft before creating another.',
        DRAFT_PAYLOAD_LIMIT: 'This draft exceeds the bounded storage limit. It has not been saved.',
        DRAFT_PAYLOAD_INVALID: 'This stored draft could not be verified. Recovery was blocked.',
        MAPPING_CONFIRMATION_REQUIRED: 'Review and confirm the template mapping before continuing.',
        MAPPING_CORRECTION_REQUIRED: 'Correct unresolved mapping fields before confirming.',
        CURRENT_APPROVAL_REQUIRED: 'The current document needs human review and approval.',
        CURRENT_HUMAN_REVIEW_REQUIRED: 'Complete human review before approving this document.',
        DOCUMENT_VALIDATION_REQUIRED: 'Correct the current validation issues before continuing.',
        SESSION_VERSION_CONFLICT: 'Another request changed this session. Reload server state and retry.',
        SESSION_EXPIRED: 'This session expired. Start a new private session.',
        SESSION_NOT_FOUND: 'This private session is unavailable.',
        STUDIO_MEMBERSHIP_REQUIRED: 'Your account needs an explicit server-side Studio membership.',
        INVALID_EDITED_VALUE: 'Use a valid value for this field type.',
        STUDIO_BUSY: 'The Studio is busy. Please retry shortly.',
        EXAMPLE_STRUCTURE_MISMATCH: 'The filled example must preserve sheet names, cell positions, headings, formulas, merges and supported formatting. One to five contiguous example rows are supported with verified footer relocation.',
        SESSION_BYTE_LIMIT: 'Template, example and sources together must fit within the existing 2 MiB session budget.',
        UNRESOLVED_SCHEMA_FIELD: 'Choose a destination or explicitly omit this optional field.',
        INVALID_SCHEMA_FIELDS: 'Check field keys, types, required/omitted settings and duplicate fields.',
        INVALID_SCHEMA_MAPPING: 'Destinations must be unique, with header cells above the repeat region and one supported prototype row.',
        INVALID_SCHEMA_DESTINATION: 'Choose an existing unmerged cell without a formula, inside the supported region.',
        IDENTIFIER_REQUIRES_TEXT: 'Leading-zero identifiers must remain Text to preserve their values.',
        UNSUPPORTED_SCOPE_DESTINATION: 'After changing scope, choose a compatible existing destination under the correction controls.',
        SINGLE_SHEET_EDITOR_REQUIRED: 'This editor supports one data worksheet. Explicitly omit fields on other sheets.',
        SINGLE_PROTOTYPE_REQUIRED: 'Choose the same single supported prototype row for all line fields.',
        SCHEMA_CORRECTION_REQUIRED: 'Apply your field corrections before confirming the schema and mapping.',
        UNSUPPORTED_OR_INVALID_TEMPLATE: 'This workbook contains an unsupported or invalid structure. Use the documented bounded XLSX subset.',
      };
      throw new Error(`${messages[code] || code}${error.details?.length ? ` Details: ${error.details.join("; ")}` : ""} [${code}] (HTTP ${response.status})`);
    }
    return response;
  }
  async bootstrap(signal?: AbortSignal): Promise<StudioBootstrap> {
    const data = await (await this.request('/bootstrap', 'GET', undefined, signal)).json() as StudioBootstrap;
    this.csrf = data.csrfToken; return data;
  }
  async drafts(): Promise<StudioDraft[]> { return (await this.request('/drafts')).json(); }
  async saveDraft(session: StudioSession, draft?: StudioDraft): Promise<{ draft: StudioDraft; session: StudioSession }> {
    return (await this.request(`/sessions/${session.id}/save-draft`, 'POST', { expectedVersion: session.version, draftId: draft?.id ?? null, expectedRevision: draft?.revision ?? 0 })).json();
  }
  async resumeDraft(draft: StudioDraft): Promise<StudioSession> { return (await this.request(`/drafts/${draft.id}/resume`, 'POST', { expectedRevision: draft.revision })).json(); }
  async deleteDraft(draft: StudioDraft): Promise<void> { await this.request(`/drafts/${draft.id}`, 'DELETE', { expectedRevision: draft.revision }); }
  async create(workflow: string): Promise<StudioSession> { return (await this.request('/sessions', 'POST', { workflow })).json(); }
  async read(id: string): Promise<StudioSession> { return (await this.request(`/sessions/${encodeURIComponent(id)}`)).json(); }
  async act(session: StudioSession, action: string, data: Record<string, unknown> = {}, method = 'POST'): Promise<StudioSession> {
    return (await this.request(`/sessions/${session.id}/${action}`, method, { expectedVersion: session.version, ...data })).json();
  }
  async upload(session: StudioSession, file: File, action = 'template'): Promise<StudioSession> {
    const form = new FormData(); form.set('file', file); form.set('expectedVersion', String(session.version));
    return (await this.request(`/sessions/${session.id}/${action}`, 'POST', form)).json();
  }
  async export(session: StudioSession): Promise<Blob> {
    return (await this.request(`/sessions/${session.id}/export`, 'POST', { expectedVersion: session.version })).blob();
  }
  async uploadSource(session: StudioSession, file: File): Promise<StudioSession> {
    const form = new FormData(); form.set('file', file); form.set('expectedVersion', String(session.version));
    return (await this.request(`/sessions/${session.id}/sources`, 'POST', form)).json();
  }
  async deleteSource(session: StudioSession, sourceId: string): Promise<StudioSession> {
    return (await this.request(`/sessions/${session.id}/sources/${encodeURIComponent(sourceId)}`, 'DELETE', { expectedVersion: session.version })).json();
  }
  async sample(workflow: string): Promise<Blob> { return (await this.request(`/templates/${encodeURIComponent(workflow)}`)).blob(); }
  async remove(session: StudioSession): Promise<void> { await this.request(`/sessions/${session.id}`, 'DELETE', { expectedVersion: session.version }); }
}
