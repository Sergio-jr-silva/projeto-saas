import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
import {
  Wrench,
  ArrowLeft,
  Plus,
  Trash2,
  Save,
  CheckCircle2,
  AlertCircle,
  Clock,
  MapPin,
  ShieldCheck,
  Sliders,
  Users,
  Store,
  FileText
} from 'lucide-react';

interface FornecedorOpcao {
  id: string;
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  cidade: string;
  uf: string;
}

interface FilialOpcao {
  id: string;
  codigo: string;
  nome: string;
  cidade?: string;
  uf?: string;
}

interface NovaAtividade {
  ordem: number;
  etapa: string;
  nome: string;
  descricao: string;
  quantidade: number;
  unidadeMedicao: string;
  localExecucao: string;
  requisitosTecnicos: string;
  resultadoEsperado: string;
  valorEstimadoUnitario: number;
}

export const NovaCotacaoServico: React.FC = () => {
  const navigate = useNavigate();
  const { user } = useAuth();

  // Dados Gerais do Escopo e Identificação
  const [titulo, setTitulo] = useState('');
  const [categoria, setCategoria] = useState('ManutencaoPredial');
  const [prioridade, setPrioridade] = useState('Media');
  const [filialId, setFilialId] = useState<string>('');
  const [responsavelNome, setResponsavelNome] = useState(user?.nome || '');
  const [dataLimiteResposta, setDataLimiteResposta] = useState(
    new Date(Date.now() + 7 * 86400000).toISOString().split('T')[0]
  );
  const [dataDesejadaInicio, setDataDesejadaInicio] = useState(
    new Date(Date.now() + 15 * 86400000).toISOString().split('T')[0]
  );
  const [prazoEstimadoDias, setPrazoEstimadoDias] = useState<number>(30);
  const [necessidadeMotivacao, setNecessidadeMotivacao] = useState('');
  const [descricaoDetalhada, setDescricaoDetalhada] = useState('');
  const [objetivoEsperado, setObjetivoEsperado] = useState('');
  const [resultadoEsperado, setResultadoEsperado] = useState('');

  // Local de Execução
  const [atendimentoPresencial, setAtendimentoPresencial] = useState(true);
  const [cep, setCep] = useState('');
  const [endereco, setEndereco] = useState('');
  const [numeroLocal, setNumeroLocal] = useState('');
  const [complemento, setComplemento] = useState('');
  const [bairro, setBairro] = useState('');
  const [cidade, setCidade] = useState('');
  const [uf, setUf] = useState('');
  const [pontoReferencia, setPontoReferencia] = useState('');
  const [horarioPermitido, setHorarioPermitido] = useState('Horário comercial padrão (08h às 18h)');

  // Requisitos e Condições
  const [materiaisInclusosExigidos, setMateriaisInclusosExigidos] = useState('');
  const [equipamentosExigidos, setEquipamentosExigidos] = useState('');
  const [garantiaEsperadaMeses, setGarantiaEsperadaMeses] = useState<number>(12);
  const [condicoesEspeciais, setCondicoesEspeciais] = useState('');
  const [certificacoesExigidas, setCertificacoesExigidas] = useState('');

  // Critérios Ponderados
  const [pesoPreco, setPesoPreco] = useState<number>(50);
  const [pesoPrazo, setPesoPrazo] = useState<number>(20);
  const [pesoTecnico, setPesoTecnico] = useState<number>(20);
  const [pesoCondicoes, setPesoCondicoes] = useState<number>(10);

  // Atividades do Escopo
  const [atividades, setAtividades] = useState<NovaAtividade[]>([
    {
      ordem: 1,
      etapa: 'Fase 1',
      nome: 'Inspeção e Levantamento Técnico',
      descricao: 'Visita técnica in loco, diagnóstico e alinhamento do escopo',
      quantidade: 1,
      unidadeMedicao: 'SV',
      localExecucao: 'Área da intervenção',
      requisitosTecnicos: 'Profissional qualificado com registro técnico',
      resultadoEsperado: 'Relatório diagnóstico e plano de execução aprovado',
      valorEstimadoUnitario: 1500
    }
  ]);

  // Fornecedores e Filiais
  const [fornecedoresDisponiveis, setFornecedoresDisponiveis] = useState<FornecedorOpcao[]>([]);
  const [fornecedoresSelecionados, setFornecedoresSelecionados] = useState<string[]>([]);
  const [filiaisDisponiveis, setFiliaisDisponiveis] = useState<FilialOpcao[]>([]);

  const [loadingInicial, setLoadingInicial] = useState(true);
  const [saving, setSaving] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    let ativo = true;

    const carregarDados = async () => {
      try {
        setLoadingInicial(true);

        // Carregar fornecedores de forma segura
        const respForn = await api.get<any>('/fornecedores').catch(() => null);
        if (ativo) {
          const listaForn = Array.isArray(respForn)
            ? respForn
            : Array.isArray(respForn?.itens)
            ? respForn.itens
            : Array.isArray(respForn?.fornecedores)
            ? respForn.fornecedores
            : [];
          setFornecedoresDisponiveis(listaForn);
        }

        // Carregar filiais de forma segura
        const respFiliais = await api.get<any>('/filiais').catch(() => null);
        if (ativo) {
          const listaFiliais = Array.isArray(respFiliais)
            ? respFiliais
            : Array.isArray(respFiliais?.itens)
            ? respFiliais.itens
            : [];
          setFiliaisDisponiveis(listaFiliais);
          if (listaFiliais.length > 0 && !filialId) {
            setFilialId(listaFiliais[0].id);
          }
        }
      } catch (e) {
        console.warn('Aviso ao carregar dados iniciais de serviços:', e);
      } finally {
        if (ativo) setLoadingInicial(false);
      }
    };

    carregarDados();

    return () => {
      ativo = false;
    };
  }, []);

  const adicionarAtividade = () => {
    const novaOrdem = atividades.length + 1;
    setAtividades([
      ...atividades,
      {
        ordem: novaOrdem,
        etapa: `Fase ${novaOrdem}`,
        nome: '',
        descricao: '',
        quantidade: 1,
        unidadeMedicao: 'SV',
        localExecucao: '',
        requisitosTecnicos: '',
        resultadoEsperado: '',
        valorEstimadoUnitario: 0
      }
    ]);
  };

  const removerAtividade = (index: number) => {
    if (atividades.length <= 1) return;
    const novaLista = atividades.filter((_, idx) => idx !== index);
    setAtividades(novaLista.map((at, idx) => ({ ...at, ordem: idx + 1 })));
  };

  const atualizarAtividade = (index: number, campo: keyof NovaAtividade, valor: any) => {
    const copia = [...atividades];
    copia[index] = { ...copia[index], [campo]: valor };
    setAtividades(copia);
  };

  const toggleFornecedor = (id: string) => {
    if (fornecedoresSelecionados.includes(id)) {
      setFornecedoresSelecionados(fornecedoresSelecionados.filter((f) => f !== id));
    } else {
      setFornecedoresSelecionados([...fornecedoresSelecionados, id]);
    }
  };

  const somaPesos = Number(pesoPreco) + Number(pesoPrazo) + Number(pesoTecnico) + Number(pesoCondicoes);

  const totalEstimado = atividades.reduce(
    (acc, at) => acc + (Number(at.quantidade) || 0) * (Number(at.valorEstimadoUnitario) || 0),
    0
  );

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErro(null);

    if (!titulo.trim()) {
      setErro('Informe o título da cotação de serviço.');
      return;
    }

    if (atividades.length === 0 || atividades.some((a) => !a.nome.trim())) {
      setErro('Todas as atividades do escopo precisam ter um nome/descrição preenchida.');
      return;
    }

    if (somaPesos !== 100) {
      setErro(`A soma dos pesos dos critérios deve ser exatamente 100%. Atualmente está em ${somaPesos}%.`);
      return;
    }

    setSaving(true);
    try {
      const payload = {
        titulo: titulo.trim(),
        categoria,
        prioridade,
        filialId: filialId ? filialId : null,
        dataLimiteResposta: new Date(dataLimiteResposta + 'T23:59:59Z').toISOString(),
        dataDesejadaInicio: dataDesejadaInicio ? new Date(dataDesejadaInicio + 'T00:00:00Z').toISOString() : null,
        prazoEstimadoDias: Number(prazoEstimadoDias) || 30,
        necessidadeMotivacao: necessidadeMotivacao.trim(),
        descricaoDetalhada: descricaoDetalhada.trim(),
        objetivoEsperado: objetivoEsperado.trim(),
        resultadoEsperado: resultadoEsperado.trim(),
        atendimentoPresencial,
        cep: cep.trim(),
        endereco: endereco.trim(),
        numeroLocal: numeroLocal.trim(),
        complemento: complemento.trim(),
        bairro: bairro.trim(),
        cidade: cidade.trim(),
        uf: uf.trim().toUpperCase(),
        pontoReferencia: pontoReferencia.trim(),
        horarioPermitido: horarioPermitido.trim(),
        materiaisInclusosExigidos: materiaisInclusosExigidos.trim(),
        equipamentosExigidos: equipamentosExigidos.trim(),
        garantiaEsperadaMeses: Number(garantiaEsperadaMeses) || 12,
        condicoesEspeciais: condicoesEspeciais.trim(),
        certificacoesExigidas: certificacoesExigidas.trim(),
        pesoPreco: Number(pesoPreco),
        pesoPrazo: Number(pesoPrazo),
        pesoTecnico: Number(pesoTecnico),
        pesoCondicoes: Number(pesoCondicoes),
        atividades: atividades.map((a) => ({
          etapa: a.etapa?.trim() || '',
          nome: a.nome.trim(),
          descricao: a.descricao?.trim() || '',
          quantidade: Number(a.quantidade) || 1,
          unidadeMedicao: a.unidadeMedicao?.trim() || 'SV',
          localExecucao: a.localExecucao?.trim() || '',
          requisitosTecnicos: a.requisitosTecnicos?.trim() || '',
          resultadoEsperado: a.resultadoEsperado?.trim() || '',
          valorEstimadoUnitario: Number(a.valorEstimadoUnitario) || 0
        })),
        fornecedoresIds: fornecedoresSelecionados
      };

      const res = await api.post<{ id: string; numero: string }>('/servicos', payload);
      navigate(`/servicos/${res.id}`);
    } catch (err: any) {
      setErro(err.message || 'Erro ao criar cotação de serviço.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
      {/* HEADER */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <button
            type="button"
            onClick={() => navigate('/servicos')}
            className="btn btn-secondary"
            style={{ padding: '8px 12px' }}
          >
            <ArrowLeft size={16} />
          </button>
          <div>
            <h1 style={{ fontSize: '22px', fontWeight: 800, margin: 0, color: 'var(--text-main)', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Wrench size={22} color="var(--blue-primary)" />
              Nova Cotação de Serviço
            </h1>
            <p style={{ margin: 0, fontSize: '13px', color: 'var(--text-muted)' }}>
              Estruture o escopo completo, defina as entregas e convide prestadores para enviar propostas.
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
          <button
            type="button"
            onClick={() => navigate('/servicos')}
            className="btn btn-secondary"
          >
            Cancelar
          </button>
          <button
            type="submit"
            className="btn btn-primary"
            disabled={saving}
            style={{ display: 'flex', alignItems: 'center', gap: '8px' }}
          >
            <Save size={16} />
            {saving ? 'Criando Cotação...' : 'Criar Cotação de Serviço'}
          </button>
        </div>
      </div>

      {erro && (
        <div style={{ backgroundColor: '#fef2f2', border: '1px solid #fecaca', padding: '12px 16px', borderRadius: '8px', color: '#991b1b', display: 'flex', alignItems: 'center', gap: '10px', fontSize: '13.5px' }}>
          <AlertCircle size={18} />
          <span>{erro}</span>
        </div>
      )}

      {loadingInicial && (
        <div style={{ padding: '12px 16px', background: '#eff6ff', borderRadius: '8px', color: '#1e40af', fontSize: '13px', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Clock size={16} />
          <span>Carregando dados complementares da empresa e fornecedores...</span>
        </div>
      )}

      {/* 1. ESCOPO GERAL & OBJETIVOS */}
      <div className="card" style={{ padding: '24px' }}>
        <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Wrench size={18} color="var(--blue-primary)" />
          1. Identificação do Serviço e Justificativa
        </h3>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: '16px' }}>
          <div style={{ gridColumn: 'span 2' }}>
            <label className="label">Título da Cotação / Objeto da Contratação *</label>
            <input
              type="text"
              className="input"
              required
              placeholder="Ex.: Manutenção Preventiva do Sistema de Climatização Central"
              value={titulo}
              onChange={(e) => setTitulo(e.target.value)}
            />
          </div>

          <div>
            <label className="label">Categoria do Serviço</label>
            <select
              className="input"
              value={categoria}
              onChange={(e) => setCategoria(e.target.value)}
            >
              <option value="ManutencaoPredial">Manutenção Predial</option>
              <option value="TI">TI, Telecom & Redes</option>
              <option value="Facilities">Facilities & Limpeza</option>
              <option value="Obras">Obras, Reformas & Construção</option>
              <option value="Consultoria">Consultoria & Auditoria</option>
              <option value="Juridico">Serviços Jurídicos / Contábeis</option>
              <option value="Logistica">Logística & Transportes Especializados</option>
              <option value="Engenharia">Projetos & Engenharia</option>
              <option value="Marketing">Marketing & Design</option>
              <option value="Outros">Outros Serviços</option>
            </select>
          </div>

          <div>
            <label className="label">Nível de Prioridade / Urgência</label>
            <select
              className="input"
              value={prioridade}
              onChange={(e) => setPrioridade(e.target.value)}
            >
              <option value="Baixa">Baixa</option>
              <option value="Media">Média</option>
              <option value="Alta">Alta</option>
              <option value="Urgente">Urgente</option>
            </select>
          </div>

          {filiaisDisponiveis.length > 0 && (
            <div>
              <label className="label">Unidade / Filial Solicitante</label>
              <select
                className="input"
                value={filialId}
                onChange={(e) => setFilialId(e.target.value)}
              >
                <option value="">Matriz / Todas as unidades</option>
                {filiaisDisponiveis.map((f) => (
                  <option key={f.id} value={f.id}>
                    {f.codigo ? `[${f.codigo}] ` : ''}{f.nome} {f.cidade ? `(${f.cidade}/${f.uf})` : ''}
                  </option>
                ))}
              </select>
            </div>
          )}

          <div>
            <label className="label">Responsável pela Solicitação</label>
            <input
              type="text"
              className="input"
              placeholder="Nome do solicitante"
              value={responsavelNome}
              onChange={(e) => setResponsavelNome(e.target.value)}
            />
          </div>

          <div>
            <label className="label">Data Limite para Resposta dos Fornecedores *</label>
            <input
              type="date"
              className="input"
              required
              value={dataLimiteResposta}
              onChange={(e) => setDataLimiteResposta(e.target.value)}
            />
          </div>

          <div>
            <label className="label">Data Desejada de Início</label>
            <input
              type="date"
              className="input"
              value={dataDesejadaInicio}
              onChange={(e) => setDataDesejadaInicio(e.target.value)}
            />
          </div>

          <div>
            <label className="label">Prazo Estimado de Execução (dias corridos)</label>
            <input
              type="number"
              className="input"
              min="1"
              value={prazoEstimadoDias}
              onChange={(e) => setPrazoEstimadoDias(Number(e.target.value) || 1)}
            />
          </div>

          <div style={{ gridColumn: 'span 2' }}>
            <label className="label">Motivação da Necessidade / Justificativa</label>
            <textarea
              className="input"
              rows={2}
              placeholder="Descreva por que o serviço é necessário (ex.: conformidade legal, desgaste de peças, melhoria operacional)..."
              value={necessidadeMotivacao}
              onChange={(e) => setNecessidadeMotivacao(e.target.value)}
            />
          </div>

          <div style={{ gridColumn: 'span 2' }}>
            <label className="label">Descrição Detalhada do Escopo</label>
            <textarea
              className="input"
              rows={3}
              placeholder="Descreva detalhadamente as instruções, escopo completo de trabalho e o que deve ser atendido..."
              value={descricaoDetalhada}
              onChange={(e) => setDescricaoDetalhada(e.target.value)}
            />
          </div>

          <div>
            <label className="label">Objetivo Principal</label>
            <input
              type="text"
              className="input"
              placeholder="Ex.: Restabelecer 100% da refrigeração da sala de servidores"
              value={objetivoEsperado}
              onChange={(e) => setObjetivoEsperado(e.target.value)}
            />
          </div>

          <div>
            <label className="label">Resultado / Entregável Esperado</label>
            <input
              type="text"
              className="input"
              placeholder="Ex.: Laudo técnico emitido, sistema funcionando e garantia de 12 meses"
              value={resultadoEsperado}
              onChange={(e) => setResultadoEsperado(e.target.value)}
            />
          </div>
        </div>
      </div>

      {/* 2. LOCAL DE EXECUÇÃO */}
      <div className="card" style={{ padding: '24px' }}>
        <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <MapPin size={18} color="var(--blue-primary)" />
          2. Local e Condições de Atendimento
        </h3>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px' }}>
          <div style={{ gridColumn: 'span 3', display: 'flex', alignItems: 'center', gap: '10px' }}>
            <input
              type="checkbox"
              id="presencial"
              checked={atendimentoPresencial}
              onChange={(e) => setAtendimentoPresencial(e.target.checked)}
              style={{ width: '18px', height: '18px' }}
            />
            <label htmlFor="presencial" style={{ fontWeight: 600, fontSize: '14px', cursor: 'pointer' }}>
              Exige atendimento presencial no endereço do cliente
            </label>
          </div>

          {atendimentoPresencial && (
            <>
              <div>
                <label className="label">CEP</label>
                <input
                  type="text"
                  className="input"
                  placeholder="00000-000"
                  value={cep}
                  onChange={(e) => setCep(e.target.value)}
                />
              </div>

              <div style={{ gridColumn: 'span 2' }}>
                <label className="label">Endereço (Logradouro)</label>
                <input
                  type="text"
                  className="input"
                  placeholder="Rua, Avenida..."
                  value={endereco}
                  onChange={(e) => setEndereco(e.target.value)}
                />
              </div>

              <div>
                <label className="label">Número</label>
                <input
                  type="text"
                  className="input"
                  value={numeroLocal}
                  onChange={(e) => setNumeroLocal(e.target.value)}
                />
              </div>

              <div>
                <label className="label">Complemento / Galpão / Sala</label>
                <input
                  type="text"
                  className="input"
                  value={complemento}
                  onChange={(e) => setComplemento(e.target.value)}
                />
              </div>

              <div>
                <label className="label">Bairro</label>
                <input
                  type="text"
                  className="input"
                  value={bairro}
                  onChange={(e) => setBairro(e.target.value)}
                />
              </div>

              <div>
                <label className="label">Cidade</label>
                <input
                  type="text"
                  className="input"
                  value={cidade}
                  onChange={(e) => setCidade(e.target.value)}
                />
              </div>

              <div>
                <label className="label">UF</label>
                <input
                  type="text"
                  className="input"
                  maxLength={2}
                  value={uf}
                  onChange={(e) => setUf(e.target.value.toUpperCase())}
                  placeholder="SC"
                />
              </div>

              <div style={{ gridColumn: 'span 2' }}>
                <label className="label">Ponto de Referência</label>
                <input
                  type="text"
                  className="input"
                  placeholder="Próximo à portaria 2, bloco administrativo..."
                  value={pontoReferencia}
                  onChange={(e) => setPontoReferencia(e.target.value)}
                />
              </div>

              <div style={{ gridColumn: 'span 3' }}>
                <label className="label">Janela de Horário Permitida para Execução</label>
                <input
                  type="text"
                  className="input"
                  value={horarioPermitido}
                  onChange={(e) => setHorarioPermitido(e.target.value)}
                />
              </div>
            </>
          )}
        </div>
      </div>

      {/* 3. ATIVIDADES DO ESCOPO */}
      <div className="card" style={{ padding: '24px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <div>
            <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 4px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <FileText size={18} color="var(--blue-primary)" />
              3. Atividades, Entregas Técnicas e Estimativas
            </h3>
            <p style={{ margin: 0, fontSize: '13px', color: 'var(--text-muted)' }}>
              Detalhamento das fases do serviço para que os fornecedores possam cotar item a item.
            </p>
          </div>
          <button
            type="button"
            onClick={adicionarAtividade}
            className="btn btn-secondary"
            style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
          >
            <Plus size={15} />
            <span>Adicionar Atividade</span>
          </button>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
          {atividades.map((ativ, idx) => (
            <div
              key={idx}
              style={{
                border: '1px solid var(--border)',
                borderRadius: '8px',
                padding: '16px',
                backgroundColor: '#fbfcfe'
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
                <span style={{ fontWeight: 700, fontSize: '14px', color: 'var(--blue-primary)' }}>
                  Atividade #{ativ.ordem}
                </span>
                {atividades.length > 1 && (
                  <button
                    type="button"
                    onClick={() => removerAtividade(idx)}
                    className="btn btn-secondary"
                    style={{ padding: '4px 8px', color: '#dc2626' }}
                    title="Remover atividade"
                  >
                    <Trash2 size={14} />
                  </button>
                )}
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '12px' }}>
                <div>
                  <label className="label">Etapa / Fase</label>
                  <input
                    type="text"
                    className="input"
                    placeholder="Ex: Fase 1 ou Diagnóstico"
                    value={ativ.etapa}
                    onChange={(e) => atualizarAtividade(idx, 'etapa', e.target.value)}
                  />
                </div>

                <div style={{ gridColumn: 'span 2' }}>
                  <label className="label">Nome da Atividade / Serviço *</label>
                  <input
                    type="text"
                    className="input"
                    required
                    placeholder="Ex.: Limpeza química das serpentinas dos evaporadores"
                    value={ativ.nome}
                    onChange={(e) => atualizarAtividade(idx, 'nome', e.target.value)}
                  />
                </div>

                <div>
                  <label className="label">Quantidade</label>
                  <input
                    type="number"
                    className="input"
                    min="1"
                    value={ativ.quantidade}
                    onChange={(e) => atualizarAtividade(idx, 'quantidade', Number(e.target.value) || 1)}
                  />
                </div>

                <div>
                  <label className="label">Unidade de Medição</label>
                  <select
                    className="input"
                    value={ativ.unidadeMedicao}
                    onChange={(e) => atualizarAtividade(idx, 'unidadeMedicao', e.target.value)}
                  >
                    <option value="SV">SV (Serviço)</option>
                    <option value="HR">HR (Hora técnica)</option>
                    <option value="UN">UN (Unidade)</option>
                    <option value="M2">M² (Metro quadrado)</option>
                    <option value="M">M (Metro linear)</option>
                    <option value="GL">GL (Global)</option>
                    <option value="DIA">DIA (Diária)</option>
                  </select>
                </div>

                <div>
                  <label className="label">Estimativa Unitária (R$)</label>
                  <input
                    type="number"
                    className="input"
                    step="0.01"
                    min="0"
                    value={ativ.valorEstimadoUnitario}
                    onChange={(e) => atualizarAtividade(idx, 'valorEstimadoUnitario', Number(e.target.value) || 0)}
                  />
                </div>

                <div style={{ gridColumn: 'span 3' }}>
                  <label className="label">Descrição Detalhada do Método e Procedimentos</label>
                  <textarea
                    className="input"
                    rows={2}
                    placeholder="Procedimentos técnicos necessários, normas de segurança e especificações..."
                    value={ativ.descricao}
                    onChange={(e) => atualizarAtividade(idx, 'descricao', e.target.value)}
                  />
                </div>

                <div>
                  <label className="label">Local Específico</label>
                  <input
                    type="text"
                    className="input"
                    placeholder="Ex.: Sala de máquinas 2"
                    value={ativ.localExecucao}
                    onChange={(e) => atualizarAtividade(idx, 'localExecucao', e.target.value)}
                  />
                </div>

                <div>
                  <label className="label">Exigências Técnicas / Certificações</label>
                  <input
                    type="text"
                    className="input"
                    placeholder="Ex.: NR-10, NR-35"
                    value={ativ.requisitosTecnicos}
                    onChange={(e) => atualizarAtividade(idx, 'requisitosTecnicos', e.target.value)}
                  />
                </div>

                <div>
                  <label className="label">Resultado Esperado desta Etapa</label>
                  <input
                    type="text"
                    className="input"
                    placeholder="Ex.: Laudo de limpeza assinado"
                    value={ativ.resultadoEsperado}
                    onChange={(e) => atualizarAtividade(idx, 'resultadoEsperado', e.target.value)}
                  />
                </div>
              </div>

              <div style={{ textAlign: 'right', marginTop: '10px', fontSize: '13px', fontWeight: 600, color: 'var(--text-main)' }}>
                Subtotal Estimado:{' '}
                <span style={{ color: 'var(--blue-primary)', fontWeight: 700 }}>
                  R$ {((Number(ativ.quantidade) || 0) * (Number(ativ.valorEstimadoUnitario) || 0)).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                </span>
              </div>
            </div>
          ))}
        </div>

        <div style={{ marginTop: '16px', padding: '14px 20px', background: '#f1f5f9', borderRadius: '8px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontWeight: 700, fontSize: '14px' }}>
            Valor Total Estimado do Escopo ({atividades.length} atividade{atividades.length > 1 ? 's' : ''}):
          </span>
          <span style={{ fontSize: '18px', fontWeight: 800, color: 'var(--blue-primary)' }}>
            R$ {totalEstimado.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
          </span>
        </div>
      </div>

      {/* 4. REQUISITOS, MATERIAIS & GARANTIA */}
      <div className="card" style={{ padding: '24px' }}>
        <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <ShieldCheck size={18} color="var(--blue-primary)" />
          4. Materiais, Equipamentos e Condições de Garantia
        </h3>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '16px' }}>
          <div>
            <label className="label">Materiais que Devem Estar Inclusos no Preço</label>
            <textarea
              className="input"
              rows={2}
              placeholder="Ex.: Gás refrigerante R410A, filtros novos, cabos, conectores..."
              value={materiaisInclusosExigidos}
              onChange={(e) => setMateriaisInclusosExigidos(e.target.value)}
            />
          </div>

          <div>
            <label className="label">Equipamentos e Ferramental Exigidos</label>
            <textarea
              className="input"
              rows={2}
              placeholder="Ex.: Manômetro digital calibrado, bomba de vácuo, EPIs certificados..."
              value={equipamentosExigidos}
              onChange={(e) => setEquipamentosExigidos(e.target.value)}
            />
          </div>

          <div>
            <label className="label">Garantia Mínima Exigida (meses)</label>
            <input
              type="number"
              className="input"
              min="0"
              value={garantiaEsperadaMeses}
              onChange={(e) => setGarantiaEsperadaMeses(Number(e.target.value) || 0)}
            />
          </div>

          <div>
            <label className="label">Certificações e Habilitações Exigidas</label>
            <input
              type="text"
              className="input"
              placeholder="Ex.: Registro no CREA, certificados NR-35 e NR-10 válidos"
              value={certificacoesExigidas}
              onChange={(e) => setCertificacoesExigidas(e.target.value)}
            />
          </div>

          <div style={{ gridColumn: 'span 2' }}>
            <label className="label">Condições Especiais e Cláusulas Contratuais</label>
            <textarea
              className="input"
              rows={2}
              placeholder="Ex.: Pagamento mediante medição da fase concluída; retenção de impostos na fonte..."
              value={condicoesEspeciais}
              onChange={(e) => setCondicoesEspeciais(e.target.value)}
            />
          </div>
        </div>
      </div>

      {/* 5. CRITÉRIOS DE COMPARAÇÃO E PONDERIZAÇÃO */}
      <div className="card" style={{ padding: '24px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
          <h3 style={{ fontSize: '16px', fontWeight: 700, margin: 0, display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Sliders size={18} color="var(--blue-primary)" />
            5. Critérios de Avaliação Ponderada (Total = 100%)
          </h3>
          <span
            style={{
              fontWeight: 800,
              fontSize: '13px',
              color: somaPesos === 100 ? '#16a34a' : '#dc2626',
              padding: '4px 10px',
              borderRadius: '6px',
              backgroundColor: somaPesos === 100 ? '#f0fdf4' : '#fef2f2'
            }}
          >
            Soma atual: {somaPesos}%
          </span>
        </div>
        <p style={{ margin: '0 0 16px 0', fontSize: '13px', color: 'var(--text-muted)' }}>
          Pesos percentuais utilizados para gerar o ranking comparativo automático entre as propostas recebidas.
        </p>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '16px' }}>
          <div>
            <label className="label">Peso Preço (%)</label>
            <input
              type="number"
              className="input"
              min="0"
              max="100"
              value={pesoPreco}
              onChange={(e) => setPesoPreco(Number(e.target.value) || 0)}
            />
            <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '4px' }}>
              Impacto do valor financeiro total.
            </div>
          </div>

          <div>
            <label className="label">Peso Prazo (%)</label>
            <input
              type="number"
              className="input"
              min="0"
              max="100"
              value={pesoPrazo}
              onChange={(e) => setPesoPrazo(Number(e.target.value) || 0)}
            />
            <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '4px' }}>
              Prazo de execução e data de início.
            </div>
          </div>

          <div>
            <label className="label">Peso Capacidade Técnica (%)</label>
            <input
              type="number"
              className="input"
              min="0"
              max="100"
              value={pesoTecnico}
              onChange={(e) => setPesoTecnico(Number(e.target.value) || 0)}
            />
            <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '4px' }}>
              Atestados, experiência e equipe.
            </div>
          </div>

          <div>
            <label className="label">Peso Condições Comerciais (%)</label>
            <input
              type="number"
              className="input"
              min="0"
              max="100"
              value={pesoCondicoes}
              onChange={(e) => setPesoCondicoes(Number(e.target.value) || 0)}
            />
            <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '4px' }}>
              Parcelamento, flexibilidade e garantia oferecida.
            </div>
          </div>
        </div>
      </div>

      {/* 6. FORNECEDORES CONVIDADOS */}
      <div className="card" style={{ padding: '24px' }}>
        <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 8px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Users size={18} color="var(--blue-primary)" />
          6. Seleção de Fornecedores Concorrentes
        </h3>
        <p style={{ margin: '0 0 16px 0', fontSize: '13px', color: 'var(--text-muted)' }}>
          Selecione os fornecedores cadastrados que receberão o convite com link seguro para visualizar o escopo e enviar suas propostas.
        </p>

        {!Array.isArray(fornecedoresDisponiveis) || fornecedoresDisponiveis.length === 0 ? (
          <div style={{ padding: '20px', background: '#f8fafc', borderRadius: '8px', textAlign: 'center', color: 'var(--text-muted)' }}>
            Nenhum fornecedor cadastrado no momento. Você poderá adicionar convites a qualquer momento após salvar a cotação.
          </div>
        ) : (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '10px', maxHeight: '240px', overflowY: 'auto' }}>
            {fornecedoresDisponiveis.map((f) => {
              const selecionado = fornecedoresSelecionados.includes(f.id);
              return (
                <div
                  key={f.id}
                  onClick={() => toggleFornecedor(f.id)}
                  style={{
                    padding: '10px 14px',
                    border: `1.5px solid ${selecionado ? 'var(--blue-primary)' : 'var(--border)'}`,
                    borderRadius: '8px',
                    backgroundColor: selecionado ? '#eff6ff' : '#fff',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '12px',
                    transition: 'all 0.15s ease'
                  }}
                >
                  <input
                    type="checkbox"
                    checked={selecionado}
                    onChange={() => {}}
                    style={{ width: '16px', height: '16px' }}
                  />
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontWeight: 700, fontSize: '13px', color: 'var(--text-main)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {f.razaoSocial || f.nomeFantasia || 'Fornecedor'}
                    </div>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                      CNPJ: {f.cnpj || '-'} {f.cidade ? `• ${f.cidade}/${f.uf}` : ''}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* FOOTER ACTIONS */}
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
        <button
          type="button"
          onClick={() => navigate('/servicos')}
          className="btn btn-secondary"
        >
          Cancelar
        </button>
        <button
          type="submit"
          className="btn btn-primary"
          disabled={saving}
          style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '10px 24px', fontSize: '14px' }}
        >
          <Save size={16} />
          {saving ? 'Criando Cotação...' : 'Criar Cotação de Serviço'}
        </button>
      </div>
    </form>
  );
};
