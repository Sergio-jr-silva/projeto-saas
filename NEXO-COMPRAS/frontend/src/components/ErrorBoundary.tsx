import React, { Component, ErrorInfo, ReactNode } from 'react';
import { AlertTriangle, RefreshCw, Home, ChevronDown, ChevronUp } from 'lucide-react';

interface Props {
  children: ReactNode;
  fallbackTitle?: string;
}

interface State {
  hasError: boolean;
  error: Error | null;
  errorInfo: ErrorInfo | null;
  showDetails: boolean;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
      errorInfo: null,
      showDetails: false,
    };
  }

  static getDerivedStateFromError(error: Error): Partial<State> {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    this.setState({ errorInfo });
    console.error('ErrorBoundary capturou uma exceção:', error, errorInfo);
  }

  handleReset = () => {
    this.setState({
      hasError: false,
      error: null,
      errorInfo: null,
      showDetails: false,
    });
  };

  handleGoHome = () => {
    window.location.href = '/dashboard';
  };

  toggleDetails = () => {
    this.setState((prev) => ({ showDetails: !prev.showDetails }));
  };

  render() {
    if (this.state.hasError) {
      return (
        <div
          style={{
            minHeight: '400px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            padding: '32px 16px',
          }}
        >
          <div
            className="card"
            style={{
              maxWidth: '680px',
              width: '100%',
              padding: '32px',
              border: '1px solid #fee2e2',
              backgroundColor: '#fff',
              boxShadow: '0 10px 25px -5px rgba(239, 68, 68, 0.1)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: '16px' }}>
              <div
                style={{
                  width: '48px',
                  height: '48px',
                  borderRadius: '12px',
                  backgroundColor: '#fef2f2',
                  color: '#dc2626',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  flexShrink: 0,
                }}
              >
                <AlertTriangle size={24} />
              </div>
              <div style={{ flex: 1 }}>
                <h2
                  style={{
                    fontSize: '18px',
                    fontWeight: 700,
                    color: '#991b1b',
                    margin: '0 0 6px 0',
                  }}
                >
                  {this.props.fallbackTitle || 'Ocorreu uma falha ao exibir esta tela'}
                </h2>
                <p
                  style={{
                    fontSize: '13.5px',
                    color: '#4b5563',
                    margin: '0 0 16px 0',
                    lineHeight: 1.5,
                  }}
                >
                  Um erro inesperado de renderização foi interceptado com segurança pelo Nexo Compras para evitar que a página ficasse em branco. Os dados salvos no servidor permanecem intactos.
                </p>

                {this.state.error && (
                  <div
                    style={{
                      backgroundColor: '#f8fafc',
                      border: '1px solid #e2e8f0',
                      borderRadius: '8px',
                      padding: '12px 14px',
                      marginBottom: '16px',
                    }}
                  >
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        cursor: 'pointer',
                        fontSize: '12.5px',
                        fontWeight: 600,
                        color: '#475569',
                      }}
                      onClick={this.toggleDetails}
                    >
                      <span>Erro: {this.state.error.message || this.state.error.toString()}</span>
                      {this.state.showDetails ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
                    </div>

                    {this.state.showDetails && (
                      <pre
                        style={{
                          margin: '10px 0 0 0',
                          fontSize: '11px',
                          whiteSpace: 'pre-wrap',
                          wordBreak: 'break-all',
                          maxHeight: '180px',
                          overflowY: 'auto',
                          background: '#0f172a',
                          color: '#f1f5f9',
                          padding: '10px',
                          borderRadius: '6px',
                        }}
                      >
                        {this.state.error.stack}
                        {this.state.errorInfo?.componentStack}
                      </pre>
                    )}
                  </div>
                )}

                <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap' }}>
                  <button
                    onClick={this.handleReset}
                    className="btn btn-primary"
                    style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px' }}
                  >
                    <RefreshCw size={14} />
                    <span>Tentar Novamente</span>
                  </button>

                  <button
                    onClick={this.handleGoHome}
                    className="btn btn-secondary"
                    style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px' }}
                  >
                    <Home size={14} />
                    <span>Voltar ao Início</span>
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
