/**
 * Utilitários oficiais de validação e formatação de CNPJ
 * Regra oficial da Secretaria da Receita Federal do Brasil
 */

/**
 * Remove qualquer caractere não numérico
 */
export function cleanCnpj(value: string | null | undefined): string {
  if (!value) return '';
  return value.replace(/\D/g, '').slice(0, 14);
}

/**
 * Aplica máscara 00.000.000/0000-00 dinamicamente conforme digitação
 */
export function maskCnpj(value: string | null | undefined): string {
  const digits = cleanCnpj(value);
  if (digits.length <= 2) return digits;
  if (digits.length <= 5) return `${digits.slice(0, 2)}.${digits.slice(2)}`;
  if (digits.length <= 8) return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5)}`;
  if (digits.length <= 12) return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5, 8)}/${digits.slice(8)}`;
  return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5, 8)}/${digits.slice(8, 12)}-${digits.slice(12, 14)}`;
}

/**
 * Valida os dígitos verificadores do CNPJ pelo algoritmo oficial módulo 11
 */
export function validarCnpj(cnpj: string | null | undefined): boolean {
  const digits = cleanCnpj(cnpj);

  // Deve ter exatamente 14 dígitos numéricos
  if (digits.length !== 14) return false;

  // Rejeita sequências com todos os dígitos iguais (ex: 00000000000000, 11111111111111)
  if (/^(\d)\1{13}$/.test(digits)) return false;

  // Cálculo do 1º Dígito Verificador
  const multiplicadores1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
  let soma = 0;
  for (let i = 0; i < 12; i++) {
    soma += parseInt(digits[i], 10) * multiplicadores1[i];
  }
  let resto = soma % 11;
  const dv1 = resto < 2 ? 0 : 11 - resto;
  if (parseInt(digits[12], 10) !== dv1) return false;

  // Cálculo do 2º Dígito Verificador
  const multiplicadores2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
  soma = 0;
  for (let i = 0; i < 13; i++) {
    soma += parseInt(digits[i], 10) * multiplicadores2[i];
  }
  resto = soma % 11;
  const dv2 = resto < 2 ? 0 : 11 - resto;
  return parseInt(digits[13], 10) === dv2;
}

/**
 * Retorna os 8 primeiros dígitos (CNPJ-Base)
 */
export function getCnpjBase(cnpj: string | null | undefined): string {
  const digits = cleanCnpj(cnpj);
  return digits.length >= 8 ? digits.slice(0, 8) : digits;
}
