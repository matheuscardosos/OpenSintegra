namespace OpenSintegra.Core;

public static class SintegraValidator
{
    public static ValidationReport Validate(SintegraDocument doc)
    {
        var diagnostics = new List<Diagnostic>();

        if (doc.Lines.Count == 0)
        {
            diagnostics.Add(new Diagnostic(
                "SIN-SYN-001", Severity.Error, 1, 1, 1, "--", "Arquivo",
                "Arquivo Sintegra vazio (0 linhas).", "0 bytes", "Registros 10, 11 e 90", false));
        }

        string ufEstab = doc.Reg10?.Uf.Trim() ?? "";

        // 1. Line-level CRLF & length checks (Convenio 57/95 + MS Reg 88 rules)
        foreach (var line in doc.Lines)
        {
            if (!line.HasCr || !line.HasLf)
            {
                string msg = (!line.HasCr && line.HasLf)
                    ? "Falta CR (Carriage Return 0x0D) no final da linha."
                    : (line.HasCr && !line.HasLf)
                        ? "Falta LF (Line Feed 0x0A) no final da linha."
                        : "Falta terminador de linha CRLF (0x0D 0x0A).";

                diagnostics.Add(new Diagnostic(
                    "SIN-SYN-002", Severity.Error, line.LineNumber,
                    Math.Max(1, line.CharLen), Math.Max(1, line.CharLen),
                    line.RecordType, "Terminador CRLF", msg,
                    $"CR={line.HasCr} LF={line.HasLf}", "CRLF (0x0D 0x0A)", true));
            }

            bool isMsReg88 = ufEstab == "MS" && line.RecordType == "88" && line.CharLen is >= 126 and <= 140;
            if (line.CharLen != 126 && !isMsReg88)
            {
                bool safeFixLen = SintegraAutoFixer.CanSafelyNormalizeLength(line.RecordType, line.RawText);
                diagnostics.Add(new Diagnostic(
                    "SIN-SYN-003", Severity.Error, line.LineNumber,
                    1, Math.Max(1, line.CharLen), line.RecordType, "Comprimento do Registro",
                    $"Comprimento da linha invalido: {line.CharLen} colunas encontradas (esperado 126).",
                    $"{line.CharLen} colunas", "126 colunas", safeFixLen));
            }
        }

        if (doc.Lines.Count >= 1 && doc.Lines[0].RecordType != "10")
        {
            diagnostics.Add(new Diagnostic(
                "SIN-SYN-004", Severity.Error, 1, 1, 2, doc.Lines[0].RecordType,
                "Ordem Inicial", "A primeira linha do arquivo deve ser obrigatoriamente o Registro 10.",
                doc.Lines[0].RecordType, "10", true));
        }
        if (doc.Lines.Count >= 2 && doc.Lines[1].RecordType != "11")
        {
            diagnostics.Add(new Diagnostic(
                "SIN-SYN-004", Severity.Error, 2, 1, 2, doc.Lines[1].RecordType,
                "Ordem Inicial", "A segunda linha do arquivo deve ser obrigatoriamente o Registro 11.",
                doc.Lines[1].RecordType, "11", true));
        }
        if (doc.Lines.Count >= 1 && doc.Lines[^1].RecordType != "90")
        {
            var last = doc.Lines[^1];
            diagnostics.Add(new Diagnostic(
                "SIN-SYN-005", Severity.Error, last.LineNumber, 1, 2, last.RecordType,
                "Fechamento", "O arquivo deve encerrar obrigatoriamente com o Registro 90.",
                last.RecordType, "90", true));
        }

        // 2. Record 10 & 11 checks
        if (doc.Reg10 is { } r10)
        {
            if (!TaxIdValidator.ValidateCnpj(r10.Cnpj))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R10-001", Severity.Error, r10.Line, 3, 16, "10", "CNPJ",
                    "Digito verificador ou formato do CNPJ do contribuinte invalido.",
                    r10.Cnpj, "CNPJ valido (14 digitos)", false));
            }
            if (!TaxIdValidator.IsValidUf(r10.Uf))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R10-002", Severity.Error, r10.Line, 96, 97, "10", "UF",
                    "Unidade da Federacao invalida no Registro 10.",
                    r10.Uf, "UF valida (ex: MG, SP, RS)", false));
            }
            else if (!TaxIdValidator.ValidateIe(r10.Uf, r10.Ie))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R10-002", Severity.Error, r10.Line, 17, 30, "10", "Inscricao Estadual",
                    $"Inscricao Estadual invalida para a UF {r10.Uf.Trim()}.",
                    r10.Ie, $"IE valida para {r10.Uf.Trim()}", false));
            }

            bool diOk = TaxIdValidator.ValidateDateYyyyMmDd(r10.DataInicial);
            bool dfOk = TaxIdValidator.ValidateDateYyyyMmDd(r10.DataFinal);
            if (!diOk || !dfOk || string.CompareOrdinal(r10.DataInicial, r10.DataFinal) > 0)
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R10-003", Severity.Error, r10.Line, 108, 123, "10", "Periodo",
                    "Data inicial ou final de competencia invalida no Registro 10.",
                    $"{r10.DataInicial} a {r10.DataFinal}", "AAAAMMDD inicial <= final", false));
            }
            else
            {
                if (r10.DataInicial[..6] != r10.DataFinal[..6])
                {
                    int monthsSpan = CountMonthsInclusive(r10.DataInicial, r10.DataFinal);
                    diagnostics.Add(new Diagnostic(
                        "SIN-R10-006", Severity.Info, r10.Line, 108, 123, "10", "Periodo Consolidado Multi-Mes",
                        $"Arquivo Sintegra consolidado abrangendo {monthsSpan} mes(es) ({TaxIdValidator.FormatDateBr(r10.DataInicial)} a {TaxIdValidator.FormatDateBr(r10.DataFinal)}). O validador legado da SEFAZ aceita apenas 1 mes por arquivo, mas o OpenSintegra valida e audita todo o periodo.",
                        $"{monthsSpan} meses", "Leitura anual/acumulada habilitada", false));
                }

                if (string.CompareOrdinal(r10.DataInicial, "20040101") >= 0 && r10.CodEstrutura != "3")
                {
                    diagnostics.Add(new Diagnostic(
                        "SIN-R10-004", Severity.Error, r10.Line, 124, 124, "10", "Codigo de Estrutura",
                        "Para arquivos a partir de 01/01/2004 o codigo de estrutura (pos 124) deve ser 3 (Convenio 76/03).",
                        r10.CodEstrutura, "3", false));
                }
            }

            if (r10.CodNatureza is not ("1" or "2" or "3"))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R10-005", Severity.Error, r10.Line, 125, 125, "10", "Codigo da Natureza",
                    "Codigo da natureza das operacoes invalido (pos 125).",
                    r10.CodNatureza, "1, 2 ou 3", false));
            }
            if (r10.CodFinalidade is not ("1" or "2" or "3" or "5"))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R10-005", Severity.Error, r10.Line, 126, 126, "10", "Codigo da Finalidade",
                    "Codigo da finalidade do arquivo invalido (pos 126).",
                    r10.CodFinalidade, "1, 2, 3 ou 5", false));
            }
        }

        if (doc.Reg11 is { } r11)
        {
            if (r11.Cep.Trim().Length != 8 || !r11.Cep.All(char.IsAsciiDigit) || r11.Cep == "00000000")
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R11-001", Severity.Warning, r11.Line, 79, 86, "11", "CEP",
                    "CEP invalido ou zerado no Registro 11.",
                    r11.Cep, "8 digitos numericos", false));
            }
        }

        // 3. Record 50 checks & invoice indexing
        var seenR50Keys = new Dictionary<string, int>(StringComparer.Ordinal);
        var r50ByInvoice = new Dictionary<string, List<Reg50>>(StringComparer.Ordinal);

        foreach (var r50 in doc.Reg50)
        {
            if (!TaxIdValidator.ValidateCnpjOrCpf(r50.Cnpj))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R50-001", Severity.Error, r50.Line, 3, 16, "50", "CNPJ/CPF",
                    $"CNPJ/CPF invalido na Nota Fiscal {r50.Numero.Trim()}.",
                    r50.Cnpj, "CNPJ ou CPF valido", false));
            }
            if (!TaxIdValidator.IsValidUf(r50.Uf) || !TaxIdValidator.ValidateIe(r50.Uf, r50.Ie))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R50-001", Severity.Error, r50.Line, 17, 40, "50", "IE / UF",
                    $"Inscricao Estadual ou UF invalida na NF {r50.Numero.Trim()} ({r50.Uf}).",
                    $"{r50.Ie} / {r50.Uf}", "IE compativel com a UF", false));
            }
            if (!TaxIdValidator.ValidateDateYyyyMmDd(r50.DataEmissao))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R50-002", Severity.Error, r50.Line, 31, 38, "50", "Data de Emissao",
                    "Data de emissao/recebimento invalida no Registro 50.",
                    r50.DataEmissao, "AAAAMMDD", false));
            }
            if (!TaxIdValidator.ValidModels50.Contains(r50.Modelo) || !TaxIdValidator.ValidateCfop(r50.Cfop))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R50-003", Severity.Error, r50.Line, 41, 55, "50", "Modelo / CFOP",
                    "Modelo de documento fiscal ou CFOP invalido no Registro 50.",
                    $"Mod={r50.Modelo} CFOP={r50.Cfop}", "Modelo e CFOP validos", false));
            }

            if (r50.Situacao == "S" && (r50.ValorTotalCents != 0 || r50.BaseIcmsCents != 0 || r50.ValorIcmsCents != 0))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R50-004", Severity.Error, r50.Line, 57, 126, "50", "Nota Cancelada com Valores",
                    "Nota Fiscal cancelada (Situacao = S) deve ter todos os valores contabeis e fiscais zerados.",
                    $"R$ {TaxIdValidator.FormatCentsBrl(r50.ValorTotalCents)}", "R$ 0,00", true));
            }

            if (r50.Situacao != "S" && r50.BaseIcmsCents > 0 && r50.AliquotaBps > 0 && r50.ValorIcmsCents > 0)
            {
                long expectedIcms = (r50.BaseIcmsCents * r50.AliquotaBps + 5000) / 10000;
                if (Math.Abs(r50.ValorIcmsCents - expectedIcms) > 2)
                {
                    diagnostics.Add(new Diagnostic(
                        "SIN-R50-005", Severity.Warning, r50.Line, 70, 125, "50", "Calculo ICMS",
                        $"Divergencia matematica no calculo do ICMS da NF {r50.Numero.Trim()} (Base * Aliquota).",
                        $"R$ {TaxIdValidator.FormatCentsBrl(r50.ValorIcmsCents)}",
                        $"R$ {TaxIdValidator.FormatCentsBrl(expectedIcms)}", false));
                }
            }

            string dupKey = $"{r50.Cnpj}|{r50.Modelo}|{r50.Serie}|{r50.Numero}|{r50.Emitente}|{r50.Cfop}|{r50.AliquotaBps}";
            if (seenR50Keys.TryGetValue(dupKey, out int prevLine))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-REL-001", Severity.Error, r50.Line, 3, 55, "50", "Chave Duplicada",
                    $"Registro 50 duplicado para a NF {r50.Numero.Trim()} CFOP {r50.Cfop} (ja declarado na linha {prevLine}).",
                    $"NF {r50.Numero.Trim()} CFOP {r50.Cfop}", "Chave unica por CFOP e Aliquota", false));
            }
            else
            {
                seenR50Keys[dupKey] = r50.Line;
            }

            string invKey = $"{r50.Cnpj}|{r50.Modelo}|{r50.Serie}|{r50.Numero}";
            if (!r50ByInvoice.TryGetValue(invKey, out var list))
            {
                list = new List<Reg50>();
                r50ByInvoice[invKey] = list;
            }
            list.Add(r50);
        }

        // 4. Record 75 catalog indexing
        var r75ByProd = new Dictionary<string, List<Reg75>>(StringComparer.Ordinal);
        string r10Di = doc.Reg10?.DataInicial ?? "";
        string r10Df = doc.Reg10?.DataFinal ?? "";
        bool isMultiMonth = TaxIdValidator.ValidateDateYyyyMmDd(r10Di)
            && TaxIdValidator.ValidateDateYyyyMmDd(r10Df)
            && r10Di[..6] != r10Df[..6];

        foreach (var r75 in doc.Reg75)
        {
            if (string.IsNullOrEmpty(r75.CodProduto) || string.IsNullOrEmpty(r75.Descricao) || string.IsNullOrEmpty(r75.Unidade))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R75-001", Severity.Error, r75.Line, 19, 99, "75", "Cadastro de Produto",
                    "Codigo, descricao ou unidade de medida em branco no Registro 75.",
                    $"Cod='{r75.CodProduto}' Desc='{r75.Descricao}'", "Campos obrigatorios preenchidos", false));
            }
            if (!string.IsNullOrEmpty(r10Di) && !string.IsNullOrEmpty(r10Df))
            {
                bool mismatch = isMultiMonth
                    ? (string.CompareOrdinal(r75.DataFinal, r10Di) < 0 || string.CompareOrdinal(r75.DataInicial, r10Df) > 0)
                    : (r75.DataInicial != r10Di || r75.DataFinal != r10Df);

                if (mismatch)
                {
                    diagnostics.Add(new Diagnostic(
                        "SIN-R75-002", Severity.Warning, r75.Line, 3, 18, "75", "Validade do Produto",
                        $"Periodo de validade do Registro 75 (produto '{r75.CodProduto}') difere do periodo do Registro 10.",
                        $"{r75.DataInicial} a {r75.DataFinal}", $"{r10Di} a {r10Df}", true));
                }
            }

            if (!r75ByProd.TryGetValue(r75.CodProduto, out var list))
            {
                list = new List<Reg75>();
                r75ByProd[r75.CodProduto] = list;
            }
            list.Add(r75);
        }

        foreach (var (prodCode, entries) in r75ByProd)
        {
            if (entries.Count > 1)
            {
                for (int i = 1; i < entries.Count; i++)
                {
                    diagnostics.Add(new Diagnostic(
                        "SIN-REL-007", Severity.Error, entries[i].Line, 19, 32, "75", "Produto Duplicado",
                        $"Produto '{prodCode}' declarado em duplicidade no Registro 75 (primeira ocorrencia na linha {entries[0].Line}).",
                        prodCode, "Cadastro unico por codigo de produto", true));
                }
            }
        }

        // 5. Record 54 validation & cross-reference with 50 and 75
        var productMovementCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var r54ByInvoice = new Dictionary<string, List<Reg54>>(StringComparer.Ordinal);

        void RecordProdMove(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            productMovementCounts.TryGetValue(code, out int cnt);
            productMovementCounts[code] = cnt + 1;
        }

        foreach (var r54 in doc.Reg54)
        {
            bool isAccessory = r54.NumItem is "991" or "992" or "999";
            if (!TaxIdValidator.ValidateCfop(r54.Cfop) || (!isAccessory && string.IsNullOrEmpty(r54.CodProduto)))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R54-001", Severity.Error, r54.Line, 28, 51, "54", "CFOP / Produto",
                    $"CFOP ou codigo do produto invalido no item {r54.NumItem} da NF {r54.Numero.Trim()}.",
                    $"CFOP={r54.Cfop} Prod='{r54.CodProduto}'", "CFOP valido e produto informado", false));
            }

            if (!isAccessory && !string.IsNullOrEmpty(r54.CodProduto))
            {
                RecordProdMove(r54.CodProduto);
                if (!r75ByProd.ContainsKey(r54.CodProduto))
                {
                    diagnostics.Add(new Diagnostic(
                        "SIN-REL-005", Severity.Error, r54.Line, 38, 51, "54", "Produto Sem Registro 75",
                        $"Nao existe Registro 75 correspondente para o produto '{r54.CodProduto}' informado no Registro 54 (NF {r54.Numero.Trim()}).",
                        r54.CodProduto, "Cadastrar produto no Registro 75", true));
                }
            }

            string invKey = $"{r54.Cnpj}|{r54.Modelo}|{r54.Serie}|{r54.Numero}";
            if (!r50ByInvoice.ContainsKey(invKey))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-REL-002", Severity.Error, r54.Line, 3, 27, "54", "Item Sem Capa 50",
                    $"Item de Nota Fiscal (Registro 54, NF {r54.Numero.Trim()}) sem capa correspondente no Registro 50.",
                    $"CNPJ={r54.Cnpj} NF={r54.Numero.Trim()}", "Registro 50 correspondente", false));
            }

            if (!r54ByInvoice.TryGetValue(invKey, out var list))
            {
                list = new List<Reg54>();
                r54ByInvoice[invKey] = list;
            }
            list.Add(r54);
        }

        foreach (var (invKey, items) in r54ByInvoice)
        {
            bool hasAccessory = items.Any(i => i.NumItem is "991" or "992" or "999");
            bool hasNormal = items.Any(i => i.NumItem is not ("991" or "992" or "999"));
            if (hasAccessory && !hasNormal)
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-REL-003", Severity.Error, items[0].Line, 35, 37, "54", "Item de Despesa Isolado",
                    $"A NF {items[0].Numero.Trim()} possui item de despesa acessoria (991/992/999) sem nenhum item de mercadoria normal.",
                    items[0].NumItem, "Pelo menos um item normal (001..990)", false));
            }

            if (r50ByInvoice.TryGetValue(invKey, out var headers))
            {
                long sumR50 = headers.Sum(h => h.ValorTotalCents);
                long sumR54 = items.Sum(i => i.NumItem is "991" or "992" or "999"
                    ? i.ValorDescontoCents
                    : i.ValorProdutoCents - i.ValorDescontoCents + i.ValorIpiCents);

                long diff = Math.Abs(sumR50 - sumR54);
                if (diff > 0 && sumR54 > 0)
                {
                    bool hasR53 = doc.Reg53.Any(r53 => $"{r53.Cnpj}|{r53.Modelo}|{r53.Serie}|{r53.Numero}" == invKey);
                    if (!hasR53)
                    {
                        var sev = diff <= 100 ? Severity.Warning : Severity.Error;
                        diagnostics.Add(new Diagnostic(
                            "SIN-REL-004", sev, headers[0].Line, 57, 69, "50", "Total Capa 50 vs Soma Itens 54",
                            $"Divergencia entre o Valor Contabil no Registro 50 (R$ {TaxIdValidator.FormatCentsBrl(sumR50)}) e a soma liquida dos itens no Registro 54 (R$ {TaxIdValidator.FormatCentsBrl(sumR54)}) na NF {headers[0].Numero.Trim()}.",
                            $"R$ {TaxIdValidator.FormatCentsBrl(sumR50)}",
                            $"R$ {TaxIdValidator.FormatCentsBrl(sumR54)}", false));
                    }
                }
            }
        }

        foreach (var r60d in doc.Reg60D)
        {
            if (!string.IsNullOrEmpty(r60d.CodProduto))
            {
                RecordProdMove(r60d.CodProduto);
                if (!r75ByProd.ContainsKey(r60d.CodProduto))
                {
                    diagnostics.Add(new Diagnostic(
                        "SIN-REL-005", Severity.Error, r60d.Line, 32, 45, "60", "Produto Sem Registro 75",
                        $"Nao existe Registro 75 para o produto '{r60d.CodProduto}' informado no Registro 60D.",
                        r60d.CodProduto, "Produto cadastrado no Registro 75", true));
                }
            }
        }

        foreach (var r60i in doc.Reg60I)
        {
            if (!string.IsNullOrEmpty(r60i.CodProduto))
            {
                RecordProdMove(r60i.CodProduto);
                if (!r75ByProd.ContainsKey(r60i.CodProduto))
                {
                    diagnostics.Add(new Diagnostic(
                        "SIN-REL-005", Severity.Error, r60i.Line, 43, 56, "60", "Produto Sem Registro 75",
                        $"Nao existe Registro 75 para o produto '{r60i.CodProduto}' informado no Registro 60I.",
                        r60i.CodProduto, "Produto cadastrado no Registro 75", true));
                }
            }
        }

        foreach (var r74 in doc.Reg74)
        {
            if (!string.IsNullOrEmpty(r74.CodProduto))
            {
                RecordProdMove(r74.CodProduto);
                if (!r75ByProd.ContainsKey(r74.CodProduto))
                {
                    diagnostics.Add(new Diagnostic(
                        "SIN-REL-005", Severity.Error, r74.Line, 11, 24, "74", "Produto Sem Registro 75",
                        $"Nao existe Registro 75 para o produto '{r74.CodProduto}' informado no Inventario (Registro 74).",
                        r74.CodProduto, "Produto cadastrado no Registro 75", true));
                }
            }
        }

        foreach (var r60r in doc.Reg60R)
        {
            if (!string.IsNullOrEmpty(r60r.CodProduto))
            {
                RecordProdMove(r60r.CodProduto);
                if (!r75ByProd.ContainsKey(r60r.CodProduto))
                {
                    diagnostics.Add(new Diagnostic(
                        "SIN-REL-005", Severity.Error, r60r.Line, 10, 23, "60R", "Produto Sem Registro 75",
                        $"Nao existe Registro 75 para o produto '{r60r.CodProduto}' informado no Registro 60R.",
                        r60r.CodProduto, "Produto cadastrado no Registro 75", true));
                }
            }
        }

        foreach (var r61 in doc.Reg61)
        {
            if (!TaxIdValidator.ValidateDateYyyyMmDd(r61.DataEmissao))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R61-001", Severity.Error, r61.Line, 31, 38, "61", "Data Registro 61",
                    "Data de emissao invalida no Registro 61.",
                    r61.DataEmissao, "AAAAMMDD", false));
            }
            if (long.TryParse(r61.NumInicialOrdem.Trim(), out long ini) &&
                long.TryParse(r61.NumFinalOrdem.Trim(), out long fin) &&
                fin < ini)
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R61-002", Severity.Error, r61.Line, 46, 57, "61", "Ordem Inicial/Final (Reg 61)",
                    $"Numero final ({fin}) menor que numero inicial ({ini}) no Registro 61.",
                    $"{ini}..{fin}", "Inicial <= Final", false));
            }
        }

        foreach (var r61r in doc.Reg61R)
        {
            if (!TaxIdValidator.ValidateMmYyyy(r61r.MesAno))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-R61R-001", Severity.Error, r61r.Line, 4, 9, "61R", "Mes/Ano 61R",
                    "Mes e ano de emissao invalidos no Registro 61R.",
                    r61r.MesAno, "MMAAAA", false));
            }
            if (!string.IsNullOrEmpty(r61r.CodProduto))
            {
                RecordProdMove(r61r.CodProduto);
                if (!r75ByProd.ContainsKey(r61r.CodProduto))
                {
                    diagnostics.Add(new Diagnostic(
                        "SIN-REL-005", Severity.Error, r61r.Line, 10, 23, "61R", "Produto Sem Registro 75",
                        $"Nao existe Registro 75 para o produto '{r61r.CodProduto}' informado no Registro 61R (NFC-e/Consumidor).",
                        r61r.CodProduto, "Cadastrar produto no Registro 75", true));
                }
            }
        }

        foreach (var otherCode in doc.OtherProductCodes)
            RecordProdMove(otherCode);

        // Check orphan Record 75 entries across movement/inventory records 54,57,60D,60I,60R,61R,74,77,86
        foreach (var r75 in doc.Reg75)
        {
            if (!string.IsNullOrEmpty(r75.CodProduto) && !productMovementCounts.ContainsKey(r75.CodProduto))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-REL-006", Severity.Error, r75.Line, 19, 32, "75", "Produto Orfao no 75",
                    $"Nao existe registro 54, 57, 60D, 60I, 60R, 61R, 74, 77 ou 86 referenciando o produto '{r75.CodProduto}' declarado neste Registro 75.",
                    r75.CodProduto, "Remover Registro 75 sem movimento no periodo", true));
            }
        }

        // 6. Record 60M -> 60A check
        var r60mKeys = new HashSet<string>(
            doc.Reg60M.Select(m => $"{m.DataEmissao}|{m.NumSerieFab}"),
            StringComparer.Ordinal);
        foreach (var r60a in doc.Reg60A)
        {
            if (!r60mKeys.Contains($"{r60a.DataEmissao}|{r60a.NumSerieFab}"))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-REL-008", Severity.Error, r60a.Line, 4, 31, "60", "Vinculo 60M",
                    $"Registro 60A (ECF serie {r60a.NumSerieFab}) sem Registro 60M correspondente na data {r60a.DataEmissao}.",
                    $"{r60a.DataEmissao} / {r60a.NumSerieFab}", "Registro 60M correspondente", false));
            }
        }

        // 7. Record 90 totalizer check
        var declaredCounts90 = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var r90 in doc.Reg90)
        {
            if (doc.Reg10 is { } r10Header && (r90.Cnpj != r10Header.Cnpj || r90.Ie != r10Header.Ie))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-REL-009", Severity.Error, r90.Line, 3, 30, "90", "CNPJ / IE do Registro 90",
                    "CNPJ ou Inscricao Estadual do Registro 90 diverge do Registro 10.",
                    $"{r90.Cnpj} / {r90.Ie}", $"{r10Header.Cnpj} / {r10Header.Ie}", true));
            }
            foreach (var c in r90.Counters)
            {
                declaredCounts90[c.TipoATotalizar] = c.TotalDeclarado;
                if (c.TotalDeclarado == 0 && c.TipoATotalizar != "99")
                {
                    diagnostics.Add(new Diagnostic(
                        "SIN-REL-011", Severity.Warning, r90.Line, 31, 120, "90",
                        $"Totalizador Zerado Tipo {c.TipoATotalizar}",
                        $"Totalizador do Registro {c.TipoATotalizar} no Registro 90 esta zerado (00000000). O Convenio ICMS 57/95 (regra 2192) nao permite totalizadores zerados.",
                        $"{c.TipoATotalizar}00000000", "Remover totalizadores zerados via Auto-Fix", true));
                }
            }
        }

        int r90RefLine = doc.Reg90.FirstOrDefault()?.Line ?? Math.Max(1, doc.Lines.Count);
        foreach (var (recType, physCount) in doc.PhysicalCounts)
        {
            if (recType is "10" or "11" or "90")
            {
                if (declaredCounts90.TryGetValue(recType, out long declOpt) && declOpt != physCount)
                {
                    diagnostics.Add(new Diagnostic(
                        "SIN-REL-009", Severity.Error, r90RefLine, 31, 120, "90",
                        $"Totalizador Tipo {recType}",
                        $"Totalizador do Registro {recType} no Registro 90 declara {declOpt}, mas existem {physCount} linhas fisicas.",
                        declOpt.ToString(), physCount.ToString(), true));
                }
                continue;
            }

            if (!declaredCounts90.TryGetValue(recType, out long decl))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-REL-009", Severity.Error, r90RefLine, 31, 120, "90",
                    $"Totalizador Tipo {recType}",
                    $"Falta o totalizador do Registro {recType} no Registro 90 ({physCount} linhas presentes no arquivo).",
                    "Ausente", $"{recType}{physCount:00000000}", true));
            }
            else if (decl != physCount)
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-REL-009", Severity.Error, r90RefLine, 31, 120, "90",
                    $"Totalizador Tipo {recType}",
                    $"Totalizador do Registro {recType} no Registro 90 declara {decl}, mas existem {physCount} linhas fisicas.",
                    decl.ToString(), physCount.ToString(), true));
            }
        }

        long totalPhysicalLines = doc.Lines.Count;
        if (doc.Reg90.Count > 0)
        {
            if (!declaredCounts90.TryGetValue("99", out long decl99))
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-REL-009", Severity.Error, doc.Reg90[^1].Line, 31, 120, "90", "Total Geral (99)",
                    "Totalizador geral '99' ausente no Registro 90.",
                    "Ausente", $"99{totalPhysicalLines:00000000}", true));
            }
            else if (decl99 != totalPhysicalLines)
            {
                diagnostics.Add(new Diagnostic(
                    "SIN-REL-009", Severity.Error, doc.Reg90[^1].Line, 31, 120, "90", "Total Geral (99)",
                    $"Total geral (99) no Registro 90 declara {decl99}, mas o arquivo possui {totalPhysicalLines} linhas.",
                    decl99.ToString(), totalPhysicalLines.ToString(), true));
            }
        }

        // 8. Sequence gap check on own-issuance invoices
        var ownNumbers = new SortedDictionary<string, SortedSet<long>>(StringComparer.Ordinal);
        foreach (var r50 in doc.Reg50)
        {
            if (r50.Emitente == "P" && long.TryParse(r50.Numero.Trim(), out long n) && n > 0)
            {
                string key = $"{r50.Modelo}|{r50.Serie}";
                if (!ownNumbers.TryGetValue(key, out var set))
                {
                    set = new SortedSet<long>();
                    ownNumbers[key] = set;
                }
                set.Add(n);
            }
        }

        var sequenceGaps = new List<SequenceGap>();
        foreach (var (key, set) in ownNumbers)
        {
            var parts = key.Split('|');
            var arr = set.ToArray();
            for (int i = 0; i + 1 < arr.Length; i++)
            {
                if (arr[i + 1] > arr[i] + 1)
                {
                    long missing = arr[i + 1] - arr[i] - 1;
                    sequenceGaps.Add(new SequenceGap(parts[0], parts[1], arr[i] + 1, arr[i + 1] - 1, missing));
                    diagnostics.Add(new Diagnostic(
                        "SIN-REL-010", Severity.Info, 1, 46, 51, "50", "Sequencia de Numeracao Propria",
                        $"Salto de numeracao em notas de emissao propria (Mod {parts[0]} Serie '{parts[1]}'): {missing} nota(s) ausente(s) entre {arr[i] + 1} e {arr[i + 1] - 1}.",
                        $"{arr[i]} -> {arr[i + 1]}", "Conferir inutilizacao ou omissao", false));
                }
            }
        }

        // 9. CFOP Summary Table & Monthly Balance (Entradas vs Saidas)
        var cfopMap = new SortedDictionary<string, CfopSummaryRow>(StringComparer.Ordinal);
        long vcEntradas = 0, vcSaidas = 0, icmsEntradas = 0, icmsSaidas = 0;
        long comprasEntradasTotal = 0, outrasEntradasTotal = 0;
        long saidasReg50Total = 0, saidasReg61Total = 0, saidasReg60MTotal = 0;

        var monthBuckets = new SortedDictionary<string, MonthBucket>(StringComparer.Ordinal);
        if (TaxIdValidator.ValidateDateYyyyMmDd(r10Di) && TaxIdValidator.ValidateDateYyyyMmDd(r10Df))
        {
            int y1 = int.Parse(r10Di[..4]), m1 = int.Parse(r10Di[4..6]);
            int y2 = int.Parse(r10Df[..4]), m2 = int.Parse(r10Df[4..6]);
            int totalMonths = (y2 - y1) * 12 + (m2 - m1) + 1;
            if (totalMonths is >= 1 and <= 120)
            {
                int cy = y1, cm = m1;
                for (int k = 0; k < totalMonths; k++)
                {
                    string ym = $"{cy:0000}{cm:00}";
                    monthBuckets[ym] = new MonthBucket();
                    cm++;
                    if (cm > 12) { cm = 1; cy++; }
                }
            }
        }

        MonthBucket GetMonth(string dateYyyymmdd)
        {
            string ym = (dateYyyymmdd.Length >= 6 && dateYyyymmdd[..6].All(char.IsAsciiDigit))
                ? dateYyyymmdd[..6]
                : "000000";
            if (!monthBuckets.TryGetValue(ym, out var b))
            {
                b = new MonthBucket();
                monthBuckets[ym] = b;
            }
            return b;
        }

        CfopSummaryRow GetOrAddCfop(string cfop)
        {
            if (!cfopMap.TryGetValue(cfop, out var row))
            {
                row = new CfopSummaryRow { Cfop = cfop, Nature = TaxIdValidator.CfopNature(cfop) };
                cfopMap[cfop] = row;
            }
            return row;
        }

        foreach (var r50 in doc.Reg50)
        {
            if (r50.Situacao == "S") continue;
            var row = GetOrAddCfop(r50.Cfop);
            row.DocumentCount++;
            row.ValorContabilCents += r50.ValorTotalCents;
            row.BaseIcmsCents += r50.BaseIcmsCents;
            row.ValorIcmsCents += r50.ValorIcmsCents;
            row.IsentaCents += r50.IsentaNaoTribCents;
            row.OutrasCents += r50.OutrasCents;

            var mb = GetMonth(r50.DataEmissao);
            if (TaxIdValidator.IsEntradaCfop(r50.Cfop))
            {
                vcEntradas += r50.ValorTotalCents;
                icmsEntradas += r50.ValorIcmsCents;
                mb.Reg50EntradasDocs++;
                mb.IcmsEntradasCents += r50.ValorIcmsCents;
                if (TaxIdValidator.IsCompraComercialCfop(r50.Cfop))
                {
                    comprasEntradasTotal += r50.ValorTotalCents;
                    mb.ComprasEntradasCents += r50.ValorTotalCents;
                }
                else
                {
                    outrasEntradasTotal += r50.ValorTotalCents;
                    mb.OutrasEntradasCents += r50.ValorTotalCents;
                }
            }
            else
            {
                vcSaidas += r50.ValorTotalCents;
                icmsSaidas += r50.ValorIcmsCents;
                saidasReg50Total += r50.ValorTotalCents;
                mb.Reg50SaidasDocs++;
                mb.SaidasReg50Cents += r50.ValorTotalCents;
                mb.IcmsSaidasCents += r50.ValorIcmsCents;
            }
        }

        foreach (var r51 in doc.Reg51)
        {
            if (r51.Situacao == "S") continue;
            GetOrAddCfop(r51.Cfop).ValorIpiCents += r51.ValorIpiCents;
        }

        foreach (var r53 in doc.Reg53)
        {
            if (r53.Situacao == "S") continue;
            var row = GetOrAddCfop(r53.Cfop);
            row.BaseStCents += r53.BaseStCents;
            row.IcmsRetidoCents += r53.IcmsRetidoCents;
        }

        foreach (var r61 in doc.Reg61)
        {
            vcSaidas += r61.ValorTotalCents;
            icmsSaidas += r61.ValorIcmsCents;
            saidasReg61Total += r61.ValorTotalCents;

            var mb = GetMonth(r61.DataEmissao);
            mb.Reg61SaidasLines++;
            mb.SaidasReg61Cents += r61.ValorTotalCents;
            mb.IcmsSaidasCents += r61.ValorIcmsCents;
            if (long.TryParse(r61.NumInicialOrdem.Trim(), out long ini) &&
                long.TryParse(r61.NumFinalOrdem.Trim(), out long fin) &&
                fin >= ini)
            {
                mb.Nfce61QtyDocs += (fin - ini + 1);
            }
            else
            {
                mb.Nfce61QtyDocs++;
            }
        }

        foreach (var r60m in doc.Reg60M)
        {
            vcSaidas += r60m.VendaBrutaCents;
            saidasReg60MTotal += r60m.VendaBrutaCents;
            var mb = GetMonth(r60m.DataEmissao);
            mb.SaidasReg60MCents += r60m.VendaBrutaCents;
        }

        foreach (var r70 in doc.Reg70)
        {
            if (r70.Situacao == "S") continue;
            var row = GetOrAddCfop(r70.Cfop);
            row.DocumentCount++;
            row.ValorContabilCents += r70.ValorTotalCents;
            row.BaseIcmsCents += r70.BaseIcmsCents;
            row.ValorIcmsCents += r70.ValorIcmsCents;
            row.IsentaCents += r70.IsentaCents;
            row.OutrasCents += r70.OutrasCents;

            var mb = GetMonth(r70.DataEmissao);
            if (TaxIdValidator.IsEntradaCfop(r70.Cfop))
            {
                vcEntradas += r70.ValorTotalCents;
                icmsEntradas += r70.ValorIcmsCents;
                outrasEntradasTotal += r70.ValorTotalCents;
                mb.OutrasEntradasCents += r70.ValorTotalCents;
                mb.IcmsEntradasCents += r70.ValorIcmsCents;
            }
            else
            {
                vcSaidas += r70.ValorTotalCents;
                icmsSaidas += r70.ValorIcmsCents;
                saidasReg50Total += r70.ValorTotalCents;
                mb.SaidasReg50Cents += r70.ValorTotalCents;
                mb.IcmsSaidasCents += r70.ValorIcmsCents;
            }
        }

        var monthlyRows = new List<MonthlyBalanceRow>(monthBuckets.Count);
        foreach (var (ym, mb) in monthBuckets)
        {
            if (ym == "000000" && mb.Reg50EntradasDocs == 0 && mb.Reg50SaidasDocs == 0 && mb.Reg61SaidasLines == 0)
                continue;
            long totEnt = mb.ComprasEntradasCents + mb.OutrasEntradasCents;
            long totSai = mb.SaidasReg50Cents + mb.SaidasReg61Cents + mb.SaidasReg60MCents;
            long saldo = totSai - totEnt;
            double? pct = totEnt > 0 ? Math.Round(totSai * 100.0 / totEnt, 1) : null;

            monthlyRows.Add(new MonthlyBalanceRow(
                ym,
                TaxIdValidator.FormatMonthLabel(ym),
                mb.Reg50EntradasDocs,
                mb.ComprasEntradasCents,
                mb.OutrasEntradasCents,
                totEnt,
                mb.IcmsEntradasCents,
                mb.Reg50SaidasDocs,
                mb.SaidasReg50Cents,
                mb.Reg61SaidasLines,
                mb.Nfce61QtyDocs,
                mb.SaidasReg61Cents,
                mb.SaidasReg60MCents,
                totSai,
                mb.IcmsSaidasCents,
                saldo,
                pct));
        }

        int distinctMonths = Math.Max(1, monthlyRows.Count);
        int errCount = diagnostics.Count(d => d.Severity == Severity.Error);
        int warnCount = diagnostics.Count(d => d.Severity == Severity.Warning);
        int infoCount = diagnostics.Count(d => d.Severity == Severity.Info);

        return new ValidationReport(
            new ValidationSummary(
                errCount == 0,
                errCount,
                warnCount,
                infoCount,
                doc.Lines.Count,
                doc.ByteSize,
                doc.Crc32Hex,
                doc.Sha256Hex,
                doc.Reg50.Count,
                doc.Reg54.Count,
                doc.Reg75.Count,
                doc.Reg70.Count,
                doc.Reg61.Count,
                doc.Reg61R.Count,
                isMultiMonth || distinctMonths > 1,
                distinctMonths,
                comprasEntradasTotal,
                outrasEntradasTotal,
                vcEntradas,
                saidasReg50Total,
                saidasReg61Total,
                saidasReg60MTotal,
                vcSaidas,
                icmsEntradas,
                icmsSaidas
            ),
            doc.Reg10,
            doc.Reg11,
            diagnostics,
            cfopMap.Values.ToList(),
            monthlyRows,
            sequenceGaps,
            doc.PhysicalCounts,
            declaredCounts90,
            productMovementCounts
        );
    }

    private sealed class MonthBucket
    {
        public int Reg50EntradasDocs;
        public long ComprasEntradasCents;
        public long OutrasEntradasCents;
        public long IcmsEntradasCents;
        public int Reg50SaidasDocs;
        public long SaidasReg50Cents;
        public int Reg61SaidasLines;
        public long Nfce61QtyDocs;
        public long SaidasReg61Cents;
        public long SaidasReg60MCents;
        public long IcmsSaidasCents;
    }

    private static int CountMonthsInclusive(string diYyyyMmDd, string dfYyyyMmDd)
    {
        int y1 = int.Parse(diYyyyMmDd[..4]), m1 = int.Parse(diYyyyMmDd[4..6]);
        int y2 = int.Parse(dfYyyyMmDd[..4]), m2 = int.Parse(dfYyyyMmDd[4..6]);
        return Math.Max(1, (y2 - y1) * 12 + (m2 - m1) + 1);
    }
}
