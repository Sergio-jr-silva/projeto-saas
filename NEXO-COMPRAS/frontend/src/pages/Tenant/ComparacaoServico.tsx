import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { ComparacaoServicoResponse, PropostaComparativo } from '../../types/service';
import {
  GitCompare,
  ArrowLeft,
  Award,
  CheckCircle2,
  Clock,
  ShieldCheck,
  FileCheck,
  Printer,
  ChevronDown,
  Sliders,
  AlertCircle
} from 'lucide-react';

export const ComparacaoServicoPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [data, setData] = useState<ComparacaoServicoResponse | null>(null);
  const [loading, setLoading] = useState(true);

  // Modal de Aprovação / Contratação
  const [propostaAprovar, setPropostaAprovar] = useState<PropostaComparativo | null>(null);
  const [justificativa, setJustificativa] = useState('');
  const [observacao, setObservacao] = useState('');
  const [aprovando, setAprovando] = useState(false);
  const [sucessoMsg, setSucessoMsg] = useState<string | null>(null);

  const carregarComparacao = async () => {
    if (!id) return;
    setLoading(true);
    try {
      const res = await api.get<ComparacaoServicoResponse>(`/servicos/${id}/comparacao`);
      setData(res);
    } catch (err: any) {
      console.error('Erro ao carregar comparação:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregarComparacao();
  }, [id]);

  const handleAprovar = async () => {
    if (!id || !propostaAprovar) return;
    setAprovando(true);
    try {
      const res = await api.post<{ ordemServicoId: string; ordemServicoNumero: string }>(
        `/servicos/${id}/aprovar`,
        {
          fornecedorServicoId: propostaAprovar.fornecedorServicoId,
          fornecedorId: propostaAprovar.fornecedorId,
          justificativa: justificativa.trim() || 'Melhor pontuação técnica e comercial na matriz ponderada.',
          observacao: observacao.trim()
        }
      );

      setSucessoMsg(`Cotação contratada com sucesso! Ordem de Serviço ${res.ordemServicoNumero} gerada.`);
      setPropostaAprovar(null);
      setTimeout(() => {
        navigate(`/servicos/ordens/${res.ordemServicoId}`);
      }, 1500);
    } catch (err: any) {
      alert(err.message || 'Erro ao aprovar proposta.');
    } finally {
      setAprovando(false);
    }
  };

  if (loading) {
    return (
      <div style={{ padding: '60px', textAlign: 'center', color: 'var(--text-muted)' }}>
        Carregando matriz comparativa de propostas de serviços...
      </div>
    );
  }

  if (!data || !data.temPropostas || data.propostas.length === 0) {
    return (
      <div style={{ padding: '60px', textAlign: 'center' }}>
        <GitCompare size={42} style={{ color: 'var(--text-muted)', opacity: 0.4, marginBottom: '12px' }} />
        <h3>Nenhuma proposta de serviço enviada até o momento</h3>
        <p style={{ color: 'var(--text-muted)', fontSize: '13.5px', maxWidth: '420px', margin: '8px auto 18px' }}>
          Aguarde o envio das propostas comerciais e técnicas pelos prestadores convidados para comparar os valores e pontuações.
        </p>
        <button onClick={() => navigate(`/servicos/${id}`)} className="btn btn-secondary">
          Voltar para Detalhes da Cotação
        </button>
      </div>
    );
  }

  const melhorPreco = Math.min(...data.propostas.map((p) => p.valorTotal));
  const melhorPrazo = Math.min(...data.propostas.map((p) => p.prazoExecucaoDias));

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
      {/* HEADER */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <button
            onClick={() => navigate(`/servicos/${id}`)}
            className="btn btn-secondary"
            style={{ padding: '8px 12px' }}
          >
            <ArrowLeft size={16} />
          </button>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <span style={{ fontSize: '15px', fontWeight: 800, color: 'var(--blue-primary)' }}>
                {data.numero}
              </span>
              <span className="badge badge-green">{data.propostas.length} Proposta(s) Recebida(s)</span>
            </div>
            <h1 style={{ fontSize: '22px', fontWeight: 800, margin: '4px 0 0 0', color: 'var(--text-main)', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <GitCompare size={22} color="var(--blue-primary)" />
              Comparação Técnica & Comercial de Serviços
            </h1>
          </div>
        </div>

        <button
          onClick={() => window.print()}
          className="btn btn-secondary"
          style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
        >
          <Printer size={16} />
          Imprimir Quadro Comparativo
        </button>
      </div>

      {sucessoMsg && (
        <div style={{ backgroundColor: '#ecfdf5', border: '1px solid #a7f3d0', padding: '14px 18px', borderRadius: '8px', color: '#065f46', display: 'flex', alignItems: 'center', gap: '10px', fontWeight: 600 }}>
          <CheckCircle2 size={20} />
          <span>{sucessoMsg}</span>
        </div>
      )}

      {/* REGRAS DOS PESOS PONDERADOS */}
      <div className="card" style={{ padding: '16px 20px', backgroundColor: '#f8fafc', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '12px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <Sliders size={20} color="var(--blue-primary)" />
          <div>
            <div style={{ fontWeight: 700, fontSize: '13px' }}>Critérios de Avaliação Ponderada:</div>
            <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
              Comercial/Preço: <strong>{data.pesos.pesoPreco}%</strong> • Prazo de Execução: <strong>{data.pesos.pesoPrazo}%</strong> • Capacidade Técnica: <strong>{data.pesos.pesoTecnico}%</strong> • Condições & Garantia: <strong>{data.pesos.pesoCondicoes}%</strong>
            </div>
          </div>
        </div>
        <div style={{ fontSize: '12px', color: '#16a34a', fontWeight: 700 }}>
          Score Máximo: 100 pontos
        </div>
      </div>

      {/* MATRIZ COMPARATIVA LADO A LADO */}
      <div style={{ display: 'grid', gridTemplateColumns: `repeat(${data.propostas.length}, minmax(320px, 1fr))`, gap: '20px', alignItems: 'stretch' }}>
        {data.propostas.map((prop, index) => {
          const isPrimeiroLugar = index === 0;
          return (
            <div
              key={prop.propostaId}
              className="card"
              style={{
                padding: '24px',
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'space-between',
                border: isPrimeiroLugar ? '2px solid #059669' : '1px solid var(--border)',
                backgroundColor: isPrimeiroLugar ? '#f0fdf4' : '#fff',
                position: 'relative'
              }}
            >
              {isPrimeiroLugar && (
                <div
                  style={{
                    position: 'absolute',
                    top: '-12px',
                    right: '20px',
                    backgroundColor: '#059669',
                    color: '#fff',
                    padding: '3px 12px',
                    borderRadius: '20px',
                    fontSize: '11px',
                    fontWeight: 800,
                    display: 'flex',
                    alignItems: 'center',
                    gap: '4px',
                    boxShadow: '0 2px 4px rgba(0,0,0,0.1)'
                  }}
                >
                  <Award size={13} />
                  1º LUGAR NO SCORE
                </div>
              )}

              <div>
                {/* FORNECEDOR & SCORE */}
                <div style={{ borderBottom: '1px solid var(--border)', paddingBottom: '16px', marginBottom: '16px' }}>
                  <div style={{ fontSize: '16px', fontWeight: 800, color: 'var(--text-main)' }}>
                    {prop.razaoSocial}
                  </div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
                    CNPJ: {prop.cnpj}
                  </div>

                  {/* SCORE PONDERADO */}
                  <div style={{ marginTop: '14px', backgroundColor: '#fff', padding: '12px', borderRadius: '8px', border: '1px solid var(--border)' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
                      <span style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>PONTUAÇÃO GERAL:</span>
                      <span style={{ fontSize: '24px', fontWeight: 900, color: isPrimeiroLugar ? '#059669' : 'var(--blue-primary)' }}>
                        {prop.pontuacaoPonderada.toFixed(1)} <span style={{ fontSize: '13px', fontWeight: 600 }}>/ 100</span>
                      </span>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '6px', marginTop: '8px', fontSize: '11.5px', color: 'var(--text-muted)' }}>
                      <div>Preço: <strong>{prop.scoreComercial.toFixed(0)} pts</strong></div>
                      <div>Prazo: <strong>{prop.scorePrazo.toFixed(0)} pts</strong></div>
                      <div>Técnica: <strong>{prop.scoreTecnico.toFixed(0)} pts</strong></div>
                      <div>Condições: <strong>{prop.scoreCondicoes.toFixed(0)} pts</strong></div>
                    </div>
                  </div>
                </div>

                {/* VALOR COMERCIAL */}
                <div style={{ marginBottom: '16px' }}>
                  <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
                    VALOR TOTAL PROPOSTO:
                  </div>
                  <div style={{ fontSize: '26px', fontWeight: 900, color: prop.valorTotal === melhorPreco ? '#16a34a' : 'var(--text-main)', marginTop: '2px' }}>
                    R$ {prop.valorTotal.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                  </div>

                  {/* COMPOSIÇÃO DOS VALORES */}
                  <div style={{ fontSize: '11.5px', color: 'var(--text-muted)', marginTop: '6px', lineHeight: 1.4 }}>
                    Mão de Obra: R$ {prop.valorMaoDeObra.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                    <br />
                    Materiais Inclusos: R$ {prop.valorMateriaisInclusos.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                    {prop.deslocamento > 0 && (
                      <> • Deslocamento: R$ {prop.deslocamento.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</>
                    )}
                  </div>
                </div>

                {/* PRAZOS & CONDIÇÕES */}
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', padding: '12px 0', borderTop: '1px solid var(--border)', borderBottom: '1px solid var(--border)', marginBottom: '16px' }}>
                  <div>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 700 }}>PRAZO EXECUÇÃO:</div>
                    <div style={{ fontSize: '14px', fontWeight: 800, color: prop.prazoExecucaoDias === melhorPrazo ? '#16a34a' : 'var(--text-main)', marginTop: '2px' }}>
                      {prop.prazoExecucaoDias} dias
                    </div>
                  </div>
                  <div>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 700 }}>GARANTIA:</div>
                    <div style={{ fontSize: '14px', fontWeight: 800, color: 'var(--text-main)', marginTop: '2px' }}>
                      {prop.garantiaMeses} meses
                    </div>
                  </div>
                  <div style={{ gridColumn: 'span 2' }}>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 700 }}>CONDIÇÕES DE PAGAMENTO:</div>
                    <div style={{ fontSize: '12.5px', fontWeight: 600, color: 'var(--text-main)', marginTop: '2px' }}>
                      {prop.condicoesPagamento}
                    </div>
                  </div>
                </div>

                {/* DETALHES TÉCNICOS & METODOLOGIA */}
                <div style={{ fontSize: '12.5px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                  <div>
                    <div style={{ fontWeight: 700, color: 'var(--text-muted)', fontSize: '11px' }}>METODOLOGIA & SOLUÇÃO:</div>
                    <div style={{ marginTop: '2px', color: 'var(--text-main)', lineHeight: 1.4 }}>
                      {prop.metodologia || prop.descricaoSolucao || 'Conforme escopo aprovado.'}
                    </div>
                  </div>

                  <div>
                    <div style={{ fontWeight: 700, color: 'var(--text-muted)', fontSize: '11px' }}>EQUIPE ALOCADA:</div>
                    <div style={{ marginTop: '2px', color: 'var(--text-main)' }}>
                      {prop.equipeAlocada || 'Profissionais habilitados'}
                    </div>
                  </div>

                  {prop.materiaisInclusosTexto && (
                    <div>
                      <div style={{ fontWeight: 700, color: 'var(--text-muted)', fontSize: '11px' }}>MATERIAIS INCLUSOS:</div>
                      <div style={{ marginTop: '2px', color: 'var(--text-main)' }}>
                        {prop.materiaisInclusosTexto}
                      </div>
                    </div>
                  )}

                  {prop.certificacoesEquipe && (
                    <div>
                      <div style={{ fontWeight: 700, color: 'var(--text-muted)', fontSize: '11px' }}>CERTIFICAÇÕES:</div>
                      <div style={{ marginTop: '2px', color: '#0284c7', fontWeight: 600 }}>
                        {prop.certificacoesEquipe}
                      </div>
                    </div>
                  )}
                </div>
              </div>

              {/* BOTÃO DE SELEÇÃO E CONTRATAÇÃO */}
              <div style={{ marginTop: '24px', paddingTop: '16px', borderTop: '1px solid var(--border)' }}>
                {prop.isVencedora ? (
                  <div style={{ textAlign: 'center', padding: '10px', backgroundColor: '#ecfdf5', borderRadius: '6px', color: '#065f46', fontWeight: 700, fontSize: '13px', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '6px' }}>
                    <CheckCircle2 size={16} />
                    PROPOSTA CONTRATADA
                  </div>
                ) : (
                  <button
                    onClick={() => {
                      setPropostaAprovar(prop);
                      setJustificativa(`Proposta selecionada por obter pontuação de ${prop.pontuacaoPonderada.toFixed(1)} pontos e apresentar excelente custo-benefício.`);
                    }}
                    className={`btn ${isPrimeiroLugar ? 'btn-primary' : 'btn-secondary'}`}
                    style={{ width: '100%', padding: '10px', fontWeight: 700, fontSize: '13.5px', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '8px' }}
                  >
                    <Award size={16} />
                    Contratar Este Fornecedor
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {/* MODAL DE CONFIRMAÇÃO DE CONTRATAÇÃO & GERAÇÃO DE ORDEM DE SERVIÇO */}
      {propostaAprovar && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 999 }}>
          <div className="card" style={{ width: '100%', maxWidth: '540px', padding: '24px', margin: '20px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 800, margin: '0 0 12px 0', color: 'var(--text-main)', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <FileCheck size={20} color="var(--blue-primary)" />
              Confirmar Contratação e Gerar Ordem de Serviço
            </h3>

            <p style={{ fontSize: '13px', color: 'var(--text-muted)', margin: '0 0 16px 0' }}>
              Você está contratando o fornecedor <strong>{propostaAprovar.razaoSocial}</strong> pelo valor total de{' '}
              <strong style={{ color: '#16a34a' }}>
                R$ {propostaAprovar.valorTotal.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
              </strong>.
            </p>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '14px', marginBottom: '20px' }}>
              <div>
                <label className="label">Justificativa da Escolha (Auditável)</label>
                <textarea
                  className="input"
                  rows={3}
                  value={justificativa}
                  onChange={(e) => setJustificativa(e.target.value)}
                  placeholder="Informe a motivação para seleção desta proposta..."
                />
              </div>

              <div>
                <label className="label">Observações e Recomendações Iniciais</label>
                <input
                  type="text"
                  className="input"
                  value={observacao}
                  onChange={(e) => setObservacao(e.target.value)}
                  placeholder="Ex.: Solicitar crachás de acesso antes da segunda-feira"
                />
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                type="button"
                onClick={() => setPropostaAprovar(null)}
                className="btn btn-secondary"
                disabled={aprovando}
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={handleAprovar}
                className="btn btn-primary"
                disabled={aprovando}
                style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
              >
                <CheckCircle2 size={16} />
                {aprovando ? 'Gerando Ordem de Serviço...' : 'Confirmar Contratação'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

