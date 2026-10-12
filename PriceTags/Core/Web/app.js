'use strict';
const el = id => document.getElementById(id), input = el('barcode');
const storageKey = 'fanshop-price-tags-' + location.host;
let state = { count: 0, queue: [] }, sending = false;
try { const saved = JSON.parse(sessionStorage.getItem(storageKey)); if (saved && Array.isArray(saved.queue)) state = saved; } catch (_) {}
function persist() {
  try { sessionStorage.setItem(storageKey, JSON.stringify(state)); }
  catch (_) { el('help').textContent = 'Браузер не разрешил локальное сохранение. Не закрывайте страницу, пока есть ожидающие сканы.'; }
  el('count').textContent = state.count; el('pending').textContent = state.queue.length;
}
function focus() { if (!document.hidden) input.focus(); }
function showError(message) { el('result').className = 'error'; el('message').textContent = message; }
async function drain() {
  if (sending) return; sending = true;
  try {
    while (state.queue.length) {
      const scan = state.queue[0];
      let response;
      try { response = await fetch('/api/scan', {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(scan), signal:AbortSignal.timeout(8000)}); }
      catch (_) { showError('Нет ответа ноутбука. Скан сохранён в очереди; отправка будет повторена.'); break; }
      const data = await response.json();
      if (!response.ok) { showError(data.error || 'Ошибка отправки. Скан остаётся в очереди.'); break; }
      state.queue.shift(); state.count++; persist();
      el('result').className = data.found ? 'ok' : 'error';
      el('message').textContent = data.message; el('last').textContent = data.barcode;
      el('name').textContent = data.product?.name || '—'; el('article').textContent = data.product?.article || '—'; el('size').textContent = data.product?.characteristic || '—';
    }
  } catch (_) { showError('Не удалось прочитать ответ. Ожидающий скан будет отправлен повторно.'); }
  finally { sending = false; focus(); }
}
function newId() {
  // crypto.randomUUID requires a secure context; a LAN HTTP page is not one.
  const bytes = new Uint8Array(16); crypto.getRandomValues(bytes); bytes[6]=(bytes[6]&15)|64; bytes[8]=(bytes[8]&63)|128;
  const h = Array.from(bytes, b=>b.toString(16).padStart(2,'0')).join(''); return `${h.slice(0,8)}-${h.slice(8,12)}-${h.slice(12,16)}-${h.slice(16,20)}-${h.slice(20)}`;
}
el('form').addEventListener('submit', e => {
  e.preventDefault(); const barcode=input.value.trim(); input.value=''; focus(); if (!barcode) return;
  state.queue.push({barcode, requestId:newId()}); persist(); void drain();
});
el('retry').addEventListener('click', () => { void drain(); focus(); });
input.addEventListener('blur', () => setTimeout(focus,100)); document.addEventListener('visibilitychange', focus); window.addEventListener('focus',focus);
async function status() {
  try { const r=await fetch('/api/status',{signal:AbortSignal.timeout(4000)}); if (!r.ok) throw new Error(); const data=await r.json(); el('connection').textContent='Подключено · '+data.message; if(data.ready) void drain(); }
  catch (_) { el('connection').textContent='Нет соединения с ноутбуком'; }
}
persist(); focus(); void status(); setInterval(status,3000);
