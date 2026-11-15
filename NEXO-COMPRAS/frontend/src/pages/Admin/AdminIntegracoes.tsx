import React, { useState } from 'react';
import { api } from '../../services/api';
import { Server, Database, CheckCircle2, AlertCircle, RefreshCw, ShieldCheck } from 'lucide-react';

export const AdminIntegracoes: React.FC = () => {
  const [tipoBanco, setTipoBanco] = useState<'Firebird' | 'PostgreSQL'>('Firebird');
  const [host, setHost] = useState('127.0.0.1');
  const [porta, setPorta] = useState(3050);
  const [caminho, setCaminho] = useState('C:\\DATACASH\\DADOS\\BM.FDB');
  const [testando, setTestando] = useState(false);
  const [resultado, setResultado] = useState<{ sucesso: boolean; mensagem: string } | null>(null);

  const handleTipoBancoChange = (tipo: 'Firebird' | 'PostgreSQL') => {
    setTipoBanco(tipo);
    setResultado(null);
    if (tipo === 'Firebird') {
      setPorta(3050);
      setCaminho('C:\\DATACASH\\DADOS\\BM.FDB');
    } else {
      setPorta(5432);
      setCaminho('nexo_compras');
    }
  };

  const testarConexao = async () => {
    setTestando(true);
    setResultado(null);
    try {
      const res = await api.post<{ sucesso: boolean; mensagem: string }>('/admin/testar-conexao-erp', {
        tipoBanco,
        host,
        porta: Number(porta),
      });
      setResultado(res);
    } catch (err: any) {
      setResultado({
        sucesso: false,
        mensagem: err.message || 'Falha ao conectar com o host especificado.',
      });
    } finally {
      setTestando(false);
    }
  };

  return (
    <div>
      <div style={{ marginBottom: '24px' }}>
        <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
          Bancos de Dados & Integrações ERP
        </h1>
        <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
          Diagnóstico e teste de conectividade remota com os bancos de dados dos clientes (Firebird e PostgreSQL).
        </p>
      </div>

      <div className="grid-cols-2" style={{ gap: '24px' }}>
        {/* FORMULÁRIO DE TESTE DE CONECTIVIDADE */}
        <div className="card">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '18px' }}>
            <Server size={22} color="var(--blue-primary)" />
            <div>
              <h2 style={{ fontSize: '17px', fontWeight: 700 }}>Testador de Conectividade TCP</h2>
              <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                Valida a comunicação de rede na porta do banco antes da ativação do sincronizador.
              </p>
            </div>
          </div>

          <div className="form-group">
            <label>Tipo de Banco de Dados</label>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                type="button"
                onClick={() => handleTipoBancoChange('Firebird')}
                className={`btn ${tipoBanco === 'Firebird' ? 'btn-primary' : 'btn-secondary'}`}
                style={{ flex: 1, padding: '8px' }}
              >
                Firebird (3050)
              </button>
              <button
                type="button"
                onClick={() => handleTipoBancoChange('PostgreSQL')}
                className={`btn ${tipoBanco === 'PostgreSQL' ? 'btn-primary' : 'btn-secondary'}`}
                style={{ flex: 1, padding: '8px' }}
              >
                PostgreSQL (5432)
              </button>
            </div>
          </div>

          <div className="grid-cols-2">
            <div className="form-group">
              <label>Host / IP do Servidor</label>
              <input
                placeholder="127.0.0.1 ou IP da VPN"
                value={host}
                onChange={(e) => setHost(e.target.value)}
              />
            </div>

            <div className="form-group">
              <label>Porta TCP</label>
              <input
                type="number"
                value={porta}
                onChange={(e) => setPorta(Number(e.target.value))}
              />
            </div>
          </div>

          <div className="form-group">
            <label>{tipoBanco === 'Firebird' ? 'Caminho do Arquivo (.FDB)' : 'Nome do Banco de Dados'}</label>
            <input
              value={caminho}
              onChange={(e) => setCaminho(e.target.value)}
            />
          </div>

          <button
            type="button"
            onClick={testarConexao}
            disabled={testando}
            className="btn btn-primary"
            style={{ width: '100%', padding: '10px', marginTop: '8px' }}
          >
            {testando ? (
              <>
                <RefreshCw size={15} className="animate-spin" /> Testando Porta {porta}...
              </>
            ) : (
              <>
                <Server size={15} /> Executar Teste de Socket
              </>
            )}
          </button>

          {resultado && (
            <div style={{
              marginTop: '16px',
              padding: '12px 14px',
              borderRadius: 'var(--radius-sm)',
              fontSize: '13px',
              backgroundColor: resultado.sucesso ? 'var(--green-light)' : 'var(--red-light)',
              color: resultado.sucesso ? 'var(--green-text)' : 'var(--red-text)',
              display: 'flex',
              alignItems: 'center',
              gap: '10px'
            }}>
              {resultado.sucesso ? <CheckCircle2 size={18} /> : <AlertCircle size={18} />}
              <span>{resultado.mensagem}</span>
            </div>
          )}
        </div>

        {/* INFORMAÇÕES TÉCNICAS E ARQUITETURA */}
        <div className="card">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '18px' }}>
            <ShieldCheck size={22} color="var(--green)" />
            <div>
              <h2 style={{ fontSize: '17px', fontWeight: 700 }}>Segurança e Isolamento Multiempresa</h2>
              <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                Diretrizes de comunicação e proteção dos dados dos clientes.
              </p>
            </div>
          </div>

          <div style={{ fontSize: '13px', lineHeight: 1.7, color: 'var(--text-muted)' }}>
            <p style={{ marginBottom: '12px' }}>
              🔒 <strong>Criptografia Local DPAPI:</strong> O aplicativo Nexo Compras Agent (SINc) instalado no computador do cliente criptografa as senhas locais do banco de dados utilizando a API de Proteção de Dados do Windows (DPAPI), impedindo vazamento de senhas.
            </p>
            <p style={{ marginBottom: '12px' }}>
              📡 <strong>Isolamento por Tenant:</strong> Cada pacote sincronizado pelo SINc via API exige autenticação por par de credenciais exclusivo da empresa e da instalação (Token de Filial + Token do Agente).
            </p>
            <p style={{ marginBottom: '12px' }}>
              🛡️ <strong>Row-Level Security (RLS):</strong> Todas as tabelas no PostgreSQL da nuvem possuem políticas ativas de RLS, garantindo que mesmo que dois clientes utilizem bancos idênticos, seus produtos, cotações e estoques fiquem rigorosamente isolados.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

