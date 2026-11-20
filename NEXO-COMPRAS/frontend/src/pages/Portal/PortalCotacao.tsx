import React, { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import {
  PortalCotacao,
  PortalItem,
  RespostaItem,
  Anexo
} from '../../types';
import {
  Lock,
  ShieldCheck,
  Send,
  Save,
  CheckCircle2,
  AlertTriangle,
  Upload,
  FileText,
  Clock,
  Building2,
  Trash2
} from 'lucide-react';

export const PortalCotacaoPage: React.FC = () => {
  const { token } = useParams<{ token: string }>();

  // Estado de Autenticação com Código
  const [sessionToken, setSessionToken] = useState<string | null>(() => {
    return sessionStorage.getItem(`portal_token_${token}`);
  });
  const [codigoInput, setCodigoInput] = useState('');
  const [erroAuth, setErroAuth] = useState('');
  const [autenticando, setAutenticando] = useState(false);

  // Estado dos Dados da Cotação
  const [cotacao, setCotacao] = useState<PortalCotacao | null>(null);
  const [loading, setLoading] = useState(false);

  // Campos do formulário
  const [prazoEntrega, setPrazoEntrega] = useState(5);
  const [condicaoPagamento, setCondicaoPagamento] = useState('28 dias');
  const [validadeProposta, setValidadeProposta] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() + 15);
    return d.toISOString().split('T')[0];
  });
  const [frete, setFrete] = useState(0);
  const [outrasDespesas, setOutrasDespesas] = useState(0);
  const [contatoNome, setContatoNome] = useState('');
  const [observacoes, setObservacoes] = useState('');

  // Itens da Proposta
  const [itensProposta, setItensProposta] = useState<Record<string, RespostaItem>>({});

  // Anexos
  const [anexos, setAnexos] = useState<Anexo[]>([]);
  const [uploading, setUploading] = useState(false);

  // Status de Envio
  const [salvando, setSalvando] = useState(false);
  const [msgSucesso, setMsgSucesso] = useState('');
  const [errosValidacao, setErrosValidacao] = useState<string[]>([]);

  // Formatação do Código de 12 dígitos (XXXX-XXXX-XXXX)
  const formatarCodigo = (val: string) => {
    const raw = val.replace(/[^a-zA-Z0-9]/g, '').toUpperCase().slice(0, 12);
    const parts = [];
    for (let i = 0; i < raw.length; i += 4) {
      parts.push(raw.slice(i, i + 4));
    }
    return parts.join('-');
  };

  const handleCodigoChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setCodigoInput(formatarCodigo(e.target.value));
  };

  const autenticarPortal = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!token) return;
    setErroAuth('');
    setAutenticando(true);
    try {
      const res = await fetch(`/api/portal/cotacao/${token}/acessar`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ codigo: codigoInput.replace(/-/g, '') }),
      });

      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.erro || 'Código de acesso incorreto ou expirado.');
      }

      sessionStorage.setItem(`portal_token_${token}`, data.sessaoToken);
      setSessionToken(data.sessaoToken);
    } catch (err: any) {
      setErroAuth(err.message);
    } finally {
      setAutenticando(false);
    }
  };

  const carregarDadosCotacao = async () => {
    if (!sessionToken) return;
    setLoading(true);
    try {
      const res = await fetch('/api/portal/cotacao', {
        headers: { Authorization: `Bearer ${sessionToken}` },
      });

      if (res.status === 401) {
        sessionStorage.removeItem(`portal_token_${token}`);
        setSessionToken(null);
        return;
      }

      const data: PortalCotacao = await res.json();
      setCotacao(data);
      setAnexos(data.anexos || []);

      if (data.resposta) {
        setPrazoEntrega(data.resposta.prazoEntregaDias || 5);
        setCondicaoPagamento(data.resposta.condicaoPagamento || '');
        if (data.resposta.validadeProposta) setValidadeProposta(data.resposta.validadeProposta);
        setFrete(data.resposta.frete || 0);
        setOutrasDespesas(data.resposta.outrasDespesas || 0);
        setContatoNome(data.resposta.contatoNome || '');
        setObservacoes(data.resposta.observacoes || '');

        const dict: Record<string, RespostaItem> = {};
        data.resposta.itens.forEach((it) => {
          dict[it.cotacaoItemId] = it;
        });
        setItensProposta(dict);
      } else {
        // Inicializa itens com valores padrão
        const dict: Record<string, RespostaItem> = {};
        data.itens.forEach((it) => {
          dict[it.id] = {
            cotacaoItemId: it.id,
            disponivel: true,
            quantidadeOfertada: it.quantidade,
            precoUnitario: 0,
            descontoPerc: 0,
            cst: '000',
            cfop: '5102',
            icmsAliq: 12,
            ipiAliq: 0,
            mva: 0,
            icmsStAliq: 0,
            icmsStValor: 0,
            observacao: '',
          };
        });
        setItensProposta(dict);
      }
    } catch (err: any) {
      alert('Erro ao carregar dados da cotação: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (sessionToken) {
      carregarDadosCotacao();
    }
  }, [sessionToken]);

  const atualizarItem = (id: string, campo: keyof RespostaItem, valor: any) => {
    setItensProposta((prev) => ({
      ...prev,
      [id]: {
        ...prev[id],
        [campo]: valor,
      },
    }));
  };

  const handleSalvarProposta = async (final: boolean) => {
    if (!sessionToken || !cotacao) return;
    setSalvando(true);
    setMsgSucesso('');
    setErrosValidacao([]);

    try {
      const payload = {
        frete,
        outrasDespesas,
        prazoEntregaDias: prazoEntrega,
        condicaoPagamento,
        validadeProposta: validadeProposta || null,
        contatoNome,
        observacoes,
        itens: Object.values(itensProposta),
      };

      const url = final ? '/api/portal/cotacao/resposta/enviar' : '/api/portal/cotacao/resposta';
      const method = final ? 'POST' : 'PUT';

      const res = await fetch(url, {
        method,
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${sessionToken}`,
        },
        body: JSON.stringify(payload),
      });

      const data = await res.json();
      if (!res.ok) {
        if (data.erros) setErrosValidacao(data.erros);
        throw new Error(data.erro || 'Erro ao processar proposta.');
      }

      setMsgSucesso(data.mensagem || (final ? 'Proposta enviada com sucesso!' : 'Rascunho salvo com sucesso.'));
      carregarDadosCotacao();
    } catch (err: any) {
      alert(err.message);
    } finally {
      setSalvando(false);
    }
  };

  const handleUploadAnexo = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!sessionToken || !e.target.files || e.target.files.length === 0) return;
    const file = e.target.files[0];
    const formData = new FormData();
    formData.append('arquivo', file);

    setUploading(true);
    try {
      const res = await fetch('/api/portal/cotacao/anexos', {
        method: 'POST',
        headers: { Authorization: `Bearer ${sessionToken}` },
        body: formData,
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.erro || 'Falha no upload do anexo.');
      setAnexos((prev) => [...prev, data]);
    } catch (err: any) {
      alert(err.message);
    } finally {
      setUploading(false);
    }
  };

  // TELA 1: AUTENTICAÇÃO COM CÓDIGO DE ACESSO
  if (!sessionToken) {
    return (
      <div
        style={{
          minHeight: '100vh',
          backgroundColor: '#F8FAFC',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          padding: '20px',
        }}
      >
        <div className="card" style={{ maxWidth: '440px', width: '100%', padding: '36px 30px', textAlign: 'center' }}>
          <div
            style={{
              width: '56px',
              height: '56px',
              borderRadius: '14px',
              backgroundColor: 'var(--blue-light)',
              color: 'var(--primary)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              margin: '0 auto 18px',
            }}
          >
            <Lock size={28} />
          </div>

          <h2 style={{ fontSize: '22px', fontWeight: 800, color: 'var(--text-main)', marginBottom: '8px' }}>
            Portal de Cotações
          </h2>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)', marginBottom: '24px', lineHeight: 1.5 }}>
            Digite o código de acesso recebido junto ao link para visualizar a solicitação de compras e preencher sua proposta comercial.
          </p>

          <form onSubmit={autenticarPortal}>
            <div style={{ marginBottom: '20px' }}>
              <input
                placeholder="XXXX-XXXX-XXXX"
                value={codigoInput}
                onChange={handleCodigoChange}
                maxLength={14}
                style={{
                  fontSize: '18px',
                  fontWeight: 800,
                  textAlign: 'center',
                  letterSpacing: '3px',
                  fontFamily: 'monospace',
                  color: 'var(--primary)',
                  padding: '12px',
                }}
                autoFocus
              />
            </div>

            {erroAuth && (
              <div
                style={{
                  backgroundColor: '#fef2f2',
                  border: '1px solid var(--red)',
                  color: 'var(--red)',
                  padding: '10px 14px',
                  borderRadius: '8px',
                  fontSize: '13px',
                  marginBottom: '20px',
                  textAlign: 'left',
                }}
              >
                {erroAuth}
              </div>
            )}

            <button
              type="submit"
              disabled={autenticando || codigoInput.replace(/-/g, '').length !== 12}
              className="btn btn-primary"
              style={{ width: '100%', padding: '12px', fontSize: '15px', justifyContent: 'center' }}
            >
              <ShieldCheck size={18} />
              {autenticando ? 'Validando Acesso...' : 'Acessar Solicitação'}
            </button>
          </form>

          <div style={{ marginTop: '24px', fontSize: '12px', color: 'var(--text-muted)' }}>
            Nexo Compras • Plataforma Segura de Gestão de Compras
          </div>
        </div>
      </div>
    );
  }

  // TELA 2: FORMULÁRIO DE PROPOSTA
  if (loading || !cotacao) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        Carregando solicitação de cotação...
      </div>
    );
  }

  const propostaEnviada = cotacao.status === 'Respondida' || cotacao.resposta?.status === 'Enviada';

  return (
    <div style={{ minHeight: '100vh', backgroundColor: '#F8FAFC', padding: '30px 20px' }}>
      <div style={{ maxWidth: '1100px', margin: '0 auto' }}>
        {/* CABEÇALHO DO PORTAL */}
        <div className="card" style={{ padding: '24px 28px', marginBottom: '20px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '16px' }}>
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
                <Building2 size={20} color="var(--primary)" />
                <span style={{ fontSize: '14px', fontWeight: 700, color: 'var(--primary)' }}>
                  {cotacao.empresa}
                </span>
              </div>
              <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', marginBottom: '4px' }}>
                Solicitação de Cotação #{cotacao.numero}
              </h1>
              <p style={{ fontSize: '15px', color: 'var(--text-muted)' }}>{cotacao.titulo}</p>
            </div>

            <div style={{ textAlign: 'right' }}>
              <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Fornecedor Convidado:</div>
              <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text-main)' }}>
                {cotacao.fornecedorNome}
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '6px', marginTop: '6px', fontSize: '13px' }}>
                <Clock size={14} color="var(--primary)" />
                <span>Prazo Limite: <strong>{new Date(cotacao.dataLimiteResposta).toLocaleDateString('pt-BR')}</strong></span>
              </div>
            </div>
          </div>

          {cotacao.observacoes && (
            <div style={{ marginTop: '14px', padding: '10px 14px', backgroundColor: 'var(--bg-main)', borderRadius: '8px', fontSize: '13.5px' }}>
              <strong>Instruções do Comprador:</strong> {cotacao.observacoes}
            </div>
          )}

          {propostaEnviada && (
            <div
              style={{
                marginTop: '16px',
                padding: '12px 16px',
                backgroundColor: 'var(--green-light)',
                border: '1px solid var(--green)',
                borderRadius: '8px',
                color: 'var(--green)',
                display: 'flex',
                alignItems: 'center',
                gap: '10px',
                fontWeight: 600,
              }}
            >
              <CheckCircle2 size={20} />
              <span>Sua proposta comercial foi enviada com sucesso! Você ainda pode atualizar valores enquanto o prazo estiver aberto.</span>
            </div>
          )}
        </div>

        {/* FEEDBACK DE ERROS DE VALIDAÇÃO FISCAL */}
        {errosValidacao.length > 0 && (
          <div
            style={{
              backgroundColor: '#fef2f2',
              border: '1px solid var(--red)',
              borderRadius: '8px',
              padding: '16px 20px',
              marginBottom: '20px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 700, color: 'var(--red)', marginBottom: '8px' }}>
              <AlertTriangle size={18} /> Pendências Fiscais Identificadas:
            </div>
            <ul style={{ margin: 0, paddingLeft: '20px', fontSize: '13.5px', color: 'var(--text-main)' }}>
              {errosValidacao.map((e, idx) => (
                <li key={idx} style={{ marginBottom: '4px' }}>{e}</li>
              ))}
            </ul>
          </div>
        )}

        {/* TABELA DE PRODUTOS E PREENCHIMENTO */}
        <div className="card" style={{ padding: '24px', marginBottom: '20px' }}>
          <h2 style={{ fontSize: '18px', fontWeight: 800, marginBottom: '6px', color: 'var(--text-main)' }}>
            Produtos Solicitados ({cotacao.itens.length})
          </h2>
          <p style={{ fontSize: '13.5px', color: 'var(--text-muted)', marginBottom: '18px' }}>
            Preencha os valores unitários e os impostos aplicáveis para a sua empresa (ICMS, IPI e ST).
          </p>

          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th style={{ width: '220px' }}>Item</th>
                  <th style={{ textAlign: 'center', width: '90px' }}>Disponível</th>
                  <th style={{ textAlign: 'right', width: '110px' }}>Qtd Ofertada</th>
                  <th style={{ textAlign: 'right', width: '130px' }}>Preço Unit. (R$)</th>
                  <th style={{ textAlign: 'right', width: '100px' }}>Desc. (%)</th>
                  <th style={{ textAlign: 'center', width: '100px' }}>CST / CSOSN</th>
                  <th style={{ textAlign: 'center', width: '90px' }}>CFOP</th>
                  <th style={{ textAlign: 'right', width: '90px' }}>ICMS (%)</th>
                  <th style={{ textAlign: 'right', width: '90px' }}>IPI (%)</th>
                  <th style={{ textAlign: 'right', width: '110px' }}>ST (R$)</th>
                </tr>
              </thead>
              <tbody>
                {cotacao.itens.map((it) => {
                  const itemResp = itensProposta[it.id] || {
                    cotacaoItemId: it.id,
                    disponivel: true,
                    quantidadeOfertada: it.quantidade,
                    precoUnitario: 0,
                    descontoPerc: 0,
                    cst: '000',
                    cfop: '5102',
                    icmsAliq: 12,
                    ipiAliq: 0,
                    mva: 0,
                    icmsStAliq: 0,
                    icmsStValor: 0,
                    observacao: '',
                  };

                  return (
                    <tr key={it.id} style={{ opacity: itemResp.disponivel ? 1 : 0.6 }}>
                      <td>
                        <div style={{ fontWeight: 700, color: 'var(--text-main)' }}>{it.descricao}</div>
                        <div style={{ fontSize: '11.5px', color: 'var(--text-muted)' }}>
                          Código: {it.codigo} • Qtd Solicitada: {it.quantidade} {it.unidade}
                        </div>
                      </td>

                      <td style={{ textAlign: 'center' }}>
                        <input
                          type="checkbox"
                          checked={itemResp.disponivel}
                          onChange={(e) => atualizarItem(it.id, 'disponivel', e.target.checked)}
                          style={{ width: 'auto' }}
                        />
                      </td>

                      <td style={{ textAlign: 'right' }}>
                        <input
                          type="number"
                          step="any"
                          disabled={!itemResp.disponivel}
                          value={itemResp.quantidadeOfertada}
                          onChange={(e) => atualizarItem(it.id, 'quantidadeOfertada', parseFloat(e.target.value) || 0)}
                          style={{ textAlign: 'right', width: '90px' }}
                        />
                      </td>

                      <td style={{ textAlign: 'right' }}>
                        <input
                          type="number"
                          step="0.01"
                          disabled={!itemResp.disponivel}
                          value={itemResp.precoUnitario || ''}
                          placeholder="0,00"
                          onChange={(e) => atualizarItem(it.id, 'precoUnitario', parseFloat(e.target.value) || 0)}
                          style={{ textAlign: 'right', fontWeight: 700, width: '110px' }}
                        />
                      </td>

                      <td style={{ textAlign: 'right' }}>
                        <input
                          type="number"
                          step="0.1"
                          disabled={!itemResp.disponivel}
                          value={itemResp.descontoPerc || ''}
                          placeholder="0"
                          onChange={(e) => atualizarItem(it.id, 'descontoPerc', parseFloat(e.target.value) || 0)}
                          style={{ textAlign: 'right', width: '80px' }}
                        />
                      </td>

                      <td style={{ textAlign: 'center' }}>
                        <input
                          type="text"
                          disabled={!itemResp.disponivel}
                          value={itemResp.cst || ''}
                          placeholder="000"
                          onChange={(e) => atualizarItem(it.id, 'cst', e.target.value)}
                          style={{ textAlign: 'center', width: '85px', fontFamily: 'monospace' }}
                        />
                      </td>

                      <td style={{ textAlign: 'center' }}>
                        <input
                          type="text"
                          disabled={!itemResp.disponivel}
                          value={itemResp.cfop || ''}
                          placeholder="5102"
                          onChange={(e) => atualizarItem(it.id, 'cfop', e.target.value)}
                          style={{ textAlign: 'center', width: '80px', fontFamily: 'monospace' }}
                        />
                      </td>

                      <td style={{ textAlign: 'right' }}>
                        <input
                          type="number"
                          step="0.5"
                          disabled={!itemResp.disponivel}
                          value={itemResp.icmsAliq ?? ''}
                          onChange={(e) => atualizarItem(it.id, 'icmsAliq', parseFloat(e.target.value) || 0)}
                          style={{ textAlign: 'right', width: '75px' }}
                        />
                      </td>

                      <td style={{ textAlign: 'right' }}>
                        <input
                          type="number"
                          step="0.5"
                          disabled={!itemResp.disponivel}
                          value={itemResp.ipiAliq ?? ''}
                          onChange={(e) => atualizarItem(it.id, 'ipiAliq', parseFloat(e.target.value) || 0)}
                          style={{ textAlign: 'right', width: '75px' }}
                        />
                      </td>

                      <td style={{ textAlign: 'right' }}>
                        <input
                          type="number"
                          step="0.01"
                          disabled={!itemResp.disponivel}
                          value={itemResp.icmsStValor || ''}
                          placeholder="0,00"
                          onChange={(e) => atualizarItem(it.id, 'icmsStValor', parseFloat(e.target.value) || 0)}
                          style={{ textAlign: 'right', width: '95px' }}
                        />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>

        {/* CONDIÇÕES COMERCIAIS GERAIS */}
        <div className="card" style={{ padding: '24px', marginBottom: '24px' }}>
          <h2 style={{ fontSize: '18px', fontWeight: 800, marginBottom: '16px', color: 'var(--text-main)' }}>
            Condições Comerciais da Proposta
          </h2>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '16px' }}>
            <div>
              <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                Prazo de Entrega (dias úteis):
              </label>
              <input
                type="number"
                min="0"
                value={prazoEntrega}
                onChange={(e) => setPrazoEntrega(parseInt(e.target.value) || 0)}
              />
            </div>

            <div>
              <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                Condições de Pagamento:
              </label>
              <input
                placeholder="Ex: 28 dias, 14/28 dias, À vista"
                value={condicaoPagamento}
                onChange={(e) => setCondicaoPagamento(e.target.value)}
              />
            </div>

            <div>
              <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                Validade da Proposta:
              </label>
              <input
                type="date"
                value={validadeProposta}
                onChange={(e) => setValidadeProposta(e.target.value)}
              />
            </div>

            <div>
              <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                Valor do Frete (R$):
              </label>
              <input
                type="number"
                step="0.01"
                value={frete}
                onChange={(e) => setFrete(parseFloat(e.target.value) || 0)}
                placeholder="0,00 (0 para frete CIF/grátis)"
              />
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '16px' }}>
            <div>
              <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                Nome do Responsável / Vendedor:
              </label>
              <input
                placeholder="Seu nome completo"
                value={contatoNome}
                onChange={(e) => setContatoNome(e.target.value)}
              />
            </div>

            <div>
              <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                Outras Despesas Acessórias (R$):
              </label>
              <input
                type="number"
                step="0.01"
                value={outrasDespesas}
                onChange={(e) => setOutrasDespesas(parseFloat(e.target.value) || 0)}
                placeholder="0,00"
              />
            </div>
          </div>

          <div>
            <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
              Observações Adicionais para o Comprador:
            </label>
            <textarea
              rows={3}
              value={observacoes}
              onChange={(e) => setObservacoes(e.target.value)}
              placeholder="Ex: Preços válidos para pedido mínimo de R$ 1.500. Entrega por frota própria."
              style={{ width: '100%', resize: 'vertical' }}
            />
          </div>
        </div>

        {/* ANEXOS / PROPOSTAS EM PDF */}
        <div className="card" style={{ padding: '24px', marginBottom: '24px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
            <div>
              <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text-main)' }}>
                Anexos da Proposta (PDF, Espelho de Pedido, Imagens)
              </h2>
              <span style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                Você pode anexar sua proposta formal emitida pelo seu sistema.
              </span>
            </div>

            <label className="btn btn-secondary" style={{ cursor: 'pointer' }}>
              <Upload size={16} />
              {uploading ? 'Enviando...' : 'Adicionar Anexo'}
              <input
                type="file"
                accept=".pdf,.png,.jpg,.jpeg,.xml"
                onChange={handleUploadAnexo}
                style={{ display: 'none' }}
                disabled={uploading}
              />
            </label>
          </div>

          {anexos.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              {anexos.map((a) => (
                <div
                  key={a.id}
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    padding: '10px 14px',
                    backgroundColor: 'var(--bg-main)',
                    borderRadius: '8px',
                    border: '1px solid var(--border)',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <FileText size={16} color="var(--primary)" />
                    <span style={{ fontWeight: 600, fontSize: '13.5px' }}>{a.nomeArquivo}</span>
                    <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                      ({(a.tamanho / 1024).toFixed(0)} KB)
                    </span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* BARRA DE AÇÃO FIXA */}
        <div
          style={{
            display: 'flex',
            justifyContent: 'flex-end',
            gap: '12px',
            padding: '20px 0',
          }}
        >
          <button
            type="button"
            onClick={() => handleSalvarProposta(false)}
            disabled={salvando}
            className="btn btn-secondary"
            style={{ padding: '12px 20px', fontSize: '15px' }}
          >
            <Save size={18} /> Salvar Rascunho
          </button>

          <button
            type="button"
            onClick={() => handleSalvarProposta(true)}
            disabled={salvando}
            className="btn btn-primary"
            style={{ padding: '12px 24px', fontSize: '15px' }}
          >
            <Send size={18} />
            {salvando ? 'Transmitindo...' : 'Enviar Proposta Comercial'}
          </button>
        </div>
      </div>
    </div>
  );
};

