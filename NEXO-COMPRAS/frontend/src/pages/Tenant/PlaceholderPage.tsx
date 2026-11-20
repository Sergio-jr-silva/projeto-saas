import React from 'react';
import { Construction } from 'lucide-react';

export const PlaceholderPage: React.FC<{ title: string; description: string; phase: number }> = ({
  title,
  description,
  phase,
}) => {
  return (
    <div className="card" style={{ textAlign: 'center', padding: '60px 20px' }}>
      <div style={{
        width: '56px',
        height: '56px',
        borderRadius: '16px',
        backgroundColor: 'var(--blue-light)',
        color: 'var(--blue-primary)',
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        marginBottom: '16px'
      }}>
        <Construction size={28} />
      </div>

      <h1 style={{ fontSize: '22px', fontWeight: 800, color: 'var(--text-main)', marginBottom: '8px' }}>
        {title}
      </h1>

      <p style={{ fontSize: '14px', color: 'var(--text-muted)', maxWidth: '540px', margin: '0 auto 20px' }}>
        {description}
      </p>

      <span className="badge badge-blue" style={{ fontSize: '13px', padding: '6px 14px' }}>
        Módulo Planejado para a Fase {phase} do Nexo Compras
      </span>
    </div>
  );
};

