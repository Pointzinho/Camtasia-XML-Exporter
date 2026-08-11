using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CamtasiaXmlExporter
{
    internal class Program
    {
        [STAThread]
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
                        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                        {
                            // TENTATIVA 1: Busca pelo nome exato do arquivo SRT
                            string expectedXmlName = Path.GetFileNameWithoutExtension(subtitleFileName) + "_config.xml";
                            string potentialXml = Path.Combine(directory, expectedXmlName);

                            if (File.Exists(potentialXml))
                            {
                                xmlPath = potentialXml;
                            }
                            else
                            {
                                // TENTATIVA 2: Varredura inteligente. Se o nome for diferente, 
                                // mas existir APENAS UM arquivo _config.xml na pasta, usa ele!
                                string[] configFiles = Directory.GetFiles(directory, "*_config.xml");
                                if (configFiles.Length == 1)
                                {
                                    xmlPath = configFiles[0];
                                }
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
                    {
                        string erroMsg = "Não foi possível localizar o arquivo _config.xml na pasta.\n\nCertifique-se de que o nome da legenda é igual ao do vídeo, ou que a pasta contenha apenas um pacote SCORM.";
                        EscreverErro(responseFilePath, erroMsg);
                        MessageBox.Show(erroMsg, "Camtasia XML Exporter - Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Injeta as legendas e ativa os botões CC
                    AtualizarXmlCamtasia(xmlPath, srtSubtitle);

                    string okResponse = "{\"apiVersion\":1,\"status\":\"ok\",\"message\":\"XML e Player do Camtasia atualizados com sucesso!\"}";
                    File.WriteAllText(responseFilePath, okResponse, Encoding.UTF8);

                    // POPUP DE SUCESSO
                    MessageBox.Show($"As legendas foram injetadas com sucesso no arquivo:\n\n{Path.GetFileName(xmlPath)}", "Camtasia XML Exporter - Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                EscreverErro(responseFilePath, ex.Message);
                MessageBox.Show($"Ocorreu um erro durante a exportação:\n\n{ex.Message}", "Camtasia XML Exporter - Erro Crítico", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            // --- INÍCIO: Detecção de 720p ---
            bool is720p = Regex.IsMatch(xmlConteudo, @"stDim:h=""720""") || Regex.IsMatch(xmlConteudo, @"stDim:w=""1280""");
            int tamanhoFonte = -1;

            if (is720p)
            {
                DialogResult result = MessageBox.Show(
                    "Detectamos que este vídeo está na resolução 720p.\n\nDeseja definir o tamanho da fonte das legendas em 28 para não ficar muito grande na tela?",
                    "Camtasia XML Exporter - Ajuste de Fonte",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    tamanhoFonte = 28;
                }
            }
            // --- FIM: Detecção de 720p ---

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

            string padraoRegex = @"(xmpDM:trackName=""Captioning""[^>]*>\s*<xmpDM:markers>\s*<rdf:Seq>)([\s\S]*?)(</rdf:Seq>)";
            string xmlSubstituido = Regex.Replace(xmlConteudo, padraoRegex, $"${{1}}\n{novosMarkers.ToString()}                           ${{3}}");

            string padraoCorTagsFaltando = @"(trackName=""Captioning""[\s\S]*?</xmpDM:markers>)\s*</rdf:Description>";
            if (Regex.IsMatch(xmlSubstituido, padraoCorTagsFaltando))
            {
                xmlSubstituido = Regex.Replace(
                    xmlSubstituido,
                    padraoCorTagsFaltando,
                    "$1\r\n                     <tsc:fgColor xmpG:red=\"255\" xmpG:green=\"255\" xmpG:blue=\"255\"/>\r\n                     <tsc:bgColor xmpG:red=\"0\" xmpG:green=\"0\" xmpG:blue=\"0\"/>\r\n                  </rdf:Description>"
                );
            }

            // --- Aplica o tamanho 28 da fonte apenas se o usuário tiver confirmado ---
            if (tamanhoFonte > 0)
            {
                xmlSubstituido = Regex.Replace(xmlSubstituido, @"tscDM:fontSize=""\d+""", $"tscDM:fontSize=\"{tamanhoFonte}\"");
            }

            xmlSubstituido = Regex.Replace(xmlSubstituido, @"(<rdf:li xmpDM:name=""captionsenabled"" xmpDM:value="")[^""]*("")", "${1}true${2}", RegexOptions.IgnoreCase);

            File.WriteAllText(xmlPath, xmlSubstituido, new UTF8Encoding(false));

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

                    if (conteudoAtualizado != conteudo)
                    {
                        File.WriteAllText(arquivo, conteudoAtualizado, utf8SemBom);
                    }
                }
            }
        }

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