import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { CotacaoServicoDetalhe as CotacaoServicoDetalheType } from '../../types/service';
import {
  Wrench,
  ArrowLeft,
  Send,
  GitCompare,
  Printer,
  Copy,
  ExternalLink,
  Check,
  Calendar,
  Clock,
  MapPin,
  ShieldCheck,
  Sliders,
  Users,
  FileText,
  AlertCircle
} from 'lucide-react';

export const CotacaoServicoDetalhePage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [cotacao, setCotacao] = useState<CotacaoServicoDetalheType | null>(null);
  const [loading, setLoading] = useState(true);
  const [enviando, setEnviando] = useState(false);
  const [copiedToken, setCopiedToken] = useState<string | null>(null);
  const [msgSucesso, setMsgSucesso] = useState<string | null>(null);

  const carregarDetalhe = async () => {
    if (!id) return;
    setLoading(true);
    try {
      const data = await api.get<CotacaoServicoDetalheType>(`/servicos/${id}`);
      setCotacao(data);
    } catch (err: any) {
      console.error('Erro ao carregar detalhes do serviço:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregarDetalhe();
  }, [id]);

  const handleEnviar = async () => {
    if (!id) return;
    setEnviando(true);
    try {
      await api.post(`/servicos/${id}/enviar`);
      setMsgSucesso('Cotação enviada com sucesso aos fornecedores!');
      carregarDetalhe();
      setTimeout(() => setMsgSucesso(null), 4000);
    } catch (err: any) {
      alert(err.message || 'Erro ao enviar cotação.');
    } finally {
      setEnviando(false);
    }
  };

  const copiarLink = (link: string, tokenId: string) => {
    navigator.clipboard.writeText(link);
    setCopiedToken(tokenId);
    setTimeout(() => setCopiedToken(null), 2500);
  };

  const abrirPdf = () => {
    if (!id) return;
    const baseUrl = api.getBaseUrl();
    window.open(`${baseUrl}/servicos/${id}/pdf`, '_blank');
  };

  if (loading) {
    return (
      <div style={{ padding: '60px', textAlign: 'center', color: 'var(--text-muted)' }}>
        Carregando detalhes da cotação de serviço...
      </div>
    );
  }

  if (!cotacao) {
    return (
      <div style={{ padding: '60px', textAlign: 'center' }}>
        <h3>Cotação de serviço não encontrada</h3>
        <button onClick={() => navigate('/servicos')} className="btn btn-secondary" style={{ marginTop: '16px' }}>
          Voltar para Cotações de Serviços
        </button>
      </div>
    );
  }

  const propostasRecebidas = cotacao.fornecedores.filter((f) => f.temProposta);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
      {/* HEADER & ACTIONS */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '16px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <button
            onClick={() => navigate('/servicos')}
            className="btn btn-secondary"
            style={{ padding: '8px 12px' }}
          >
            <ArrowLeft size={16} />
          </button>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <span style={{ fontSize: '15px', fontWeight: 800, color: 'var(--blue-primary)' }}>
                {cotacao.numero}
              </span>
              <span className="badge badge-blue">{cotacao.categoria}</span>
              <span className="badge badge-yellow">Prioridade {cotacao.prioridade}</span>
              <span className="badge badge-green">Status: {cotacao.status}</span>
            </div>
            <h1 style={{ fontSize: '22px', fontWeight: 800, margin: '4px 0 0 0', color: 'var(--text-main)' }}>
              {cotacao.titulo}
            </h1>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
          <button
            onClick={abrirPdf}
            className="btn btn-secondary"
            style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
          >
            <Printer size={16} />
            Imprimir PDF (ProSyst)
          </button>

          {cotacao.status === 'Rascunho' && (
            <button
              onClick={handleEnviar}
              disabled={enviando || cotacao.fornecedores.length === 0}
              className="btn btn-primary"
              style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
            >
              <Send size={16} />
              {enviando ? 'Enviando...' : 'Enviar para Fornecedores'}
            </button>
          )}

          <button
            onClick={() => navigate(`/servicos/${cotacao.id}/comparacao`)}
            className="btn btn-primary"
            style={{ display: 'flex', alignItems: 'center', gap: '6px', backgroundColor: '#059669' }}
          >
            <GitCompare size={16} />
            Comparar Propostas ({propostasRecebidas.length})
          </button>
        </div>
      </div>

      {msgSucesso && (
        <div style={{ backgroundColor: '#ecfdf5', border: '1px solid #a7f3d0', padding: '12px 16px', borderRadius: '8px', color: '#065f46', display: 'flex', alignItems: 'center', gap: '10px' }}>
          <Check size={18} />
          <span>{msgSucesso}</span>
        </div>
      )}

      {/* METRICAS DA COTAÇÃO */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px' }}>
        <div className="card" style={{ padding: '16px 20px' }}>
          <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 600, textTransform: 'uppercase' }}>
            Prazo Limite Resposta
          </div>
          <div style={{ fontSize: '16px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
            {new Date(cotacao.dataLimiteResposta).toLocaleDateString('pt-BR')}
          </div>
        </div>
        <div className="card" style={{ padding: '16px 20px' }}>
          <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 600, textTransform: 'uppercase' }}>
            Prazo de Execução
          </div>
          <div style={{ fontSize: '16px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
            {cotacao.prazoEstimadoDias} dias corridos
          </div>
        </div>
        <div className="card" style={{ padding: '16px 20px' }}>
          <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 600, textTransform: 'uppercase' }}>
            Valor Total Estimado
          </div>
          <div style={{ fontSize: '16px', fontWeight: 800, color: 'var(--blue-primary)', marginTop: '4px' }}>
            R$ {cotacao.totalEstimado.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
          </div>
        </div>
        <div className="card" style={{ padding: '16px 20px' }}>
          <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 600, textTransform: 'uppercase' }}>
            Propostas Enviadas
          </div>
          <div style={{ fontSize: '16px', fontWeight: 800, color: propostasRecebidas.length > 0 ? '#16a34a' : 'var(--text-muted)', marginTop: '4px' }}>
            {propostasRecebidas.length} de {cotacao.fornecedores.length} convidados
          </div>
        </div>
      </div>

      {/* DETALHES DO ESCOPO & LOCAL */}
      <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '20px' }}>
        <div className="card" style={{ padding: '24px' }}>
          <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <FileText size={18} color="var(--blue-primary)" />
            Memorial Descritivo e Justificativa
          </h3>

          <div style={{ marginBottom: '16px' }}>
            <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
              Motivação da Necessidade:
            </div>
            <div style={{ fontSize: '14px', color: 'var(--text-main)', marginTop: '4px', lineHeight: 1.5 }}>
              {cotacao.necessidadeMotivacao || 'Não informada.'}
            </div>
          </div>

          <div style={{ marginBottom: '16px' }}>
            <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
              Descrição Detalhada do Escopo:
            </div>
            <div style={{ fontSize: '14px', color: 'var(--text-main)', marginTop: '4px', lineHeight: 1.5, whiteSpace: 'pre-wrap' }}>
              {cotacao.descricaoDetalhada || 'Não informada.'}
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', paddingTop: '12px', borderTop: '1px solid var(--border)' }}>
            <div>
              <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>OBJETIVO:</div>
              <div style={{ fontSize: '13.5px', fontWeight: 600, color: 'var(--text-main)', marginTop: '2px' }}>
                {cotacao.objetivoEsperado || '-'}
              </div>
            </div>
            <div>
              <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>ENTREGÁVEL / RESULTADO:</div>
              <div style={{ fontSize: '13.5px', fontWeight: 600, color: 'var(--text-main)', marginTop: '2px' }}>
                {cotacao.resultadoEsperado || '-'}
              </div>
            </div>
          </div>
        </div>

        {/* LOCAL E CONDIÇÕES */}
        <div className="card" style={{ padding: '24px', display: 'flex', flexDirection: 'column', gap: '14px' }}>
          <h3 style={{ fontSize: '16px', fontWeight: 700, margin: 0, display: 'flex', alignItems: 'center', gap: '8px' }}>
            <MapPin size={18} color="var(--blue-primary)" />
            Local & Requisitos
          </h3>

          <div>
            <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)' }}>ENDEREÇO DE ATENDIMENTO:</div>
            <div style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-main)', marginTop: '2px' }}>
              {cotacao.atendimentoPresencial ? (
                <>
                  {cotacao.endereco}, {cotacao.numeroLocal} {cotacao.complemento}
                  <br />
                  {cotacao.bairro} - {cotacao.cidade}/{cotacao.uf}
                  <br />
                  CEP: {cotacao.cep} (Ref: {cotacao.pontoReferencia || '-'})
                </>
              ) : (
                'Atendimento Remoto / Online'
              )}
            </div>
          </div>

          <div>
            <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)' }}>HORÁRIO PERMITIDO:</div>
            <div style={{ fontSize: '13px', color: 'var(--text-main)', marginTop: '2px' }}>
              {cotacao.horarioPermitido}
            </div>
          </div>

          <div>
            <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)' }}>GARANTIA EXIGIDA:</div>
            <div style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-main)', marginTop: '2px' }}>
              {cotacao.garantiaEsperadaMeses} meses
            </div>
          </div>

          <div>
            <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)' }}>CERTIFICAÇÕES DA EQUIPE:</div>
            <div style={{ fontSize: '13px', color: 'var(--text-main)', marginTop: '2px' }}>
              {cotacao.certificacoesExigidas || 'Nenhuma exigência prévia'}
            </div>
          </div>

          <div style={{ paddingTop: '10px', borderTop: '1px solid var(--border)' }}>
            <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)' }}>PESOS DO SCORE:</div>
            <div style={{ fontSize: '12px', color: 'var(--text-main)', marginTop: '4px' }}>
              Preço: <strong>{cotacao.pesoPreco}%</strong> • Prazo: <strong>{cotacao.pesoPrazo}%</strong>
              <br />
              Técnico: <strong>{cotacao.pesoTecnico}%</strong> • Condições: <strong>{cotacao.pesoCondicoes}%</strong>
            </div>
          </div>
        </div>
      </div>

      {/* ETAPAS / ATIVIDADES CADASTRADAS */}
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)', background: '#f8fafc' }}>
          <h3 style={{ fontSize: '15px', fontWeight: 700, margin: 0, display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Clock size={16} color="var(--blue-primary)" />
            Atividades e Marcos de Entrega ({cotacao.atividades.length})
          </h3>
        </div>

        <table className="table" style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ background: '#f8fafc', borderBottom: '1px solid var(--border)' }}>
              <th style={{ padding: '10px 16px', textAlign: 'left', fontSize: '11.5px', fontWeight: 700, color: 'var(--text-muted)', width: '60px' }}>#</th>
              <th style={{ padding: '10px 16px', textAlign: 'left', fontSize: '11.5px', fontWeight: 700, color: 'var(--text-muted)' }}>ETAPA / ATIVIDADE</th>
              <th style={{ padding: '10px 16px', textAlign: 'center', fontSize: '11.5px', fontWeight: 700, color: 'var(--text-muted)', width: '90px' }}>UNIDADE</th>
              <th style={{ padding: '10px 16px', textAlign: 'right', fontSize: '11.5px', fontWeight: 700, color: 'var(--text-muted)', width: '90px' }}>QTD</th>
              <th style={{ padding: '10px 16px', textAlign: 'right', fontSize: '11.5px', fontWeight: 700, color: 'var(--text-muted)', width: '130px' }}>VALOR ESTIMADO</th>
              <th style={{ padding: '10px 16px', textAlign: 'right', fontSize: '11.5px', fontWeight: 700, color: 'var(--text-muted)', width: '130px' }}>SUBTOTAL</th>
            </tr>
          </thead>
          <tbody>
            {cotacao.atividades.map((at) => (
              <tr key={at.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td style={{ padding: '12px 16px', fontWeight: 700, color: 'var(--text-muted)' }}>
                  {at.ordem}
                </td>
                <td style={{ padding: '12px 16px' }}>
                  <div style={{ fontWeight: 700, fontSize: '13.5px', color: 'var(--text-main)' }}>
                    {at.etapa ? `[${at.etapa}] ` : ''}{at.nome}
                  </div>
                  {at.descricao && (
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
                      {at.descricao}
                    </div>
                  )}
                  {at.requisitosTecnicos && (
                    <div style={{ fontSize: '11px', color: '#0284c7', marginTop: '2px' }}>
                      Requisitos: {at.requisitosTecnicos}
                    </div>
                  )}
                </td>
                <td style={{ padding: '12px 16px', textAlign: 'center', fontSize: '13px' }}>
                  <span className="badge badge-gray">{at.unidadeMedicao}</span>
                </td>
                <td style={{ padding: '12px 16px', textAlign: 'right', fontSize: '13px', fontWeight: 600 }}>
                  {at.quantidade}
                </td>
                <td style={{ padding: '12px 16px', textAlign: 'right', fontSize: '13px' }}>
                  R$ {(at.valorEstimadoUnitario || 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                </td>
                <td style={{ padding: '12px 16px', textAlign: 'right', fontSize: '13.5px', fontWeight: 700, color: 'var(--blue-primary)' }}>
                  R$ {(at.subtotal || 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* FORNECEDORES CONVIDADOS & LINKS PÚBLICOS */}
      <div className="card" style={{ padding: '24px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <div>
            <h3 style={{ fontSize: '16px', fontWeight: 700, margin: 0, display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Users size={18} color="var(--blue-primary)" />
              Fornecedores Convidados e Links de Acesso Seguro
            </h3>
            <p style={{ margin: '2px 0 0 0', fontSize: '12.5px', color: 'var(--text-muted)' }}>
              Cada fornecedor possui um link exclusivo com token criptografado para submeter sua proposta sem enxergar concorrentes.
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
          {cotacao.fornecedores.map((f) => {
            const copiado = copiedToken === f.id;
            return (
              <div
                key={f.id}
                style={{
                  border: '1px solid var(--border)',
                  borderRadius: '8px',
                  padding: '14px 18px',
                  backgroundColor: f.temProposta ? '#f0fdf4' : '#fff',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  gap: '16px'
                }}
              >
                <div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <span style={{ fontWeight: 700, fontSize: '14px', color: 'var(--text-main)' }}>
                      {f.razaoSocial}
                    </span>
                    <span className="badge badge-gray">{f.status}</span>
                    {f.temProposta && <span className="badge badge-green">Proposta Enviada</span>}
                  </div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
                    CNPJ: {f.cnpj} • Contato: {f.telefone} • {f.email}
                  </div>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                  {f.temProposta && (
                    <div style={{ textAlign: 'right' }}>
                      <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Valor Cotado:</div>
                      <div style={{ fontSize: '15px', fontWeight: 800, color: '#16a34a' }}>
                        R$ {(f.valorTotalProposta || 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                      </div>
                    </div>
                  )}

                  {f.linkAcesso ? (
                    <div style={{ display: 'flex', gap: '8px' }}>
                      <button
                        onClick={() => copiarLink(f.linkAcesso!, f.id)}
                        className="btn btn-secondary"
                        style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px' }}
                      >
                        {copiado ? <Check size={14} color="#16a34a" /> : <Copy size={14} />}
                        {copiado ? 'Copiado!' : 'Copiar Link do Portal'}
                      </button>
                      <button
                        onClick={() => window.open(f.linkAcesso, '_blank')}
                        className="btn btn-secondary"
                        title="Abrir Portal do Fornecedor"
                        style={{ padding: '8px' }}
                      >
                        <ExternalLink size={15} />
                      </button>
                    </div>
                  ) : (
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)', fontStyle: 'italic' }}>
                      Clique em "Enviar para Fornecedores" para ativar os links.
                    </span>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};

