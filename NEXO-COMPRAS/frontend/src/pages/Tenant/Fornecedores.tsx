import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { Fornecedor } from '../../types';
import { Search, Plus, Users, Phone, Mail, Building2 } from 'lucide-react';

export const Fornecedores: React.FC = () => {
  const [fornecedores, setFornecedores] = useState<Fornecedor[]>([]);
  const [busca, setBusca] = useState('');
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);

  // Form
  const [razaoSocial, setRazaoSocial] = useState('');
  const [nomeFantasia, setNomeFantasia] = useState('');
  const [cnpj, setCnpj] = useState('');
  const [email, setEmail] = useState('');
  const [telefone, setTelefone] = useState('');
  const [whatsapp, setWhatsapp] = useState('');
  const [contato, setContato] = useState('');
  const [cidade, setCidade] = useState('');
  const [uf, setUf] = useState('');
  const [prazoEntrega, setPrazoEntrega] = useState('3');

  const loadFornecedores = async () => {
    setLoading(true);
    try {
      const res = await api.get<any>(`/fornecedores?q=${encodeURIComponent(busca)}`);
      const rawList = Array.isArray(res) ? res : (res?.itens || []);
      const itens: Fornecedor[] = rawList.map((x: any) => x.fornecedor || x);
      setFornecedores(itens);
    } catch (err: any) {
      alert('Erro ao carregar fornecedores: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const timer = setTimeout(() => {
      loadFornecedores();
    }, 250);
    return () => clearTimeout(timer);
  }, [busca]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.post('/fornecedores', {
        razaoSocial,
        nomeFantasia,
        cnpj,
        email,
        telefone,
        whatsapp,
        contato,
        cidade,
        uf,
        prazoEntregaPadrao: parseInt(prazoEntrega, 10) || 3,
      });

      setShowModal(false);
      setRazaoSocial('');
      setNomeFantasia('');
      setCnpj('');
      setEmail('');
      setTelefone('');
      setWhatsapp('');
      setContato('');
      setCidade('');
      setUf('');
      loadFornecedores();
    } catch (err: any) {
      alert('Erro ao cadastrar fornecedor: ' + err.message);
    }
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px', flexWrap: 'wrap', gap: '12px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
            Fornecedores
          </h1>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
            Base de fornecedores habilitados para cotações e negociações ({fornecedores.length} cadastrados).
          </p>
        </div>

        <button onClick={() => setShowModal(true)} className="btn btn-primary">
          <Plus size={16} /> Novo Fornecedor
        </button>
      </div>

      <div className="card" style={{ marginBottom: '20px', padding: '14px 18px' }}>
        <div style={{ position: 'relative' }}>
          <Search size={16} style={{ position: 'absolute', left: '10px', top: '11px', color: 'var(--text-muted)' }} />
          <input
            placeholder="Buscar por razão social, nome fantasia ou CNPJ..."
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            style={{ paddingLeft: '34px' }}
          />
        </div>
      </div>

      <div className="card">
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>Fornecedor</th>
                <th>CNPJ</th>
                <th>Contato & Comunicação</th>
                <th>Localização</th>
                <th>Prazo de Entrega</th>
                <th>Origem</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                    Carregando fornecedores...
                  </td>
                </tr>
              ) : fornecedores.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                    Nenhum fornecedor cadastrado. Conecte o ERP local ou cadastre manualmente acima.
                  </td>
                </tr>
              ) : (
                fornecedores.map((f) => (
                  <tr key={f.id}>
                    <td>
                      <div style={{ fontWeight: 600, color: 'var(--text-main)' }}>{f.nomeFantasia || f.razaoSocial}</div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{f.razaoSocial}</div>
                      {f.contato && (
                        <div style={{ fontSize: '11px', color: 'var(--blue-primary)' }}>Responsável: {f.contato}</div>
                      )}
                    </td>
                    <td>{f.cnpj || '—'}</td>
                    <td>
                      {f.email && <div style={{ fontSize: '13px' }}>✉ {f.email}</div>}
                      {f.whatsapp && <div style={{ fontSize: '12px', color: 'var(--green-text)' }}>📱 WhatsApp: {f.whatsapp}</div>}
                      {f.telefone && !f.whatsapp && <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>📞 {f.telefone}</div>}
                    </td>
                    <td>
                      {f.cidade ? `${f.cidade}/${f.uf}` : '—'}
                    </td>
                    <td>
                      <span className="badge badge-blue">
                        {f.prazoEntregaPadrao} {f.prazoEntregaPadrao === 1 ? 'dia' : 'dias'}
                      </span>
                    </td>
                    <td>
                      <span className={`badge ${f.origem === 'erp' ? 'badge-yellow' : 'badge-green'}`}>
                        {f.origem === 'erp' ? 'ERP Firebird' : 'Manual'}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {showModal && (
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
          <div className="card" style={{ width: '100%', maxWidth: '600px', maxHeight: '90vh', overflowY: 'auto' }}>
            <h2 style={{ fontSize: '18px', fontWeight: 700, marginBottom: '16px' }}>
              Novo Fornecedor
            </h2>

            <form onSubmit={handleCreate}>
              <div className="grid-cols-2">
                <div className="form-group">
                  <label>Razão Social *</label>
                  <input
                    required
                    placeholder="Ex.: Distribuidora de Alimentos Silva Ltda"
                    value={razaoSocial}
                    onChange={(e) => setRazaoSocial(e.target.value)}
                  />
                </div>

                <div className="form-group">
                  <label>Nome Fantasia</label>
                  <input
                    placeholder="Ex.: Silva Alimentos"
                    value={nomeFantasia}
                    onChange={(e) => setNomeFantasia(e.target.value)}
                  />
                </div>
              </div>

              <div className="grid-cols-2">
                <div className="form-group">
                  <label>CNPJ</label>
                  <input
                    placeholder="00.000.000/0001-00"
                    value={cnpj}
                    onChange={(e) => setCnpj(e.target.value)}
                  />
                </div>

                <div className="form-group">
                  <label>Pessoa de Contato</label>
                  <input
                    placeholder="Ex.: Carlos Vendedor"
                    value={contato}
                    onChange={(e) => setContato(e.target.value)}
                  />
                </div>
              </div>

              <div className="grid-cols-3">
                <div className="form-group">
                  <label>E-mail</label>
                  <input
                    type="email"
                    placeholder="vendas@fornecedor.com.br"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                  />
                </div>

                <div className="form-group">
                  <label>WhatsApp</label>
                  <input
                    placeholder="(81) 99999-9999"
                    value={whatsapp}
                    onChange={(e) => setWhatsapp(e.target.value)}
                  />
                </div>

                <div className="form-group">
                  <label>Prazo de Entrega (dias)</label>
                  <input
                    type="number"
                    min="1"
                    value={prazoEntrega}
                    onChange={(e) => setPrazoEntrega(e.target.value)}
                  />
                </div>
              </div>

              <div className="grid-cols-2">
                <div className="form-group">
                  <label>Cidade</label>
                  <input
                    placeholder="Recife"
                    value={cidade}
                    onChange={(e) => setCidade(e.target.value)}
                  />
                </div>

                <div className="form-group">
                  <label>Estado (UF)</label>
                  <input
                    placeholder="PE"
                    maxLength={2}
                    value={uf}
                    onChange={(e) => setUf(e.target.value.toUpperCase())}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                <button type="button" onClick={() => setShowModal(false)} className="btn btn-secondary">
                  Cancelar
                </button>
                <button type="submit" className="btn btn-primary">
                  Salvar Fornecedor
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

