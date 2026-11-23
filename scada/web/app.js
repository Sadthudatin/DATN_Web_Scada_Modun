(function () {
  "use strict";

  var DEFAULT_CONFIG = { device: "dvp24-01", user: "web", password: "web-pass-2026" };
  var cfg = window.SCADA_CONFIG || {};
  var preferredDevice =
    typeof cfg.device === "string" && cfg.device ? cfg.device : DEFAULT_CONFIG.device;
  var mqttUser = typeof cfg.user === "string" && cfg.user ? cfg.user : DEFAULT_CONFIG.user;
  var mqttPass =
    typeof cfg.password === "string" && cfg.password ? cfg.password : DEFAULT_CONFIG.password;

  var TOPIC_CONFIG = "plc/+/config";
  var TOPIC_TELEMETRY = "plc/+/telemetry";
  var TOPIC_ACK = "plc/+/ack";
  var TOPIC_STATUS = "plc/+/status";
  var TOPIC_LOG = "plc/log/file";
  var SUBSCRIBE_TOPICS = [TOPIC_CONFIG, TOPIC_TELEMETRY, TOPIC_ACK, TOPIC_STATUS, TOPIC_LOG];
  var BROKER_URL = (location.protocol === "https:" ? "wss://" : "ws://") + location.host + "/mqtt";
  var MAX_POINTS = 120;
  var MAX_LOG = 20;
  var ACK_TIMEOUT_MS = 10000;
  var STALE_SECONDS = 5;
  var ERROR_LOG_INTERVAL_MS = 5000;
  var CONN_LABELS = {
    connecting: "CONNECTING",
    online: "ONLINE",
    reconnect: "RECONNECT",
    offline: "OFFLINE",
    error: "ERROR"
  };

  function byId(id) {
    return document.getElementById(id);
  }

  var el = {
    connBadge: byId("connBadge"),
    connText: byId("connText"),
    scopeChip: byId("scopeChip"),
    qualityBadge: byId("qualityBadge"),
    qualityText: byId("qualityText"),
    deviceBadge: byId("deviceBadge"),
    deviceText: byId("deviceText"),
    deviceName: byId("deviceName"),
    deviceModbus: byId("deviceModbus"),
    deviceCycle: byId("deviceCycle"),
    seqValue: byId("seqValue"),
    ageValue: byId("ageValue"),
    d100: byId("d100Value"),
    d101: byId("d101Value"),
    d102: byId("d102Value"),
    spark: byId("spark"),
    sparkWrap: byId("sparkWrap"),
    sparkStat: byId("sparkStat"),
    legendTarget: byId("legendTarget"),
    logList: byId("logList"),
    logEmpty: byId("logEmpty"),
    kpiInterval: byId("kpiInterval"),
    kpiMissed: byId("kpiMissed"),
    kpiErrors: byId("kpiErrors"),
    scanStrip: byId("scanStrip"),
    finding: byId("finding"),
    fleetList: byId("fleetList"),
    fleetCount: byId("fleetCount"),
    addDeviceBtn: byId("addDeviceBtn"),
    refreshBtn: byId("refreshBtn"),
    addDialog: byId("addDialog"),
    addForm: byId("addForm"),
    addId: byId("addId"),
    addName: byId("addName"),
    addSlave: byId("addSlave"),
    addCycle: byId("addCycle"),
    addError: byId("addError"),
    addClose: byId("addClose"),
    addCancel: byId("addCancel"),
    csvBtn: byId("csvBtn"),
    exportLogBtn: byId("exportLogBtn")
  };

  function collectCells(selector) {
    var map = {};
    var nodes = document.querySelectorAll(selector);
    for (var i = 0; i < nodes.length; i++) {
      var node = nodes[i];
      var key = node.getAttribute("data-key");
      if (!key) continue;
      map[key] = {
        root: node,
        led: node.querySelector("[data-role='led']"),
        val: node.querySelector("[data-role='val']")
      };
    }
    return map;
  }

  var xCells = collectCells("#xGrid [data-key]");
  var yCells = collectCells("#yGrid [data-key]");
  var mCells = collectCells("#mGrid [data-key]");

  var state = {
    client: null,
    connected: false,
    conn: "connecting",
    connLabel: "CONNECTING",
    devices: Object.create(null),
    order: [],
    selected: null,
    lastErrorText: "",
    lastErrorAt: 0,
    fleetSignature: null
  };

  function pad2(value) {
    return value < 10 ? "0" + value : String(value);
  }

  function clockText() {
    var now = new Date();
    return (
      pad2(now.getHours()) +
      ":" +
      pad2(now.getMinutes()) +
      ":" +
      pad2(now.getSeconds()) +
      "." +
      String(now.getMilliseconds()).padStart(3, "0")
    );
  }

  function errorText(err) {
    if (!err) return "không rõ nguyên nhân";
    if (typeof err === "string") return err;
    if (err.message) return String(err.message);
    return String(err);
  }

  function log(kind, text) {
    if (!el.logList) return;
    var line = document.createElement("li");
    line.className = "log-line log-" + kind;
    var time = document.createElement("time");
    time.className = "log-time";
    time.textContent = clockText();
    var body = document.createElement("span");
    body.className = "log-text";
    body.textContent = text;
    line.appendChild(time);
    line.appendChild(body);
    el.logList.insertBefore(line, el.logList.firstChild);
    while (el.logList.children.length > MAX_LOG) {
      el.logList.removeChild(el.logList.lastChild);
    }
    if (el.logEmpty) el.logEmpty.hidden = true;
  }

  function logErrorThrottled(text) {
    var now = Date.now();
    if (text === state.lastErrorText && now - state.lastErrorAt < ERROR_LOG_INTERVAL_MS) return;
    state.lastErrorText = text;
    state.lastErrorAt = now;
    log("err", text);
  }

  function setConn(kind, detail) {
    var label = kind === "error" && detail ? detail : CONN_LABELS[kind] || "OFFLINE";
    if (state.conn === kind && state.connLabel === label) return;
    var previous = state.conn;
    state.conn = kind;
    state.connLabel = label;
    if (el.connBadge) {
      el.connBadge.className = "pill pill-" + (kind === "error" ? "error" : kind);
    }
    if (el.connText) el.connText.textContent = label;
    if (previous === kind) return;
    if (kind === "online") {
      log("event", "Đã kết nối broker MQTT");
    } else if (kind === "reconnect") {
      log("event", previous === "offline" ? "Đang kết nối lại broker" : "Mất kết nối — đang kết nối lại");
    } else if (kind === "offline") {
      log("event", "Mất kết nối — chế độ OFFLINE");
    } else if (kind === "error") {
      log("err", detail || "Lỗi kết nối MQTT");
    }
  }

  function deviceIdFrom(topic, leaf) {
    if (typeof topic !== "string") return null;
    var suffix = "/" + leaf;
    if (topic.length <= 4 + suffix.length) return null;
    if (topic.slice(0, 4) !== "plc/") return null;
    if (topic.slice(-suffix.length) !== suffix) return null;
    var id = topic.slice(4, -suffix.length);
    if (!id || id.indexOf("/") !== -1) return null;
    return id;
  }

  function newDevice(id) {
    return {
      id: id,
      name: "",
      slave: null,
      cycle: 300,
      status: null,
      quality: null,
      hasData: false,
      lastTs: 0,
      lastMsgTs: 0,
      seq: null,
      x: {},
      y: {},
      m: {},
      d: {},
      intervals: [],
      cmdErrors: 0,
      pending: {},
      pendingByTarget: {},
      cmdCounter: 0,
      node: null,
      valNode: null,
      ageNode: null
    };
  }

  function getDevice(id) {
    var device = state.devices[id];
    if (!device) {
      device = newDevice(id);
      state.devices[id] = device;
      state.order.push(id);
    }
    return device;
  }

  function selectedDevice() {
    return state.selected ? state.devices[state.selected] || null : null;
  }

  function targetMs() {
    var device = selectedDevice();
    var cycle = device && device.cycle ? Number(device.cycle) : 0;
    return cycle > 0 ? cycle : 300;
  }

  function currentIntervals() {
    var device = selectedDevice();
    return device ? device.intervals : [];
  }

  function setQuality(value) {
    var text = value === null || value === undefined || value === "" ? "—" : String(value).toUpperCase();
    if (el.qualityText) el.qualityText.textContent = text;
    if (el.qualityBadge) {
      var kind = "unknown";
      if (text === "GOOD") kind = "good";
      else if (text !== "—") kind = "bad";
      el.qualityBadge.className = "pill pill-" + kind;
    }
  }

  function setDeviceStatus(device) {
    var text = "—";
    if (device) {
      if (device.status === "online") text = "ONLINE";
      else if (device.status === "offline") text = "OFFLINE";
      else if (device.hasData) text = "ONLINE";
    }
    if (el.deviceText) el.deviceText.textContent = text;
    if (el.deviceBadge) {
      var kind = "unknown";
      if (text === "ONLINE") kind = "good";
      else if (text === "OFFLINE") kind = "bad";
      el.deviceBadge.className = "pill pill-" + kind;
    }
  }

  function toBit(value) {
    return value === true || value === 1 || value === "1" ? 1 : 0;
  }

  function toNumber(value) {
    if (value === null || value === undefined || value === "") return null;
    var num = typeof value === "number" ? value : Number(value);
    return isFinite(num) ? num : null;
  }

  function setCell(cell, value) {
    if (!cell) return;
    var on = toBit(value) === 1;
    cell.root.classList.toggle("is-on", on);
    if (cell.val) cell.val.textContent = on ? "1" : "0";
    if (cell.root.tagName === "BUTTON") {
      cell.root.setAttribute("aria-pressed", on ? "true" : "false");
    }
  }

  function setRegister(node, value, decimals) {
    if (!node) return;
    if (value === null) {
      node.textContent = "—";
      return;
    }
    node.textContent = decimals > 0 ? value.toFixed(decimals) : String(Math.round(value));
  }

  function updateAge() {
    if (!el.ageValue) return;
    var device = selectedDevice();
    if (!device || !device.hasData || !device.lastTs) {
      el.ageValue.textContent = "—";
      el.ageValue.classList.remove("is-stale");
      return;
    }
    var age = (Date.now() - device.lastTs) / 1000;
    if (!isFinite(age) || age < 0) age = 0;
    el.ageValue.textContent = age.toFixed(1) + " s";
    el.ageValue.classList.toggle("is-stale", age > STALE_SECONDS);
  }

  var sparkCtx = el.spark && el.spark.getContext ? el.spark.getContext("2d") : null;

  function formatAxis(value, step) {
    if (!isFinite(value)) return "—";
    var digits = 0;
    if (step !== undefined) {
      digits = Math.max(0, Math.min(3, Math.ceil(-Math.log(step) / Math.LN10)));
    } else if (Math.abs(value) < 10) {
      digits = 2;
    } else if (Math.abs(value) < 100) {
      digits = 1;
    }
    return value.toFixed(digits);
  }

  function updateKpis() {
    var device = selectedDevice();
    var pts = device ? device.intervals : [];
    var target = targetMs();
    var last = pts.length ? pts[pts.length - 1] : null;
    if (el.kpiInterval) {
      el.kpiInterval.textContent = last === null ? "—" : String(Math.round(last));
    }
    var missed = 0;
    for (var i = 0; i < pts.length; i++) {
      if (pts[i] > target * 2.2) missed++;
    }
    if (device) device.missed = missed;
    if (el.kpiMissed) el.kpiMissed.textContent = pts.length ? String(missed) : "—";
    if (el.kpiErrors) el.kpiErrors.textContent = String(device ? device.cmdErrors : 0);
    if (el.legendTarget) el.legendTarget.textContent = "mục tiêu " + target + " ms";
    if (el.finding) {
      if (!device) {
        el.finding.textContent = "Chưa có mạch — bấm “Thêm mạch” để bắt đầu theo dõi.";
      } else if (!pts.length) {
        el.finding.textContent = "Đang chờ gói telemetry đầu tiên từ mạch " + device.id + ".";
      } else if (missed === 0) {
        el.finding.textContent =
          device.id + " ổn định · 0 gói trễ trong " + pts.length + " gói gần nhất";
      } else {
        el.finding.textContent =
          device.id + " · " + missed + " gói trễ trong " + pts.length + " gói gần nhất";
      }
    }
  }

  function renderStrip() {
    if (!el.scanStrip) return;
    var pts = currentIntervals();
    var target = targetMs();
    var html = "";
    for (var i = 0; i < pts.length; i++) {
      var v = pts[i];
      var miss = v > target * 2.2;
      var late = !miss && v > target * 1.4;
      var h = miss ? 28 : Math.max(2, Math.min(28, Math.round((v / (target * 1.6)) * 28)));
      html +=
        '<span class="tick' +
        (miss ? " miss" : late ? " late" : "") +
        '" style="height:' +
        h +
        'px"></span>';
    }
    el.scanStrip.innerHTML = html;
  }

  function drawSpark() {
    if (!sparkCtx || !el.spark || !el.sparkWrap) return;
    var rect = el.sparkWrap.getBoundingClientRect();
    var width = Math.max(1, Math.round(rect.width));
    var height = Math.max(1, Math.round(rect.height));
    var dpr = window.devicePixelRatio || 1;
    var pixelW = Math.round(width * dpr);
    var pixelH = Math.round(height * dpr);
    if (el.spark.width !== pixelW) el.spark.width = pixelW;
    if (el.spark.height !== pixelH) el.spark.height = pixelH;

    var ctx = sparkCtx;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, width, height);

    var padLeft = 48;
    var padRight = 10;
    var padTop = 12;
    var padBottom = 14;
    var plotW = width - padLeft - padRight;
    var plotH = height - padTop - padBottom;
    if (plotW < 20 || plotH < 20) return;

    ctx.strokeStyle = "rgba(255, 255, 255, 0.07)";
    ctx.lineWidth = 1;
    ctx.font = "11px " + "system-ui, -apple-system, Segoe UI, sans-serif";
    ctx.textAlign = "right";
    ctx.textBaseline = "middle";

    var target = targetMs();
    var points = currentIntervals();
    var lo = target;
    var hi = target;
    var p;
    for (p = 0; p < points.length; p++) {
      if (points[p] < lo) lo = points[p];
      if (points[p] > hi) hi = points[p];
    }
    var steps = [1, 2, 5, 10, 20, 25, 50, 100, 200, 500];
    var step = steps[steps.length - 1];
    for (p = 0; p < steps.length; p++) {
      if (hi - lo <= steps[p] * 3) {
        step = steps[p];
        break;
      }
    }
    var mid = (hi + lo) / 2;
    var minValue = Math.floor((mid - step * 2) / step) * step;
    var maxValue = minValue + step * 4;
    var span = maxValue - minValue || 1;

    for (var g = 0; g <= 4; g++) {
      var gy = padTop + (plotH * g) / 4;
      ctx.beginPath();
      ctx.moveTo(padLeft, gy);
      ctx.lineTo(padLeft + plotW, gy);
      ctx.stroke();
      if (points.length) {
        ctx.fillStyle = "#8e8e92";
        ctx.fillText(formatAxis(maxValue - (span * g) / 4, span / 4), padLeft - 8, gy);
      }
    }

    if (points.length && target > minValue && target < maxValue) {
      var ty = padTop + (1 - (target - minValue) / span) * plotH;
      ctx.save();
      ctx.setLineDash([3, 3]);
      ctx.strokeStyle = "rgba(255, 255, 255, 0.18)";
      ctx.beginPath();
      ctx.moveTo(padLeft, ty);
      ctx.lineTo(padLeft + plotW, ty);
      ctx.stroke();
      ctx.restore();
    }

    if (!points.length) {
      ctx.textAlign = "center";
      ctx.fillStyle = "#8e8e92";
      ctx.font = "12px system-ui, -apple-system, Segoe UI, sans-serif";
      ctx.fillText(state.selected ? "Đang chờ gói telemetry" : "Chưa có mạch", padLeft + plotW / 2, padTop + plotH / 2);
      if (el.sparkStat) el.sparkStat.textContent = "0 gói";
      return;
    }

    var coords = [];
    var denom = points.length > 1 ? points.length - 1 : 1;
    for (var i = 0; i < points.length; i++) {
      var x = points.length === 1 ? padLeft + plotW / 2 : padLeft + (i / denom) * plotW;
      var y = padTop + (1 - (points[i] - minValue) / span) * plotH;
      coords.push({ x: x, y: y });
    }

    ctx.beginPath();
    ctx.moveTo(coords[0].x, coords[0].y);
    for (var c2 = 1; c2 < coords.length; c2++) ctx.lineTo(coords[c2].x, coords[c2].y);
    ctx.strokeStyle = "#4ec9b0";
    ctx.lineWidth = 2;
    ctx.lineJoin = "round";
    ctx.lineCap = "round";
    ctx.stroke();

    var last = coords[coords.length - 1];
    ctx.beginPath();
    ctx.arc(last.x, last.y, 2.5, 0, Math.PI * 2);
    ctx.fillStyle = "#4ec9b0";
    ctx.fill();

    ctx.fillStyle = "#8e8e92";
    ctx.font = "11px system-ui, -apple-system, Segoe UI, sans-serif";
    ctx.textAlign = "left";
    ctx.fillText("−" + Math.round((points.length * target) / 1000) + " s", padLeft, height - 4);
    ctx.textAlign = "right";
    ctx.fillText("bây giờ", padLeft + plotW, height - 4);

    if (el.sparkStat) {
      el.sparkStat.textContent = points.length + " gói · ms";
    }
  }

  function observeSpark() {
    if (!el.sparkWrap) return;
    if (typeof window.ResizeObserver === "function") {
      var observer = new window.ResizeObserver(function () {
        drawSpark();
      });
      observer.observe(el.sparkWrap);
    } else {
      window.addEventListener("resize", drawSpark);
    }
  }

  function parsePayload(payload, label) {
    try {
      return JSON.parse(String(payload));
    } catch (err) {
      log("err", "Dữ liệu " + label + " không phải JSON hợp lệ");
      return null;
    }
  }

  function buildFleetItem(id) {
    var device = state.devices[id];
    var btn = document.createElement("button");
    btn.type = "button";
    btn.className = "fleet-item";
    btn.setAttribute("data-id", id);
    btn.setAttribute("role", "listitem");
    btn.title = id;

    var dot = document.createElement("span");
    dot.className = "dot";
    dot.setAttribute("aria-hidden", "true");
    var name = document.createElement("span");
    name.className = "fleet-id";
    name.textContent = id;
    var val = document.createElement("span");
    val.className = "fleet-val num";
    val.textContent = "—";
    var age = document.createElement("span");
    age.className = "fleet-age num";
    age.textContent = "—";

    btn.appendChild(dot);
    btn.appendChild(name);
    btn.appendChild(val);
    btn.appendChild(age);
    btn.addEventListener("click", function () {
      selectDevice(id);
    });

    device.node = btn;
    device.valNode = val;
    device.ageNode = age;
    return btn;
  }

  function updateFleetItem(device) {
    if (!device || !device.node) return;
    var age = device.lastTs ? (Date.now() - device.lastTs) / 1000 : null;
    var kind = "unknown";
    if (device.status === "online") kind = "online";
    else if (device.status === "offline") kind = "offline";
    else if (device.hasData) kind = "online";
    var fresh = age !== null && age <= STALE_SECONDS;
    device.node.classList.toggle("is-online", kind === "online" && fresh);
    device.node.classList.toggle("is-stale", !(kind === "online" && fresh) && kind !== "offline");
    device.node.classList.toggle("is-offline", kind === "offline");
    device.node.classList.toggle("is-active", state.selected === device.id);
    if (device.valNode) {
      var d100 = device.d.D100;
      device.valNode.textContent =
        d100 === undefined || d100 === null ? "—" : String(Math.round(d100));
    }
    if (device.ageNode) {
      device.ageNode.textContent = age === null ? "—" : age.toFixed(1) + "s";
    }
  }

  function renderFleet() {
    if (!el.fleetList) return;
    var signature = state.order.join(",");
    if (signature !== state.fleetSignature) {
      state.fleetSignature = signature;
      el.fleetList.innerHTML = "";
      if (!state.order.length) {
        var empty = document.createElement("p");
        empty.className = "fleet-empty";
        empty.textContent = "Chưa có mạch — bấm “Thêm mạch”.";
        el.fleetList.appendChild(empty);
      } else {
        for (var i = 0; i < state.order.length; i++) {
          el.fleetList.appendChild(buildFleetItem(state.order[i]));
        }
      }
    }
    if (el.fleetCount) el.fleetCount.textContent = String(state.order.length);
    for (var j = 0; j < state.order.length; j++) {
      updateFleetItem(state.devices[state.order[j]]);
    }
  }

  function renderDetail() {
    var device = selectedDevice();

    for (var i = 0; i < 8; i++) {
      var xKey = "X" + i;
      var yKey = "Y" + i;
      setCell(xCells[xKey], device ? device.x[xKey] : 0);
      setCell(yCells[yKey], device ? device.y[yKey] : 0);
    }
    for (var m = 0; m < 4; m++) {
      var mKey = "M" + m;
      setCell(mCells[mKey], device ? device.m[mKey] : 0);
    }

    setRegister(el.d100, device ? toNumber(device.d.D100) : null, 0);
    setRegister(el.d101, device ? toNumber(device.d.D101) : null, 1);
    setRegister(el.d102, device ? toNumber(device.d.D102) : null, 0);

    setQuality(device ? device.quality : null);
    setDeviceStatus(device);

    if (el.deviceName) {
      el.deviceName.textContent = device ? device.name || device.id : "—";
    }
    if (el.deviceModbus) {
      el.deviceModbus.textContent =
        device && device.slave ? "slave " + device.slave + " · 38400 8E1" : "—";
    }
    if (el.deviceCycle) {
      el.deviceCycle.textContent = device ? String(device.cycle) + " ms" : "—";
    }
    if (el.seqValue) {
      el.seqValue.textContent =
        device && device.seq !== null && device.seq !== undefined ? String(device.seq) : "—";
    }
    if (el.scopeChip) {
      el.scopeChip.textContent = device ? device.id : "—";
    }

    updateKpis();
    updateAge();
    drawSpark();
    renderStrip();
  }

  function selectDevice(id) {
    if (!state.devices[id]) return;
    state.selected = id;
    if (el.scopeChip) el.scopeChip.textContent = id;
    renderFleet();
    renderDetail();
  }

  function removeDevice(id) {
    var device = state.devices[id];
    if (!device) return;
    for (var key in device.pending) {
      if (!Object.prototype.hasOwnProperty.call(device.pending, key)) continue;
      window.clearTimeout(device.pending[key].timer);
    }
    delete state.devices[id];
    var at = state.order.indexOf(id);
    if (at !== -1) state.order.splice(at, 1);
    if (state.selected === id) {
      state.selected = state.order.length ? state.order[0] : null;
      if (state.selected) selectDevice(state.selected);
      else renderDetail();
    }
    renderFleet();
    log("event", "Đã gỡ mạch " + id);
  }

  function handleConfig(id, payload) {
    var raw = payload === null || payload === undefined ? "" : String(payload);
    if (!raw) {
      removeDevice(id);
      return;
    }
    var data = parsePayload(raw, "config");
    if (!data || typeof data !== "object") return;
    var isNew = !state.devices[id];
    var device = getDevice(id);
    if (typeof data.name === "string" && data.name) device.name = data.name.slice(0, 32);
    else if (!device.name) device.name = id;
    var slave = toNumber(data.slave);
    if (slave !== null) device.slave = slave;
    var cycle = toNumber(data.cycle);
    if (cycle !== null && cycle > 0) device.cycle = cycle;
    if (!state.selected) {
      selectDevice(state.devices[preferredDevice] ? preferredDevice : id);
    } else {
      renderFleet();
      if (state.selected === id) renderDetail();
    }
    if (isNew) log("event", "Đã nạp mạch " + id + " · " + device.name);
  }

  function handleTelemetry(id, payload) {
    var data = parsePayload(payload, "telemetry");
    if (!data || typeof data !== "object") return;
    var device = getDevice(id);

    for (var i = 0; i < 8; i++) {
      var xKey = "X" + i;
      var yKey = "Y" + i;
      device.x[xKey] = toBit(data[xKey]);
      device.y[yKey] = toBit(data[yKey]);
    }
    for (var m = 0; m < 4; m++) {
      var mKey = "M" + m;
      device.m[mKey] = toBit(data[mKey]);
    }
    device.d.D100 = toNumber(data.D100);
    device.d.D101 = toNumber(data.D101);
    device.d.D102 = toNumber(data.D102);
    device.quality = data.quality === null || data.quality === undefined ? null : data.quality;
    device.seq = toNumber(data.seq);

    var msgTs = toNumber(data.ts);
    if (msgTs !== null && msgTs > 0) {
      if (device.lastMsgTs > 0) {
        var delta = msgTs - device.lastMsgTs;
        if (delta > 0 && delta < 10000) {
          device.intervals.push(delta);
          if (device.intervals.length > MAX_POINTS) device.intervals.shift();
        }
      }
      device.lastMsgTs = msgTs;
      device.lastTs = msgTs;
    } else {
      device.lastTs = Date.now();
    }
    device.hasData = true;

    updateFleetItem(device);
    if (state.selected === id) renderDetail();
  }

  function clearPending(deviceId, cmdId) {
    var device = state.devices[deviceId];
    if (!device) return null;
    var pending = device.pending[cmdId];
    if (!pending) return null;
    window.clearTimeout(pending.timer);
    delete device.pending[cmdId];
    if (device.pendingByTarget[pending.target] === cmdId) {
      delete device.pendingByTarget[pending.target];
    }
    var cell = yCells[pending.target];
    if (cell && state.selected === deviceId) cell.root.classList.remove("is-pending");
    return pending;
  }

  function handleAck(id, payload) {
    var device = state.devices[id];
    if (!device) return;
    var data = parsePayload(payload, "ack");
    if (!data || typeof data !== "object") return;

    var cmdId = data.id === null || data.id === undefined ? "" : String(data.id);
    var latency = null;
    var pending = null;
    if (cmdId && device.pending[cmdId]) {
      latency = Math.max(0, Date.now() - device.pending[cmdId].sentAt);
      pending = clearPending(id, cmdId);
    }

    var target =
      data.target !== null && data.target !== undefined
        ? String(data.target)
        : pending
        ? pending.target
        : "?";
    var value =
      data.value !== null && data.value !== undefined ? data.value : pending ? pending.value : null;
    var ok = data.ok === true || data.ok === 1 || data.ok === "true" || data.ok === "1";
    var suffix = latency !== null ? " · " + latency + " ms" : " · không khớp lệnh";

    if (ok) {
      log("ack", "← ACK " + id + " · " + target + " = " + toBit(value) + suffix);
      if (/^Y[0-7]$/.test(target)) {
        device.y[target] = toBit(value);
        if (state.selected === id) setCell(yCells[target], device.y[target]);
      }
    } else {
      log(
        "err",
        "← ACK thất bại " + id + " · " + target + (value !== null ? " = " + toBit(value) : "") + suffix
      );
    }
  }

  function expirePending(deviceId, cmdId) {
    var pending = clearPending(deviceId, cmdId);
    if (!pending) return;
    var device = state.devices[deviceId];
    if (device) {
      device.cmdErrors += 1;
      if (state.selected === deviceId) updateKpis();
    }
    log("err", "Hết thời gian chờ ACK cho " + deviceId + " · " + pending.target + " (" + cmdId + ")");
  }

  function handleStatus(id, payload) {
    var text = String(payload).trim().toLowerCase();
    if (text !== "online" && text !== "offline") return;
    var device = getDevice(id);
    var previous = device.status;
    device.status = text;
    updateFleetItem(device);
    if (previous && previous !== text) {
      log("event", "Mạch " + id + " báo " + text.toUpperCase());
    }
    if (state.selected === id) setDeviceStatus(device);
  }

  function handleLogFile(payload) {
    var text = String(payload === null || payload === undefined ? "" : payload).trim();
    if (!text) return;
    var data;
    try {
      data = JSON.parse(text);
    } catch (err) {
      return;
    }
    if (!data || typeof data.name !== "string" || !data.name) return;
    if (!el.csvBtn) return;
    el.csvBtn.href = "/logs/" + data.name;
    el.csvBtn.setAttribute("download", data.name);
    el.csvBtn.title = "Tải " + data.name;
    el.csvBtn.hidden = false;
  }

  function csvCell(value) {
    var text = value === null || value === undefined ? "" : String(value);
    if (/[",\r\n]/.test(text)) return '"' + text.replace(/"/g, '""') + '"';
    return text;
  }

  function downloadText(text, name) {
    var blob = new Blob([text], { type: "text/csv;charset=utf-8" });
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = name;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.setTimeout(function () {
      URL.revokeObjectURL(url);
    }, 1000);
  }

  function exportLogCsv() {
    var lines = el.logList ? el.logList.querySelectorAll(".log-line") : [];
    if (!lines.length) {
      log("event", "Chưa có dòng nhật ký để xuất");
      return;
    }
    var rows = [["time", "kind", "text"]];
    for (var i = lines.length - 1; i >= 0; i--) {
      var line = lines[i];
      var timeNode = line.querySelector(".log-time");
      var textNode = line.querySelector(".log-text");
      var kind = "event";
      if (line.classList.contains("log-err")) kind = "err";
      else if (line.classList.contains("log-ack")) kind = "ack";
      else if (line.classList.contains("log-cmd")) kind = "cmd";
      rows.push([
        timeNode ? timeNode.textContent : "",
        kind,
        textNode ? textNode.textContent : ""
      ]);
    }
    var csv = "\uFEFF";
    for (var r = 0; r < rows.length; r++) {
      csv += rows[r].map(csvCell).join(",") + "\r\n";
    }
    var now = new Date();
    var name =
      "nhat-ky-" +
      now.getFullYear() +
      pad2(now.getMonth() + 1) +
      pad2(now.getDate()) +
      "-" +
      pad2(now.getHours()) +
      pad2(now.getMinutes()) +
      ".csv";
    downloadText(csv, name);
    log("event", "Đã xuất " + (rows.length - 1) + " dòng nhật ký ra " + name);
  }

  function sendCommand(target) {
    var device = selectedDevice();
    if (!device) {
      log("err", "Chưa chọn mạch để gửi lệnh " + target);
      return;
    }
    if (!state.client || !state.connected) {
      log("err", "Chưa kết nối broker — không gửi được lệnh " + target);
      return;
    }
    if (device.pendingByTarget[target]) {
      log("err", "Lệnh " + target + " trên " + device.id + " đang chờ ACK, bỏ qua thao tác mới");
      return;
    }

    var current = device.y[target] ? 1 : 0;
    var value = current ? 0 : 1;
    device.cmdCounter += 1;
    var cmdId = "cmd-" + Date.now() + "-" + device.cmdCounter;
    var message = { device: device.id, id: cmdId, action: "write", target: target, value: value };
    var timer = window.setTimeout(function () {
      expirePending(device.id, cmdId);
    }, ACK_TIMEOUT_MS);

    device.pending[cmdId] = {
      id: cmdId,
      target: target,
      value: value,
      sentAt: Date.now(),
      timer: timer
    };
    device.pendingByTarget[target] = cmdId;
    var cell = yCells[target];
    if (cell) cell.root.classList.add("is-pending");
    log("cmd", "→ GỬI " + device.id + " · " + target + " = " + value + " · " + cmdId);

    try {
      state.client.publish(
        "plc/" + device.id + "/cmd",
        JSON.stringify(message),
        { qos: 1 },
        function (err) {
          if (!err) return;
          var failed = clearPending(device.id, cmdId);
          if (failed) log("err", "Gửi lệnh " + failed.target + " thất bại: " + errorText(err));
        }
      );
    } catch (err) {
      var failedNow = clearPending(device.id, cmdId);
      if (failedNow) log("err", "Gửi lệnh " + failedNow.target + " thất bại: " + errorText(err));
    }
  }

  function bindCell(cell, target) {
    if (!cell || !cell.root) return;
    cell.root.addEventListener("click", function () {
      sendCommand(target);
    });
  }

  function subscribeAll() {
    if (!state.client || !state.connected) return;
    for (var i = 0; i < SUBSCRIBE_TOPICS.length; i++) {
      state.client.subscribe(SUBSCRIBE_TOPICS[i], { qos: 1 }, function (err) {
        if (err) logErrorThrottled("Đăng ký topic thất bại: " + errorText(err));
      });
    }
  }

  function startMqtt() {
    var mqttLib = window.mqtt;
    if (!mqttLib || typeof mqttLib.connect !== "function") {
      setConn("error", "THIẾU MQTT.JS");
      setQuality(null);
      log(
        "err",
        "Không tìm thấy thư viện mqtt.js (vendor/mqtt.min.js) — giao diện không thể kết nối."
      );
      return;
    }

    var client;
    try {
      client = mqttLib.connect(BROKER_URL, {
        username: mqttUser,
        password: mqttPass,
        clientId: "web-" + Math.random().toString(16).slice(2, 10),
        clean: true,
        reconnectPeriod: 2000,
        keepalive: 30,
        connectTimeout: 10000,
        protocolVersion: 4
      });
    } catch (err) {
      setConn("error", "LỖI KHỞI TẠO");
      log("err", "Không khởi tạo được MQTT client: " + errorText(err));
      return;
    }
    state.client = client;

    client.on("connect", function () {
      state.connected = true;
      setConn("online");
      subscribeAll();
    });

    client.on("reconnect", function () {
      state.connected = false;
      if (!(typeof navigator !== "undefined" && navigator.onLine === false)) {
        setConn("reconnect");
      }
    });

    client.on("close", function () {
      state.connected = false;
      if (state.conn === "online") setConn("reconnect");
    });

    client.on("offline", function () {
      state.connected = false;
      if (typeof navigator !== "undefined" && navigator.onLine === false) {
        setConn("offline");
      } else {
        setConn("reconnect");
      }
    });

    client.on("error", function (err) {
      logErrorThrottled("MQTT: " + errorText(err));
    });

    client.on("message", function (topic, payload) {
      if (topic === TOPIC_LOG) {
        handleLogFile(payload);
        return;
      }
      var id = deviceIdFrom(topic, "config");
      if (id) {
        handleConfig(id, payload);
        return;
      }
      id = deviceIdFrom(topic, "telemetry");
      if (id) {
        handleTelemetry(id, payload);
        return;
      }
      id = deviceIdFrom(topic, "ack");
      if (id) {
        handleAck(id, payload);
        return;
      }
      id = deviceIdFrom(topic, "status");
      if (id) handleStatus(id, payload);
    });
  }

  function nextDeviceId() {
    var n = state.order.length + 1;
    while (n < 100) {
      var candidate = "dvp24-" + String(n).padStart(2, "0");
      if (!state.devices[candidate]) return candidate;
      n += 1;
    }
    return "dvp24-" + Date.now();
  }

  function openAddDialog() {
    if (!el.addDialog) return;
    el.addError.hidden = true;
    el.addError.textContent = "";
    el.addId.value = nextDeviceId();
    el.addName.value = "";
    el.addSlave.value = String(Math.min(247, state.order.length + 1));
    el.addCycle.value = "300";
    if (typeof el.addDialog.showModal === "function") el.addDialog.showModal();
    else el.addDialog.setAttribute("open", "");
    el.addId.focus();
    el.addId.select();
  }

  function closeAddDialog() {
    if (!el.addDialog) return;
    if (typeof el.addDialog.close === "function") el.addDialog.close();
    else el.addDialog.removeAttribute("open");
  }

  function submitAdd(event) {
    event.preventDefault();
    var id = el.addId.value.trim().toLowerCase();
    var name = el.addName.value.trim();
    var slave = Math.round(Number(el.addSlave.value));
    var cycle = Number(el.addCycle.value) || 300;
    var problem = "";

    if (!/^[a-z0-9](?:[a-z0-9-]{1,22}[a-z0-9])?$/.test(id)) {
      problem = "Mã mạch gồm a–z, 0–9 và dấu gạch, dài 3–24 ký tự.";
    } else if (state.devices[id]) {
      problem = "Mã " + id + " đã có trong hệ thống.";
    } else if (!(slave >= 1 && slave <= 247)) {
      problem = "Địa chỉ Modbus phải trong khoảng 1–247.";
    }

    if (!problem && (!state.client || !state.connected)) {
      problem = "Chưa kết nối broker — không gửi được cấu hình.";
    }

    if (problem) {
      el.addError.textContent = problem;
      el.addError.hidden = false;
      return;
    }

    var payload = {
      id: id,
      name: name || id,
      slave: slave,
      cycle: cycle
    };

    state.client.publish(
      "plc/" + id + "/config",
      JSON.stringify(payload),
      { qos: 1, retain: true },
      function (err) {
        if (err) {
          el.addError.textContent = "Gửi cấu hình thất bại: " + errorText(err);
          el.addError.hidden = false;
          return;
        }
        closeAddDialog();
        getDevice(id).name = payload.name;
        selectDevice(id);
        log("event", "Đã thêm mạch " + id + " (slave " + slave + ", " + cycle + " ms)");
      }
    );
  }

  window.addEventListener("offline", function () {
    state.connected = false;
    setConn("offline");
  });

  window.addEventListener("online", function () {
    if (state.client) setConn("reconnect");
  });

  function init() {
    for (var i = 0; i < 8; i++) {
      setCell(xCells["X" + i], 0);
      var yKey = "Y" + i;
      setCell(yCells[yKey], 0);
      bindCell(yCells[yKey], yKey);
    }
    for (var m = 0; m < 4; m++) {
      setCell(mCells["M" + m], 0);
    }

    if (el.fleetList) el.fleetList.setAttribute("role", "list");
    setQuality(null);
    setDeviceStatus(null);
    renderFleet();
    renderDetail();
    observeSpark();

    if (el.addDeviceBtn) el.addDeviceBtn.addEventListener("click", openAddDialog);
    if (el.exportLogBtn) el.exportLogBtn.addEventListener("click", exportLogCsv);
    if (el.addForm) el.addForm.addEventListener("submit", submitAdd);
    if (el.addClose) el.addClose.addEventListener("click", closeAddDialog);
    if (el.addCancel) el.addCancel.addEventListener("click", closeAddDialog);
    if (el.refreshBtn) {
      el.refreshBtn.addEventListener("click", function () {
        subscribeAll();
        renderFleet();
        renderDetail();
        log("event", "Đã làm mới giao diện và đăng ký lại topic");
      });
    }

    window.setInterval(function () {
      updateAge();
      for (var k = 0; k < state.order.length; k++) {
        updateFleetItem(state.devices[state.order[k]]);
      }
    }, 500);

    log("event", "Khởi động giao diện DVP24 SCADA");
    startMqtt();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
