const express = require('express');
const path = require('path');
const config = require('./lib/config');
const store = require('./lib/store');
const bm = require('./lib/bm');
const cr = require('./lib/crypto');
const A = require('./lib/analysis');
const Calc = require('./public/calc');

const app = express();
app.disable('x-powered-by');
app.use(express.json({ limit: '1mb' }));
app.use((req, res, next) => {
  res.setHeader('X-Content-Type-Options', 'nosniff');
  res.setHeader('Referrer-Policy', 'no-referrer');
  res.setHeader('X-Frame-Options', 'DENY');
  next();
});

// ---------------- Controle de acesso ----------------
// Rotas públicas (fornecedor): somente /f/*, /api/f/* e os arquivos da página do fornecedor.
// Todo o resto é do comprador e só responde a acessos locais diretos (sem proxy/túnel).
const PUBLIC = [/^\/f\/[\w-]+$/, /^\/api\/f\//, /^\/fornecedor\.(js|css)$/, /^\/calc\.js$/, /^\/app\.css$/, /^\/favicon\.svg$/];
const LOCAL_HOSTS = new Set(['localhost', '127.0.0.1', '[::1]']);
app.use((req, res, next) => {
  if (PUBLIC.some((r) => r.test(req.path))) return next();
  const host = String(req.headers.host || '').replace(/:\d+$/, '').toLowerCase();
  const ip = req.socket.remoteAddress || '';
  const loopback = ip === '127.0.0.1' || ip === '::1' || ip === '::ffff:127.0.0.1';
  const proxied = req.headers['x-forwarded-for'] || req.headers['x-forwarded-host'];
  if (loopback && !proxied && LOCAL_HOSTS.has(host)) return next();
  res.status(403).type('text').send('Acesso restrito ao computador do comprador.');
});

const wrap = (fn) => (req, res) => Promise.resolve(fn(req, res)).catch((e) => { console.error(e); res.status(500).json({ erro: e.message }); });
const ok = (res, d) => res.json(d === undefined ? { ok: true } : d);
const bad = (res, msg, code = 400) => res.status(code).json({ erro: msg });
const nowIso = () => new Date().toISOString();

// ---------------- BM / sincronização ----------------
app.get('/api/sync', (req, res) => { const s = bm.load(); ok(res, s ? { syncedAt: s.syncedAt, refDate: s.refDate, produtos: s.produtos.length, fornecedores: s.fornecedores.length } : { syncedAt: null }); });
app.post('/api/sync', wrap(async (req, res) => {
  try { const s = await bm.sync(); ok(res, { syncedAt: s.syncedAt, refDate: s.refDate, produtos: s.produtos.length, fornecedores: s.fornecedores.length, ms: s.tookMs }); }
  catch (e) { bad(res, `Não foi possível ler o BM.FDB: ${e.message}. Verifique se o servidor Firebird está em execução.`, 502); }
}));

app.get('/api/config', (req, res) => ok(res, store.get().config));
app.put('/api/config', (req, res) => {
  const c = store.get().config; const b = req.body || {};
  for (const k of Object.keys(c)) if (b[k] !== undefined) c[k] = typeof c[k] === 'number' ? Number(b[k]) : typeof c[k] === 'boolean' ? !!b[k] : String(b[k]).trim();
  c.baseUrl = c.baseUrl.replace(/\/+$/, '');
  store.save(); ok(res, c);
});

app.get('/api/dashboard', (req, res) => ok(res, A.dashboard()));
app.get('/api/alertas', (req, res) => ok(res, A.alertas({ incluirSemGiro: req.query.semGiro === '1' })));
app.get('/api/produtos', (req, res) => ok(res, A.produtos({ q: String(req.query.q || ''), grupo: String(req.query.grupo || ''), page: Number(req.query.page) || 1 })));
app.get('/api/produtos/:id', (req, res) => { const p = A.produtoDetalhe(Number(req.params.id)); p ? ok(res, p) : bad(res, 'Produto não encontrado', 404); });
app.post('/api/candidatos', (req, res) => {
  const ids = (req.body && req.body.produtos) || [];
  ok(res, Object.fromEntries(ids.map((id) => [id, A.candidatosProduto(Number(id))])));
});

// ---------------- Fornecedores ----------------
app.get('/api/fornecedores', (req, res) => {
  const hist = new Map();
  const snap = bm.load();
  if (snap) for (const e of snap.entradas) { const h = hist.get(e.idPessoa) || { total: 0, notas: 0, ultima: '' }; h.total += e.total; h.notas++; if (e.data > h.ultima) h.ultima = e.data; hist.set(e.idPessoa, h); }
  ok(res, A.allSuppliers().map((s) => ({ ...s, historico: s.origem === 'bm' ? hist.get(s.id) || null : null })));
});
const supFields = ['nome', 'fantasia', 'cnpj', 'email', 'whatsapp', 'celular', 'fone', 'contato', 'cidade', 'uf', 'prazoEntrega', 'endereco'];
app.post('/api/fornecedores', (req, res) => {
  const b = req.body || {};
  if (!String(b.nome || '').trim()) return bad(res, 'Informe o nome do fornecedor');
  const st = store.get();
  const f = { id: ++st.seq.fornecedor, criadoEm: nowIso() };
  supFields.forEach((k) => { f[k] = k === 'prazoEntrega' ? Number(b[k]) || 0 : String(b[k] || '').trim(); });
  st.fornecedores.push(f); store.save(); ok(res, { ...f, key: `app:${f.id}` });
});
app.put('/api/fornecedores/:key', (req, res) => {
  const st = store.get(); const key = req.params.key; const b = req.body || {};
  if (key.startsWith('app:')) {
    const f = st.fornecedores.find((x) => `app:${x.id}` === key);
    if (!f) return bad(res, 'Fornecedor não encontrado', 404);
    supFields.forEach((k) => { if (b[k] !== undefined) f[k] = k === 'prazoEntrega' ? Number(b[k]) || 0 : String(b[k]).trim(); });
  } else if (key.startsWith('bm:')) { // BM é somente leitura: guardamos apenas contatos complementares
    const o = st.overrides[key] || {};
    ['email', 'whatsapp', 'contato', 'prazoEntrega'].forEach((k) => { if (b[k] !== undefined) o[k] = k === 'prazoEntrega' ? Number(b[k]) || 0 : String(b[k]).trim(); });
    st.overrides[key] = o;
  } else return bad(res, 'Chave inválida');
  store.save(); ok(res);
});
app.delete('/api/fornecedores/:key', (req, res) => {
  const st = store.get(); const key = req.params.key;
  if (!key.startsWith('app:')) return bad(res, 'Fornecedores do BM não podem ser excluídos pelo aplicativo');
  st.fornecedores = st.fornecedores.filter((x) => `app:${x.id}` !== key); store.save(); ok(res);
});
app.get('/api/fornecedores/:key/historico', (req, res) => { const h = A.historicoFornecedor(req.params.key); h ? ok(res, h) : bad(res, 'Fornecedor não encontrado', 404); });

// ---------------- Cotações / pedidos ----------------
const linkOf = (p) => `${store.get().config.baseUrl}/f/${p.publicId}#c=${cr.openWithMaster(p.codeSealed)}`;
const codeOf = (p) => cr.openWithMaster(p.codeSealed);
const keyOf = (p) => cr.deriveKey(codeOf(p), p.salt);
function respostaOf(p) { if (!p.respostaEnc) return null; try { return cr.decrypt(p.respostaEnc, keyOf(p)); } catch { return null; } }
const evento = (p, status, nota) => { p.status = status; p.eventos.push({ t: nowIso(), status, nota: nota || '' }); };
const findCot = (id) => store.get().cotacoes.find((c) => c.id === Number(id));
const EM_ANDAMENTO = ['aprovado', 'confirmado', 'faturado', 'transporte', 'recebido'];

function payloadFor(cot, p) {
  const cfg = store.get().config;
  return {
    numero: cot.numero, titulo: cot.titulo, observacoes: cot.observacoes, expiraEm: p.expiraEm,
    empresa: { nome: cfg.empresa, cnpj: cfg.cnpj, contato: cfg.contato, email: cfg.email },
    fornecedor: p.nomeFornecedor,
    itens: p.itensIdx.map((i) => { const it = cot.itens[i]; return { idx: i, codigo: it.codigo, nome: it.nome, unidade: it.unidade, qtd: it.qtd, referencia: it.referencia || '' }; }),
  };
}
function summary(c) {
  const sup = A.supplierMap();
  return {
    id: c.id, numero: c.numero, titulo: c.titulo, criadoEm: c.criadoEm, status: c.status, itens: c.itens.length, economia: c.economia || null,
    pedidos: c.pedidos.map((p) => ({ fk: p.fk, nome: A.supplierName(sup.get(p.fk)) || p.nomeFornecedor, status: p.expiraEm < nowIso() && ['enviado', 'visualizado'].includes(p.status) ? 'expirado' : p.status, totalAprovado: p.totalAprovado || 0 })),
  };
}
app.get('/api/cotacoes', (req, res) => ok(res, store.get().cotacoes.slice().reverse().map(summary)));

app.post('/api/cotacoes', (req, res) => {
  const b = req.body || {}; const st = store.get(); const ix = A.indexes();
  if (!ix) return bad(res, 'Sincronize com o BM.FDB primeiro');
  const itensIn = Array.isArray(b.itens) ? b.itens : [];
  if (!itensIn.length) return bad(res, 'Selecione ao menos um produto');
  const envios = Array.isArray(b.envios) ? b.envios.filter((e) => Array.isArray(e.itens) && e.itens.length) : [];
  if (!envios.length) return bad(res, 'Selecione ao menos um fornecedor com itens');
  const sup = A.supplierMap();
  const itens = [];
  for (const [i, it] of itensIn.entries()) {
    const p = ix.prodById.get(Number(it.produtoId));
    if (!p) return bad(res, `Produto ${it.produtoId} inválido`);
    const qtd = Number(it.qtd);
    if (!(qtd > 0)) return bad(res, `Quantidade inválida para ${p.nome}`);
    const lc = ix.lastCost.get(p.id);
    itens.push({ idx: i, produtoId: p.id, codigo: p.codigo, nome: p.nome, unidade: p.unidade, referencia: p.referencia, qtd, ultimoCusto: lc ? Math.round(lc.custo * 10000) / 10000 : 0, precoVenda: p.precoVenda });
  }
  const dias = Number(b.validadeDias) || st.config.validadeDias;
  const cot = { id: ++st.seq.cotacao, numero: `PC-${new Date().getFullYear()}-${String(st.seq.cotacao).padStart(4, '0')}`, titulo: String(b.titulo || '').trim() || 'Pedido de compra', observacoes: String(b.observacoes || '').trim(), criadoEm: nowIso(), validadeDias: dias, itens, pedidos: [], status: 'aberta', escolhas: {} };
  for (const e of envios) {
    const s = sup.get(e.fornecedorKey);
    if (!s) return bad(res, `Fornecedor inválido: ${e.fornecedorKey}`);
    if (cot.pedidos.some((p) => p.fk === s.key)) continue;
    const code = cr.generateCode(); const salt = cr.newSalt();
    const p = { fk: s.key, nomeFornecedor: A.supplierName(s), publicId: cr.publicId(), salt, codeSealed: cr.sealWithMaster(code), itensIdx: e.itens.map(Number).filter((i) => itens[i]), respostaEnc: null, status: 'enviado', eventos: [], tentativas: 0, bloqueadoAte: null, expiraEm: new Date(Date.now() + dias * 86400000).toISOString() };
    evento(p, 'enviado', 'Pedido criado e link gerado');
    p.payloadEnc = cr.encrypt(payloadFor(cot, p), cr.deriveKey(code, salt));
    cot.pedidos.push(p);
  }
  st.cotacoes.push(cot); store.save();
  ok(res, { id: cot.id });
});

function detail(cot) {
  const sup = A.supplierMap();
  const respostas = {};
  const pedidos = cot.pedidos.map((p) => {
    const r = respostaOf(p); if (r) respostas[p.fk] = r;
    const s = sup.get(p.fk) || {};
    return { fk: p.fk, nome: A.supplierName(s) || p.nomeFornecedor, email: s.email || '', whatsapp: s.whatsapp || s.celular || '', status: p.status, expirado: p.expiraEm < nowIso(), expiraEm: p.expiraEm, itensIdx: p.itensIdx, eventos: p.eventos, link: linkOf(p), codigo: codeOf(p),
      respondidoEm: p.respondidoEm || null, visualizadoEm: p.visualizadoEm || null, previsaoEntrega: p.previsaoEntrega || '', totalAprovado: p.totalAprovado || 0, itensAprovados: p.itensAprovados || [], bloqueado: !!(p.bloqueadoAte && p.bloqueadoAte > nowIso()) };
  });
  const comparacao = Object.keys(respostas).length ? A.compararCotacao({ ...cot, pedidos: cot.pedidos.filter((p) => respostas[p.fk]) }, respostas) : null;
  return { id: cot.id, numero: cot.numero, titulo: cot.titulo, observacoes: cot.observacoes, criadoEm: cot.criadoEm, status: cot.status, itens: cot.itens, pedidos, respostas, comparacao, escolhas: cot.escolhas, economia: cot.economia || null, creditoIcms: store.get().config.creditoIcms };
}
app.get('/api/cotacoes/:id', (req, res) => { const c = findCot(req.params.id); c ? ok(res, detail(c)) : bad(res, 'Cotação não encontrada', 404); });

app.post('/api/cotacoes/:id/aprovar', (req, res) => {
  const cot = findCot(req.params.id); if (!cot) return bad(res, 'Cotação não encontrada', 404);
  if (cot.status !== 'aberta') return bad(res, 'Esta cotação já foi encerrada');
  const escolhas = (req.body && req.body.escolhas) || {};
  const respostas = {}; cot.pedidos.forEach((p) => { const r = respostaOf(p); if (r) respostas[p.fk] = r; });
  const cmp = A.compararCotacao({ ...cot, pedidos: cot.pedidos.filter((p) => respostas[p.fk]) }, respostas);
  const sel = {};
  for (const [i, fk] of Object.entries(escolhas)) {
    const f = cmp.fornecedores.find((x) => x.fk === fk);
    if (f && f.itens[i] && f.itens[i].calc) sel[i] = fk;
  }
  if (!Object.keys(sel).length) return bad(res, 'Escolha ao menos um item com proposta válida');
  cot.escolhas = sel;
  for (const p of cot.pedidos) {
    const meus = Object.entries(sel).filter(([, fk]) => fk === p.fk).map(([i]) => Number(i));
    if (meus.length) {
      const f = cmp.fornecedores.find((x) => x.fk === p.fk);
      p.itensAprovados = meus;
      p.totalAprovado = Calc.r2(meus.reduce((s, i) => s + f.itens[i].calc.total, 0) + Calc.n(respostas[p.fk].frete));
      evento(p, 'aprovado', `Aprovado: ${meus.length} item(ns)`);
    } else if (p.status !== 'cancelado') evento(p, 'recusado', 'Proposta não selecionada');
  }
  cot.status = 'aprovada'; cot.aprovadoEm = nowIso();
  cot.economia = A.calcularEconomia(cot, cmp, sel);
  store.save(); ok(res, detail(cot));
});

app.post('/api/cotacoes/:id/pedidos/:fk/status', (req, res) => {
  const cot = findCot(req.params.id); if (!cot) return bad(res, 'Cotação não encontrada', 404);
  const p = cot.pedidos.find((x) => x.fk === req.params.fk); if (!p) return bad(res, 'Pedido não encontrado', 404);
  const { status, nota, previsao } = req.body || {};
  if (!['confirmado', 'faturado', 'transporte', 'recebido', 'cancelado'].includes(status)) return bad(res, 'Status inválido');
  if (!EM_ANDAMENTO.includes(p.status) && status !== 'cancelado') return bad(res, 'Apenas pedidos aprovados podem avançar de etapa');
  if (previsao) p.previsaoEntrega = String(previsao);
  evento(p, status, nota);
  if (cot.pedidos.every((x) => ['recebido', 'cancelado', 'recusado'].includes(x.status))) cot.status = 'concluida';
  store.save(); ok(res, detail(cot));
});
app.post('/api/cotacoes/:id/pedidos/:fk/renovar', (req, res) => { // novo código + nova validade (revoga o anterior)
  const cot = findCot(req.params.id); if (!cot) return bad(res, 'Cotação não encontrada', 404);
  const p = cot.pedidos.find((x) => x.fk === req.params.fk); if (!p) return bad(res, 'Pedido não encontrado', 404);
  if (EM_ANDAMENTO.includes(p.status)) return bad(res, 'Pedido já aprovado');
  const resp = respostaOf(p);
  const code = cr.generateCode(); p.salt = cr.newSalt(); p.codeSealed = cr.sealWithMaster(code); p.publicId = cr.publicId();
  p.expiraEm = new Date(Date.now() + cot.validadeDias * 86400000).toISOString(); p.tentativas = 0; p.bloqueadoAte = null;
  const key = cr.deriveKey(code, p.salt);
  p.payloadEnc = cr.encrypt(payloadFor(cot, p), key);
  if (resp) p.respostaEnc = cr.encrypt(resp, key);
  evento(p, resp ? 'respondido' : 'enviado', 'Novo código gerado; link anterior revogado');
  store.save(); ok(res, detail(cot));
});
app.post('/api/cotacoes/:id/cancelar', (req, res) => {
  const cot = findCot(req.params.id); if (!cot) return bad(res, 'Cotação não encontrada', 404);
  if (cot.status !== 'aberta') return bad(res, 'Só é possível cancelar cotações abertas');
  cot.status = 'cancelada'; cot.pedidos.forEach((p) => evento(p, 'cancelado', 'Cotação cancelada pelo comprador'));
  store.save(); ok(res, detail(cot));
});

// ---------------- Área do fornecedor (pública, protegida por código) ----------------
const MAX_TENTATIVAS = 8;
function authPedido(req, res) {
  const found = (() => { for (const c of store.get().cotacoes) for (const p of c.pedidos) if (p.publicId === req.params.pid) return { c, p }; return null; })();
  const code = (req.body && req.body.codigo) || '';
  if (!found) { bad(res, 'Link ou código inválido', 404); return null; }
  const { c, p } = found;
  if (p.bloqueadoAte && p.bloqueadoAte > nowIso()) { bad(res, 'Muitas tentativas incorretas. Tente novamente mais tarde.', 429); return null; }
  let payload;
  try { payload = cr.decrypt(p.payloadEnc, cr.deriveKey(code, p.salt)); }
  catch {
    p.tentativas = (p.tentativas || 0) + 1;
    if (p.tentativas >= MAX_TENTATIVAS) { p.bloqueadoAte = new Date(Date.now() + 15 * 60000).toISOString(); p.tentativas = 0; }
    store.save(); bad(res, 'Link ou código inválido', 403); return null;
  }
  p.tentativas = 0;
  return { c, p, payload, key: cr.deriveKey(code, p.salt) };
}
const publicStatus = (p) => (p.expiraEm < nowIso() && ['enviado', 'visualizado'].includes(p.status) ? 'expirado' : p.status);

app.post('/api/f/:pid/abrir', (req, res) => {
  const a = authPedido(req, res); if (!a) return;
  const { p, payload, key } = a;
  if (p.status === 'enviado') { p.visualizadoEm = nowIso(); evento(p, 'visualizado', 'Fornecedor abriu o pedido'); }
  store.save();
  let resposta = null; if (p.respostaEnc) try { resposta = cr.decrypt(p.respostaEnc, key); } catch { /* ignore */ }
  ok(res, { ...payload, status: publicStatus(p), editavel: ['enviado', 'visualizado', 'respondido'].includes(p.status) && p.expiraEm >= nowIso(), resposta, itensAprovados: p.itensAprovados || [], previsaoEntrega: p.previsaoEntrega || '' });
});

const num = (v, min = 0, max = 1e9) => { const x = Number(String(v ?? '').replace(',', '.')); return isFinite(x) ? Math.min(max, Math.max(min, x)) : 0; };
app.post('/api/f/:pid/responder', (req, res) => {
  const a = authPedido(req, res); if (!a) return;
  const { c, p, key } = a;
  if (!['enviado', 'visualizado', 'respondido'].includes(p.status) || c.status !== 'aberta') return bad(res, 'Este pedido não aceita mais respostas.', 409);
  if (p.expiraEm < nowIso()) return bad(res, 'O prazo para resposta expirou. Solicite um novo link ao comprador.', 410);
  const r = (req.body && req.body.resposta) || {};
  const itens = [];
  for (const idx of p.itensIdx) {
    const i = (r.itens || []).find((x) => Number(x.idx) === idx) || {};
    const disponivel = i.disponivel !== false;
    itens.push({
      idx, disponivel, qtd: disponivel ? num(i.qtd, 0, 1e7) : 0, preco: disponivel ? num(i.preco, 0, 1e7) : 0, descPerc: num(i.descPerc, 0, 100),
      cst: String(i.cst || '').replace(/\D/g, '').slice(0, 3), icmsPerc: num(i.icmsPerc, 0, 100), ipiPerc: num(i.ipiPerc, 0, 100),
      mva: num(i.mva, 0, 1000), stPerc: num(i.stPerc, 0, 100), stValor: num(i.stValor, 0, 1e8), obs: String(i.obs || '').slice(0, 200),
    });
  }
  if (!itens.some((i) => i.disponivel && i.preco > 0 && i.qtd > 0)) return bad(res, 'Informe preço e quantidade de ao menos um item.');
  const resposta = {
    itens, frete: num(r.frete, 0, 1e8), prazoPagamento: String(r.prazoPagamento || '').slice(0, 80), prazoEntrega: num(r.prazoEntrega, 0, 365),
    validade: String(r.validade || '').slice(0, 10), contato: String(r.contato || '').slice(0, 120), observacoes: String(r.observacoes || '').slice(0, 1000), respondidoEm: nowIso(),
  };
  p.respostaEnc = cr.encrypt(resposta, key); p.respondidoEm = resposta.respondidoEm;
  evento(p, 'respondido', p.eventos.some((e) => e.status === 'respondido') ? 'Proposta atualizada pelo fornecedor' : 'Proposta recebida');
  store.save(); ok(res, { status: p.status });
});

app.get('/f/:pid', (req, res) => res.sendFile(path.join(__dirname, 'public', 'fornecedor.html')));
app.use(express.static(path.join(__dirname, 'public'), { index: 'index.html' }));

if (require.main === module) {
  store.get(); bm.load();
  app.listen(config.port, '0.0.0.0', () => {
    console.log(`\n  Pedidos de Compra rodando em http://localhost:${config.port}\n`);
    if (!bm.load()) bm.sync().then(() => console.log('  BM.FDB sincronizado.')).catch((e) => console.error('  Falha ao sincronizar BM.FDB:', e.message));
  });
}
module.exports = app;
