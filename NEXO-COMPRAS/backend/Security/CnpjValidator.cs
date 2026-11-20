using System.Linq;

namespace Nexo.Api.Security;

public static class CnpjValidator
{
    /// <summary>
    /// Limpa qualquer caractere não numérico e retorna até 14 dígitos.
    /// </summary>
    public static string Clean(string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj)) return string.Empty;
        return new string(cnpj.Where(char.IsDigit).Take(14).ToArray());
    }

    /// <summary>
    /// Validação estrita do CNPJ pelo algoritmo oficial do módulo 11 da Receita Federal do Brasil.
    /// </summary>
    public static bool IsValid(string? cnpj)
    {
        var digits = Clean(cnpj);
        if (digits.Length != 14) return false;

        // Rejeita sequências conhecidas de dígitos repetidos
        if (digits.Distinct().Count() == 1) return false;

        // 1º Dígito Verificador
        int[] multiplicadores1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        var soma = 0;
        for (var i = 0; i < 12; i++)
            soma += (digits[i] - '0') * multiplicadores1[i];

        var resto = soma % 11;
        var dv1 = resto < 2 ? 0 : 11 - resto;
        if (digits[12] - '0' != dv1) return false;

        // 2º Dígito Verificador
        int[] multiplicadores2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        soma = 0;
        for (var i = 0; i < 13; i++)
            soma += (digits[i] - '0') * multiplicadores2[i];

        resto = soma % 11;
        var dv2 = resto < 2 ? 0 : 11 - resto;
        return digits[13] - '0' == dv2;
    }

    /// <summary>
    /// Formata 14 dígitos no formato padrão 00.000.000/0000-00.
    /// </summary>
    public static string Format(string? cnpj)
    {
        var d = Clean(cnpj);
        if (d.Length != 14) return d;
        return $"{d[..2]}.{d.Substring(2, 3)}.{d.Substring(5, 3)}/{d.Substring(8, 4)}-{d.Substring(12, 2)}";
    }

    /// <summary>
    /// Retorna os 8 primeiros dígitos (CNPJ-Base).
    /// </summary>
    public static string GetCnpjBase(string? cnpj)
    {
        var d = Clean(cnpj);
        return d.Length >= 8 ? d[..8] : d;
    }
}
