namespace OpenSintegra.Core;

/// <summary>
/// Decomposes any 126-character Sintegra record into its official Convenio ICMS 57/95 fields
/// for the interactive field inspector table in the desktop application.
/// </summary>
public static class RecordSchemaDecomposer
{
    private sealed record ColDef(int Start, int End, string Name, string Kind);

    public static IReadOnlyList<FieldSlice> DecomposeLine(string rawLine)
    {
        if (string.IsNullOrEmpty(rawLine))
            return Array.Empty<FieldSlice>();

        string recType = SintegraParser.SliceCols(rawLine, 1, 2);
        string sub = SintegraParser.SliceCols(rawLine, 3, 3);

        var defs = recType switch
        {
            "10" => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 16, "CNPJ do Estabelecimento", "Cnpj"),
                new ColDef(17, 30, "Inscrição Estadual", "Text"),
                new ColDef(31, 65, "Nome Comercial / Razão Social", "Text"),
                new ColDef(66, 95, "Município", "Text"),
                new ColDef(96, 97, "Unidade da Federação (UF)", "Code"),
                new ColDef(98, 107, "Fax", "Text"),
                new ColDef(108, 115, "Data Inicial do Período", "Date"),
                new ColDef(116, 123, "Data Final do Período", "Date"),
                new ColDef(124, 124, "Código da Identificação do Convênio", "Code"),
                new ColDef(125, 125, "Código da Natureza das Operações", "Code"),
                new ColDef(126, 126, "Código da Finalidade do Arquivo", "Code"),
            },
            "11" => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 36, "Logradouro", "Text"),
                new ColDef(37, 41, "Número", "Int"),
                new ColDef(42, 63, "Complemento", "Text"),
                new ColDef(64, 78, "Bairro", "Text"),
                new ColDef(79, 86, "CEP", "Text"),
                new ColDef(87, 114, "Nome do Contato", "Text"),
                new ColDef(115, 126, "Telefone de Contato", "Text"),
            },
            "50" => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 16, "CNPJ do Remetente / Destinatário", "Cnpj"),
                new ColDef(17, 30, "Inscrição Estadual", "Text"),
                new ColDef(31, 38, "Data de Emissão ou Recebimento", "Date"),
                new ColDef(39, 40, "Unidade da Federação (UF)", "Code"),
                new ColDef(41, 42, "Código do Modelo da Nota Fiscal", "Code"),
                new ColDef(43, 45, "Série da Nota Fiscal", "Text"),
                new ColDef(46, 51, "Número da Nota Fiscal", "Int"),
                new ColDef(52, 55, "CFOP", "Code"),
                new ColDef(56, 56, "Emitente (P=Próprio, T=Terceiros)", "Code"),
                new ColDef(57, 69, "Valor Total da Nota Fiscal", "Cents"),
                new ColDef(70, 82, "Base de Cálculo do ICMS", "Cents"),
                new ColDef(83, 95, "Valor do ICMS", "Cents"),
                new ColDef(96, 108, "Isenta ou Não-Tributada", "Cents"),
                new ColDef(109, 121, "Outras", "Cents"),
                new ColDef(122, 125, "Alíquota do ICMS", "Bps"),
                new ColDef(126, 126, "Situação da Nota Fiscal (N/S/E/X)", "Code"),
            },
            "51" => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 16, "CNPJ do Remetente / Destinatário", "Cnpj"),
                new ColDef(17, 30, "Inscrição Estadual", "Text"),
                new ColDef(31, 38, "Data de Emissão ou Recebimento", "Date"),
                new ColDef(39, 40, "Unidade da Federação (UF)", "Code"),
                new ColDef(41, 43, "Série da Nota Fiscal", "Text"),
                new ColDef(44, 49, "Número da Nota Fiscal", "Int"),
                new ColDef(50, 53, "CFOP", "Code"),
                new ColDef(54, 66, "Valor Total da Nota Fiscal", "Cents"),
                new ColDef(67, 79, "Valor do IPI", "Cents"),
                new ColDef(80, 92, "Valor Isento ou Não-Tributado de IPI", "Cents"),
                new ColDef(93, 105, "Valor Outras de IPI", "Cents"),
                new ColDef(106, 125, "Brancos", "Text"),
                new ColDef(126, 126, "Situação da Nota Fiscal", "Code"),
            },
            "53" => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 16, "CNPJ do Contribuinte Substituído", "Cnpj"),
                new ColDef(17, 30, "Inscrição Estadual", "Text"),
                new ColDef(31, 38, "Data de Emissão ou Recebimento", "Date"),
                new ColDef(39, 40, "Unidade da Federação (UF)", "Code"),
                new ColDef(41, 42, "Modelo da Nota Fiscal", "Code"),
                new ColDef(43, 45, "Série da Nota Fiscal", "Text"),
                new ColDef(46, 51, "Número da Nota Fiscal", "Int"),
                new ColDef(52, 55, "CFOP", "Code"),
                new ColDef(56, 56, "Emitente (P/T)", "Code"),
                new ColDef(57, 69, "Base de Cálculo Retenção ICMS-ST", "Cents"),
                new ColDef(70, 82, "ICMS Retido por Substituição", "Cents"),
                new ColDef(83, 95, "Despesas Acessórias (Frete/Seguro/Outras)", "Cents"),
                new ColDef(96, 96, "Situação da Nota Fiscal", "Code"),
                new ColDef(97, 97, "Código de Antecipação", "Code"),
                new ColDef(98, 126, "Brancos", "Text"),
            },
            "54" => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 16, "CNPJ do Remetente / Destinatário", "Cnpj"),
                new ColDef(17, 18, "Modelo da Nota Fiscal", "Code"),
                new ColDef(19, 21, "Série da Nota Fiscal", "Text"),
                new ColDef(22, 27, "Número da Nota Fiscal", "Int"),
                new ColDef(28, 31, "CFOP", "Code"),
                new ColDef(32, 34, "CST (Código de Situação Tributária)", "Code"),
                new ColDef(35, 37, "Número de Ordem do Item (001..990 / 991..999)", "Code"),
                new ColDef(38, 51, "Código do Produto ou Serviço", "Text"),
                new ColDef(52, 62, "Quantidade do Produto (3 decimais)", "Milli"),
                new ColDef(63, 74, "Valor Bruto do Produto", "Cents"),
                new ColDef(75, 86, "Valor do Desconto / Despesa Acessória", "Cents"),
                new ColDef(87, 98, "Base de Cálculo do ICMS", "Cents"),
                new ColDef(99, 110, "Base de Cálculo do ICMS-ST", "Cents"),
                new ColDef(111, 122, "Valor do IPI", "Cents"),
                new ColDef(123, 126, "Alíquota do ICMS", "Bps"),
            },
            "60" when sub == "R" => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 3, "Subtipo R (Resumo Mensal ECF)", "Code"),
                new ColDef(4, 9, "Mês e Ano de Emissão (MMAAAA)", "Code"),
                new ColDef(10, 23, "Código do Produto", "Text"),
                new ColDef(24, 36, "Quantidade Acumulada (3 decimais)", "Milli"),
                new ColDef(37, 52, "Valor Líquido Acumulado", "Cents"),
                new ColDef(53, 68, "Base de Cálculo do ICMS", "Cents"),
                new ColDef(69, 72, "Situação Tributária / Alíquota", "Bps"),
                new ColDef(73, 126, "Brancos", "Text"),
            },
            "61" when sub == "R" => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 3, "Subtipo R (Resumo Mensal Consumidor/NFC-e)", "Code"),
                new ColDef(4, 9, "Mês e Ano de Emissão (MMAAAA)", "Code"),
                new ColDef(10, 23, "Código do Produto ou Serviço", "Text"),
                new ColDef(24, 36, "Quantidade Vendida no Mês (3 decimais)", "Milli"),
                new ColDef(37, 52, "Valor Bruto do Produto", "Cents"),
                new ColDef(53, 68, "Base de Cálculo do ICMS", "Cents"),
                new ColDef(69, 72, "Alíquota do ICMS", "Bps"),
                new ColDef(73, 126, "Brancos", "Text"),
            },
            "61" => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 16, "Brancos", "Text"),
                new ColDef(17, 30, "Brancos", "Text"),
                new ColDef(31, 38, "Data de Emissão", "Date"),
                new ColDef(39, 40, "Modelo do Documento (02 / 65 NFC-e)", "Code"),
                new ColDef(41, 43, "Série do Documento", "Text"),
                new ColDef(44, 45, "Subsérie", "Text"),
                new ColDef(46, 51, "Número Inicial de Ordem", "Int"),
                new ColDef(52, 57, "Número Final de Ordem", "Int"),
                new ColDef(58, 70, "Valor Total Diário", "Cents"),
                new ColDef(71, 83, "Base de Cálculo do ICMS", "Cents"),
                new ColDef(84, 95, "Valor do ICMS", "Cents"),
                new ColDef(96, 108, "Isenta ou Não-Tributada", "Cents"),
                new ColDef(109, 121, "Outras", "Cents"),
                new ColDef(122, 125, "Alíquota do ICMS", "Bps"),
                new ColDef(126, 126, "Branco", "Text"),
            },
            "70" => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 16, "CNPJ do Emitente / Tomador", "Cnpj"),
                new ColDef(17, 30, "Inscrição Estadual", "Text"),
                new ColDef(31, 38, "Data de Emissão / Utilização", "Date"),
                new ColDef(39, 40, "Unidade da Federação (UF)", "Code"),
                new ColDef(41, 42, "Modelo do Conhecimento (07, 08, 57)", "Code"),
                new ColDef(43, 43, "Série", "Text"),
                new ColDef(44, 45, "Subsérie", "Text"),
                new ColDef(46, 51, "Número do Documento", "Int"),
                new ColDef(52, 55, "CFOP", "Code"),
                new ColDef(56, 68, "Valor Total do Documento Fiscal", "Cents"),
                new ColDef(69, 82, "Base de Cálculo do ICMS", "Cents"),
                new ColDef(83, 96, "Valor do ICMS", "Cents"),
                new ColDef(97, 110, "Isenta ou Não-Tributada", "Cents"),
                new ColDef(111, 124, "Outras", "Cents"),
                new ColDef(125, 125, "Modalidade Frete (1=CIF, 2=FOB)", "Code"),
                new ColDef(126, 126, "Situação do Documento", "Code"),
            },
            "74" => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 10, "Data do Inventário", "Date"),
                new ColDef(11, 24, "Código do Produto", "Text"),
                new ColDef(25, 37, "Quantidade (3 decimais)", "Milli"),
                new ColDef(38, 50, "Valor Total do Produto", "Cents"),
                new ColDef(51, 51, "Código de Posse das Mercadorias (1, 2, 3)", "Code"),
                new ColDef(52, 65, "CNPJ do Possuidor / Proprietário", "Cnpj"),
                new ColDef(66, 79, "Inscrição Estadual do Possuidor", "Text"),
                new ColDef(80, 81, "UF do Possuidor / Proprietário", "Code"),
                new ColDef(82, 126, "Brancos", "Text"),
            },
            "75" => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 10, "Data Inicial de Validade", "Date"),
                new ColDef(11, 18, "Data Final de Validade", "Date"),
                new ColDef(19, 32, "Código do Produto ou Serviço", "Text"),
                new ColDef(33, 40, "Código NCM (Nomenclatura Comum do Mercosul)", "Code"),
                new ColDef(41, 93, "Descrição do Produto ou Serviço", "Text"),
                new ColDef(94, 99, "Unidade de Medida de Comercialização", "Text"),
                new ColDef(100, 104, "Alíquota do IPI", "Bps"),
                new ColDef(105, 108, "Alíquota do ICMS", "Bps"),
                new ColDef(109, 113, "Redução da Base de Cálculo do ICMS", "Bps"),
                new ColDef(114, 126, "Base de Cálculo do ICMS de Substituição Tributária", "Cents"),
            },
            "90" => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 16, "CNPJ do Contribuinte Informante", "Cnpj"),
                new ColDef(17, 30, "Inscrição Estadual do Informante", "Text"),
                new ColDef(31, 120, "Blocos Totalizadores (Até 9 blocos de 10 bytes: Tipo[2] + Qtd[8])", "Text"),
                new ColDef(121, 125, "Brancos", "Text"),
                new ColDef(126, 126, "Número de Registros Tipo 90 no Arquivo", "Int"),
            },
            _ => new[]
            {
                new ColDef(1, 2, "Tipo de Registro", "Code"),
                new ColDef(3, 126, $"Conteúdo do Registro {recType}{sub}", "Text"),
            }
        };

        var result = new List<FieldSlice>(defs.Length);
        for (int idx = 0; idx < defs.Length; idx++)
        {
            var d = defs[idx];
            string raw = SintegraParser.SliceCols(rawLine, d.Start, d.End);
            string formatted = FormatField(raw, d.Kind);
            result.Add(new FieldSlice(idx + 1, d.Start, d.End, d.Name, raw, formatted));
        }
        return result;
    }

    private static string FormatField(string raw, string kind)
    {
        string trimmed = raw.Trim();
        return kind switch
        {
            "Cnpj" => TaxIdValidator.FormatCnpj(trimmed),
            "Date" => TaxIdValidator.FormatDateBr(trimmed),
            "Cents" => long.TryParse(trimmed, out long c) ? $"R$ {TaxIdValidator.FormatCentsBrl(c)}" : trimmed,
            "Milli" => long.TryParse(trimmed, out long m) ? (m / 1000.0m).ToString("N3", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) : trimmed,
            "Bps" => long.TryParse(trimmed, out long b) ? $"{(b / 100.0m).ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}%" : trimmed,
            "Int" => long.TryParse(trimmed, out long n) ? n.ToString() : trimmed,
            _ => trimmed
        };
    }
}
