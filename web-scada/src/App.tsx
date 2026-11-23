import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  BrowserRouter, Link, NavLink, Navigate, Outlet, Route, Routes,
  useNavigate, useSearchParams
} from 'react-router-dom';
import {
  CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis
} from 'recharts';
import { api, formatDateTime, formatValue, isStale } from './api';
import { AuthProvider, useAuth } from './auth';
import { useScadaHub } from './useScadaHub';
import type {
  Alarm, AuditEntry, Command, Gateway, GatewayStatusEvent, HistoryPoint,
  LiveTelemetry, Tag
} from './types';

/* ----------------------------- shared bits ----------------------------- */

function StatusBadge({ status }: { status: string }) {
  const cls = status === 'online' ? 'badge ok' : status === 'offline' ? 'badge danger' : 'badge warn';
  return <span className={cls}>{status.toUpperCase()}</span>;
}

function ErrorBanner({ error }: { error: string | null }) {
  if (!error) return null;
  return <div className="error-banner">{error}</div>;
}

function PageHeader({ title, subtitle, children }: { title: string; subtitle?: string; children?: ReactNode }) {
  return (
    <div className="page-header">
      <div>
        <h1>{title}</h1>
        {subtitle && <p>{subtitle}</p>}
      </div>
      {children && <div className="page-actions">{children}</div>}
    </div>
  );
}

function useAsyncError() {
  const [error, setError] = useState<string | null>(null);
  const run = useCallback(async (action: () => Promise<void>) => {
    setError(null);
    try {
      await action();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Thao tác thất bại.');
    }
  }, []);
  return { error, setError, run };
}

/* ------------------------------- login -------------------------------- */

function LoginPage() {
  const { token, login } = useAuth();
  const navigate = useNavigate();
  const [username, setUsername] = useState('admin');
  const [password, setPassword] = useState('Admin123!');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (token) return <Navigate to="/" replace />;

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(username, password);
      navigate('/', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Đăng nhập thất bại.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={submit}>
        <div className="brand-mark">GW</div>
        <h1>Web SCADA</h1>
        <p className="muted">Giám sát và điều khiển PLC qua IoT Gateway</p>
        <label>
          Tên đăng nhập
          <input value={username} onChange={e => setUsername(e.target.value)} autoComplete="username" />
        </label>
        <label>
          Mật khẩu
          <input type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="current-password" />
        </label>
        <ErrorBanner error={error} />
        <button className="primary" disabled={busy}>{busy ? 'Đang đăng nhập...' : 'Đăng nhập'}</button>
        <small>Mặc định dev: admin / Admin123! — đổi trước khi dùng thật.</small>
      </form>
    </div>
  );
}

/* ------------------------------- layout -------------------------------- */

function Layout() {
  const { token, user, logout } = useAuth();
  if (!token) return <Navigate to="/login" replace />;
  const role = user?.role ?? 'Viewer';
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <span className="brand-mark small">GW</span>
          <div>
            <strong>SCADA</strong>
            <small>IoT Gateway</small>
          </div>
        </div>
        <nav>
          <NavLink to="/" end>Dashboard</NavLink>
          <NavLink to="/gateways">Gateway / PLC</NavLink>
          <NavLink to="/tags">Tag dữ liệu</NavLink>
          <NavLink to="/history">Lịch sử</NavLink>
          <NavLink to="/alarms">Alarm</NavLink>
          <NavLink to="/commands">Điều khiển</NavLink>
          {(role === 'Admin' || role === 'Engineer') && <NavLink to="/audit">Audit</NavLink>}
        </nav>
        <div className="sidebar-footer">
          <div>{user?.displayName}</div>
          <small>{role}</small>
          <button className="ghost" onClick={logout}>Đăng xuất</button>
        </div>
      </aside>
      <main className="main">
        <header className="topbar">
          <span>PLC Delta DVP · MQTT · C# Control</span>
          <span className="live-dot">LIVE</span>
        </header>
        <div className="content"><Outlet /></div>
      </main>
    </div>
  );
}

/* ------------------------------ dashboard ----------------------------- */

function DashboardPage() {
  const { token } = useAuth();
  const [gateways, setGateways] = useState<Gateway[]>([]);
  const [tags, setTags] = useState<Tag[]>([]);
  const [alarms, setAlarms] = useState<Alarm[]>([]);
  const { error, run } = useAsyncError();

  const load = useCallback(() => run(async () => {
    const [g, t, a] = await Promise.all([api.gateways(), api.tags(), api.alarms(true)]);
    setGateways(g); setTags(t); setAlarms(a);
  }), [run]);

  useEffect(() => { void load(); }, [load]);

  useScadaHub(token, {
    onTelemetry: (event: LiveTelemetry) => {
      setTags(prev => {
        const byId = new Map(event.tags.map(t => [t.tagId, t]));
        return prev.map(tag => {
          const live = byId.get(tag.id);
          if (!live) return tag;
          return { ...tag, latest: { numeric: live.numeric, bool: live.boolValue, text: live.text, quality: live.quality, timestamp: live.timestamp, seq: live.seq } };
        });
      });
      setGateways(prev => prev.map(g => g.id === event.gatewayId ? { ...g, status: 'online', lastSeen: event.timestamp } : g));
    },
    onGatewayStatus: (event: GatewayStatusEvent) => {
      setGateways(prev => prev.map(g => g.id === event.id ? { ...g, status: event.status, lastSeen: event.lastSeen } : g));
    },
    onAlarm: () => { void api.alarms(true).then(setAlarms).catch(() => undefined); }
  });

  const online = gateways.filter(g => g.status === 'online').length;
  const stale = tags.filter(isStale).length;

  return (
    <>
      <PageHeader title="Tổng quan hệ thống" subtitle="Dữ liệu trực tiếp từ Gateway qua MQTT" />
      <ErrorBanner error={error} />
      <div className="stat-grid">
        <div className="stat"><span>Gateway online</span><strong>{online}/{gateways.length}</strong></div>
        <div className="stat"><span>Tổng tag</span><strong>{tags.length}</strong></div>
        <div className="stat"><span>Alarm chưa xác nhận</span><strong>{alarms.length}</strong></div>
        <div className="stat"><span>Tag quá hạn</span><strong>{stale}</strong></div>
      </div>
      <div className="two-col">
        <section className="panel">
          <h2>Giá trị tag mới nhất</h2>
          <table>
            <thead><tr><th>Tag</th><th>Giá trị</th><th>Quality</th><th>Cập nhật</th></tr></thead>
            <tbody>
              {tags.slice(0, 10).map(tag => (
                <tr key={tag.id}>
                  <td><strong>{tag.name}</strong><small>{tag.plcId}</small></td>
                  <td className={isStale(tag) ? 'muted' : ''}>{formatValue(tag)}</td>
                  <td>{tag.latest?.quality ?? '—'}</td>
                  <td>{formatDateTime(tag.latest?.timestamp)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
        <section className="panel">
          <h2>Alarm đang active</h2>
          {alarms.length === 0 && <p className="muted">Không có alarm active.</p>}
          {alarms.map(alarm => (
            <div className="alarm-row" key={alarm.id}>
              <span className="badge danger">{alarm.severity}</span>
              <div><strong>{alarm.tagName}</strong><small>{alarm.message}</small></div>
              <small>{formatDateTime(alarm.startedAt)}</small>
            </div>
          ))}
        </section>
      </div>
    </>
  );
}

/* ------------------------------- gateways ------------------------------ */

function GatewaysPage() {
  const [items, setItems] = useState<Gateway[]>([]);
  const { error, run } = useAsyncError();
  useEffect(() => { void run(async () => setItems(await api.gateways())); }, [run]);
  return (
    <>
      <PageHeader title="Gateway / PLC" subtitle="Danh sách thiết bị đã cấu hình" />
      <ErrorBanner error={error} />
      <section className="panel">
        <table>
          <thead><tr><th>Gateway</th><th>Vị trí</th><th>Trạng thái</th><th>PLC</th><th>Tag</th><th>Last seen</th></tr></thead>
          <tbody>
            {items.map(item => (
              <tr key={item.id}>
                <td><strong>{item.name}</strong><small>{item.id}</small></td>
                <td>{item.location ?? '—'}</td>
                <td><StatusBadge status={item.status} /></td>
                <td>{item.plcCount}</td>
                <td>{item.tagCount}</td>
                <td>{formatDateTime(item.lastSeen)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </>
  );
}

/* --------------------------------- tags -------------------------------- */

function TagsPage() {
  const { token } = useAuth();
  const [tags, setTags] = useState<Tag[]>([]);
  const [filter, setFilter] = useState('');
  const { error, run } = useAsyncError();

  useEffect(() => { void run(async () => setTags(await api.tags())); }, [run]);
  useScadaHub(token, {
    onTelemetry: (event: LiveTelemetry) => {
      const byId = new Map(event.tags.map(t => [t.tagId, t]));
      setTags(prev => prev.map(tag => {
        const live = byId.get(tag.id);
        return live ? { ...tag, latest: { numeric: live.numeric, bool: live.boolValue, text: live.text, quality: live.quality, timestamp: live.timestamp, seq: live.seq } } : tag;
      }));
    }
  });

  const filtered = useMemo(() => tags.filter(tag =>
    tag.name.toLowerCase().includes(filter.toLowerCase()) ||
    tag.address.toLowerCase().includes(filter.toLowerCase())), [tags, filter]);

  return (
    <>
      <PageHeader title="Tag dữ liệu" subtitle="Giá trị PLC theo thời gian thực">
        <input placeholder="Lọc tag..." value={filter} onChange={e => setFilter(e.target.value)} />
      </PageHeader>
      <ErrorBanner error={error} />
      <section className="panel">
        <table>
          <thead><tr><th>Tag</th><th>Địa chỉ</th><th>Kiểu</th><th>Giá trị</th><th>Quality</th><th>Timestamp</th><th></th></tr></thead>
          <tbody>
            {filtered.map(tag => (
              <tr key={tag.id}>
                <td><strong>{tag.name}</strong><small>{tag.plcId}</small></td>
                <td>{tag.address}</td>
                <td>{tag.dataType}{tag.writable ? ' • W' : ''}</td>
                <td className={isStale(tag) ? 'muted' : 'value'}>{formatValue(tag)}</td>
                <td>{tag.latest?.quality ?? '—'}</td>
                <td>{formatDateTime(tag.latest?.timestamp)}</td>
                <td><Link to={`/history?tag=${encodeURIComponent(tag.id)}`}>Lịch sử</Link></td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </>
  );
}

/* -------------------------------- history ------------------------------- */

function toLocalInput(date: Date) {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function HistoryPage() {
  const [params] = useSearchParams();
  const [tags, setTags] = useState<Tag[]>([]);
  const [tagId, setTagId] = useState(params.get('tag') ?? '');
  const [from, setFrom] = useState(toLocalInput(new Date(Date.now() - 3600_000)));
  const [to, setTo] = useState(toLocalInput(new Date()));
  const [points, setPoints] = useState<HistoryPoint[]>([]);
  const [loading, setLoading] = useState(false);
  const { error, run } = useAsyncError();

  useEffect(() => {
    void run(async () => {
      const result = await api.tags();
      setTags(result);
      if (!tagId && result.length > 0) setTagId(result[0].id);
    });
  }, [run, tagId]);

  const loadHistory = useCallback(async () => {
    if (!tagId) return;
    await run(async () => {
      setLoading(true);
      try {
        const result = await api.history(tagId, new Date(from).toISOString(), new Date(to).toISOString());
        setPoints(result);
      } finally {
        setLoading(false);
      }
    });
  }, [tagId, from, to, run]);

  useEffect(() => { void loadHistory(); }, [loadHistory]);

  const data = points.map(p => ({
    time: new Date(p.timestamp).toLocaleTimeString('vi-VN'),
    value: p.value
  }));

  return (
    <>
      <PageHeader title="Lịch sử dữ liệu" subtitle="Truy vấn telemetry từ InfluxDB / bộ nhớ tạm" />
      <ErrorBanner error={error} />
      <section className="panel filters">
        <label>Tag
          <select value={tagId} onChange={e => setTagId(e.target.value)}>
            {tags.map(tag => <option key={tag.id} value={tag.id}>{tag.name} — {tag.id}</option>)}
          </select>
        </label>
        <label>Từ<input type="datetime-local" value={from} onChange={e => setFrom(e.target.value)} /></label>
        <label>Đến<input type="datetime-local" value={to} onChange={e => setTo(e.target.value)} /></label>
        <button className="primary" onClick={() => void loadHistory()} disabled={loading}>{loading ? 'Đang tải...' : 'Truy vấn'}</button>
      </section>
      <section className="panel">
        <h2>{tags.find(t => t.id === tagId)?.name ?? 'Biểu đồ'}</h2>
        {data.length === 0 ? <p className="muted">Chưa có dữ liệu trong khoảng thời gian này.</p> : (
          <div className="chart">
            <ResponsiveContainer width="100%" height={360}>
              <LineChart data={data}>
                <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                <XAxis dataKey="time" minTickGap={30} />
                <YAxis />
                <Tooltip />
                <Line type="monotone" dataKey="value" stroke="#0f766e" strokeWidth={2} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        )}
      </section>
    </>
  );
}

/* --------------------------------- alarms ------------------------------- */

function AlarmsPage() {
  const { token } = useAuth();
  const [alarms, setAlarms] = useState<Alarm[]>([]);
  const [onlyActive, setOnlyActive] = useState(false);
  const { error, run } = useAsyncError();

  const load = useCallback(() => run(async () => setAlarms(await api.alarms(onlyActive))), [onlyActive, run]);
  useEffect(() => { void load(); }, [load]);
  useScadaHub(token, { onAlarm: () => { void api.alarms(onlyActive).then(setAlarms).catch(() => undefined); } });

  async function ack(id: string) {
    await run(async () => {
      await api.ackAlarm(id);
      setAlarms(await api.alarms(onlyActive));
    });
  }

  return (
    <>
      <PageHeader title="Alarm / Event" subtitle="Cảnh báo theo ngưỡng và xác nhận của người vận hành">
        <label className="inline"><input type="checkbox" checked={onlyActive} onChange={e => setOnlyActive(e.target.checked)} /> Chỉ active</label>
      </PageHeader>
      <ErrorBanner error={error} />
      <section className="panel">
        <table>
          <thead><tr><th>Tag</th><th>Mức</th><th>Nội dung</th><th>Bắt đầu</th><th>Trạng thái</th><th>Ack</th></tr></thead>
          <tbody>
            {alarms.map(alarm => (
              <tr key={alarm.id}>
                <td><strong>{alarm.tagName}</strong><small>{alarm.address}</small></td>
                <td><span className="badge danger">{alarm.severity}</span></td>
                <td>{alarm.message}</td>
                <td>{formatDateTime(alarm.startedAt)}</td>
                <td>{alarm.isActive ? <span className="badge danger">ACTIVE</span> : <span className="badge">CLEARED</span>}</td>
                <td>{alarm.ackAt ? formatDateTime(alarm.ackAt) : <button className="ghost" onClick={() => void ack(alarm.id)}>Xác nhận</button>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </>
  );
}

/* -------------------------------- commands ------------------------------ */

function CommandsPage() {
  const { token } = useAuth();
  const [tags, setTags] = useState<Tag[]>([]);
  const [commands, setCommands] = useState<Command[]>([]);
  const [tagId, setTagId] = useState('');
  const [value, setValue] = useState('1');
  const { error, run } = useAsyncError();

  const load = useCallback(() => run(async () => {
    const [t, c] = await Promise.all([api.tags(), api.commands()]);
    setTags(t); setCommands(c);
    if (!tagId) {
      const first = t.find(x => x.writable);
      if (first) { setTagId(first.id); setValue(first.dataType === 'bool' ? '1' : '0'); }
    }
  }), [run, tagId]);

  useEffect(() => { void load(); }, [load]);
  useScadaHub(token, {
    onCommandStatus: (event: Command) => {
      setCommands(prev => [event, ...prev.filter(x => x.id !== event.id)]);
    }
  });

  const selected = tags.find(t => t.id === tagId);
  async function send(e: React.FormEvent) {
    e.preventDefault();
    if (!selected) return;
    if (!window.confirm(`Gửi lệnh ${selected.address} = ${value}?`)) return;
    await run(async () => {
      await api.createCommand(tagId, value);
      setCommands(await api.commands());
    });
  }

  return (
    <>
      <PageHeader title="Điều khiển" subtitle="Lệnh đi qua C# Control Service, có xác nhận và audit" />
      <ErrorBanner error={error} />
      <div className="two-col">
        <section className="panel">
          <h2>Gửi lệnh</h2>
          <form className="command-form" onSubmit={send}>
            <label>Tag ghi được
              <select value={tagId} onChange={e => {
                setTagId(e.target.value);
                const tag = tags.find(x => x.id === e.target.value);
                setValue(tag?.dataType === 'bool' ? '1' : '0');
              }}>
                {tags.filter(t => t.writable).map(tag => <option key={tag.id} value={tag.id}>{tag.name} — {tag.address}</option>)}
              </select>
            </label>
            <label>Giá trị
              <input value={value} onChange={e => setValue(e.target.value)} placeholder={selected?.dataType === 'bool' ? '0/1' : 'Giá trị'} />
            </label>
            <button className="primary" disabled={!selected}>Gửi lệnh</button>
          </form>
          <p className="muted small">Command chỉ được gửi khi Gateway online; giá trị phải nằm trong min/max.</p>
        </section>
        <section className="panel">
          <h2>Lịch sử lệnh</h2>
          <table>
            <thead><tr><th>Cmd</th><th>Tag</th><th>Giá trị</th><th>Trạng thái</th><th>Thời gian</th></tr></thead>
            <tbody>
              {commands.map(command => (
                <tr key={command.id}>
                  <td><small>{command.cmdId}</small></td>
                  <td>{command.tagId}</td>
                  <td>{command.value}</td>
                  <td><span className={command.status === 'acked' ? 'badge ok' : command.status === 'failed' ? 'badge danger' : 'badge warn'}>{command.status}</span></td>
                  <td>{formatDateTime(command.requestedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      </div>
    </>
  );
}

/* --------------------------------- audit -------------------------------- */

function AuditPage() {
  const [items, setItems] = useState<AuditEntry[]>([]);
  const { error, run } = useAsyncError();
  useEffect(() => { void run(async () => setItems(await api.audit())); }, [run]);
  return (
    <>
      <PageHeader title="Audit log" subtitle="Lịch sử thao tác cấu hình và điều khiển" />
      <ErrorBanner error={error} />
      <section className="panel">
        <table>
          <thead><tr><th>Thời gian</th><th>Hành động</th><th>Đối tượng</th><th>Chi tiết</th><th>User</th></tr></thead>
          <tbody>
            {items.map(item => (
              <tr key={item.id}>
                <td>{formatDateTime(item.timestamp)}</td>
                <td>{item.action}</td>
                <td>{item.entityType} {item.entityId}</td>
                <td>{item.detail}</td>
                <td><small>{item.userId}</small></td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </>
  );
}

/* --------------------------------- app ---------------------------------- */

function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<Layout />}>
        <Route index element={<DashboardPage />} />
        <Route path="gateways" element={<GatewaysPage />} />
        <Route path="tags" element={<TagsPage />} />
        <Route path="history" element={<HistoryPage />} />
        <Route path="alarms" element={<AlarmsPage />} />
        <Route path="commands" element={<CommandsPage />} />
        <Route path="audit" element={<AuditPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
    </AuthProvider>
  );
}
