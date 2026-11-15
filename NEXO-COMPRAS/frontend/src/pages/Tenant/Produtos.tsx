import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { Produto } from '../../types';
import {
  Search,
  Plus,
  AlertTriangle,
  Package,
  Check,
  Edit2,
  Send,
  TrendingDown,
  Layers,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight
} from 'lucide-react';

export const Produtos: React.FC = () => {
  const navigate = useNavigate();
  const [produtos, setProdutos] = useState<Produto[]>([]);
  const [total, setTotal] = useState(0);
  const [busca, setBusca] = useState('');
  const [apenasEstoqueBaixo, setApenasEstoqueBaixo] = useState(false);
  const [loading, setLoading] = useState(true);

  // Paginação completa
  const [paginaAtual, setPaginaAtual] = useState(1);
  const [tamanhoPagina, setTamanhoPagina] = useState(50);
  const totalPaginas = Math.max(1, Math.ceil(total / tamanhoPagina));

  // Seleção múltipla para cotação rápida
  const [selecionados, setSelecionados] = useState<string[]>([]);

  // Edição inline de estoque mínimo
  const [editandoMinimoId, setEditandoMinimoId] = useState<string | null>(null);
  const [novoMinimoValor, setNovoMinimoValor] = useState<string>('');

  // Modal novo produto
  const [showModal, setShowModal] = useState(false);
  const [codigoErp, setCodigoErp] = useState('');
  const [descricao, setDescricao] = useState('');
  const [unidade, setUnidade] = useState('UN');
  const [grupo, setGrupo] = useState('');
  const [categoria, setCategoria] = useState('');
  const [precoVenda, setPrecoVenda] = useState('');
  const [custoAtual, setCustoAtual] = useState('');
  const [estoqueAtual, setEstoqueAtual] = useState('');
  const [estoqueMinimo, setEstoqueMinimo] = useState('');

  const loadProdutos = async () => {
    setLoading(true);
    try {
      const query = `/produtos?q=${encodeURIComponent(busca)}&estoqueBaixo=${apenasEstoqueBaixo}&page=${paginaAtual}&size=${tamanhoPagina}`;
      const res = await api.get<{ total: number; itens: Produto[] }>(query);
      setProdutos(res.itens || []);
      setTotal(res.total || 0);
    } catch (err: any) {
      alert('Erro ao carregar produtos: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const timer = setTimeout(() => {
      loadProdutos();
    }, 250);
    return () => clearTimeout(timer);
  }, [busca, apenasEstoqueBaixo, paginaAtual, tamanhoPagina]);

  const handleBuscaChange = (val: string) => {
    setBusca(val);
    setPaginaAtual(1);
  };

  const handleEstoqueBaixoChange = (val: boolean) => {
    setApenasEstoqueBaixo(val);
    setPaginaAtual(1);
  };

  const toggleSelect = (id: string) => {
    setSelecionados((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  const selectAll = () => {
    if (selecionados.length === produtos.length) {
      setSelecionados([]);
    } else {
      setSelecionados(produtos.map((p) => p.id));
    }
  };

  const iniciarEdicaoMinimo = (p: Produto) => {
    setEditandoMinimoId(p.id);
    setNovoMinimoValor(p.estoqueMinimo.toString());
  };

  const salvarEstoqueMinimo = async (id: string) => {
    const val = parseFloat(novoMinimoValor);
    if (isNaN(val) || val < 0) {
      alert('Informe um estoque mínimo válido.');
      return;
    }
    try {
      await api.put(`/produtos/${id}/estoque-minimo`, { estoqueMinimo: val });
      setProdutos((prev) =>
        prev.map((p) => (p.id === id ? { ...p, estoqueMinimo: val } : p))
      );
      setEditandoMinimoId(null);
    } catch (err: any) {
      alert('Erro ao atualizar estoque mínimo: ' + err.message);
    }
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.post('/produtos', {
        codigoErp,
        descricao,
        unidade,
        grupo,
        categoria: categoria || 'Geral',
        precoVenda: parseFloat(precoVenda) || 0,
        custoAtual: parseFloat(custoAtual) || 0,
        estoqueAtual: parseFloat(estoqueAtual) || 0,
        estoqueMinimo: parseFloat(estoqueMinimo) || 0,
      });

      setShowModal(false);
      setCodigoErp('');
      setDescricao('');
      setGrupo('');
      setCategoria('');
      setPrecoVenda('');
      setCustoAtual('');
      setEstoqueAtual('');
      setEstoqueMinimo('');
      loadProdutos();
    } catch (err: any) {
      alert('Erro ao cadastrar produto: ' + err.message);
    }
  };

  const badgeNivel = (nivel: string) => {
    switch (nivel) {
      case 'Zerado':
        return <span className="badge badge-red" style={{ fontWeight: 800 }}>ZERADO</span>;
      case 'Critico':
        return <span className="badge badge-red">CRÍTICO</span>;
      case 'Baixo':
        return <span className="badge badge-yellow">BAIXO</span>;
      case 'Indeterminado':
        return <span className="badge" style={{ background: '#E5E7EB', color: '#374151' }} title="Estoque desconhecido (não informado pelo ERP)">INDETERMINADO</span>;
      default:
        return <span className="badge badge-green">NORMAL</span>;
    }
  };

  const irParaNovaCotacao = () => {
    if (selecionados.length === 0) return;
    navigate(`/cotacoes/nova?itens=${selecionados.join(',')}`);
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px', flexWrap: 'wrap', gap: '12px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
            Produtos e Monitoramento de Estoque
          </h1>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
            Catálogo unificado integrado ao Firebird ERP ({total} itens registrados).
          </p>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          {selecionados.length > 0 && (
            <button onClick={irParaNovaCotacao} className="btn btn-primary" style={{ backgroundColor: 'var(--green)' }}>
              <Send size={16} /> Criar Cotação com Selecionados ({selecionados.length})
            </button>
          )}

          <button onClick={() => setShowModal(true)} className="btn btn-secondary">
            <Plus size={16} /> Cadastrar Manualmente
          </button>
        </div>
      </div>

      {/* FILTROS */}
      <div className="card" style={{ marginBottom: '20px', padding: '14px 18px' }}>
        <div style={{ display: 'flex', gap: '16px', alignItems: 'center', flexWrap: 'wrap' }}>
          <div style={{ flex: 1, minWidth: '240px', position: 'relative' }}>
            <Search size={16} style={{ position: 'absolute', left: '10px', top: '11px', color: 'var(--text-muted)' }} />
            <input
              placeholder="Pesquisar por descrição, código ERP ou código de barras..."
              value={busca}
              onChange={(e) => handleBuscaChange(e.target.value)}
              style={{ paddingLeft: '34px' }}
            />
          </div>

          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', fontSize: '13.5px', fontWeight: 600, color: 'var(--text-main)' }}>
            <input
              type="checkbox"
              checked={apenasEstoqueBaixo}
              onChange={(e) => handleEstoqueBaixoChange(e.target.checked)}
              style={{ width: 'auto' }}
            />
            <span style={{ display: 'flex', alignItems: 'center', gap: '4px', color: 'var(--red)' }}>
              <AlertTriangle size={15} /> Apenas produtos em nível Baixo, Crítico ou Zerado
            </span>
          </label>
        </div>
      </div>

      {/* TABELA DE PRODUTOS */}
      <div className="card">
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th style={{ width: '40px', textAlign: 'center' }}>
                  <input
                    type="checkbox"
                    checked={produtos.length > 0 && selecionados.length === produtos.length}
                    onChange={selectAll}
                    style={{ width: 'auto' }}
                  />
                </th>
                <th>Código ERP</th>
                <th>Descrição do Produto</th>
                <th>Nível Estoque</th>
                <th style={{ textAlign: 'right' }}>Estoque Atual</th>
                <th style={{ textAlign: 'right' }}>Estoque Mín.</th>
                <th style={{ textAlign: 'right' }}>Consumo 90d</th>
                <th style={{ textAlign: 'right' }}>Sugestão Compra</th>
                <th style={{ textAlign: 'right' }}>Último Custo</th>
                <th style={{ textAlign: 'right' }}>Preço Venda</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={10} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                    Consultando estoque e produtos...
                  </td>
                </tr>
              ) : produtos.length === 0 ? (
                <tr>
                  <td colSpan={10} style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                    Nenhum produto encontrado. Sincronize com o ERP Firebird ou cadastre manualmente.
                  </td>
                </tr>
              ) : (
                produtos.map((p) => {
                  const check = selecionados.includes(p.id);
                  const isCritico = p.nivelEstoque === 'Zerado' || p.nivelEstoque === 'Critico';
                  return (
                    <tr key={p.id} style={{ backgroundColor: check ? 'var(--blue-light)' : undefined }}>
                      <td style={{ textAlign: 'center' }}>
                        <input
                          type="checkbox"
                          checked={check}
                          onChange={() => toggleSelect(p.id)}
                          style={{ width: 'auto' }}
                        />
                      </td>
                      <td style={{ fontFamily: 'monospace', fontWeight: 600 }}>{p.codigoErp || '—'}</td>
                      <td>
                        <div style={{ fontWeight: 600, color: 'var(--text-main)' }}>{p.descricao}</div>
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                          {p.grupo} • {p.unidade} {p.codigoBarras && `• EAN: ${p.codigoBarras}`}
                        </div>
                      </td>
                      <td>{badgeNivel(p.nivelEstoque)}</td>
                      <td style={{ textAlign: 'right' }}>
                        <span style={{ fontWeight: 700, color: isCritico ? 'var(--red)' : 'var(--text-main)' }}>
                          {p.estoqueAtual === null || p.estoqueAtual === undefined ? '—' : p.estoqueAtual.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                        </span>
                      </td>

                      {/* EDIÇÃO INLINE DE ESTOQUE MÍNIMO */}
                      <td style={{ textAlign: 'right' }}>
                        {editandoMinimoId === p.id ? (
                          <div style={{ display: 'flex', gap: '4px', justifyContent: 'flex-end', alignItems: 'center' }}>
                            <input
                              type="number"
                              step="any"
                              value={novoMinimoValor}
                              onChange={(e) => setNovoMinimoValor(e.target.value)}
                              style={{ width: '70px', padding: '4px', textAlign: 'right', fontSize: '12px' }}
                              autoFocus
                            />
                            <button
                              onClick={() => salvarEstoqueMinimo(p.id)}
                              className="btn btn-primary"
                              style={{ padding: '4px 6px' }}
                            >
                              <Check size={12} />
                            </button>
                          </div>
                        ) : (
                          <div
                            onClick={() => iniciarEdicaoMinimo(p)}
                            style={{
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '4px',
                              cursor: 'pointer',
                              padding: '2px 6px',
                              borderRadius: '4px',
                            }}
                            title="Clique para editar o estoque mínimo"
                          >
                            <span>{p.estoqueMinimo.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</span>
                            <Edit2 size={12} color="var(--text-muted)" />
                          </div>
                        )}
                      </td>

                      <td style={{ textAlign: 'right', color: 'var(--text-muted)' }}>
                        {p.consumo90Dias.toLocaleString('pt-BR', { minimumFractionDigits: 1 })}
                      </td>

                      <td style={{ textAlign: 'right' }}>
                        <span
                          style={{
                            fontWeight: 800,
                            color: p.sugestaoCompra > 0 ? 'var(--primary)' : 'var(--text-muted)',
                          }}
                        >
                          {p.sugestaoCompra > 0 ? p.sugestaoCompra.toLocaleString('pt-BR') : '—'}
                        </span>
                      </td>

                      <td style={{ textAlign: 'right' }}>
                        R$ {p.custoAtual.toFixed(2).replace('.', ',')}
                      </td>
                      <td style={{ textAlign: 'right', fontWeight: 600 }}>
                        R$ {p.precoVenda.toFixed(2).replace('.', ',')}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {/* BARRA DE PAGINAÇÃO COMPLETA */}
        <div style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: '16px 20px',
          borderTop: '1px solid var(--border)',
          flexWrap: 'wrap',
          gap: '12px',
          backgroundColor: '#FFFFFF',
          fontSize: '13px',
          color: 'var(--text-muted)'
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span>
              Exibindo <strong>{produtos.length > 0 ? (paginaAtual - 1) * tamanhoPagina + 1 : 0}</strong> a{' '}
              <strong>{Math.min(paginaAtual * tamanhoPagina, total)}</strong> de <strong>{total}</strong> produtos
            </span>
            <span style={{ margin: '0 6px' }}>•</span>
            <span>Itens por página:</span>
            <select
              value={tamanhoPagina}
              onChange={(e) => {
                setTamanhoPagina(Number(e.target.value));
                setPaginaAtual(1);
              }}
              style={{
                padding: '4px 8px',
                border: '1px solid var(--border)',
                borderRadius: '4px',
                fontSize: '12.5px',
                backgroundColor: '#fff',
                cursor: 'pointer'
              }}
            >
              <option value={25}>25</option>
              <option value={50}>50</option>
              <option value={100}>100</option>
              <option value={200}>200</option>
            </select>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            {/* PRIMEIRA PÁGINA */}
            <button
              onClick={() => setPaginaAtual(1)}
              disabled={paginaAtual <= 1 || loading}
              className="btn btn-secondary"
              style={{ padding: '6px 10px', fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px' }}
              title="Primeira página"
            >
              <ChevronsLeft size={14} /> Primeira
            </button>

            {/* PÁGINA ANTERIOR */}
            <button
              onClick={() => setPaginaAtual((p) => Math.max(1, p - 1))}
              disabled={paginaAtual <= 1 || loading}
              className="btn btn-secondary"
              style={{ padding: '6px 10px', fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px' }}
              title="Página anterior"
            >
              <ChevronLeft size={14} /> Anterior
            </button>

            {/* BOTÕES NUMÉRICOS (1, 2, 3...) */}
            <div style={{ display: 'flex', gap: '4px' }}>
              {(() => {
                const paginas: number[] = [];
                const inicio = Math.max(1, paginaAtual - 2);
                const fim = Math.min(totalPaginas, paginaAtual + 2);
                for (let i = inicio; i <= fim; i++) {
                  paginas.push(i);
                }
                return paginas.map((num) => (
                  <button
                    key={num}
                    onClick={() => setPaginaAtual(num)}
                    style={{
                      padding: '5px 10px',
                      borderRadius: '4px',
                      fontSize: '12.5px',
                      fontWeight: 600,
                      cursor: 'pointer',
                      border: num === paginaAtual ? '1px solid var(--blue-primary)' : '1px solid var(--border)',
                      backgroundColor: num === paginaAtual ? 'var(--blue-primary)' : '#FFFFFF',
                      color: num === paginaAtual ? '#FFFFFF' : 'var(--text-main)',
                      minWidth: '32px'
                    }}
                  >
                    {num}
                  </button>
                ));
              })()}
            </div>

            {/* PRÓXIMA PÁGINA */}
            <button
              onClick={() => setPaginaAtual((p) => Math.min(totalPaginas, p + 1))}
              disabled={paginaAtual >= totalPaginas || loading}
              className="btn btn-secondary"
              style={{ padding: '6px 10px', fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px' }}
              title="Próxima página"
            >
              Próxima <ChevronRight size={14} />
            </button>

            {/* BOTÃO PARA A ÚLTIMA PÁGINA */}
            <button
              onClick={() => setPaginaAtual(totalPaginas)}
              disabled={paginaAtual >= totalPaginas || loading}
              className="btn btn-secondary"
              style={{ padding: '6px 10px', fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px' }}
              title="Ir para a última página"
            >
              Última ({totalPaginas}) <ChevronsRight size={14} />
            </button>
          </div>
        </div>
      </div>

      {/* MODAL NOVO PRODUTO */}
      {showModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(30, 41, 59, 0.4)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 50,
            padding: '20px',
          }}
        >
          <div className="card" style={{ width: '100%', maxWidth: '600px', maxHeight: '90vh', overflowY: 'auto' }}>
            <div style={{ padding: '18px 24px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h2 style={{ fontSize: '18px', fontWeight: 700, color: 'var(--text-main)' }}>
                Novo Produto (Cadastro Manual)
              </h2>
              <button onClick={() => setShowModal(false)} style={{ background: 'none', border: 'none', fontSize: '20px', cursor: 'pointer' }}>
                ×
              </button>
            </div>

            <form onSubmit={handleCreate} style={{ padding: '24px' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '14px', marginBottom: '14px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                    Código ERP / Ref.
                  </label>
                  <input
                    value={codigoErp}
                    onChange={(e) => setCodigoErp(e.target.value)}
                    placeholder="Ex: PROD-100"
                  />
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                    Descrição do Produto *
                  </label>
                  <input
                    required
                    value={descricao}
                    onChange={(e) => setDescricao(e.target.value)}
                    placeholder="Ex: Arroz Branco Tipo 1 5kg"
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '14px', marginBottom: '14px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                    Unidade
                  </label>
                  <select value={unidade} onChange={(e) => setUnidade(e.target.value)}>
                    <option value="UN">UN</option>
                    <option value="KG">KG</option>
                    <option value="CX">CX</option>
                    <option value="FD">FD</option>
                    <option value="PCT">PCT</option>
                    <option value="LT">LT</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                    Grupo / Setor
                  </label>
                  <input
                    value={grupo}
                    onChange={(e) => setGrupo(e.target.value)}
                    placeholder="Ex: Alimentos"
                  />
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                    Categoria
                  </label>
                  <input
                    value={categoria}
                    onChange={(e) => setCategoria(e.target.value)}
                    placeholder="Ex: Grãos"
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                    Preço de Venda (R$)
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    value={precoVenda}
                    onChange={(e) => setPrecoVenda(e.target.value)}
                    placeholder="0,00"
                  />
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                    Custo Atual (R$)
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    value={custoAtual}
                    onChange={(e) => setCustoAtual(e.target.value)}
                    placeholder="0,00"
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '24px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                    Estoque Atual
                  </label>
                  <input
                    type="number"
                    step="any"
                    value={estoqueAtual}
                    onChange={(e) => setEstoqueAtual(e.target.value)}
                    placeholder="0"
                  />
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                    Estoque Mínimo de Segurança
                  </label>
                  <input
                    type="number"
                    step="any"
                    value={estoqueMinimo}
                    onChange={(e) => setEstoqueMinimo(e.target.value)}
                    placeholder="0"
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button type="button" onClick={() => setShowModal(false)} className="btn btn-secondary">
                  Cancelar
                </button>
                <button type="submit" className="btn btn-primary">
                  Cadastrar Produto
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
