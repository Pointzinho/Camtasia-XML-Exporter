using System;
using System.IO;
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
            string requestJsonText = File.ReadAllText(requestPath);

            using (JsonDocument doc = JsonDocument.Parse(requestJsonText))
            {
                var root = doc.RootElement;
                string responseFilePath = root.GetProperty("responseFilePath").GetString();
                
                // Pega a legenda atual em formato SubRip (SRT) enviada pelo Subtitle Edit
                string srtSubtitle = root.GetProperty("subtitle").GetProperty("subRip").GetString();

                // Pede para o usuário escolher o arquivo _config.xml do Camtasia
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "Camtasia Config XML (*_config.xml)|*_config.xml|Arquivos XML (*.xml)|*.xml";
                    openFileDialog.Title = "Selecione o arquivo de configuração do Camtasia";

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            AtualizarXmlCamtasia(openFileDialog.FileName, srtSubtitle);

                            // Escreve a resposta de sucesso exigida pelo Subtitle Edit 5
                            string responseJson = "{\"apiVersion\":1,\"status\":\"ok\",\"message\":\"XML do Camtasia atualizado com sucesso!\"}";
                            File.WriteAllText(responseFilePath, responseJson, Encoding.UTF8);
                        }
                        catch (Exception ex)
                        {
                            string responseError = $"{{\"apiVersion\":1,\"status\":\"error\",\"message\":\"Erro: {ex.Message}\"}}";
                            File.WriteAllText(responseFilePath, responseError, Encoding.UTF8);
                        }
                    }
                    else
                    {
                        // Se o usuário cancelar a seleção do arquivo
                        string responseCancel = "{\"apiVersion\":1,\"status\":\"cancelled\"}";
                        File.WriteAllText(responseFilePath, responseCancel, Encoding.UTF8);
                    }
                }
            }
        }

        private static void AtualizarXmlCamtasia(string xmlPath, string srtText)
        {
            string xmlConteudo = File.ReadAllText(xmlPath, Encoding.UTF8);
            
            // Lógica para converter o SRT e gerar os marcadores <rdf:li> em RTF do Camtasia
            // ... (mesma conversão de tempo e tags que ajustamos anteriormente)
            
            File.WriteAllText(xmlPath, xmlConteudo, Encoding.UTF8);
        }
    }
}