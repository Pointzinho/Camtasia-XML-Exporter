using System;
using System.IO;
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

            // Movido para fora do try para poder ser usado no catch também
            string responseFilePath = string.Empty;

            try
            {
                string requestJsonText = File.ReadAllText(requestPath, Encoding.UTF8);

                using (JsonDocument doc = JsonDocument.Parse(requestJsonText))
                {
                    var root = doc.RootElement;
                    responseFilePath = root.GetProperty("responseFilePath").GetString() ?? string.Empty;

                    // Pega a legenda atual formatada como SubRip (SRT)
                    string srtSubtitle = root.GetProperty("subtitle").GetProperty("subRip").GetString() ?? string.Empty;

                    // Pega a pasta temporária ou o diretório do arquivo original enviado pelo Subtitle Edit
                    string subtitleFileName = root.TryGetProperty("subtitle", out var subProp) && subProp.TryGetProperty("fileName", out var fnProp)
                        ? fnProp.GetString() ?? string.Empty
                        : string.Empty;

                    // Tenta achar o _config.xml na mesma pasta do arquivo de legenda
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

                    // Se não encontrou o XML correspondente, informa erro no response
                    if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
                    {
                        EscreverErro(responseFilePath, "Não foi possível localizar o arquivo _config.xml na mesma pasta da legenda.");
                        return;
                    }

                    // Processa e atualiza as tags no XML do Camtasia
                    AtualizarXmlCamtasia(xmlPath, srtSubtitle);

                    // Devolve a resposta OK para o Subtitle Edit 5
                    string okResponse = "{\"apiVersion\":1,\"status\":\"ok\",\"message\":\"XML do Camtasia atualizado com sucesso!\"}";
                    File.WriteAllText(responseFilePath, okResponse, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                // Em caso de exceção, envia a mensagem para o Subtitle Edit exibir,
                // em vez de deixar o response.json ausente.
                EscreverErro(responseFilePath, ex.Message);
            }
        }

        private static void EscreverErro(string responseFilePath, string mensagem)
        {
            if (string.IsNullOrEmpty(responseFilePath))
                return;

            try
            {
                string errJson = JsonSerializer.Serialize(new
                {
                    apiVersion = 1,
                    status = "error",
                    message = mensagem
                });
                File.WriteAllText(responseFilePath, errJson, Encoding.UTF8);
            }
            catch
            {
                // Último recurso: se nem isso funcionar, não há mais nada a fazer.
            }
        }

        private static void AtualizarXmlCamtasia(string xmlPath, string srtText)
        {
            string xmlConteudo = File.ReadAllText(xmlPath, Encoding.UTF8);

            // Transforma o SRT e gera os marcadores em RTF exigidos pelo Camtasia Smart Player
            var blocos = Regex.Matches(srtText, @"(\d+)\r?\n(\d{2}:\d{2}:\d{2}[\.,]\d{3}) --> (\d{2}:\d{2}:\d{2}[\.,]\d{3})\r?\n([\s\S]*?)(?=\r?\n\r?\n|\Z)");

            var novosMarkers = new StringBuilder();
            foreach (Match match in blocos)
            {
                long startMs = SrtParaMs(match.Groups[2].Value);
                long endMs = SrtParaMs(match.Groups[3].Value);
                long durationMs = endMs - startMs;

                string textoLegenda = match.Groups[4].Value.Trim();
                string[] linhas = textoLegenda.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                string rtf = @"{\rtf1 " + string.Join(@"\par ", linhas) + "}";

                novosMarkers.AppendLine($"                           <rdf:li><rdf:Description xmpDM:duration=\"{durationMs}\" xmpDM:startTime=\"{startMs}\" tscDM:valign=\"bottom\" tscDM:halign=\"center\"><xmpDM:name><rdf:Alt><rdf:li xml:lang=\"pt-BR\">{rtf}</rdf:li></rdf:Alt></xmpDM:name></rdf:Description></rdf:li>");
            }

            string padraoRegex = @"(<xmpDM:trackName=""Captioning""[^>]*>\s*<xmpDM:markers>\s*<rdf:Seq>)([\s\S]*?)(</rdf:Seq>)";
            string xmlSubstituido = Regex.Replace(xmlConteudo, padraoRegex, $"${{1}}\n{novosMarkers.ToString()}                           ${{3}}");

            File.WriteAllText(xmlPath, xmlSubstituido, Encoding.UTF8);
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