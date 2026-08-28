const base = '/api/v1'
async function req(path, opts = {}) {
  const res = await fetch(base + path, {
    headers: { 'Content-Type': 'application/json' }, credentials: 'same-origin',
    ...opts, body: opts.body ? JSON.stringify(opts.body) : undefined
  })
  const text = await res.text(); const data = text ? JSON.parse(text) : null
  if (!res.ok) throw new Error(data?.error || `Lỗi ${res.status}`)
  return { data, cache: res.headers.get('X-Cache') }
}
export const api = {
  dashboard: () => req('/dashboard'),
  ctes: () => req('/ctes'),
  createCte: (b) => req('/ctes', { method: 'POST', body: b }),
  addKde: (id, b) => req(`/ctes/${id}/kdes`, { method: 'POST', body: b }),
  glns: () => req('/glns'),
  createGln: (b) => req('/glns', { method: 'POST', body: b }),
  lots: (q) => req(`/lots${q ? `?q=${encodeURIComponent(q)}` : ''}`),
  lot: (id) => req(`/lots/${id}`),
  createLot: (b) => req('/lots', { method: 'POST', body: b }),
  addEvent: (id, b) => req(`/lots/${id}/events`, { method: 'POST', body: b }),
  link: (id, b) => req(`/lots/${id}/link`, { method: 'POST', body: b }),
  trace: (code) => req(`/trace/${encodeURIComponent(code)}`)
}
export const fmtDate = (s) => s ? new Date(s).toLocaleDateString('vi-VN') : '—'
export const fmtDateTime = (s) => s ? new Date(s).toLocaleString('vi-VN') : '—'
export const GLNTYPES = ['Trang trại', 'Nhà máy', 'Kho', 'Cửa hàng', 'Vận chuyển']
export const parseKde = (json) => { try { return Object.entries(JSON.parse(json || '{}')) } catch { return [] } }
