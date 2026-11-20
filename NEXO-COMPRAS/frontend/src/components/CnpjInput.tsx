import React, { useState } from 'react';
import { maskCnpj, cleanCnpj, validarCnpj } from '../utils/cnpj';

interface CnpjInputProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'onChange'> {
  value: string;
  onChange: (value: string, rawDigits: string, isValid: boolean) => void;
  label?: string;
  required?: boolean;
  showError?: boolean;
  errorMessage?: string;
}

export const CnpjInput: React.FC<CnpjInputProps> = ({
  value,
  onChange,
  label,
  required = false,
  showError = true,
  errorMessage = 'CNPJ inválido. Verifique os números digitados.',
  className = '',
  style,
  ...props
}) => {
  const [touched, setTouched] = useState(false);

  const rawDigits = cleanCnpj(value);
  const isValid = rawDigits.length === 0 ? !required : validarCnpj(rawDigits);
  const hasError = touched && rawDigits.length > 0 && !isValid;

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const inputVal = e.target.value;
    const digits = cleanCnpj(inputVal);
    const masked = maskCnpj(digits);
    const valid = digits.length === 14 ? validarCnpj(digits) : false;
    onChange(masked, digits, valid);
  };

  const handleBlur = (e: React.FocusEvent<HTMLInputElement>) => {
    setTouched(true);
    if (props.onBlur) props.onBlur(e);
  };

  return (
    <div style={{ width: '100%', marginBottom: label ? '12px' : '0' }}>
      {label && (
        <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', marginBottom: '4px' }}>
          {label} {required && <span style={{ color: 'var(--red)' }}>*</span>}
        </label>
      )}
      <input
        type="text"
        inputMode="numeric"
        placeholder="00.000.000/0000-00"
        maxLength={18}
        value={maskCnpj(value)}
        onChange={handleChange}
        onBlur={handleBlur}
        className={className}
        style={{
          width: '100%',
          padding: '8px 12px',
          borderRadius: '6px',
          border: `1px solid ${hasError ? 'var(--red)' : 'var(--border)'}`,
          fontSize: '13.5px',
          fontFamily: 'monospace',
          backgroundColor: '#FFF',
          outline: 'none',
          boxShadow: hasError ? '0 0 0 2px var(--red-light)' : undefined,
          ...style
        }}
        {...props}
      />
      {showError && hasError && (
        <div style={{ fontSize: '11.5px', color: 'var(--red-text)', marginTop: '4px', fontWeight: 600 }}>
          {errorMessage}
        </div>
      )}
    </div>
  );
};
