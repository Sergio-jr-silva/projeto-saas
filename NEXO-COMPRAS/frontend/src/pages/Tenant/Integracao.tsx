import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
import { AgenteStatus } from '../../types';
import { Server, Key, ShieldCheck, Download, Check, Copy, AlertCircle } from 'lucide-react';

export const Integracao: React.FC = () => {
  const { tenant } = useAuth();
  const [agentes, setAgentes] = useState<AgenteStatus[]>([]);
  const [lojaInfo, setLojaInfo] = useState<{ id: string; razaoSocial: string; nomeFantasia: string; cnpj: string } | null>(null);
  const [loading, setLoading] = useState(true);
  const [chaveGerada, setChaveGerada] = useState<{ chave: string; instrucoes: string } | null>(null);
  const [copiado, setCopiado] = useState(false);
  const [copiadoIdLoja, setCopiadoIdLoja] = useState(false);

  const loadAgentes = async () => {
    try {
      const res = await api.get<AgenteStatus[]>('/integration/agents');
      setAgentes(res);
    } catch (err: any) {
      alert('Erro ao carregar agentes: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  const loadLojaInfo = async () => {
    try {
      const res = await api.get<{ id: string; razaoSocial: string; nomeFantasia: string; cnpj: string }>('/integration/loja-info');
      setLojaInfo(res);
    } catch (err: any) {
      console.warn('Aviso: endpoint loja-info', err);
    }
  };

  useEffect(() => {
    loadAgentes();
    loadLojaInfo();
  }, []);

  const idDaLoja = lojaInfo?.id || tenant?.id || '';
  const nomeDaLoja = lojaInfo?.nomeFantasia || lojaInfo?.razaoSocial || tenant?.nomeFantasia || tenant?.razaoSocial || 'Loja Local';

  const handleCopyIdLoja = () => {
    if (idDaLoja) {
      navigator.clipboard.writeText(idDaLoja);
      setCopiadoIdLoja(true);
      setTimeout(() => setCopiadoIdLoja(false), 2500);
    }
  };

  const handleGenerateKey = async () => {
    try {
      const res = await api.post<{ chaveAtivacao?: string; chave?: string; instrucoes?: string }>(
        '/integration/agents/generate-key',
        { nomeIdentificador: 'Servidor Matriz BM.FDB' }
      );
      const chaveFinal = res.chaveAtivacao || res.chave || idDaLoja;
      setChaveGerada({
        chave: chaveFinal,
        instrucoes: res.instrucoes || 'Copie o ID da Loja abaixo e informe no Sincronizador Local (no botão "🔑 Vincular Loja").',
      });
      loadAgentes();
      loadLojaInfo();
    } catch (err: any) {
      alert('Erro ao gerar chave: ' + err.message);
    }
  };

  const handleCopy = () => {
    if (chaveGerada) {
      navigator.clipboard.writeText(chaveGerada.chave);
      setCopiado(true);
      setTimeout(() => setCopiado(false), 2500);
    }
  };

  return (
    <div>
      <div style={{ marginBottom: '24px' }}>
        <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
          Central de Integração ERP (Firebird)
        </h1>
        <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
          Conecte o banco BM.FDB da sua loja à nuvem do Nexo Compras de forma segura e automatizada.
        </p>
      </div>

      {/* IDENTIFICADOR ÚNICO DA LOJA (TOKEN DE ATIVAÇÃO DO SINCRONIZADOR) */}
      <div className="card" style={{
        marginBottom: '24px',
        backgroundColor: '#F0FDF4',
        borderColor: '#86EFAC',
        borderWidth: '2px',
        padding: '20px'
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '16px' }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
              <span className="badge badge-green" style={{ fontSize: '12px', fontWeight: 700 }}>
                TOKEN DE ATIVAÇÃO DO SINCRONIZADOR
              </span>
              <span style={{ fontSize: '13px', fontWeight: 600, color: '#166534' }}>
                {nomeDaLoja}
              </span>
            </div>
            <div style={{ fontSize: '13.5px', color: '#14532D', marginBottom: '8px' }}>
              Utilize o <strong>ID DA LOJA</strong> abaixo no aplicativo sincronizador para conectar o estoque do seu ERP Firebird:
            </div>
            <div style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '12px',
              backgroundColor: '#FFFFFF',
              padding: '8px 14px',
              borderRadius: '8px',
              border: '1px solid #BBF7D0'
            }}>
              <span style={{ fontSize: '11px', fontWeight: 700, color: '#15803D' }}>ID DA LOJA:</span>
              <code style={{ fontSize: '16px', fontWeight: 800, fontFamily: 'monospace', color: '#166534', letterSpacing: '0.03em' }}>
                {idDaLoja || 'Carregando ID da Loja...'}
              </code>
            </div>
          </div>
          <div>
            <button
              onClick={handleCopyIdLoja}
              className="btn btn-primary"
              style={{
                backgroundColor: '#15803D',
                borderColor: '#15803D',
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                padding: '10px 18px',
                fontSize: '14px',
                fontWeight: 700
              }}
            >
              {copiadoIdLoja ? <Check size={18} /> : <Copy size={18} />}
              {copiadoIdLoja ? 'ID Copiado!' : 'Copiar ID da Loja'}
            </button>
          </div>
        </div>
      </div>

      {/* ARQUITETURA DE SEGURANÇA */}
      <div className="card" style={{ marginBottom: '24px', backgroundColor: 'var(--blue-soft)', borderColor: 'var(--blue-light)' }}>
        <div style={{ display: 'flex', gap: '16px', alignItems: 'flex-start' }}>
          <div style={{
            width: '42px',
            height: '42px',
            borderRadius: '10px',
            backgroundColor: 'var(--blue-primary)',
            color: '#fff',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0
          }}>
            <ShieldCheck size={24} />
          </div>
          <div>
            <h3 style={{ fontSize: '15px', fontWeight: 700, color: 'var(--blue-primary)', marginBottom: '4px' }}>
              Comunicação Segura Unidirecional (Zero Port Forwarding)
            </h3>
            <p style={{ fontSize: '13.5px', color: 'var(--text-main)', lineHeight: 1.6 }}>
              O <strong>Nexo Compras Agent</strong> é instalado localmente no servidor da sua loja (onde o arquivo BM.FDB está instalado).
              A comunicação é iniciada <strong>sempre de dentro para fora via HTTPS</strong> (porta 443).
              Você <strong>não</strong> precisa abrir a porta 3050 do Firebird nem expor seu IP ou banco de dados à internet.
            </p>
          </div>
        </div>
      </div>

      {/* AGENTES ATIVOS */}
      <div className="card" style={{ marginBottom: '24px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', flexWrap: 'wrap', gap: '12px' }}>
          <div>
            <h2 style={{ fontSize: '17px', fontWeight: 700 }}>Agentes Conectados</h2>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
              Serviços de sincronização registrados para a sua empresa.
            </p>
          </div>

          <button onClick={handleGenerateKey} className="btn btn-primary">
            <Key size={16} /> Gerar Nova Chave de Ativação
          </button>
        </div>

        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>Nome da Máquina / Servidor</th>
                <th>Versão</th>
                <th>Status</th>
                <th>Última Sincronização</th>
                <th>Data de Criação</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={5} style={{ textAlign: 'center', padding: '24px', color: 'var(--text-muted)' }}>
                    Verificando agentes...
                  </td>
                </tr>
              ) : agentes.length === 0 ? (
                <tr>
                  <td colSpan={5} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                    Nenhum agente instalado. Clique no botão acima para gerar a Chave de Ativação do servidor.
                  </td>
                </tr>
              ) : (
                agentes.map((a) => (
                  <tr key={a.id}>
                    <td>
                      <div style={{ fontWeight: 600 }}>{a.nomeMaquina}</div>
                      <div style={{ fontSize: '11px', color: 'var(--text-light)', fontFamily: 'monospace' }}>
                        ID: {a.id}
                      </div>
                    </td>
                    <td>v{a.versao}</td>
                    <td>
                      <span className={`badge ${a.status === 'Online' ? 'badge-green' : a.status === 'Pendente' ? 'badge-yellow' : 'badge-gray'}`}>
                        {a.status}
                      </span>
                    </td>
                    <td>
                      {a.ultimaSincronizacao ? new Date(a.ultimaSincronizacao).toLocaleString('pt-BR') : 'Nunca sincronizou'}
                    </td>
                    <td>{new Date(a.criadoEm || a.dataCriacao || Date.now()).toLocaleDateString('pt-BR')}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* PASSO A PASSO DA INSTALAÇÃO */}
      <div className="card">
        <h2 style={{ fontSize: '17px', fontWeight: 700, marginBottom: '14px' }}>
          Como Conectar o Sincronizador Local ao Estoque da Loja
        </h2>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px' }}>
          <div style={{ padding: '16px', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)' }}>
            <div style={{ fontWeight: 700, fontSize: '14px', color: 'var(--blue-primary)', marginBottom: '6px' }}>
              1. Copiar Token / ID da Filial
            </div>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
              Copie o <strong>ID DA LOJA</strong> no card verde acima ou acesse a aba <strong>Filiais</strong> para copiar o Token exclusivo de cada filial (ex: FLK-...).
            </p>
          </div>

          <div style={{ padding: '16px', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)' }}>
            <div style={{ fontWeight: 700, fontSize: '14px', color: 'var(--blue-primary)', marginBottom: '6px' }}>
              2. Vincular no Sincronizador (SINc)
            </div>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
              No Nexo Compras Agent, clique em <strong>🔑 Vincular Filial</strong> e cole o token. As credenciais seguras serão gravadas via Windows DPAPI.
            </p>
          </div>

          <div style={{ padding: '16px', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)' }}>
            <div style={{ fontWeight: 700, fontSize: '14px', color: 'var(--blue-primary)', marginBottom: '6px' }}>
              3. Sincronização Automática
            </div>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
              O agente realiza a sincronização do Firebird/Postgres silenciosamente em segundo plano, atualizando estoque e produtos sem abrir terminal.
            </p>
          </div>
        </div>
      </div>

      {/* MODAL DE CHAVE GERADA */}
      {chaveGerada && (
        <div style={{
          position: 'fixed',
          inset: 0,
          backgroundColor: 'rgba(30, 41, 59, 0.4)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 50,
          padding: '20px'
        }}>
          <div className="card" style={{ width: '100%', maxWidth: '560px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', color: 'var(--green-text)', marginBottom: '12px' }}>
              <Check size={22} />
              <h2 style={{ fontSize: '18px', fontWeight: 700, color: 'var(--text-main)' }}>
                Chave de Ativação do Agente Gerada
              </h2>
            </div>

            <p style={{ fontSize: '13.5px', color: 'var(--text-muted)', marginBottom: '16px' }}>
              {chaveGerada.instrucoes}
            </p>

            <div style={{
              display: 'flex',
              alignItems: 'center',
              backgroundColor: '#F1F5F9',
              padding: '12px',
              borderRadius: 'var(--radius-sm)',
              border: '1px solid var(--border)',
              marginBottom: '20px'
            }}>
              <code style={{ flex: 1, fontFamily: 'monospace', fontSize: '13px', wordBreak: 'break-all', fontWeight: 600 }}>
                {chaveGerada.chave}
              </code>
              <button onClick={handleCopy} className="btn btn-secondary" style={{ marginLeft: '10px', padding: '6px 12px' }}>
                {copiado ? <Check size={14} color="var(--green)" /> : <Copy size={14} />}
                {copiado ? 'Copiado!' : 'Copiar'}
              </button>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
              <button onClick={() => setChaveGerada(null)} className="btn btn-primary">
                Entendido e Concluído
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

