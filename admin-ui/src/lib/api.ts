declare global {
  interface Window {
    APP_CONFIG?: {
      apiKey?: string;
    };
  }
}

const API_BASE = '/api/query';
const API_KEY = window.APP_CONFIG?.apiKey || 'default-key-change-me';

export interface Template {
  idQueryTemplate: number;
  name: string;
  description: string | null;
  category: string | null;
  querySql?: string;
  outputFormat: string;
  maxResults: number;
  timeoutSeconds: number;
  version: number;
  active: boolean;
  creationDate: string;
  updateDate: string | null;
  tagCount: number;
}

export interface QueryTag {
  idQueryQueryTag: number;
  idQueryTemplate: number;
  version: number;
  changeReason: string | null;
  changeType: string | null;
  creationDate: string;
}

export interface QueryTagDetails extends QueryTag {
  querySql: string;
  params: string | null;
  name: string | null;
  description: string | null;
}

export interface TemplateDto {
  name: string;
  description: string | null;
  category: string | null;
  querySql: string;
  outputFormat: string;
  maxResults: number;
  timeoutSeconds: number;
  active: boolean;
}

async function request<T>(url: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      'X-API-Key': API_KEY,
      ...options.headers,
    },
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: response.statusText }));
    throw new Error(error.message || `HTTP error ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json();
}

export async function getTemplates(): Promise<Template[]> {
  return request<Template[]>(`${API_BASE}/templates`);
}

export async function getTemplate(id: number): Promise<Template> {
  return request<Template>(`${API_BASE}/templates/${id}`);
}

export async function createTemplate(data: TemplateDto): Promise<Template> {
  return request<Template>(`${API_BASE}/templates`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function updateTemplate(id: number, data: TemplateDto): Promise<Template> {
  return request<Template>(`${API_BASE}/templates/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export async function deleteTemplate(id: number): Promise<void> {
  return request<void>(`${API_BASE}/templates/${id}`, {
    method: 'DELETE',
  });
}

export async function createTag(templateId: number, data: QueryTagCreateDto): Promise<void> {
  return request<void>(`${API_BASE}/templates/${templateId}/tag`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function getTemplateTags(templateId: number): Promise<QueryTag[]> {
  return request<QueryTag[]>(`${API_BASE}/templates/${templateId}/tags`);
}

export async function getTag(tagId: number): Promise<QueryTagDetails> {
  return request<QueryTagDetails>(`${API_BASE}/tags/${tagId}`);
}

export async function deleteTag(tagId: number): Promise<void> {
  return request<void>(`${API_BASE}/tags/${tagId}`, {
    method: 'DELETE',
  });
}

export interface QueryTagCreateDto {
  changeReason: string;
  changeType: 'minor' | 'major' | 'bugfix' | 'rollback';
}

export interface ColumnInfo {
  name: string;
  dataType: string;
}

export interface QueryResponse {
  data: Record<string, unknown>[];
  rowCount: number;
  executionTimeMs: number;
  columns: ColumnInfo[];
}

export async function executeQuery(
  query: string,
  parameters?: Record<string, unknown>
): Promise<QueryResponse> {
  return request<QueryResponse>(`${API_BASE}/execute`, {
    method: 'POST',
    body: JSON.stringify({ query, parameters }),
  });
}
