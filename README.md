# Exportar para Camtasia SCORM XML

Plugin para o [Subtitle Edit 5](https://github.com/SubtitleEdit/subtitleedit) que injeta legendas `.srt` diretamente no arquivo `_config.xml` gerado por pacotes SCORM do **Camtasia Smart Player**, permitindo adicionar legendas a aulas gravadas que foram exportadas sem elas[cite: 5].

## O que ele faz

1. Lê o arquivo `.srt` aberto no Subtitle Edit[cite: 5].
2. Localiza o `_config.xml` correspondente (procurando pelo mesmo nome do vídeo, ou fazendo uma varredura inteligente na pasta caso o nome difira).
3. **Escala de Fonte Dinâmica:** Identifica a resolução nativa do vídeo (720p, 1080p, 1440p, 2160p) e calcula o ajuste proporcional do tamanho da fonte da legenda.
4. Converte cada bloco do `.srt` em um marcador de legenda no formato RTF/XMP que o Camtasia Smart Player espera[cite: 5].
5. Insere esses marcadores dentro da track `Captioning` do XML (criando a track se ela não existir)[cite: 5].
6. Ativa a exibição de legendas (`captionsenabled="true"`) no XML e no `_player.html`[cite: 5].

## Instalação

1. Baixe o pacote `.zip` da versão mais recente na aba **Releases** do repositório.
2. Extraia todo o conteúdo (o executável `CamtasiaXmlExporter.exe`, o `plugin.json` e as bibliotecas `.dll` extraídas) para:
   ```text
   C:\SubtitleEdit\Plugins\CamtasiaXmlExporter\
   ```
3. Reinicie o Subtitle Edit. O plugin aparece no menu **File** ou **Plugins**[cite: 5].

## Como usar

### 1. Configuração Inicial no Subtitle Edit
Antes de usar pela primeira vez, habilite a aba de plugins:
- Vá em **Definições** > **Aspecto**.
- Marque a caixinha **Mostrar menu de plugin**.

### 2. Fluxo de Trabalho (Transcrição e Injeção)
1. **Extraia o pacote:** Descompacte o arquivo `.zip` do seu projeto SCORM exportado do Camtasia.
2. **Importe o vídeo:** Arraste o vídeo `.mp4` que está dentro da pasta do SCORM diretamente para o Subtitle Edit.
3. **Gere a transcrição:** No menu superior, clique em **Vídeo** > **Fala para Texto...**
4. **Configure a Inteligência Artificial:**
   - **Se você tem Placa de Vídeo Dedicada (RTX):**
     - Backend: `cuBLAS`
     - Motor: `Purfview Faster Whisper XXL`
     - Modelo de IA: `large v3 turbo`
   - **Se você NÃO tem Placa de Vídeo (Apenas Processador/CPU):**
     - Backend: `CPU`
     - Motor: `Whisper CPP`
     - Modelo de IA: `ggml-small`
     > ⚠️ **Aviso:** Sem uma placa de vídeo dedicada, o processo de transcrição será consideravelmente mais demorado.
5. Clique em **Transcrever** e aguarde a finalização do processo.
6. **Aplique o Plugin:** Após o texto ser gerado, clique no menu superior em **Plugins** > **CamtasiaXmlExporter** (ou *Exportar para Camtasia SCORM XML*).
   - O plugin verificará a resolução do vídeo. Se for diferente de 1080p, ele perguntará se você deseja ajustar o tamanho da fonte.
7. **Finalização:** Reempacote a pasta SCORM (`.zip`) e suba/atualize no Moodle (ou outro LMS compatível com SCORM 1.3)[cite: 5]. Não é necessário salvar o arquivo `.srt` isoladamente, a injeção já ocorreu diretamente no XML.

> O plugin regenera **todos** os marcadores de legenda a cada execução — pode rodar de novo sobre um `_config.xml` já processado sem precisar limpar nada manualmente[cite: 5].

## Compilar a partir do código-fonte

Requer [.NET 8 SDK](https://dotnet.microsoft.com/download)[cite: 5].

```bash
cd CamtasiaXmlExporter
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

Isso gera um executável standalone (`CamtasiaXmlExporter.exe`) que **não depende** do .NET estar instalado na máquina de destino — por isso o `plugin.json` usa `"executables"` em vez de `"runtime": "dotnet"` (veja Notas técnicas abaixo)[cite: 5].

## Estrutura do `plugin.json`

```json
{
  "apiVersion": 1,
  "name": "Exportar para Camtasia SCORM XML",
  "description": "Atualiza as legendas diretamente no arquivo _config.xml do Camtasia Smart Player.",
  "version": "1.0.3",
  "author": "Natan",
  "menu": "File",
  "minSeVersion": "5.0.0",
  "executables": {
    "windows": "CamtasiaXmlExporter.exe"
  }
}
```

## Notas técnicas

Detalhes que custaram para descobrir e que vale registrar caso o plugin precise de manutenção futura[cite: 5]:

- **`"executables"` vs `"runtime": "dotnet"`**: o Subtitle Edit 5 tem dois formatos de manifesto de plugin externo[cite: 5]. Com `"runtime": "dotnet"`, o SE literalmente executa o comando `dotnet <entry>` — e falha com *"An error occurred trying to start process 'dotnet'..."* se o SDK/runtime do .NET não estiver instalado e no PATH da máquina[cite: 5]. Para um `.exe` self-contained (publicado com `--self-contained true -p:PublishSingleFile=true`), use `"executables": { "windows": "NomeDoExe.exe" }` em vez disso[cite: 5].

- **Acentuação**: o texto das legendas deve ser gravado como **UTF-8 puro**, sem escapar caracteres acentuados no formato RTF (`\uN?`)[cite: 5]. O parser de RTF embutido no TechSmith Smart Player não interpreta esse control word — ele imprime o código literalmente na tela (`\u234?` em vez de "ê")[cite: 5]. Apenas `&`, `<`, `>` (reservados em XML) e `\`, `{`, `}` (reservados em RTF) precisam de escape[cite: 5].

- **`tsc:fgColor` / `tsc:bgColor` obrigatórios**: todo bloco de track `Captioning` no `_config.xml` precisa terminar com[cite: 5]:
  ```xml
  </xmpDM:markers>
  <tsc:fgColor xmpG:red="255" xmpG:green="255" xmpG:blue="255"/>
  <tsc:bgColor xmpG:red="0" xmpG:green="0" xmpG:blue="0"/>
  </rdf:Description>
  ```
  Sem essas duas tags, o Smart Player quebra ao carregar o vídeo com o erro genérico "Parece que há um problema no acesso a certos recursos deste vídeo", e no console do navegador aparece[cite: 5]:
  ```text
  Cannot read properties of undefined (reading 'getAttribute')
    at ... addCaptionTrackFromXmpElement
  ```
  O plugin insere essas tags automaticamente, tanto ao criar uma track nova quanto ao reparar um `_config.xml` já processado por uma versão anterior do plugin que não as incluía[cite: 5].

- **Sem BOM nos arquivos regravados**: tanto o `_config.xml` quanto o `_player.html` são gravados com `UTF8Encoding(false)` (UTF-8 sem *byte order mark*)[cite: 5]. `Encoding.UTF8` do .NET grava BOM por padrão, o que pode fazer o player tratar o arquivo como corrompido[cite: 5].

- O `_player.html` só é regravado se o valor de `setCaptionsEnabled` realmente mudar — evita reescritas desnecessárias do arquivo[cite: 5].

## Requisitos

- Subtitle Edit 5.0.0 ou superior[cite: 5].
- Windows (x64)[cite: 5]. O executável é publicado como self-contained e não requer o .NET instalado separadamente[cite: 5].

## Estrutura do repositório

```text
CamtasiaXmlExporter/
├── CamtasiaExporter.cs        # Código-fonte
├── CamtasiaXmlExporter.csproj # Projeto .NET 8
├── plugin.json                # Manifesto do plugin (Subtitle Edit 5)
└── CamtasiaXmlExporter.exe    # Build compilada (self-contained)
```

## Licença

Este projeto está licenciado sob a Licença MIT - consulte o arquivo [LICENSE](LICENSE) para mais detalhes[cite: 5].
