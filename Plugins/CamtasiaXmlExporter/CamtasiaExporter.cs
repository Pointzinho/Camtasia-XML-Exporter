using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CamtasiaXmlExporter
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0) return;

            string requestPath = args[0];
            if (!File.Exists(requestPath)) return;

            string responseFilePath = string.Empty;

            try
            {
                string requestJsonText = File.ReadAllText(requestPath, Encoding.UTF8);

                using (JsonDocument doc = JsonDocument.Parse(requestJsonText))
                {
                    var root = doc.RootElement;
                    responseFilePath = root.GetProperty("responseFilePath").GetString() ?? string.Empty;

                    string srtSubtitle = root.GetProperty("subtitle").GetProperty("subRip").GetString() ?? string.Empty;

                    string subtitleFileName = root.TryGetProperty("subtitle", out var subProp) && subProp.TryGetProperty("fileName", out var fnProp)
                        ? fnProp.GetString() ?? string.Empty
                        : string.Empty;

                    string xmlPath = string.Empty;
                    if (!string.IsNullOrEmpty(subtitleFileName))
                    {
                        string? directory = Path.GetDirectoryName(subtitleFileName);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            string potentialXml = Path.Combine(directory, Path.GetFileNameWithoutExtension(subtitleFileName) + "_config.xml");
                            if (File.Exists(potentialXml))
                            {
                                xmlPath = potentialXml;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
                    {
                        EscreverErro(responseFilePath, "Não foi possível localizar o arquivo _config.xml na mesma pasta da legenda. Verifique se o nome do .srt é igual ao do vídeo.");
                        return;
                    }

                    // Injeta as legendas e ativa os botões CC
                    AtualizarXmlCamtasia(xmlPath, srtSubtitle);

                    string okResponse = "{\"apiVersion\":1,\"status\":\"ok\",\"message\":\"XML e Player do Camtasia atualizados com sucesso!\"}";
                    File.WriteAllText(responseFilePath, okResponse, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                EscreverErro(responseFilePath, ex.Message);
            }
        }

        private static void EscreverErro(string responseFilePath, string mensagem)
        {
            if (string.IsNullOrEmpty(responseFilePath)) return;

            try
            {
                string errJson = JsonSerializer.Serialize(new { apiVersion = 1, status = "error", message = mensagem });
                File.WriteAllText(responseFilePath, errJson, Encoding.UTF8);
            }
            catch { }
        }

        private static void AtualizarXmlCamtasia(string xmlPath, string srtText)
        {
            string xmlConteudo = File.ReadAllText(xmlPath, Encoding.UTF8);

            var blocos = Regex.Matches(srtText, @"(\d+)\r?\n(\d{2}:\d{2}:\d{2}[\.,]\d{3}) --> (\d{2}:\d{2}:\d{2}[\.,]\d{3})\r?\n([\s\S]*?)(?=\r?\n\r?\n|\Z)");

            var novosMarkers = new StringBuilder();
            foreach (Match match in blocos)
            {
                long startMs = SrtParaMs(match.Groups[2].Value);
                long endMs = SrtParaMs(match.Groups[3].Value);
                long durationMs = endMs - startMs;

                string textoLegenda = match.Groups[4].Value.Trim();
                string[] linhas = textoLegenda.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                string[] linhasEscapadas = linhas.Select(EscaparLinhaParaRtfXml).ToArray();
                string rtf = @"{\rtf1 " + string.Join(@"\par ", linhasEscapadas) + "}";

                novosMarkers.AppendLine($"                           <rdf:li><rdf:Description xmpDM:duration=\"{durationMs}\" xmpDM:startTime=\"{startMs}\" tscDM:valign=\"bottom\" tscDM:halign=\"center\"><xmpDM:name><rdf:Alt><rdf:li xml:lang=\"pt-BR\">{rtf}</rdf:li></rdf:Alt></xmpDM:name></rdf:Description></rdf:li>");
            }

            if (!xmlConteudo.Contains("Captioning"))
            {
                string estruturaCaptioning = @"               <rdf:li>
                  <rdf:Description xmpDM:trackType=""Caption"" xmpDM:frameRate=""f1000"" xmpDM:trackName=""Captioning"" stFnt:fontFamily=""Arial"" tscDM:fontSize=""42"" tscDM:bgOpacity=""0.750000"" tscDM:position=""overlay"">
                     <xmpDM:markers>
                        <rdf:Seq>
                        </rdf:Seq>
                     </xmpDM:markers>
                     <tsc:fgColor xmpG:red=""255"" xmpG:green=""255"" xmpG:blue=""255""/>
                     <tsc:bgColor xmpG:red=""0"" xmpG:green=""0"" xmpG:blue=""0""/>
                  </rdf:Description>
               </rdf:li>";

                xmlConteudo = Regex.Replace(xmlConteudo, @"(<xmpDM:Tracks>\s*<rdf:Bag>)", $"${{1}}\n{estruturaCaptioning}");
            }

            // CORREÇÃO: O sinal de '<' foi removido antes de xmpDM:trackName para encontrar a tag corretamente!
            string padraoRegex = @"(xmpDM:trackName=""Captioning""[^>]*>\s*<xmpDM:markers>\s*<rdf:Seq>)([\s\S]*?)(</rdf:Seq>)";
            string xmlSubstituido = Regex.Replace(xmlConteudo, padraoRegex, $"${{1}}\n{novosMarkers.ToString()}                           ${{3}}");

            // REPARO IDEMPOTENTE: se o bloco "Captioning" já existia (de uma execução
            // anterior do plugin, antes desta correção) sem as tags tsc:fgColor/tsc:bgColor,
            // o TechSmith Smart Player quebra ao tentar ler essas tags ("Cannot read
            // properties of undefined (reading 'getAttribute')"). Insere as tags se
            // estiverem faltando. Não faz nada se elas já existirem (evita duplicar).
            string padraoCorTagsFaltando = @"(trackName=""Captioning""[\s\S]*?</xmpDM:markers>)\s*</rdf:Description>";
            if (Regex.IsMatch(xmlSubstituido, padraoCorTagsFaltando))
            {
                xmlSubstituido = Regex.Replace(
                    xmlSubstituido,
                    padraoCorTagsFaltando,
                    "$1\r\n                     <tsc:fgColor xmpG:red=\"255\" xmpG:green=\"255\" xmpG:blue=\"255\"/>\r\n                     <tsc:bgColor xmpG:red=\"0\" xmpG:green=\"0\" xmpG:blue=\"0\"/>\r\n                  </rdf:Description>"
                );
            }

            // ATIVA A LEGENDA NO XML
            xmlSubstituido = Regex.Replace(xmlSubstituido, @"(<rdf:li xmpDM:name=""captionsenabled"" xmpDM:value="")[^""]*("")", "${1}true${2}", RegexOptions.IgnoreCase);

            File.WriteAllText(xmlPath, xmlSubstituido, new UTF8Encoding(false));

            // ATIVA A LEGENDA NO HTML
            AtivarLegendasNoHtml(xmlPath);
        }

        private static void AtivarLegendasNoHtml(string xmlPath)
        {
            string? pasta = Path.GetDirectoryName(xmlPath);
            if (string.IsNullOrEmpty(pasta) || !Directory.Exists(pasta)) return;

            string[] arquivosHtml = Directory.GetFiles(pasta, "*.*")
                .Where(f => f.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".php", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var utf8SemBom = new UTF8Encoding(false);

            foreach (var arquivo in arquivosHtml)
            {
                string conteudo = File.ReadAllText(arquivo, Encoding.UTF8);

                if (conteudo.Contains("setCaptionsEnabled"))
                {
                    string conteudoAtualizado = Regex.Replace(
                        conteudo, 
                        @"(TSC\.playerConfiguration\.setCaptionsEnabled\()\s*(false|true)\s*(\);)", 
                        "${1}true${3}", 
                        RegexOptions.IgnoreCase
                    );

                    // Só regrava o arquivo se algo realmente mudou. Isso evita
                    // reescrever (e potencialmente introduzir BOM/mudar encoding)
                    // um HTML que já estava com a legenda ativada.
                    if (conteudoAtualizado != conteudo)
                    {
                        // IMPORTANTE: usar UTF8 SEM BOM. "Encoding.UTF8" do .NET
                        // grava um BOM no início do arquivo, o que pode fazer o
                        // Smart Player / Moodle tratar o HTML como corrompido.
                        File.WriteAllText(arquivo, conteudoAtualizado, utf8SemBom);
                    }
                }
            }
        }

        /// <summary>
        /// Converte uma linha de texto (possivelmente com acentuação, "&amp;", etc.)
        /// em uma string segura para ser inserida DENTRO de um bloco RTF que por sua vez
        /// está dentro de um elemento XML.
        ///
        /// Por que isso é necessário:
        /// - O _config.xml do Camtasia normalmente é gerado com a flag
        ///   xmpDM:name="unicodeenabled" value="false". Isso indica que o Smart Player
        ///   NÃO espera caracteres Unicode "crus" (UTF-8 multi-byte) dentro do bloco RTF.
        ///   O padrão RTF para caracteres fora do ASCII é o control word "\uN" (código
        ///   Unicode em decimal) seguido de UM caractere de fallback ASCII, ex: "é" -> "\u233?".
        ///   Injetar "é" literal (byte cru) onde o player espera "\u233?" pode quebrar o
        ///   parser RTF interno do Smart Player, derrubando a inicialização inteira do
        ///   player — o que se manifesta como o erro genérico "problema no acesso a
        ///   recursos deste vídeo".
        /// - Também escapamos '&amp;', '&lt;', '&gt;' porque o texto vai direto para dentro
        ///   de um elemento XML (não está em CDATA), e '\', '{', '}' porque são caracteres
        ///   de controle do próprio RTF.
        /// </summary>
        private static string EscaparLinhaParaRtfXml(string linha)
        {
            var sb = new StringBuilder();
            foreach (char c in linha)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append(@"\\");
                        continue;
                    case '{':
                        sb.Append(@"\{");
                        continue;
                    case '}':
                        sb.Append(@"\}");
                        continue;
                    case '&':
                        sb.Append("&amp;");
                        continue;
                    case '<':
                        sb.Append("&lt;");
                        continue;
                    case '>':
                        sb.Append("&gt;");
                        continue;
                }

                if (c < 0x20)
                {
                    // Caracteres de controle não imprimíveis: descarta.
                    continue;
                }

                if (c > 0x7E)
                {
                    // Fora do ASCII imprimível (acentos, cedilha, "–", "…", etc.)
                    // Codifica como control word RTF \uN seguido de fallback "?".
                    int codigoUnicode = (short)c; // RTF exige inteiro decimal com sinal (16 bits)
                    sb.Append('\\').Append('u').Append(codigoUnicode).Append('?');
                    continue;
                }

                sb.Append(c);
            }
            return sb.ToString();
        }

        private static long SrtParaMs(string tempoStr)
        {
            tempoStr = tempoStr.Replace(',', '.');
            string[] partes = tempoStr.Split(':');
            string[] segsMs = partes[2].Split('.');

            int h = int.Parse(partes[0]);
            int m = int.Parse(partes[1]);
            int s = int.Parse(segsMs[0]);
            int ms = int.Parse(segsMs[1]);

            return ((h * 3600) + (m * 60) + s) * 1000 + ms;
        }
    }
}