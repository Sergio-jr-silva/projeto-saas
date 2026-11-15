import React, { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { api } from '../../services/api';
import {
  Wrench,
  Clock,
  MapPin,
  ShieldCheck,
  Send,
  Save,
  CheckCircle2,
  AlertTriangle,
  FileText,
  DollarSign,
  Building2,
  Calendar,
  XCircle
} from 'lucide-react';

export const PortalServicoCotacaoPage: React.FC = () => {
  const { token } = useParams<{ token: string }>();

  const [loading, setLoading] = useState(true);
  const [erroValidacao, setErroValidacao] = useState<string | null>(null);
  const [sucessoMsg, setSucessoMsg] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  const [cotacao, setCotacao] = useState<any>(null);
  const [fornecedor, setFornecedor] = useState<any>(null);

  // Formulário da Proposta do Fornecedor
  const [valorMaoDeObra, setValorMaoDeObra] = useState<number>(0);
  const [valorMateriaisInclusos, setValorMateriaisInclusos] = useState<number>(0);
  const [valorEquipamentos, setValorEquipamentos] = useState<number>(0);
  const [deslocamento, setDeslocamento] = useState<number>(0);
  const [outrasDespesas, setOutrasDespesas] = useState<number>(0);
  const [desconto, setDesconto] = useState<number>(0);
  const [impostosTotal, setImpostosTotal] = useState<number>(0);

  const [prazoInicioDias, setPrazoInicioDias] = useState<number>(3);
  const [prazoExecucaoDias, setPrazoExecucaoDias] = useState<number>(15);
  const [condicoesPagamento, setCondicoesPagamento] = useState('30 dias');
  const [validadeProposta, setValidadeProposta] = useState(
    new Date(Date.now() + 30 * 86400000).toISOString().split('T')[0]
  );
  const [garantiaMeses, setGarantiaMeses] = useState<number>(12);

  const [descricaoSolucao, setDescricaoSolucao] = useState('');
  const [metodologia, setMetodologia] = useState('');
  const [equipeAlocada, setEquipeAlocada] = useState('');
  const [equipamentosFerramental, setEquipamentosFerramental] = useState('');
  const [materiaisInclusosTexto, setMateriaisInclusosTexto] = useState('');
  const [materiaisNaoInclusosTexto, setMateriaisNaoInclusosTexto] = useState('');
  const [certificacoesEquipe, setCertificacoesEquipe] = useState('');
  const [limitacoesExclusoes, setLimitacoesExclusoes] = useState('');
  const [observacoes, setObservacoes] = useState('');
  const [contatoNome, setContatoNome] = useState('');
  const [contatoTelefone, setContatoTelefone] = useState('');
  const [contatoEmail, setContatoEmail] = useState('');

  // Valores por etapa
  const [etapasValores, setEtapasValores] = useState<Record<string, { valorUnitario: number; valorTotal: number; detalhes: string }>>({});

  // Recusa
  const [modalRecusa, setModalRecusa] = useState(false);
  const [motivoRecusa, setMotivoRecusa] = useState('');
  const [recusado, setRecusado] = useState(false);

  useEffect(() => {
    if (!token) return;
    setLoading(true);

    api.get<any>(`/portal-servico/${token}/detalhes`)
      .then((data) => {
        setCotacao(data.cotacao);
        setFornecedor(data.fornecedor);

        if (data.cotacao?.statusFornecedor === 'Recusado') {
          setRecusado(true);
        }

        // Se já existe proposta salva
        if (data.proposta) {
          const p = data.proposta;
          setValorMaoDeObra(p.valorMaoDeObra || 0);
          setValorMateriaisInclusos(p.valorMateriaisInclusos || 0);
          setValorEquipamentos(p.valorEquipamentos || 0);
          setDeslocamento(p.deslocamento || 0);
          setOutrasDespesas(p.outrasDespesas || 0);
          setDesconto(p.desconto || 0);
          setImpostosTotal(p.impostosTotal || 0);
          setPrazoInicioDias(p.prazoInicioDias || 3);
          setPrazoExecucaoDias(p.prazoExecucaoDias || 15);
          setCondicoesPagamento(p.condicoesPagamento || '30 dias');
          if (p.validadeProposta) setValidadeProposta(p.validadeProposta);
          setGarantiaMeses(p.garantiaMeses || 12);
          setDescricaoSolucao(p.descricaoSolucao || '');
          setMetodologia(p.metodologia || '');
          setEquipeAlocada(p.equipeAlocada || '');
          setEquipamentosFerramental(p.equipamentosFerramental || '');
          setMateriaisInclusosTexto(p.materiaisInclusosTexto || '');
          setMateriaisNaoInclusosTexto(p.materiaisNaoInclusosTexto || '');
          setCertificacoesEquipe(p.certificacoesEquipe || '');
          setLimitacoesExclusoes(p.limitacoesExclusoes || '');
          setObservacoes(p.observacoes || '');
          setContatoNome(p.contatoNome || '');
          setContatoTelefone(p.contatoTelefone || '');
          setContatoEmail(p.contatoEmail || '');

          if (p.etapas && p.etapas.length > 0) {
            const mapEtapas: Record<string, any> = {};
            p.etapas.forEach((et: any) => {
              mapEtapas[et.atividadeServicoId] = {
                valorUnitario: et.valorUnitario,
                valorTotal: et.valorTotal,
                detalhes: et.detalhesExecucao || ''
              };
            });
            setEtapasValores(mapEtapas);
          }
        }
      })
      .catch((err) => {
        setErroValidacao(err.message || 'Link inválido ou expirado.');
      })
      .finally(() => {
        setLoading(false);
      });
  }, [token]);

  const valorTotalCalculado =
    (Number(valorMaoDeObra) || 0) +
    (Number(valorMateriaisInclusos) || 0) +
    (Number(valorEquipamentos) || 0) +
    (Number(deslocamento) || 0) +
    (Number(outrasDespesas) || 0) +
    (Number(impostosTotal) || 0) -
    (Number(desconto) || 0);

  const handleSalvar = async (finalizar: boolean) => {
    if (!token) return;
    setSalvando(true);
    setSucessoMsg(null);

    try {
      const etapasPayload = (cotacao?.atividades || []).map((at: any) => {
        const item = etapasValores[at.id] || { valorUnitario: 0, valorTotal: 0, detalhes: '' };
        return {
          atividadeServicoId: at.id,
          valorUnitario: Number(item.valorUnitario) || 0,
          valorTotal: Number(item.valorTotal) || (Number(item.valorUnitario) || 0) * (Number(at.quantidade) || 1),
          detalhesExecucao: item.detalhes
        };
      });

      const payload = {
        valorMaoDeObra: Number(valorMaoDeObra) || 0,
        valorMateriaisInclusos: Number(valorMateriaisInclusos) || 0,
        valorEquipamentos: Number(valorEquipamentos) || 0,
        deslocamento: Number(deslocamento) || 0,
        outrasDespesas: Number(outrasDespesas) || 0,
        desconto: Number(desconto) || 0,
        impostosTotal: Number(impostosTotal) || 0,
        valorTotal: valorTotalCalculado,
        prazoInicioDias: Number(prazoInicioDias) || 3,
        prazoExecucaoDias: Number(prazoExecucaoDias) || 15,
        condicoesPagamento,
        validadeProposta: validadeProposta ? validadeProposta : null,
        garantiaMeses: Number(garantiaMeses) || 12,
        descricaoSolucao,
        metodologia,
        equipeAlocada,
        equipamentosFerramental,
        materiaisInclusosTexto,
        materiaisNaoInclusosTexto,
        certificacoesEquipe,
        limitacoesExclusoes,
        observacoes,
        contatoNome,
        contatoTelefone,
        contatoEmail,
        finalizarEnvio: finalizar,
        etapas: etapasPayload
      };

      const res = await api.post<any>(`/portal-servico/${token}/proposta`, payload);
      setSucessoMsg(res.mensagem || 'Proposta atualizada com sucesso!');
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch (err: any) {
      alert(err.message || 'Erro ao submeter proposta.');
    } finally {
      setSalvando(false);
    }
  };

  const handleRecusar = async () => {
    if (!token) return;
    try {
      await api.post(`/portal-servico/${token}/recusar`, { motivo: motivoRecusa });
      setRecusado(true);
      setModalRecusa(false);
    } catch (err: any) {
      alert(err.message || 'Erro ao registrar recusa.');
    }
  };

  if (loading) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: '#f1f5f9' }}>
        <div style={{ padding: '30px', textAlign: 'center', background: '#fff', borderRadius: '12px', boxShadow: '0 4px 6px rgba(0,0,0,0.05)' }}>
          <Wrench size={32} color="var(--blue-primary)" style={{ animation: 'spin 2s linear infinite', margin: '0 auto 12px' }} />
          <div style={{ fontWeight: 600 }}>Carregando Portal de Propostas de Serviços...</div>
        </div>
      </div>
    );
  }

  if (erroValidacao) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: '#f1f5f9', padding: '20px' }}>
        <div style={{ maxWidth: '480px', width: '100%', background: '#fff', padding: '36px', borderRadius: '12px', textAlign: 'center', boxShadow: '0 10px 15px -3px rgba(0,0,0,0.1)' }}>
          <AlertTriangle size={48} color="#ef4444" style={{ margin: '0 auto 16px' }} />
          <h2 style={{ fontSize: '20px', fontWeight: 800, margin: '0 0 8px 0', color: 'var(--text-main)' }}>Link Expirado ou Inválido</h2>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)', lineHeight: 1.5 }}>
            {erroValidacao}
          </p>
        </div>
      </div>
    );
  }

  if (recusado) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: '#f1f5f9', padding: '20px' }}>
        <div style={{ maxWidth: '480px', width: '100%', background: '#fff', padding: '36px', borderRadius: '12px', textAlign: 'center', boxShadow: '0 10px 15px -3px rgba(0,0,0,0.1)' }}>
          <XCircle size={48} color="#64748b" style={{ margin: '0 auto 16px' }} />
          <h2 style={{ fontSize: '20px', fontWeight: 800, margin: '0 0 8px 0' }}>Cotação Recusada</h2>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
            Você optou por não participar desta cotação. Agradecemos o seu retorno.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div style={{ minHeight: '100vh', backgroundColor: '#f8fafc', paddingBottom: '60px' }}>
      {/* HEADER SUPERIOR DO PORTAL */}
      <header style={{ backgroundColor: '#fff', borderBottom: '1px solid #e2e8f0', padding: '16px 24px', position: 'sticky', top: 0, zIndex: 100 }}>
        <div style={{ maxWidth: '1040px', margin: '0 auto', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <div style={{ width: '36px', height: '36px', borderRadius: '8px', backgroundColor: 'var(--blue-primary)', color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 800, fontSize: '18px' }}>
              N
            </div>
            <div>
              <div style={{ fontWeight: 800, fontSize: '16px', color: 'var(--blue-primary)' }}>NEXO COMPRAS</div>
              <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>PORTAL DE SERVIÇOS & FORNECEDORES</div>
            </div>
          </div>

          <div style={{ textAlign: 'right' }}>
            <div style={{ fontSize: '13px', fontWeight: 700, color: 'var(--text-main)' }}>
              {fornecedor?.razaoSocial}
            </div>
            <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
              CNPJ: {fornecedor?.cnpj}
            </div>
          </div>
        </div>
      </header>

      <main style={{ maxWidth: '1040px', margin: '24px auto', padding: '0 16px', display: 'flex', flexDirection: 'column', gap: '24px' }}>
        {/* BANNER DA COTAÇÃO */}
        <div style={{ background: 'linear-gradient(135deg, #1e3a8a 0%, #2563eb 100%)', borderRadius: '12px', padding: '28px', color: '#fff' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '12px' }}>
            <div>
              <div style={{ fontSize: '12px', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.6px', opacity: 0.9 }}>
                Solicitante: {cotacao?.empresaContratante}
              </div>
              <h1 style={{ fontSize: '24px', fontWeight: 900, margin: '6px 0', letterSpacing: '-0.5px' }}>
                {cotacao?.numero} • {cotacao?.titulo}
              </h1>
              <div style={{ fontSize: '13px', opacity: 0.9 }}>
                Categoria: {cotacao?.categoria} • Prioridade: {cotacao?.prioridade}
              </div>
            </div>

            <div style={{ background: 'rgba(255,255,255,0.15)', padding: '12px 18px', borderRadius: '8px', backdropFilter: 'blur(4px)', textAlign: 'right' }}>
              <div style={{ fontSize: '11px', textTransform: 'uppercase', opacity: 0.9 }}>Data Limite de Envio:</div>
              <div style={{ fontSize: '16px', fontWeight: 800, marginTop: '2px' }}>
                {new Date(cotacao?.dataLimiteResposta).toLocaleDateString('pt-BR')}
              </div>
            </div>
          </div>
        </div>

        {sucessoMsg && (
          <div style={{ backgroundColor: '#ecfdf5', border: '1px solid #a7f3d0', padding: '16px 20px', borderRadius: '8px', color: '#065f46', display: 'flex', alignItems: 'center', gap: '10px', fontSize: '14px', fontWeight: 700 }}>
            <CheckCircle2 size={20} />
            <span>{sucessoMsg}</span>
          </div>
        )}

        {/* DETALHES DO ESCOPO & LOCALIZAÇÃO */}
        <div className="card" style={{ padding: '24px' }}>
          <h2 style={{ fontSize: '16px', fontWeight: 800, margin: '0 0 16px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <FileText size={18} color="var(--blue-primary)" />
            Escopo do Serviço e Requisitos da Contratante
          </h2>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', fontSize: '13.5px', lineHeight: 1.5 }}>
            {cotacao?.necessidadeMotivacao && (
              <div>
                <strong style={{ color: 'var(--text-muted)', fontSize: '11.5px', textTransform: 'uppercase' }}>Motivação:</strong>
                <div>{cotacao.necessidadeMotivacao}</div>
              </div>
            )}

            <div>
              <strong style={{ color: 'var(--text-muted)', fontSize: '11.5px', textTransform: 'uppercase' }}>Descrição Detalhada do Trabalho:</strong>
              <div style={{ whiteSpace: 'pre-wrap', marginTop: '2px' }}>{cotacao?.descricaoDetalhada || 'Conforme alinhamento e etapas.'}</div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '14px', padding: '14px', background: '#f8fafc', borderRadius: '8px' }}>
              <div>
                <strong style={{ fontSize: '11px', color: 'var(--text-muted)' }}>LOCAL DE ATENDIMENTO:</strong>
                <div style={{ fontWeight: 600, marginTop: '2px' }}>
                  {cotacao?.atendimentoPresencial
                    ? `${cotacao.endereco}, ${cotacao.numeroLocal} - ${cotacao.bairro} - ${cotacao.cidade}/${cotacao.uf}`
                    : 'Atendimento Remoto'}
                </div>
              </div>
              <div>
                <strong style={{ fontSize: '11px', color: 'var(--text-muted)' }}>HORÁRIO PERMITIDO:</strong>
                <div style={{ fontWeight: 600, marginTop: '2px' }}>{cotacao?.horarioPermitido}</div>
              </div>
              <div>
                <strong style={{ fontSize: '11px', color: 'var(--text-muted)' }}>GARANTIA MÍNIMA EXIGIDA:</strong>
                <div style={{ fontWeight: 600, marginTop: '2px' }}>{cotacao?.garantiaEsperadaMeses} meses</div>
              </div>
              <div>
                <strong style={{ fontSize: '11px', color: 'var(--text-muted)' }}>CERTIFICAÇÕES EXIGIDAS:</strong>
                <div style={{ fontWeight: 600, marginTop: '2px', color: '#0284c7' }}>{cotacao?.certificacoesExigidas || 'Nenhuma exigida'}</div>
              </div>
            </div>
          </div>
        </div>

        {/* ETAPAS A SEREM COTADAS */}
        <div className="card" style={{ padding: '24px' }}>
          <h2 style={{ fontSize: '16px', fontWeight: 800, margin: '0 0 16px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Clock size={18} color="var(--blue-primary)" />
            Etapas / Atividades a Serem Executadas
          </h2>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
            {(cotacao?.atividades || []).map((at: any) => {
              const val = etapasValores[at.id] || { valorUnitario: 0, valorTotal: 0, detalhes: '' };
              return (
                <div key={at.id} style={{ border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px', backgroundColor: '#fff' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '8px' }}>
                    <div>
                      <span className="badge badge-blue">#{at.ordem}</span>
                      <strong style={{ marginLeft: '8px', fontSize: '14px' }}>{at.etapa ? `[${at.etapa}] ` : ''}{at.nome}</strong>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
                        {at.descricao}
                      </div>
                    </div>
                    <div style={{ textAlign: 'right' }}>
                      <span className="badge badge-gray">{at.quantidade} {at.unidadeMedicao}</span>
                    </div>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '12px', marginTop: '12px', paddingTop: '10px', borderTop: '1px solid #f1f5f9' }}>
                    <div>
                      <label className="label" style={{ fontSize: '11.5px' }}>Preço Unitário para esta etapa (R$)</label>
                      <input
                        type="number"
                        className="input"
                        placeholder="0,00"
                        value={val.valorUnitario || ''}
                        onChange={(e) => {
                          const unit = Number(e.target.value) || 0;
                          setEtapasValores({
                            ...etapasValores,
                            [at.id]: {
                              ...val,
                              valorUnitario: unit,
                              valorTotal: unit * (at.quantidade || 1)
                            }
                          });
                        }}
                      />
                    </div>
                    <div>
                      <label className="label" style={{ fontSize: '11.5px' }}>Detalhes / Metodologia específica desta etapa</label>
                      <input
                        type="text"
                        className="input"
                        placeholder="Ex.: Execução em 2 dias com 3 profissionais..."
                        value={val.detalhes}
                        onChange={(e) => {
                          setEtapasValores({
                            ...etapasValores,
                            [at.id]: { ...val, detalhes: e.target.value }
                          });
                        }}
                      />
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* VALORES COMERCIAIS & PRAZOS */}
        <div className="card" style={{ padding: '24px' }}>
          <h2 style={{ fontSize: '16px', fontWeight: 800, margin: '0 0 16px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <DollarSign size={18} color="var(--blue-primary)" />
            Composição Comercial da Proposta
          </h2>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px' }}>
            <div>
              <label className="label">Valor Mão de Obra (R$) *</label>
              <input
                type="number"
                className="input"
                value={valorMaoDeObra || ''}
                onChange={(e) => setValorMaoDeObra(Number(e.target.value) || 0)}
              />
            </div>

            <div>
              <label className="label">Valor Materiais Inclusos (R$)</label>
              <input
                type="number"
                className="input"
                value={valorMateriaisInclusos || ''}
                onChange={(e) => setValorMateriaisInclusos(Number(e.target.value) || 0)}
              />
            </div>

            <div>
              <label className="label">Valor Equipamentos / Locação (R$)</label>
              <input
                type="number"
                className="input"
                value={valorEquipamentos || ''}
                onChange={(e) => setValorEquipamentos(Number(e.target.value) || 0)}
              />
            </div>

            <div>
              <label className="label">Deslocamento / Viagens (R$)</label>
              <input
                type="number"
                className="input"
                value={deslocamento || ''}
                onChange={(e) => setDeslocamento(Number(e.target.value) || 0)}
              />
            </div>

            <div>
              <label className="label">Impostos Inclusos (R$)</label>
              <input
                type="number"
                className="input"
                value={impostosTotal || ''}
                onChange={(e) => setImpostosTotal(Number(e.target.value) || 0)}
              />
            </div>

            <div>
              <label className="label">Desconto Comercial (R$)</label>
              <input
                type="number"
                className="input"
                value={desconto || ''}
                onChange={(e) => setDesconto(Number(e.target.value) || 0)}
              />
            </div>

            {/* TOTAL GERAL */}
            <div style={{ gridColumn: 'span 2', padding: '16px', background: '#eff6ff', borderRadius: '8px', border: '1px solid #bfdbfe', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <div style={{ fontSize: '12px', fontWeight: 700, color: '#1e40af' }}>VALOR TOTAL FINAL DA PROPOSTA:</div>
                <div style={{ fontSize: '11px', color: '#3b82f6' }}>Soma de mão de obra + materiais + despesas - descontos</div>
              </div>
              <div style={{ fontSize: '28px', fontWeight: 900, color: '#1d4ed8' }}>
                R$ {valorTotalCalculado.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
              </div>
            </div>

            <div>
              <label className="label">Prazo para Início (dias úteis)</label>
              <input
                type="number"
                className="input"
                min="1"
                value={prazoInicioDias}
                onChange={(e) => setPrazoInicioDias(Number(e.target.value))}
              />
            </div>

            <div>
              <label className="label">Prazo Total de Execução (dias corridos) *</label>
              <input
                type="number"
                className="input"
                min="1"
                value={prazoExecucaoDias}
                onChange={(e) => setPrazoExecucaoDias(Number(e.target.value))}
              />
            </div>

            <div>
              <label className="label">Condições de Pagamento Oferecidas</label>
              <input
                type="text"
                className="input"
                value={condicoesPagamento}
                onChange={(e) => setCondicoesPagamento(e.target.value)}
              />
            </div>

            <div>
              <label className="label">Garantia Oferecida (Meses)</label>
              <input
                type="number"
                className="input"
                min="1"
                value={garantiaMeses}
                onChange={(e) => setGarantiaMeses(Number(e.target.value))}
              />
            </div>
          </div>
        </div>

        {/* METODOLOGIA TÉCNICA E EQUIPE */}
        <div className="card" style={{ padding: '24px' }}>
          <h2 style={{ fontSize: '16px', fontWeight: 800, margin: '0 0 16px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <ShieldCheck size={18} color="var(--blue-primary)" />
            Metodologia, Equipe Técnica e Exclusões
          </h2>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
            <div style={{ gridColumn: 'span 2' }}>
              <label className="label">Metodologia e Procedimentos Técnicos da Solução</label>
              <textarea
                className="input"
                rows={3}
                placeholder="Descreva as técnicas, padrões normativos e boas práticas que sua equipe adotará..."
                value={metodologia}
                onChange={(e) => setMetodologia(e.target.value)}
              />
            </div>

            <div>
              <label className="label">Equipe Alocada (Cargos e Quantidade)</label>
              <input
                type="text"
                className="input"
                placeholder="Ex.: 1 Engenheiro Residente, 2 Técnicos Eletricistas..."
                value={equipeAlocada}
                onChange={(e) => setEquipeAlocada(e.target.value)}
              />
            </div>

            <div>
              <label className="label">Certificações e Habilitações da Equipe</label>
              <input
                type="text"
                className="input"
                placeholder="Ex.: NR-10, NR-35, CREA/CFT Ativo..."
                value={certificacoesEquipe}
                onChange={(e) => setCertificacoesEquipe(e.target.value)}
              />
            </div>

            <div>
              <label className="label">Materiais Inclusos no Preço</label>
              <input
                type="text"
                className="input"
                placeholder="Ex.: Cabos, fixadores, conectores e consumíveis"
                value={materiaisInclusosTexto}
                onChange={(e) => setMateriaisInclusosTexto(e.target.value)}
              />
            </div>

            <div>
              <label className="label">Materiais e Serviços NÃO Inclusos (Exclusões)</label>
              <input
                type="text"
                className="input"
                placeholder="Ex.: Alvenaria, pintura externa, andaimes especiais"
                value={materiaisNaoInclusosTexto}
                onChange={(e) => setMateriaisNaoInclusosTexto(e.target.value)}
              />
            </div>

            <div style={{ gridColumn: 'span 2' }}>
              <label className="label">Observações Gerais da Proposta</label>
              <input
                type="text"
                className="input"
                value={observacoes}
                onChange={(e) => setObservacoes(e.target.value)}
              />
            </div>
          </div>
        </div>

        {/* DADOS DE CONTATO DO RESPONSÁVEL */}
        <div className="card" style={{ padding: '24px' }}>
          <h2 style={{ fontSize: '16px', fontWeight: 800, margin: '0 0 16px 0' }}>
            Contato do Responsável pela Proposta
          </h2>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px' }}>
            <div>
              <label className="label">Nome do Contato</label>
              <input
                type="text"
                className="input"
                value={contatoNome}
                onChange={(e) => setContatoNome(e.target.value)}
              />
            </div>
            <div>
              <label className="label">Telefone / WhatsApp</label>
              <input
                type="text"
                className="input"
                value={contatoTelefone}
                onChange={(e) => setContatoTelefone(e.target.value)}
              />
            </div>
            <div>
              <label className="label">E-mail Comercial</label>
              <input
                type="email"
                className="input"
                value={contatoEmail}
                onChange={(e) => setContatoEmail(e.target.value)}
              />
            </div>
          </div>
        </div>

        {/* FOOTER ACTIONS DO FORNECEDOR */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '16px' }}>
          <button
            type="button"
            onClick={() => setModalRecusa(true)}
            className="btn btn-secondary"
            style={{ color: '#ef4444', borderColor: '#fca5a5' }}
          >
            Recusar Participação nesta Cotação
          </button>

          <div style={{ display: 'flex', gap: '12px' }}>
            <button
              type="button"
              onClick={() => handleSalvar(false)}
              disabled={salvando}
              className="btn btn-secondary"
              style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
            >
              <Save size={16} />
              Salvar Rascunho
            </button>
            <button
              type="button"
              onClick={() => handleSalvar(true)}
              disabled={salvando || valorTotalCalculado <= 0}
              className="btn btn-primary"
              style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '12px 28px', fontSize: '14px', fontWeight: 800 }}
            >
              <Send size={16} />
              {salvando ? 'Enviando Proposta...' : 'Enviar Proposta Oficial à Contratante'}
            </button>
          </div>
        </div>
      </main>

      {/* MODAL DE RECUSA */}
      {modalRecusa && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 999 }}>
          <div className="card" style={{ width: '100%', maxWidth: '440px', padding: '24px', margin: '20px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 800, margin: '0 0 10px 0' }}>Recusar Cotação</h3>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)', margin: '0 0 16px 0' }}>
              Por favor, informe o motivo pelo qual sua empresa não poderá atender a esta solicitação de serviço:
            </p>
            <textarea
              className="input"
              rows={3}
              placeholder="Ex.: Sem disponibilidade de agenda, escopo fora da nossa área de atuação..."
              value={motivoRecusa}
              onChange={(e) => setMotivoRecusa(e.target.value)}
              style={{ marginBottom: '16px' }}
            />
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button onClick={() => setModalRecusa(false)} className="btn btn-secondary">
                Voltar
              </button>
              <button onClick={handleRecusar} className="btn" style={{ backgroundColor: '#ef4444', color: '#fff' }}>
                Confirmar Recusa
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

