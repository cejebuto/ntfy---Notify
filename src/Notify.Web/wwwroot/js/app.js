(() => {
  'use strict';

  // ---------- Constants ----------

  const STORAGE_KEY = 'notify.topic';
  const TOPIC_RE = /^[-_A-Za-z0-9]{1,64}$/;
  const MAX_TAGS = 10;
  const TOAST_MS = 3500;
  const SVG_NS = 'http://www.w3.org/2000/svg';

  const PRIORITY_LABELS = { 1: 'Mínima', 2: 'Baja', 3: 'Normal', 4: 'Alta', 5: 'Urgente' };
  const SOURCE_LABELS = {
    manual: 'Manual',
    'server-down': 'Servidor caído',
    'db-updated': 'BD actualizada',
    'process-completed': 'Proceso completado',
  };
  // Tags that ntfy renders as emojis in the mobile app (shown as a preview only).
  const EMOJI_TAGS = {
    rotating_light: '🚨', skull: '💀', floppy_disk: '💾', gear: '⚙️',
    white_check_mark: '✅', tada: '🎉', warning: '⚠️', rocket: '🚀',
  };

  // ---------- State ----------

  const state = {
    config: null,   // { topic, baseUrl, subscribeUrl }
    history: { items: [], sent: 0, failed: 0 },
    busy: false,
  };

  // ---------- DOM refs ----------

  const $ = (id) => document.getElementById(id);
  const els = {
    healthPill: $('health-pill'),
    healthText: $('health-text'),
    headerTopic: $('header-topic'),
    topicInput: $('topic-input'),
    topicError: $('topic-error'),
    topicLink: $('topic-link'),
    btnCopy: $('btn-copy'),
    btnRegen: $('btn-regen'),
    btnSave: $('btn-save'),
    form: $('send-form'),
    fTitle: $('f-title'),
    fMessage: $('f-message'),
    msgCounter: $('msg-counter'),
    msgError: $('msg-error'),
    fTags: $('f-tags'),
    tagsPreview: $('tags-preview'),
    btnSend: $('btn-send'),
    respStatus: $('response-status'),
    respEmpty: $('response-empty'),
    respBody: $('response-body'),
    simButtons: Array.from(document.querySelectorAll('.sim')),
    histCounters: $('hist-counters'),
    histEmpty: $('hist-empty'),
    histWrap: $('hist-wrap'),
    histBody: $('hist-body'),
    btnClear: $('btn-clear'),
    toasts: $('toasts'),
  };

  // ---------- DOM helpers ----------

  function h(tag, props = {}, ...children) {
    const el = document.createElement(tag);
    for (const [key, value] of Object.entries(props)) {
      if (value == null || value === false) continue;
      if (key === 'class') el.className = value;
      else if (key === 'text') el.textContent = value;
      else if (key === 'dataset') Object.assign(el.dataset, value);
      else el.setAttribute(key, value === true ? '' : value);
    }
    for (const child of children.flat()) {
      if (child == null) continue;
      el.append(child instanceof Node ? child : document.createTextNode(String(child)));
    }
    return el;
  }

  function icon(name, extraClass = '') {
    const svg = document.createElementNS(SVG_NS, 'svg');
    svg.setAttribute('class', `icon ${extraClass}`.trim());
    svg.setAttribute('aria-hidden', 'true');
    const use = document.createElementNS(SVG_NS, 'use');
    use.setAttribute('href', `#i-${name}`);
    svg.append(use);
    return svg;
  }

  // ---------- Toasts ----------

  function toast(message, kind = 'success') {
    const node = h('div', { class: `toast toast--${kind}` },
      icon(kind === 'success' ? 'check' : 'x'),
      h('span', { text: message }));
    els.toasts.append(node);
    setTimeout(() => {
      node.classList.add('is-leaving');
      setTimeout(() => node.remove(), 220);
    }, TOAST_MS);
  }

  // ---------- API ----------

  class ApiError extends Error {}

  /** Small fetch wrapper. Throws ApiError with a readable message; shows a toast unless options.silent. */
  async function api(path, options = {}) {
    const { silent = false, body, method = 'GET' } = options;
    const init = { method, headers: { Accept: 'application/json' } };
    if (body !== undefined) {
      init.headers['Content-Type'] = 'application/json';
      init.body = JSON.stringify(body);
    }

    let response;
    try {
      response = await fetch(path, init);
    } catch {
      const message = 'No se pudo contactar al backend';
      if (!silent) toast(message, 'error');
      throw new ApiError(message);
    }

    if (response.status === 204) return null;

    let data = null;
    try {
      data = await response.json();
    } catch {
      /* body is not JSON */
    }

    if (!response.ok) {
      const message = (data && data.error) || `Error ${response.status} del backend`;
      if (!silent) toast(message, 'error');
      throw new ApiError(message);
    }
    return data;
  }

  // ---------- Formatting ----------

  const pad = (n) => String(n).padStart(2, '0');

  function formatDate(iso) {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '—';
    return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ` +
           `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
  }

  function parseTags(raw) {
    const seen = new Set();
    for (const part of raw.split(',')) {
      const tag = part.trim();
      if (tag) seen.add(tag);
    }
    return Array.from(seen).slice(0, MAX_TAGS);
  }

  function prettyJson(raw) {
    if (!raw) return null;
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  }

  function readStoredTopic() {
    try { return localStorage.getItem(STORAGE_KEY); } catch { return null; }
  }

  function storeTopic(topic) {
    try { localStorage.setItem(STORAGE_KEY, topic); } catch { /* storage unavailable */ }
  }

  // ---------- Render ----------

  function renderConfig() {
    const cfg = state.config;
    if (!cfg) return;
    els.topicInput.value = cfg.topic;
    els.headerTopic.textContent = cfg.topic;
    els.topicLink.href = cfg.subscribeUrl;
    els.topicLink.replaceChildren(document.createTextNode(cfg.subscribeUrl), icon('external', 'icon--sm'));
    clearTopicError();
  }

  function showTopicError(message) {
    els.topicError.textContent = message;
    els.topicError.hidden = false;
    els.topicInput.setAttribute('aria-invalid', 'true');
  }

  function clearTopicError() {
    els.topicError.hidden = true;
    els.topicError.textContent = '';
    els.topicInput.removeAttribute('aria-invalid');
  }

  function chip(tag) {
    const emoji = EMOJI_TAGS[tag];
    return h('li', { class: 'chip', title: emoji ? `ntfy lo muestra como ${emoji}` : null },
      emoji ? h('span', { class: 'chip__emoji', 'aria-hidden': 'true', text: emoji }) : null,
      tag);
  }

  function renderTagsPreview() {
    els.tagsPreview.replaceChildren(...parseTags(els.fTags.value).map(chip));
  }

  function renderCounter() {
    const len = els.fMessage.value.length;
    const max = els.fMessage.maxLength;
    els.msgCounter.textContent = `${len} / ${max}`;
    els.msgCounter.classList.toggle('counter--near', len > max * 0.9);
  }

  function statusBadge(success) {
    return h('span', { class: `badge ${success ? 'badge--ok' : 'badge--fail'}` },
      icon(success ? 'check' : 'x'),
      success ? 'Enviado' : 'Falló');
  }

  function renderResponse(result) {
    const meta = [];
    if (result.httpStatus != null) meta.push(`HTTP ${result.httpStatus}`);
    meta.push(`${result.durationMs} ms`);

    els.respStatus.replaceChildren(
      statusBadge(result.success),
      h('span', { class: 'response__meta', text: meta.join(' · ') }));
    els.respStatus.hidden = false;

    const body = prettyJson(result.rawResponse) || result.error || '';
    const text = !result.success && result.error && result.rawResponse
      ? `${result.error}\n\n${body}`
      : body;
    els.respBody.textContent = text;
    els.respBody.hidden = false;
    els.respEmpty.hidden = true;
  }

  function priorityBadge(priority) {
    const label = PRIORITY_LABELS[priority] || '';
    return h('span', { class: `badge badge--p${priority}`, text: `${priority} · ${label}` });
  }

  function td(label, className, ...children) {
    return h('td', { 'data-label': label, class: className }, ...children);
  }

  function historyRow(entry) {
    const failed = entry.status === 'Failed';
    const isManual = entry.source === 'manual';

    const origin = td('Origen', 'cell-source',
      h('span', { class: 'badge badge--source', text: isManual ? 'Manual' : 'Evento' }),
      isManual ? null : h('span', { class: 'src-key', text: SOURCE_LABELS[entry.source] || entry.source }));

    const statusCell = td('Estado', 'cell-status', statusBadge(!failed));
    if (failed) {
      if (entry.httpStatus != null) statusCell.append(h('span', { class: 'ntfy-id', text: `HTTP ${entry.httpStatus}` }));
      statusCell.append(h('details', { class: 'err-details' },
        h('summary', {}, icon('chevron'), 'Ver error'),
        h('div', { class: 'err-details__text', text: entry.error || 'Error desconocido' })));
    } else if (entry.ntfyId) {
      statusCell.append(h('span', { class: 'ntfy-id', text: entry.ntfyId }));
    }

    const tags = h('ul', { class: 'chips' }, (entry.tags || []).map(chip));

    return h('tr', {},
      td('Fecha y hora', 'cell-time', formatDate(entry.sentAt)),
      origin,
      td('Título', 'cell-title', entry.title ? entry.title : h('span', { class: 'muted', text: '—' })),
      td('Mensaje', 'cell-message', h('span', { title: entry.message, text: entry.message })),
      td('Prioridad', 'cell-priority', priorityBadge(entry.priority)),
      td('Etiquetas', 'cell-tags', (entry.tags || []).length ? tags : h('span', { class: 'muted', text: '—' })),
      statusCell);
  }

  function renderHistory() {
    const { items, sent, failed } = state.history;
    els.histCounters.replaceChildren(
      h('span', { class: 'counters__ok', text: `${sent} ${sent === 1 ? 'enviada' : 'enviadas'}` }),
      ' · ',
      h('span', { class: 'counters__fail', text: `${failed} ${failed === 1 ? 'fallida' : 'fallidas'}` }));

    const empty = items.length === 0;
    els.histEmpty.hidden = !empty;
    els.histWrap.hidden = empty;
    els.btnClear.disabled = empty;
    els.histBody.replaceChildren(...items.map(historyRow));
  }

  function renderBusy() {
    const busy = state.busy;
    els.btnSend.disabled = busy;
    els.simButtons.forEach((b) => { b.disabled = busy; });
  }

  function setHealth(ok) {
    els.healthPill.classList.remove('pill--pending', 'pill--ok', 'pill--down');
    els.healthPill.classList.add(ok ? 'pill--ok' : 'pill--down');
    els.healthText.textContent = ok ? 'Backend OK' : 'Backend sin conexión';
  }

  // ---------- Actions ----------

  async function loadHistory() {
    try {
      state.history = await api('/api/history');
      renderHistory();
    } catch { /* toast already shown */ }
  }

  async function saveTopic() {
    const value = els.topicInput.value.trim();
    if (!value) { showTopicError('El topic es obligatorio'); return; }
    if (!TOPIC_RE.test(value)) {
      showTopicError('Solo letras, números, guiones y guion bajo (máx. 64 caracteres)');
      return;
    }
    els.btnSave.disabled = true;
    try {
      state.config = await api('/api/config', { method: 'PUT', body: { topic: value }, silent: true });
      storeTopic(state.config.topic);
      renderConfig();
      toast('Topic guardado');
    } catch (err) {
      showTopicError(err.message);
    } finally {
      els.btnSave.disabled = false;
    }
  }

  async function regenerateTopic() {
    const ok = window.confirm(
      'Se va a generar un topic nuevo y el actual dejará de funcionar.\n\n' +
      'Vas a tener que volver a suscribirte en el iPhone. ¿Continuar?');
    if (!ok) return;
    els.btnRegen.disabled = true;
    try {
      state.config = await api('/api/config/regenerate', { method: 'POST' });
      storeTopic(state.config.topic);
      renderConfig();
      toast('Topic regenerado');
    } catch { /* toast already shown */ } finally {
      els.btnRegen.disabled = false;
    }
  }

  async function copyTopic() {
    const topic = els.topicInput.value.trim() || (state.config ? state.config.topic : '');
    try {
      await navigator.clipboard.writeText(topic);
      toast('Topic copiado');
    } catch {
      els.topicInput.select();
      toast('No se pudo copiar automáticamente: el topic quedó seleccionado', 'error');
    }
  }

  /** Runs a publish request while keeping the UI locked and reflecting the outcome. */
  async function runSend(path, body, loadingEl) {
    if (state.busy) return;
    state.busy = true;
    loadingEl.classList.add('is-loading');
    renderBusy();
    try {
      const result = await api(path, { method: 'POST', body });
      renderResponse(result);
      if (result.success) toast('Notificación enviada');
      else toast(result.error || 'ntfy.sh rechazó la notificación', 'error');
      await loadHistory();
    } catch { /* toast already shown */ } finally {
      state.busy = false;
      loadingEl.classList.remove('is-loading');
      renderBusy();
    }
  }

  function submitForm(event) {
    event.preventDefault();
    const message = els.fMessage.value.trim();
    if (!message) {
      els.msgError.textContent = 'El mensaje es obligatorio';
      els.msgError.hidden = false;
      els.fMessage.setAttribute('aria-invalid', 'true');
      els.fMessage.focus();
      return;
    }
    els.msgError.hidden = true;
    els.fMessage.removeAttribute('aria-invalid');

    const priority = Number(els.form.elements.priority.value) || 3;
    runSend('/api/notifications', {
      title: els.fTitle.value.trim(),
      message,
      priority,
      tags: parseTags(els.fTags.value),
    }, els.btnSend);
  }

  async function clearHistory() {
    els.btnClear.disabled = true;
    try {
      await api('/api/history', { method: 'DELETE' });
      state.history = { items: [], sent: 0, failed: 0 };
      renderHistory();
      toast('Historial limpiado');
    } catch {
      renderHistory();
    }
  }

  // ---------- Events ----------

  function bindEvents() {
    els.btnCopy.addEventListener('click', copyTopic);
    els.btnRegen.addEventListener('click', regenerateTopic);
    els.btnSave.addEventListener('click', saveTopic);
    els.topicInput.addEventListener('input', clearTopicError);
    els.topicInput.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') { e.preventDefault(); saveTopic(); }
    });

    els.form.addEventListener('submit', submitForm);
    els.fMessage.addEventListener('input', () => {
      renderCounter();
      if (els.fMessage.value.trim()) {
        els.msgError.hidden = true;
        els.fMessage.removeAttribute('aria-invalid');
      }
    });
    els.fTags.addEventListener('input', renderTagsPreview);

    els.simButtons.forEach((button) => {
      button.addEventListener('click', () => {
        runSend(`/api/simulate/${encodeURIComponent(button.dataset.event)}`, undefined, button);
      });
    });

    els.btnClear.addEventListener('click', clearHistory);
  }

  // ---------- Init ----------

  async function init() {
    bindEvents();
    renderCounter();
    renderTagsPreview();
    renderHistory();

    try {
      state.config = await api('/api/config');
      const saved = readStoredTopic();
      if (saved && saved !== state.config.topic && TOPIC_RE.test(saved)) {
        // Keep the same topic across backend restarts so the phone stays subscribed.
        try {
          state.config = await api('/api/config', { method: 'PUT', body: { topic: saved }, silent: true });
        } catch { /* keep the server's topic */ }
      }
      storeTopic(state.config.topic);
      renderConfig();
    } catch { /* toast already shown */ }

    await loadHistory();

    try {
      await api('/api/health', { silent: true });
      setHealth(true);
    } catch {
      setHealth(false);
    }
  }

  init();
})();
