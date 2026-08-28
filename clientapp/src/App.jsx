import React, { useEffect, useState } from 'react'
import { Routes, Route, NavLink, Outlet } from 'react-router-dom'
import { api, fmtDate, fmtDateTime, GLNTYPES, parseKde } from './api'

function Badge({ text, css }) { return <span className={`badge ${css || 'secondary'}`}>{text}</span> }
function Flash({ msg }) { return msg ? <div className={`flash ${msg.ok ? 'ok' : 'err'}`}>{msg.text}</div> : null }
function Modal({ title, onClose, wide, children }) {
  return (
    <div className="modal-bg" onClick={onClose}>
      <div className="modal" style={wide ? { maxWidth: 740 } : undefined} onClick={e => e.stopPropagation()}>
        <div className="row" style={{ marginBottom: 12 }}><h2 style={{ flex: 1, margin: 0 }}>{title}</h2>
          <button className="btn gray sm" style={{ flex: 'none' }} onClick={onClose}>Đóng</button></div>{children}
      </div>
    </div>
  )
}
function Field({ label, children }) { return <div style={{ flex: 1 }}><label>{label}</label>{children}</div> }

function Layout() {
  return (
    <>
      <nav className="nav"><span className="brand">🌾 MiniOrigin</span>
        <NavLink to="/" end>Tổng quan</NavLink><NavLink to="/lots">Lô SX</NavLink>
        <NavLink to="/ctes">Sự kiện (CTE)</NavLink><NavLink to="/glns">Địa điểm (GLN)</NavLink><NavLink to="/trace">Tra cứu</NavLink></nav>
      <div className="wrap"><Outlet /></div>
    </>
  )
}

function Dashboard() {
  const [d, setD] = useState(null); const [cache, setCache] = useState('')
  useEffect(() => { api.dashboard().then(r => { setD(r.data); setCache(r.cache) }) }, [])
  if (!d) return <p className="muted">Đang tải…</p>
  return (
    <>
      <h1>Tổng quan nguồn gốc {cache && <span className="pill">cache: {cache}</span>}</h1>
      <div className="grid kpis" style={{ marginBottom: 18 }}>
        <div className="kpi"><div className="v">{d.lots}</div><div className="l">Lô sản xuất</div></div>
        <div className="kpi"><div className="v">{d.events}</div><div className="l">Sự kiện</div></div>
        <div className="kpi"><div className="v">{d.ctes}</div><div className="l">Loại sự kiện (CTE)</div></div>
        <div className="kpi"><div className="v">{d.glns}</div><div className="l">Địa điểm (GLN)</div></div>
      </div>
      <div className="card"><h2>Lô gần đây</h2>
        <table><tbody>{d.recent.map(l => <tr key={l.id}><td style={{ fontFamily: 'monospace' }}>{l.code}</td><td>{l.productName}</td><td className="right"><span className="pill">{l.status}</span></td></tr>)}</tbody></table>
      </div>
    </>
  )
}

function Lots() {
  const [rows, setRows] = useState([]); const [q, setQ] = useState(''); const [open, setOpen] = useState(null); const [show, setShow] = useState(false)
  const load = () => api.lots(q).then(r => setRows(r.data))
  useEffect(() => { load() }, [])
  return (
    <>
      <div className="toolbar"><h1 style={{ margin: 0, flex: 'none' }}>Lô sản xuất</h1><div className="sp" />
        <input style={{ maxWidth: 220 }} placeholder="Tìm mã lô/SP…" value={q} onChange={e => setQ(e.target.value)} onKeyDown={e => e.key === 'Enter' && load()} />
        <button className="btn ghost sm" style={{ flex: 'none' }} onClick={load}>Tìm</button>
        <button className="btn sm" style={{ flex: 'none' }} onClick={() => setShow(true)}>+ Tạo lô</button></div>
      <div className="card" style={{ padding: 0, overflow: 'auto' }}>
        <table><thead><tr><th>Mã lô</th><th>Sản phẩm</th><th>Xuất xứ</th><th className="right">SL</th><th className="right">Sự kiện</th><th>Trạng thái</th></tr></thead>
          <tbody>{rows.map(l => (
            <tr key={l.id} style={{ cursor: 'pointer' }} onClick={() => setOpen(l.id)}>
              <td style={{ fontFamily: 'monospace' }}>{l.code}</td><td>{l.productName}</td><td>{l.origin || '—'}</td>
              <td className="right">{l.quantity} {l.unit}</td><td className="right">{l.events}</td><td><Badge text={l.statusText} css={l.statusCss} /></td></tr>))}
            {rows.length === 0 && <tr><td colSpan={6} className="muted" style={{ padding: 20 }}>Chưa có lô.</td></tr>}</tbody></table>
      </div>
      {open && <LotDetail id={open} onClose={() => setOpen(null)} onChanged={load} />}
      {show && <LotForm onClose={() => setShow(false)} onSaved={() => { setShow(false); load() }} />}
    </>
  )
}

function LotDetail({ id, onClose, onChanged }) {
  const [l, setL] = useState(null); const [ctes, setCtes] = useState([]); const [glns, setGlns] = useState([]); const [msg, setMsg] = useState(null)
  const [ev, setEv] = useState({ cteId: '', glnId: '', operator: '', kde: {} }); const [parent, setParent] = useState('')
  const load = () => api.lot(id).then(r => setL(r.data))
  useEffect(() => { load(); api.ctes().then(r => setCtes(r.data)); api.glns().then(r => setGlns(r.data)) }, [id])
  const flash = (ok, text) => { setMsg({ ok, text }); setTimeout(() => setMsg(null), 3000) }
  if (!l) return <Modal title="…" onClose={onClose}><p className="muted">Đang tải…</p></Modal>
  const cte = ctes.find(c => c.id === Number(ev.cteId))
  const addEvent = async () => {
    try { const r = await api.addEvent(id, { cteId: Number(ev.cteId), glnId: ev.glnId ? Number(ev.glnId) : null, operator: ev.operator, kde: ev.kde }); flash(true, r.data.msg); setEv({ cteId: '', glnId: '', operator: '', kde: {} }); load(); onChanged() }
    catch (e) { flash(false, e.message) }
  }
  const linkParent = async () => { try { const r = await api.link(id, { parentCode: parent }); flash(true, r.data.msg); setParent(''); load() } catch (e) { flash(false, e.message) } }
  return (
    <Modal title={`Lô ${l.code}`} onClose={onClose} wide>
      <Flash msg={msg} />
      <dl className="dl"><dt>Sản phẩm</dt><dd>{l.productName}</dd><dt>Xuất xứ</dt><dd>{l.origin || '—'}</dd>
        <dt>Số lượng</dt><dd>{l.quantity} {l.unit}</dd><dt>Trạng thái</dt><dd>{l.statusText}</dd></dl>
      {l.parents.length > 0 && <><div className="section-t">Nguyên liệu (lô cha)</div><div>{l.parents.map(p => <span key={p.id} className="pill" style={{ marginRight: 6 }}>{p.code} · {p.productName}</span>)}</div></>}
      <div className="section-t">Hành trình sự kiện</div>
      <div style={{ borderLeft: '2px solid var(--line)', paddingLeft: 14, marginLeft: 6 }}>
        {l.events.map((e, i) => (
          <div key={i} style={{ marginBottom: 12, position: 'relative' }}>
            <div style={{ position: 'absolute', left: -21, top: 3, width: 12, height: 12, borderRadius: 6, background: 'var(--brand)' }} />
            <b>{e.cteName}</b> <span className="muted" style={{ fontSize: 12 }}>{fmtDateTime(e.eventTime)}</span><br />
            <span className="muted">{e.gln || ''}{e.operator ? ` · ${e.operator}` : ''}</span>
            {parseKde(e.kde).length > 0 && <div style={{ marginTop: 2 }}>{parseKde(e.kde).map(([k, v]) => <span key={k} className="pill" style={{ marginRight: 4 }}>{k}: {v}</span>)}</div>}
          </div>))}
      </div>
      <div className="card" style={{ background: '#f8fafc', marginTop: 10 }}>
        <div className="section-t">Ghi sự kiện mới</div>
        <div className="row"><Field label="Loại sự kiện"><select value={ev.cteId} onChange={e => setEv({ ...ev, cteId: e.target.value, kde: {} })}><option value="">—</option>{ctes.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></Field>
          <Field label="Địa điểm"><select value={ev.glnId} onChange={e => setEv({ ...ev, glnId: e.target.value })}><option value="">—</option>{glns.map(g => <option key={g.id} value={g.id}>{g.name}</option>)}</select></Field>
          <Field label="Người thực hiện"><input value={ev.operator} onChange={e => setEv({ ...ev, operator: e.target.value })} /></Field></div>
        {cte?.kdes?.length > 0 && <div className="row" style={{ marginTop: 6 }}>{cte.kdes.map(k => (
          <Field key={k.id} label={k.label + (k.unit ? ` (${k.unit})` : '')}><input value={ev.kde[k.key] || ''} onChange={e => setEv({ ...ev, kde: { ...ev.kde, [k.key]: e.target.value } })} /></Field>))}</div>}
        <div style={{ marginTop: 10 }}><button className="btn sm" onClick={addEvent} disabled={!ev.cteId}>Ghi sự kiện</button></div>
      </div>
      <div className="card" style={{ background: '#fffbeb', marginTop: 8 }}><div className="row">
        <Field label="Liên kết lô cha (mã lô nguyên liệu)"><input value={parent} onChange={e => setParent(e.target.value)} /></Field>
        <div style={{ flex: 'none', alignSelf: 'flex-end' }}><button className="btn sm" onClick={linkParent} disabled={!parent}>+ Liên kết</button></div></div></div>
    </Modal>
  )
}

function LotForm({ onClose, onSaved }) {
  const [glns, setGlns] = useState([]); const [f, setF] = useState({ code: '', productName: '', unit: 'kg', quantity: 0, originGlnId: '' }); const [err, setErr] = useState('')
  useEffect(() => { api.glns().then(r => setGlns(r.data)) }, [])
  const up = (k, v) => setF({ ...f, [k]: v })
  const save = async () => { try { if (!f.productName) { setErr('Cần tên SP'); return } await api.createLot({ ...f, quantity: Number(f.quantity), originGlnId: f.originGlnId ? Number(f.originGlnId) : null }); onSaved() } catch (e) { setErr(e.message) } }
  return (
    <Modal title="Tạo lô sản xuất" onClose={onClose}>
      {err && <Flash msg={{ ok: false, text: err }} />}
      <div className="row"><Field label="Mã lô (để trống = tự sinh)"><input value={f.code} onChange={e => up('code', e.target.value)} /></Field>
        <Field label="Sản phẩm *"><input value={f.productName} onChange={e => up('productName', e.target.value)} /></Field></div>
      <div className="row"><Field label="Số lượng"><input type="number" value={f.quantity} onChange={e => up('quantity', e.target.value)} /></Field>
        <Field label="ĐVT"><input value={f.unit} onChange={e => up('unit', e.target.value)} /></Field>
        <Field label="Xuất xứ (GLN)"><select value={f.originGlnId} onChange={e => up('originGlnId', e.target.value)}><option value="">—</option>{glns.map(g => <option key={g.id} value={g.id}>{g.name}</option>)}</select></Field></div>
      <div style={{ marginTop: 16 }}><button className="btn" onClick={save}>Tạo lô</button></div>
    </Modal>
  )
}

function Ctes() {
  const [rows, setRows] = useState([]); const [open, setOpen] = useState(null); const [nf, setNf] = useState({ name: '', code: '' }); const [err, setErr] = useState('')
  const load = () => api.ctes().then(r => setRows(r.data))
  useEffect(() => { load() }, [])
  const add = async () => { try { if (!nf.name) return; await api.createCte({ ...nf, ordinal: rows.length }); setNf({ name: '', code: '' }); load() } catch (e) { setErr(e.message) } }
  return (
    <>
      <h1>Loại sự kiện (CTE) & trường dữ liệu (KDE)</h1>{err && <Flash msg={{ ok: false, text: err }} />}
      <div className="card"><div className="row"><Field label="Mã"><input value={nf.code} onChange={e => setNf({ ...nf, code: e.target.value })} /></Field>
        <Field label="Tên loại sự kiện"><input value={nf.name} onChange={e => setNf({ ...nf, name: e.target.value })} /></Field>
        <div style={{ flex: 'none', alignSelf: 'flex-end' }}><button className="btn" onClick={add}>+ Thêm CTE</button></div></div></div>
      {rows.map(c => (
        <div className="card" key={c.id}>
          <div className="row"><h2 style={{ flex: 1, margin: 0 }}>{c.name} <span className="pill">{c.code}</span></h2>
            <button className="btn ghost sm" style={{ flex: 'none' }} onClick={() => setOpen(c.id)}>+ KDE</button></div>
          {c.kdes.length > 0 ? <table><tbody>{c.kdes.map(k => <tr key={k.id}><td>{k.label}</td><td className="muted">{k.key}</td><td>{k.unit || ''}</td><td>{k.required ? 'bắt buộc' : ''}</td></tr>)}</tbody></table> : <p className="muted">Chưa có trường dữ liệu.</p>}
          {open === c.id && <KdeForm cteId={c.id} ord={c.kdes.length} onClose={() => setOpen(null)} onSaved={() => { setOpen(null); load() }} />}
        </div>))}
    </>
  )
}

function KdeForm({ cteId, ord, onClose, onSaved }) {
  const [f, setF] = useState({ key: '', label: '', unit: '', required: false }); const [err, setErr] = useState('')
  const save = async () => { try { if (!f.key || !f.label) { setErr('Cần key + nhãn'); return } await api.addKde(cteId, { ...f, ordinal: ord }); onSaved() } catch (e) { setErr(e.message) } }
  return (
    <Modal title="Thêm trường dữ liệu (KDE)" onClose={onClose}>
      {err && <Flash msg={{ ok: false, text: err }} />}
      <div className="row"><Field label="Key *"><input value={f.key} onChange={e => setF({ ...f, key: e.target.value })} /></Field>
        <Field label="Nhãn *"><input value={f.label} onChange={e => setF({ ...f, label: e.target.value })} /></Field>
        <Field label="Đơn vị"><input value={f.unit} onChange={e => setF({ ...f, unit: e.target.value })} /></Field></div>
      <label style={{ display: 'flex', gap: 6, alignItems: 'center', marginTop: 8 }}><input type="checkbox" style={{ width: 'auto' }} checked={f.required} onChange={e => setF({ ...f, required: e.target.checked })} /> Bắt buộc</label>
      <div style={{ marginTop: 16 }}><button className="btn" onClick={save}>Lưu</button></div>
    </Modal>
  )
}

function Glns() {
  const [rows, setRows] = useState([]); const [f, setF] = useState({ name: '', code: '', type: 0, address: '' }); const [err, setErr] = useState('')
  const load = () => api.glns().then(r => setRows(r.data))
  useEffect(() => { load() }, [])
  const add = async () => { try { if (!f.name) return; await api.createGln({ ...f, type: Number(f.type) }); setF({ name: '', code: '', type: 0, address: '' }); load() } catch (e) { setErr(e.message) } }
  return (
    <>
      <h1>Địa điểm (GLN)</h1>{err && <Flash msg={{ ok: false, text: err }} />}
      <div className="card"><div className="row">
        <Field label="Tên *"><input value={f.name} onChange={e => setF({ ...f, name: e.target.value })} /></Field>
        <Field label="Loại"><select value={f.type} onChange={e => setF({ ...f, type: e.target.value })}>{GLNTYPES.map((t, i) => <option key={i} value={i}>{t}</option>)}</select></Field>
        <Field label="Địa chỉ"><input value={f.address} onChange={e => setF({ ...f, address: e.target.value })} /></Field>
        <div style={{ flex: 'none', alignSelf: 'flex-end' }}><button className="btn" onClick={add}>+ Thêm</button></div></div></div>
      <div className="card" style={{ padding: 0, overflow: 'auto' }}>
        <table><thead><tr><th>Mã</th><th>Tên</th><th>Loại</th><th>Địa chỉ</th></tr></thead>
          <tbody>{rows.map(g => <tr key={g.id}><td>{g.code}</td><td>{g.name}</td><td>{g.type}</td><td>{g.address || '—'}</td></tr>)}</tbody></table>
      </div>
    </>
  )
}

function TraceNode({ t, level = 0 }) {
  return (
    <div style={{ marginLeft: level * 16, borderLeft: level ? '2px solid var(--line)' : 'none', paddingLeft: level ? 12 : 0 }}>
      <div className="card" style={{ marginBottom: 8 }}>
        <b>{t.product}</b> <span className="pill">{t.code}</span> <span className="muted">{t.status}</span>
        <div style={{ marginTop: 6 }}>{t.events.map((e, i) => (
          <div key={i} style={{ fontSize: 13, marginBottom: 3 }}>• <b>{e.cte}</b> {e.gln ? `@ ${e.gln}` : ''} <span className="muted">{fmtDateTime(e.when)}</span>
            {e.kdes.length > 0 && ' — ' + e.kdes.map(k => `${k.label}: ${k.value}${k.unit || ''}`).join(', ')}</div>))}</div>
      </div>
      {t.parents.map((p, i) => <TraceNode key={i} t={p} level={level + 1} />)}
    </div>
  )
}

function Trace() {
  const [code, setCode] = useState(''); const [res, setRes] = useState(null); const [err, setErr] = useState(null)
  const doTrace = async () => { try { const r = await api.trace(code.trim()); setRes(r.data); setErr(null) } catch (e) { setErr(e.message); setRes(null) } }
  return (
    <>
      <h1>Tra cứu nguồn gốc theo lô</h1>
      <div className="card"><div className="row"><Field label="Mã lô"><input value={code} onChange={e => setCode(e.target.value)} onKeyDown={e => e.key === 'Enter' && doTrace()} /></Field>
        <div style={{ flex: 'none', alignSelf: 'flex-end' }}><button className="btn" onClick={doTrace}>Tra cứu</button></div></div></div>
      {err && <Flash msg={{ ok: false, text: err }} />}
      {res && <TraceNode t={res} />}
    </>
  )
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Layout />}>
        <Route index element={<Dashboard />} />
        <Route path="lots" element={<Lots />} />
        <Route path="ctes" element={<Ctes />} />
        <Route path="glns" element={<Glns />} />
        <Route path="trace" element={<Trace />} />
      </Route>
    </Routes>
  )
}
