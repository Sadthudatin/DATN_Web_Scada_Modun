import type {
  Alarm, AuditEntry, Command, Gateway, HistoryPoint, LoginResponse,
  Me, Tag
} from './types';

export const TOKEN_KEY = 'scada.token';
export const USER_KEY = 'scada.user';

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

async function request<T>(path: string, init: RequestInit = {}, token?: string | null): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set('Content-Type', 'application/json');
  const accessToken = token ?? localStorage.getItem(TOKEN_KEY);
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`);

  const response = await fetch(`/api${path}`, { ...init, headers });
  if (!response.ok) {
    let message = `HTTP ${response.status}`;
    try {
      const body = await response.json();
      message = body.error || body.title || message;
    } catch {
      const text = await response.text().catch(() => '');
      if (text) message = text;
    }
    throw new ApiError(response.status, message);
  }
  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}

export const api = {
  login: (username: string, password: string) =>
    request<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ username, password })
    }),
  me: () => request<Me>('/me'),
  gateways: () => request<Gateway[]>('/gateways'),
  tags: () => request<Tag[]>('/tags'),
  alarms: (activeOnly = false) => request<Alarm[]>(`/alarms?activeOnly=${activeOnly}`),
  ackAlarm: (id: string) => request<Alarm>(`/alarms/${id}/ack`, { method: 'POST' }),
  commands: () => request<Command[]>('/commands'),
  createCommand: (tagId: string, value: string) =>
    request<Command>('/commands', {
      method: 'POST',
      body: JSON.stringify({ tagId, value })
    }),
  history: (tagId: string, from: string, to: string) =>
    request<HistoryPoint[]>(`/telemetry?tagId=${encodeURIComponent(tagId)}&from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`),
  audit: () => request<AuditEntry[]>('/audit'),
  updateTag: (id: string, body: object) =>
    request<Tag>(`/tags/${encodeURIComponent(id)}`, { method: 'PUT', body: JSON.stringify(body) })
};

export function formatDateTime(value?: string | null): string {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString('vi-VN');
}

export function formatValue(tag: Tag): string {
  const latest = tag.latest;
  if (!latest) return '—';
  if (latest.bool !== null && latest.bool !== undefined) return latest.bool ? 'TRUE' : 'FALSE';
  if (latest.numeric !== null && latest.numeric !== undefined) {
    const unit = tag.unit && tag.unit !== 'raw' ? ` ${tag.unit}` : '';
    return `${Number(latest.numeric).toLocaleString('vi-VN')}${unit}`;
  }
  return latest.text ?? '—';
}

export function isStale(tag: Tag): boolean {
  if (!tag.latest) return true;
  return Date.now() - new Date(tag.latest.timestamp).getTime() > 5000;
}
