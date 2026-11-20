import React, { useState, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { api } from '../../services/api';
import { Produto, Fornecedor, CotacaoCriada } from '../../types';
import {
  ArrowLeft,
  Plus,
  Trash2,
  Send,
  Copy,
  Check,
  AlertCircle,
  Clock,
  ShieldCheck,
  Users,
  Package
} from 'lucide-react';

interface ItemCotacaoForm {
  produtoId: string;
  codigoErp: string;
  descricao: string;
  unidade: string;
  quantidade: number;
  estoqueAtual: number;
  estoqueMinimo: number;
  custoAtual: number;
}

export const NovaCotacao: React.FC = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  const [titulo, setTitulo] = useState('');
  const [observacoes, setObservacoes] = useState('');
  const [prazoDias, setPrazoDias] = useState(3);

  const [itens, setItens] = useState<ItemCotacaoForm[]>([]);
  const [fornecedoresSelecionados, setFornecedoresSelecionados] = useState<string[]>([]);

  const [todosProdutos, setTodosProdutos] = useState<Produto[]>([]);
  const [todosFornecedores, setTodosFornecedores] = useState<Fornecedor[]>([]);
  const [loading, setLoading] = useState(true);
  const [salvando, setSalvando] = useState(false);
  const [resultado, setResultado] = useState<CotacaoCriada | null>(null);
  const [copiadoId, setCopiadoId] = useState<string | null>(null);

  // Modal para adicionar produtos
  const [buscaProduto, setBuscaProduto] = useState('');
  const [showAddProduto, setShowAddProduto] = useState(false);

  useEffect(() => {
    carregarDadosIniciais();
  }, []);

  const carregarDadosIniciais = async () => {
    setLoading(true);
    try {
      const [resProd, resForn] = await Promise.all([
        api.get<any>('/produtos?size=200'),
        api.get<any>('/fornecedores?size=200'),
      ]);

      const listaProd: Produto[] = Array.isArray(resProd) ? resProd : (resProd?.itens || []);
      const listaForn = Array.isArray(resForn) ? resForn : (resForn?.itens || []);

      setTodosProdutos(listaProd);
      setTodosFornecedores(listaForn.map((x: any) => x.fornecedor || x).filter((f: any) => f.ativo));

      // Se veio com itens pré-selecionados pela URL (ex: produtos com estoque baixo)
      const paramItens = searchParams.get('itens');
      if (paramItens) {
        const ids = paramItens.split(',');
        const selecionados = listaProd
          .filter((p: Produto) => ids.includes(p.id))
          .map((p: Produto) => ({
            produtoId: p.id,
            codigoErp: p.codigoErp,
            descricao: p.descricao,
            unidade: p.unidade,
            quantidade: p.sugestaoCompra > 0 ? p.sugestaoCompra : Math.max(1, p.estoqueMinimo - (p.estoqueAtual ?? 0)),
            estoqueAtual: p.estoqueAtual ?? 0,
            estoqueMinimo: p.estoqueMinimo,
            custoAtual: p.custoAtual,
          }));
        setItens(selecionados);
      }
    } catch (err: any) {
      alert('Erro ao carregar catálogo: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  const adicionarProduto = (p: Produto) => {
    if (itens.some((i) => i.produtoId === p.id)) return;
    const qtdSugerida = p.sugestaoCompra > 0 ? p.sugestaoCompra : Math.max(1, p.estoqueMinimo - (p.estoqueAtual ?? 0));
    setItens((prev) => [
      ...prev,
      {
        produtoId: p.id,
        codigoErp: p.codigoErp,
        descricao: p.descricao,
        unidade: p.unidade,
        quantidade: qtdSugerida,
        estoqueAtual: p.estoqueAtual ?? 0,
        estoqueMinimo: p.estoqueMinimo,
        custoAtual: p.custoAtual,
      },
    ]);
  };

  const removerItem = (produtoId: string) => {
    setItens((prev) => prev.filter((i) => i.produtoId !== produtoId));
  };

  const alterarQuantidade = (produtoId: string, qtd: number) => {
    setItens((prev) =>
      prev.map((i) => (i.produtoId === produtoId ? { ...i, quantidade: Math.max(0.1, qtd) } : i))
    );
  };

  const toggleFornecedor = (id: string) => {
    setFornecedoresSelecionados((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  const selecionarTodosFornecedores = () => {
    if (fornecedoresSelecionados.length === todosFornecedores.length) {
      setFornecedoresSelecionados([]);
    } else {
      setFornecedoresSelecionados(todosFornecedores.map((f) => f.id));
    }
  };

  const handleSalvar = async () => {
    if (itens.length === 0) {
      alert('Selecione pelo menos um produto para cotar.');
      return;
    }
    if (fornecedoresSelecionados.length === 0) {
      alert('Selecione pelo menos um fornecedor para enviar a solicitação.');
      return;
    }

    setSalvando(true);
    try {
      const payload = {
        titulo: titulo.trim() || `Cotação de Reposição - ${new Date().toLocaleDateString('pt-BR')}`,
        observacoes: observacoes.trim(),
        prazoRespostaDias: prazoDias,
        itens: itens.map((i) => ({
          produtoId: i.produtoId,
          quantidade: i.quantidade,
        })),
        fornecedorIds: fornecedoresSelecionados,
      };

      const res = await api.post<CotacaoCriada>('/cotacoes', payload);
      setResultado(res);
    } catch (err: any) {
      alert('Erro ao criar cotação: ' + err.message);
    } finally {
      setSalvando(false);
    }
  };

  const copiarTexto = (id: string, texto: string) => {
    navigator.clipboard.writeText(texto);
    setCopiadoId(id);
    setTimeout(() => setCopiadoId(null), 2500);
  };

  if (resultado) {
    return (
      <div style={{ maxWidth: '900px', margin: '0 auto' }}>
        <div className="card" style={{ padding: '32px', textAlign: 'center', marginBottom: '24px' }}>
          <div
            style={{
              width: '64px',
              height: '64px',
              borderRadius: '50%',
              backgroundColor: 'var(--green-light)',
              color: 'var(--green)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              margin: '0 auto 16px',
            }}
          >
            <ShieldCheck size={36} />
          </div>
          <h2 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', marginBottom: '8px' }}>
            Cotação {resultado.numero} Criada com Sucesso!
          </h2>
          <p style={{ color: 'var(--text-muted)', fontSize: '15px', maxWidth: '600px', margin: '0 auto 24px' }}>
            Links exclusivos com tokens seguros e códigos de acesso individuais foram gerados para cada fornecedor convidado.
          </p>

          <div style={{ display: 'flex', justifyContent: 'center', gap: '12px' }}>
            <button onClick={() => navigate('/cotacoes')} className="btn btn-primary">
              Ver Cotações em Andamento
            </button>
            <button
              onClick={() => {
                setResultado(null);
                setItens([]);
                setFornecedoresSelecionados([]);
                setTitulo('');
              }}
              className="btn btn-secondary"
            >
              Criar Outra Cotação
            </button>
          </div>
        </div>

        {/* LISTA DE LINKS E CÓDIGOS PARA OS FORNECEDORES */}
        <div className="card" style={{ padding: '24px' }}>
          <h3 style={{ fontSize: '18px', fontWeight: 700, marginBottom: '6px', color: 'var(--text-main)' }}>
            Links de Acesso Seguro para Fornecedores
          </h3>
          <p style={{ fontSize: '13.5px', color: 'var(--text-muted)', marginBottom: '20px' }}>
            Envie o link e o código de acesso correspondente para cada fornecedor (via WhatsApp, e-mail ou mensagem).
          </p>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
            {resultado.acessos.map((a) => (
              <div
                key={a.cotacaoFornecedorId}
                style={{
                  border: '1px solid var(--border)',
                  borderRadius: '10px',
                  padding: '16px 20px',
                  backgroundColor: 'var(--bg-main)',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px' }}>
                  <div style={{ fontWeight: 700, fontSize: '16px', color: 'var(--text-main)' }}>
                    {a.fornecedor}
                  </div>
                  <span className="badge badge-blue">Válido até {new Date(a.expiraEm).toLocaleDateString('pt-BR')}</span>
                </div>

                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', marginBottom: '10px' }}>
                  <div>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)', display: 'block' }}>Link do Portal:</span>
                    <input
                      readOnly
                      value={a.link}
                      style={{ fontSize: '12px', fontFamily: 'monospace', backgroundColor: '#fff' }}
                    />
                  </div>
                  <button
                    onClick={() => copiarTexto(a.link, a.link)}
                    className="btn btn-secondary"
                    style={{ alignSelf: 'flex-start', minHeight: '38px' }}
                    title="Copiar Link"
                  >
                    {copiadoId === a.link ? <Check size={16} color="var(--green)" /> : <Copy size={16} />}
                    {copiadoId === a.link ? 'Copiado!' : 'Copiar Link'}
                  </button>
                </div>

                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  <div>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)', display: 'block' }}>Código de Acesso:</span>
                    <input
                      readOnly
                      value={a.codigo}
                      style={{
                        fontSize: '14px',
                        fontFamily: 'monospace',
                        fontWeight: 700,
                        letterSpacing: '2px',
                        color: 'var(--primary)',
                        backgroundColor: '#fff',
                        maxWidth: '260px'
                      }}
                    />
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
                    <button
                      onClick={() => copiarTexto(a.codigo, a.codigo)}
                      className="btn btn-secondary"
                      style={{ minHeight: '38px' }}
                      title="Copiar Código"
                    >
                      {copiadoId === a.codigo ? <Check size={16} color="var(--green)" /> : <Copy size={16} />}
                      {copiadoId === a.codigo ? 'Copiado!' : 'Copiar Código'}
                    </button>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                      O fornecedor precisará deste código para autenticar e preencher os preços.
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '20px' }}>
        <button onClick={() => navigate('/cotacoes')} className="btn btn-secondary" style={{ padding: '8px 12px' }}>
          <ArrowLeft size={16} /> Voltar
        </button>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>Nova Cotação Multi-Fornecedor</h1>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
            Selecione os produtos que necessitam de reposição e os fornecedores que receberão o pedido.
          </p>
        </div>
      </div>

      <div className="two-col-responsive">
        {/* COLUNA ESQUERDA: PRODUTOS DA COTAÇÃO */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          {/* CABEÇALHO DA COTAÇÃO */}
          <div className="card" style={{ padding: '20px' }}>
            <h2 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '14px', color: 'var(--text-main)' }}>
              Identificação da Cotação
            </h2>
            <div className="form-grid-responsive-2" style={{ gap: '14px' }}>
              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                  Título / Referência
                </label>
                <input
                  placeholder="Ex: Reposição Semanal Mercearia e Bebidas"
                  value={titulo}
                  onChange={(e) => setTitulo(e.target.value)}
                />
              </div>
              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                  Prazo para Resposta (dias)
                </label>
                <input
                  type="number"
                  min="1"
                  max="30"
                  value={prazoDias}
                  onChange={(e) => setPrazoDias(parseInt(e.target.value) || 3)}
                />
              </div>
            </div>
            <div style={{ marginTop: '12px' }}>
              <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                Observações para os Fornecedores (opcional)
              </label>
              <textarea
                placeholder="Ex: Favor informar prazo de entrega para entrega única no galpão central."
                value={observacoes}
                onChange={(e) => setObservacoes(e.target.value)}
                rows={2}
                style={{ width: '100%', resize: 'vertical' }}
              />
            </div>
          </div>

          {/* ITENS SELECIONADOS */}
          <div className="card" style={{ padding: '20px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
              <div>
                <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text-main)' }}>
                  Itens Solicitados ({itens.length})
                </h2>
                <span style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                  Defina as quantidades que cada fornecedor deve orçar.
                </span>
              </div>
              <button onClick={() => setShowAddProduto(true)} className="btn btn-secondary">
                <Plus size={16} /> Adicionar Produtos
              </button>
            </div>

            {itens.length === 0 ? (
              <div
                style={{
                  border: '2px dashed var(--border)',
                  borderRadius: '10px',
                  padding: '40px 20px',
                  textAlign: 'center',
                  color: 'var(--text-muted)',
                }}
              >
                <Package size={40} style={{ margin: '0 auto 12px', opacity: 0.5 }} />
                <p style={{ fontWeight: 600, fontSize: '15px', marginBottom: '6px' }}>Nenhum produto adicionado ainda</p>
                <p style={{ fontSize: '13px', marginBottom: '16px' }}>
                  Clique no botão acima para escolher os itens do estoque ou reposição.
                </p>
                <button onClick={() => setShowAddProduto(true)} className="btn btn-primary">
                  <Plus size={16} /> Escolher Produtos
                </button>
              </div>
            ) : (
              <>
                {/* DESKTOP TABLE VIEW */}
                <div className="desktop-table-view">
                  <div className="table-container">
                    <table>
                      <thead>
                        <tr>
                          <th>Código</th>
                          <th>Descrição</th>
                          <th>Unidade</th>
                          <th style={{ textAlign: 'right' }}>Estoque Atual</th>
                          <th style={{ textAlign: 'right', width: '150px' }}>Quantidade a Cotar</th>
                          <th style={{ width: '60px' }}></th>
                        </tr>
                      </thead>
                      <tbody>
                        {itens.map((it) => (
                          <tr key={it.produtoId}>
                            <td style={{ fontFamily: 'monospace', fontWeight: 600 }}>{it.codigoErp || '—'}</td>
                            <td>
                              <div style={{ fontWeight: 600, color: 'var(--text-main)' }}>{it.descricao}</div>
                              <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                                Último Custo Ref: R$ {it.custoAtual.toFixed(2).replace('.', ',')}
                              </div>
                            </td>
                            <td>{it.unidade}</td>
                            <td style={{ textAlign: 'right' }}>
                              <span
                                style={{
                                  fontWeight: 600,
                                  color: it.estoqueAtual <= it.estoqueMinimo ? 'var(--red)' : 'var(--text-main)',
                                }}
                              >
                                {it.estoqueAtual.toLocaleString('pt-BR')}
                              </span>
                            </td>
                            <td style={{ textAlign: 'right' }}>
                              <input
                                type="number"
                                step="any"
                                min="0.1"
                                value={it.quantidade}
                                onChange={(e) => alterarQuantidade(it.produtoId, parseFloat(e.target.value) || 0)}
                                style={{ width: '120px', textAlign: 'right', fontWeight: 700 }}
                              />
                            </td>
                            <td>
                              <button
                                onClick={() => removerItem(it.produtoId)}
                                style={{
                                  background: 'none',
                                  border: 'none',
                                  color: 'var(--red)',
                                  cursor: 'pointer',
                                  padding: '6px',
                                }}
                                title="Remover produto"
                              >
                                <Trash2 size={16} />
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>

                {/* MOBILE CARDS VIEW */}
                <div className="mobile-card-view">
                  {itens.map((it) => (
                    <div
                      key={it.produtoId}
                      style={{
                        border: '1px solid var(--border)',
                        borderRadius: '10px',
                        padding: '14px',
                        backgroundColor: '#FFFFFF',
                        boxShadow: 'var(--shadow-sm)'
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '8px' }}>
                        <div>
                          <div style={{ fontWeight: 700, fontSize: '14px', color: 'var(--text-main)' }}>
                            {it.descricao}
                          </div>
                          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
                            Código: <span style={{ fontFamily: 'monospace' }}>{it.codigoErp || '—'}</span> • Unidade: <strong>{it.unidade}</strong>
                          </div>
                        </div>
                        <button
                          onClick={() => removerItem(it.produtoId)}
                          style={{
                            background: 'none',
                            border: 'none',
                            color: 'var(--red)',
                            cursor: 'pointer',
                            padding: '6px',
                          }}
                          title="Remover produto"
                        >
                          <Trash2 size={18} />
                        </button>
                      </div>

                      <div
                        style={{
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                          backgroundColor: '#F8FAFC',
                          padding: '8px 12px',
                          borderRadius: '6px',
                          fontSize: '12px',
                          marginBottom: '10px'
                        }}
                      >
                        <span>
                          Estoque Atual:{' '}
                          <strong style={{ color: it.estoqueAtual <= it.estoqueMinimo ? 'var(--red)' : 'var(--text-main)' }}>
                            {it.estoqueAtual.toLocaleString('pt-BR')}
                          </strong>
                        </span>
                        <span>
                          Ref: <strong>R$ {it.custoAtual.toFixed(2).replace('.', ',')}</strong>
                        </span>
                      </div>

                      <div>
                        <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px', display: 'block' }}>
                          Quantidade a Cotar
                        </label>
                        <input
                          type="number"
                          step="any"
                          min="0.1"
                          value={it.quantidade}
                          onChange={(e) => alterarQuantidade(it.produtoId, parseFloat(e.target.value) || 0)}
                          style={{
                            fontWeight: 800,
                            fontSize: '16px',
                            color: 'var(--blue-primary)',
                            minHeight: '44px',
                            width: '100%'
                          }}
                        />
                      </div>
                    </div>
                  ))}
                </div>
              </>
            )}
          </div>
        </div>

        {/* COLUNA DIREITA: SELEÇÃO DE FORNECEDORES E RESUMO */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          <div className="card" style={{ padding: '20px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
              <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text-main)' }}>
                Fornecedores ({fornecedoresSelecionados.length}/{todosFornecedores.length})
              </h2>
              <button
                type="button"
                onClick={selecionarTodosFornecedores}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--primary)',
                  fontSize: '12px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                {fornecedoresSelecionados.length === todosFornecedores.length ? 'Desmarcar Todos' : 'Selecionar Todos'}
              </button>
            </div>

            <p style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '14px' }}>
              Cada fornecedor selecionado receberá um link protegido individual.
            </p>

            <div
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: '8px',
                maxHeight: '340px',
                overflowY: 'auto',
                paddingRight: '4px',
              }}
            >
              {todosFornecedores.length === 0 ? (
                <div style={{ fontSize: '13px', color: 'var(--text-muted)', textAlign: 'center', padding: '20px' }}>
                  Nenhum fornecedor cadastrado. Cadastre fornecedores antes de iniciar a cotação.
                </div>
              ) : (
                todosFornecedores.map((f) => {
                  const check = fornecedoresSelecionados.includes(f.id);
                  return (
                    <label
                      key={f.id}
                      style={{
                        display: 'flex',
                        alignItems: 'flex-start',
                        gap: '10px',
                        padding: '10px 12px',
                        borderRadius: '8px',
                        border: `1px solid ${check ? 'var(--primary)' : 'var(--border)'}`,
                        backgroundColor: check ? 'var(--blue-light)' : '#fff',
                        cursor: 'pointer',
                        transition: 'all 0.15s ease',
                      }}
                    >
                      <input
                        type="checkbox"
                        checked={check}
                        onChange={() => toggleFornecedor(f.id)}
                        style={{ marginTop: '3px', width: 'auto' }}
                      />
                      <div style={{ flex: 1 }}>
                        <div style={{ fontWeight: 600, fontSize: '13.5px', color: 'var(--text-main)' }}>
                          {f.nomeFantasia || f.razaoSocial}
                        </div>
                        <div style={{ fontSize: '11.5px', color: 'var(--text-muted)' }}>
                          {f.cidade}/{f.uf} • Prazo: ~{f.prazoEntregaPadrao}d
                        </div>
                      </div>
                    </label>
                  );
                })
              )}
            </div>

            <div style={{ marginTop: '20px', borderTop: '1px solid var(--border)', paddingTop: '16px' }}>
              <button
                onClick={handleSalvar}
                disabled={salvando || itens.length === 0 || fornecedoresSelecionados.length === 0}
                className="btn btn-primary"
                style={{ width: '100%', padding: '12px', fontSize: '15px', justifyContent: 'center' }}
              >
                <Send size={18} />
                {salvando ? 'Criando e Gerando Links...' : 'Gerar Pedido de Cotação'}
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* MODAL PARA SELEÇÃO DE PRODUTOS */}
      {showAddProduto && (
        <div className="modal-overlay">
          <div className="modal-responsive-card" style={{ display: 'flex', flexDirection: 'column', maxHeight: '90vh' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px', borderBottom: '1px solid var(--border-light)', paddingBottom: '12px' }}>
              <h2 style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-main)', margin: 0 }}>
                Adicionar Produtos à Cotação
              </h2>
              <button
                onClick={() => setShowAddProduto(false)}
                style={{ background: 'none', border: 'none', fontSize: '24px', cursor: 'pointer', color: 'var(--text-muted)', lineHeight: 1 }}
              >
                ×
              </button>
            </div>

            <div style={{ marginBottom: '14px' }}>
              <input
                placeholder="Buscar por descrição, código ou grupo..."
                value={buscaProduto}
                onChange={(e) => setBuscaProduto(e.target.value)}
                autoFocus
                style={{ minHeight: '44px', fontSize: '15px' }}
              />
            </div>

            <div style={{ flex: 1, overflowY: 'auto', paddingRight: '4px' }}>
              {/* DESKTOP TABLE VIEW */}
              <div className="desktop-table-view">
                <div className="table-container">
                  <table style={{ width: '100%' }}>
                    <thead>
                      <tr>
                        <th>Código</th>
                        <th>Descrição</th>
                        <th>Grupo</th>
                        <th style={{ textAlign: 'right' }}>Estoque</th>
                        <th style={{ textAlign: 'right' }}>Sugestão</th>
                        <th style={{ width: '80px' }}></th>
                      </tr>
                    </thead>
                    <tbody>
                      {todosProdutos
                        .filter(
                          (p) =>
                            !buscaProduto ||
                            p.descricao.toLowerCase().includes(buscaProduto.toLowerCase()) ||
                            p.codigoErp.toLowerCase().includes(buscaProduto.toLowerCase()) ||
                            p.grupo.toLowerCase().includes(buscaProduto.toLowerCase())
                        )
                        .map((p) => {
                          const jaAdicionado = itens.some((i) => i.produtoId === p.id);
                          return (
                            <tr key={p.id}>
                              <td style={{ fontFamily: 'monospace', fontSize: '12px' }}>{p.codigoErp}</td>
                              <td style={{ fontWeight: 600 }}>{p.descricao}</td>
                              <td style={{ fontSize: '12px' }}>{p.grupo}</td>
                              <td style={{ textAlign: 'right' }}>{p.estoqueAtual ?? '—'}</td>
                              <td style={{ textAlign: 'right', fontWeight: 600, color: 'var(--primary)' }}>
                                {p.sugestaoCompra > 0 ? p.sugestaoCompra : '—'}
                              </td>
                              <td style={{ textAlign: 'right' }}>
                                <button
                                  onClick={() => adicionarProduto(p)}
                                  disabled={jaAdicionado}
                                  className={`btn ${jaAdicionado ? 'btn-secondary' : 'btn-primary'}`}
                                  style={{ padding: '5px 10px', fontSize: '12px' }}
                                >
                                  {jaAdicionado ? 'Adicionado' : 'Adicionar'}
                                </button>
                              </td>
                            </tr>
                          );
                        })}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* MOBILE CARDS VIEW */}
              <div className="mobile-card-view">
                {todosProdutos
                  .filter(
                    (p) =>
                      !buscaProduto ||
                      p.descricao.toLowerCase().includes(buscaProduto.toLowerCase()) ||
                      p.codigoErp.toLowerCase().includes(buscaProduto.toLowerCase()) ||
                      p.grupo.toLowerCase().includes(buscaProduto.toLowerCase())
                  )
                  .map((p) => {
                    const jaAdicionado = itens.some((i) => i.produtoId === p.id);
                    return (
                      <div
                        key={p.id}
                        style={{
                          border: '1px solid var(--border)',
                          borderRadius: '8px',
                          padding: '12px',
                          backgroundColor: jaAdicionado ? '#F8FAFC' : '#FFFFFF'
                        }}
                      >
                        <div style={{ fontWeight: 700, fontSize: '14px', color: 'var(--text-main)' }}>
                          {p.descricao}
                        </div>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', margin: '4px 0' }}>
                          Código: <span style={{ fontFamily: 'monospace' }}>{p.codigoErp || '—'}</span> • Grupo: {p.grupo || 'Geral'}
                        </div>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12.5px', marginBottom: '8px' }}>
                          <span>Estoque: <strong>{p.estoqueAtual ?? 0} {p.unidade}</strong></span>
                          {p.sugestaoCompra > 0 && (
                            <span style={{ color: 'var(--blue-primary)', fontWeight: 600 }}>
                              Sugestão: {p.sugestaoCompra}
                            </span>
                          )}
                        </div>
                        <button
                          onClick={() => adicionarProduto(p)}
                          disabled={jaAdicionado}
                          className={`btn ${jaAdicionado ? 'btn-secondary' : 'btn-primary'}`}
                          style={{ width: '100%', minHeight: '42px', fontSize: '13px' }}
                        >
                          {jaAdicionado ? '✓ Já Adicionado' : '+ Adicionar'}
                        </button>
                      </div>
                    );
                  })}
              </div>
            </div>

            <div style={{ borderTop: '1px solid var(--border-light)', paddingTop: '12px', marginTop: '12px', textAlign: 'right' }}>
              <button onClick={() => setShowAddProduto(false)} className="btn btn-primary" style={{ width: '100%', minHeight: '42px' }}>
                Concluir Seleção ({itens.length} adicionados)
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

