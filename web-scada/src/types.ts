export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  displayName: string;
  role: string;
}

export interface Me {
  userId: string;
  userName: string;
  displayName: string;
  roles: string[];
}

export interface Gateway {
  id: string;
  name: string;
  location?: string;
  status: 'online' | 'offline' | string;
  lastSeen: string;
  plcCount: number;
  tagCount: number;
}

export interface TagValue {
  numeric?: number | null;
  bool?: boolean | null;
  text?: string | null;
  quality: string;
  timestamp: string;
  seq: number;
}

export interface Tag {
  id: string;
  plcId: string;
  name: string;
  address: string;
  dataType: string;
  unit?: string | null;
  scale: number;
  writable: boolean;
  minValue?: number | null;
  maxValue?: number | null;
  alarmHigh?: number | null;
  alarmLow?: number | null;
  latest?: TagValue | null;
}

export interface Alarm {
  id: string;
  tagId: string;
  tagName: string;
  address: string;
  severity: string;
  message: string;
  startedAt: string;
  clearedAt?: string | null;
  ackAt?: string | null;
  ackBy?: string | null;
  isActive: boolean;
}

export interface Command {
  id: string;
  cmdId: string;
  gatewayId: string;
  plcId: string;
  tagId: string;
  value: string;
  requestedBy?: string | null;
  requestedAt: string;
  status: string;
  ackAt?: string | null;
  error?: string | null;
}

export interface HistoryPoint {
  timestamp: string;
  value: number;
}

export interface AuditEntry {
  id: string;
  userId?: string | null;
  action: string;
  entityType?: string | null;
  entityId?: string | null;
  detail?: string | null;
  timestamp: string;
}

export interface LiveTag {
  tagId: string;
  address: string;
  numeric?: number | null;
  boolValue?: boolean | null;
  text?: string | null;
  quality: string;
  timestamp: string;
  seq: number;
}

export interface LiveTelemetry {
  gatewayId: string;
  plcId: string;
  timestamp: string;
  tags: LiveTag[];
}

export interface GatewayStatusEvent {
  id: string;
  status: string;
  lastSeen: string;
}
