import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { CotacaoDetalhe, NovoCodigoDto } from '../../types';
import {
  ArrowLeft,
  Copy,
  Check,
  RefreshCw,
  BarChart2,
  XCircle,
  Clock,
  CheckCircle2,
  AlertTriangle,
  ExternalLink,
  ShieldAlert,
  Printer
} from 'lucide-react';

export const CotacaoDetalhePage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [cotacao, setCotacao] = useState<CotacaoDetalhe | null>(null);
  const [loading, setLoading] = useState(true);
  const [copiadoId, setCopiadoId] = useState<string | null>(null);

  // Modal para exibir novo código gerado
  const [novoCodigo, setNovoCodigo] = useState<NovoCodigoDto | null>(null);
  const [gerandoCodigoId, setGerandoCodigoId] = useState<string | null>(null);

  const carregarCotacao = async () => {
    if (!id) return;
    setLoading(true);
    try {
      const res = await api.get<CotacaoDetalhe>(`/cotacoes/${id}`);
      setCotacao(res);
    } catch (err: any) {
      alert('Erro ao carregar cotação: ' + err.message);
      navigate('/cotacoes');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregarCotacao();
  }, [id]);

  const copiarTexto = (idKey: string, texto: string) => {
    navigator.clipboard.writeText(texto);
    setCopiadoId(idKey);
    setTimeout(() => setCopiadoId(null), 2500);
  };

  const handleGerarNovoCodigo = async (cfId: string) => {
    if (!confirm('Ao gerar um novo código, o link e código anteriores do fornecedor serão imediatamente cancelados. Deseja prosseguir?')) {
      return;
    }
    setGerandoCodigoId(cfId);
    try {
      const res = await api.post<NovoCodigoDto>(`/cotacoes/${id}/fornecedores/${cfId}/novo-codigo`);
      setNovoCodigo(res);
      carregarCotacao();
    } catch (err: any) {
      alert('Erro ao gerar novo código: ' + err.message);
    } finally {
      setGerandoCodigoId(null);
    }
  };

  const handleCancelarCotacao = async () => {
    const motivo = prompt('Informe a justificativa do cancelamento desta cotação:');
    if (motivo === null) return;
    try {
      await api.post(`/cotacoes/${id}/cancelar`, { motivo });
      alert('Cotação cancelada.');
      carregarCotacao();
    } catch (err: any) {
      alert('Erro ao cancelar cotação: ' + err.message);
    }
  };

  if (loading || !cotacao) {
    return <div style={{ padding: '60px', textAlign: 'center' }}>Carregando detalhes da cotação...</div>;
  }

  const temRespostas = cotacao.fornecedores.some((f) => f.respondida);

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px', flexWrap: 'wrap', gap: '12px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <button onClick={() => navigate('/cotacoes')} className="btn btn-secondary" style={{ padding: '8px 12px' }}>
            <ArrowLeft size={16} /> Voltar
          </button>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
                Cotação {cotacao.numero}
              </h1>
              <span className={`badge ${cotacao.status === 'Aprovada' ? 'badge-green' : cotacao.status === 'Cancelada' ? 'badge-red' : 'badge-blue'}`}>
                {cotacao.status}
              </span>
            </div>
            <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
              {cotacao.titulo} • Criada em {new Date(cotacao.dataCriacao).toLocaleDateString('pt-BR')} • Limite: {new Date(cotacao.dataLimiteResposta).toLocaleDateString('pt-BR')}
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          <button
            onClick={() => {
              const baseUrl = api.getBaseUrl();
              window.open(`${baseUrl}/cotacoes/${cotacao.id}/pdf`, '_blank');
            }}
            className="btn btn-secondary"
            style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
          >
            <Printer size={16} /> Imprimir Orçamento (ProSyst)
          </button>

          {cotacao.status !== 'Cancelada' && cotacao.status !== 'Aprovada' && (
            <button onClick={handleCancelarCotacao} className="btn btn-secondary" style={{ color: 'var(--red)' }}>
              <XCircle size={16} /> Cancelar Cotação
            </button>
          )}

          <button
            onClick={() => navigate(`/cotacoes/${cotacao.id}/comparacao`)}
            disabled={!temRespostas}
            className="btn btn-primary"
            style={{ padding: '8px 16px' }}
          >
            <BarChart2 size={16} /> Comparar Propostas Comerciais
          </button>
        </div>
      </div>

      {cotacao.status === 'Aprovada' && (
        <div className="card" style={{ marginBottom: '20px', backgroundColor: 'var(--green-light)', border: '1px solid var(--green)', padding: '16px 20px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', color: 'var(--green)' }}>
            <CheckCircle2 size={20} />
            <div>
              <span style={{ fontWeight: 700 }}>Cotação Aprovada</span> por {cotacao.aprovadaPorNome || 'Comprador'} em {new Date(cotacao.aprovadaEm!).toLocaleDateString('pt-BR')}
              {cotacao.observacaoAprovacao && <div style={{ fontSize: '13px', color: 'var(--text-main)', marginTop: '4px' }}>Justificativa: {cotacao.observacaoAprovacao}</div>}
            </div>
          </div>
        </div>
      )}

      {/* ITENS DA COTAÇÃO */}
      <div className="card" style={{ marginBottom: '24px', padding: '20px' }}>
        <h2 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '14px', color: 'var(--text-main)' }}>
          Produtos Solicitados ({cotacao.itens.length})
        </h2>
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>Código</th>
                <th>Descrição do Produto</th>
                <th>Unidade</th>
                <th style={{ textAlign: 'right' }}>Estoque na Criação</th>
                <th style={{ textAlign: 'right' }}>Quantidade Solicitada</th>
                <th style={{ textAlign: 'right' }}>Último Custo Ref.</th>
              </tr>
            </thead>
            <tbody>
              {cotacao.itens.map((it) => (
                <tr key={it.id}>
                  <td style={{ fontFamily: 'monospace', fontWeight: 600 }}>{it.codigo || '—'}</td>
                  <td style={{ fontWeight: 600 }}>{it.descricao}</td>
                  <td>{it.unidade}</td>
                  <td style={{ textAlign: 'right' }}>{it.estoqueNoMomento.toLocaleString('pt-BR')}</td>
                  <td style={{ textAlign: 'right', fontWeight: 700, color: 'var(--primary)' }}>
                    {it.quantidade.toLocaleString('pt-BR')}
                  </td>
                  <td style={{ textAlign: 'right' }}>
                    R$ {it.ultimoCustoRef.toFixed(2).replace('.', ',')}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* FORNECEDORES CONVIDADOS & GESTÃO DE ACESSO */}
      <div className="card" style={{ padding: '20px' }}>
        <h2 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '14px', color: 'var(--text-main)' }}>
          Fornecedores Convidados ({cotacao.fornecedores.length})
        </h2>

        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>Fornecedor</th>
                <th>Contato</th>
                <th>Status Resposta</th>
                <th>Visualizações</th>
                <th>Segurança do Acesso</th>
                <th style={{ textAlign: 'right' }}>Ações de Acesso</th>
              </tr>
            </thead>
            <tbody>
              {cotacao.fornecedores.map((f) => {
                const acesso = f.acesso;
                return (
                  <tr key={f.id}>
                    <td>
                      <div style={{ fontWeight: 700, color: 'var(--text-main)' }}>{f.nome}</div>
                    </td>
                    <td>
                      <div style={{ fontSize: '13px' }}>{f.email || '—'}</div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{f.whatsapp || ''}</div>
                    </td>
                    <td>
                      {f.respondida ? (
                        <span className="badge badge-green">Proposta Enviada</span>
                      ) : acesso && acesso.visualizacoes > 0 ? (
                        <span className="badge badge-yellow">Visualizada pelo Fornecedor</span>
                      ) : (
                        <span className="badge badge-blue">Aguardando Acesso</span>
                      )}
                    </td>
                    <td>
                      <span style={{ fontWeight: 600 }}>{acesso?.visualizacoes || 0} acessos</span>
                      {acesso?.ultimaVisualizacaoEm && (
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                          Último: {new Date(acesso.ultimaVisualizacaoEm).toLocaleDateString('pt-BR')}
                        </div>
                      )}
                    </td>
                    <td>
                      {acesso?.bloqueado ? (
                        <span className="badge badge-red"><ShieldAlert size={12} /> Bloqueado por Tentativas</span>
                      ) : acesso?.revogado ? (
                        <span className="badge badge-red">Revogado</span>
                      ) : (
                        <span className="badge badge-green">Ativo</span>
                      )}
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <button
                        onClick={() => handleGerarNovoCodigo(f.id)}
                        disabled={gerandoCodigoId === f.id || cotacao.status === 'Cancelada'}
                        className="btn btn-secondary"
                        style={{ padding: '6px 10px', fontSize: '12px' }}
                        title="Gera um novo link e código aleatório para este fornecedor"
                      >
                        <RefreshCw size={13} className={gerandoCodigoId === f.id ? 'spin' : ''} />
                        Gerar Novo Código
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      {/* MODAL NOVO CÓDIGO GERADO */}
      {novoCodigo && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(30, 41, 59, 0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 60,
            padding: '20px',
          }}
        >
          <div className="card" style={{ maxWidth: '550px', width: '100%', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-main)', marginBottom: '8px' }}>
              Novo Código Gerado com Sucesso!
            </h3>
            <p style={{ fontSize: '13.5px', color: 'var(--text-muted)', marginBottom: '16px' }}>
              O acesso anterior foi revogado. Repasse as novas credenciais abaixo para o fornecedor:
            </p>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ fontSize: '12px', fontWeight: 600, display: 'block', marginBottom: '4px' }}>
                Novo Link:
              </label>
              <div style={{ display: 'flex', gap: '8px' }}>
                <input readOnly value={novoCodigo.link} style={{ fontFamily: 'monospace', fontSize: '13px' }} />
                <button
                  onClick={() => copiarTexto('novo_link', novoCodigo.link)}
                  className="btn btn-secondary"
                >
                  {copiadoId === 'novo_link' ? <Check size={16} color="var(--green)" /> : <Copy size={16} />}
                </button>
              </div>
            </div>

            <div style={{ marginBottom: '20px' }}>
              <label style={{ fontSize: '12px', fontWeight: 600, display: 'block', marginBottom: '4px' }}>
                Novo Código de Acesso:
              </label>
              <div style={{ display: 'flex', gap: '8px' }}>
                <input
                  readOnly
                  value={novoCodigo.codigo}
                  style={{
                    fontFamily: 'monospace',
                    fontSize: '15px',
                    fontWeight: 800,
                    letterSpacing: '2px',
                    color: 'var(--primary)',
                  }}
                />
                <button
                  onClick={() => copiarTexto('novo_cod', novoCodigo.codigo)}
                  className="btn btn-secondary"
                >
                  {copiadoId === 'novo_cod' ? <Check size={16} color="var(--green)" /> : <Copy size={16} />}
                </button>
              </div>
            </div>

            <div style={{ textAlign: 'right' }}>
              <button onClick={() => setNovoCodigo(null)} className="btn btn-primary">
                Entendido / Fechar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

