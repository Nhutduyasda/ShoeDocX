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
  id: string; version: number; expiresAt: string; workflow: string; mappingState: string;
  mappingRevision: number | null; profileVersion: number; dataRevision: number | null; reviewState: string;
  sheets: string[]; mapping: StudioMapping | null; canReview: boolean; canApprove: boolean;
  suggestions: { scope: string; key: string; status: string; candidates: { sheet: string; labelCell: string; cell: string | null; column: number | null; prototypeRow: number | null }[] }[];
  fields: StudioField[]; issues: { code: string; field: string | null; rowId: string | null; rowPosition: number | null }[];
  schemaDraft: { revision: number; state: string; templateSha256: string; exampleSha256: string | null; fields: LearnedField[] } | null;
  sheetSummaries: { name: string; usedCells: number; tables: number; prototypeRows: number[]; footerRows: number[]; mergedRanges: string[] }[];
  sources: StudioSource[];
}
export interface StudioBootstrap { csrfToken: string; workflows: string[]; canReview: boolean; canApprove: boolean }

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
      const error = await response.json().catch(() => ({})) as { code?: string; title?: string };
      const code = error.code || error.title || 'STUDIO_REQUEST_FAILED';
      const messages: Record<string, string> = {
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
      };
      throw new Error(`${messages[code] || code} [${code}] (HTTP ${response.status})`);
    }
    return response;
  }
  async bootstrap(signal?: AbortSignal): Promise<StudioBootstrap> {
    const data = await (await this.request('/bootstrap', 'GET', undefined, signal)).json() as StudioBootstrap;
    this.csrf = data.csrfToken; return data;
  }
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
