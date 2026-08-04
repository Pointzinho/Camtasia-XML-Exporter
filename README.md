# Exportar para Camtasia SCORM XML

Plugin para o [Subtitle Edit 5](https://github.com/SubtitleEdit/subtitleedit) que injeta legendas `.srt` diretamente no arquivo `_config.xml` gerado por pacotes SCORM do **Camtasia Smart Player**, permitindo adicionar legendas a aulas gravadas que foram exportadas sem elas.

## O que ele faz

1. Lê o arquivo `.srt` aberto no Subtitle Edit.
2. Localiza o `_config.xml` correspondente (mesma pasta, mesmo nome do vídeo).
3. Converte cada bloco do `.srt` em um marcador de legenda no formato RTF/XMP que o Camtasia Smart Player espera.
4. Insere esses marcadores dentro da track `Captioning` do XML (criando a track se ela não existir).
5. Ativa a exibição de legendas (`captionsenabled="true"`) no XML e no `_player.html`.

## Instalação

1. Baixe o `.exe` mais recente da pasta [`CamtasiaXmlExporter/`](./CamtasiaXmlExporter) (ou compile a partir do código-fonte — veja abaixo).
2. Copie `CamtasiaXmlExporter.exe` e `plugin.json` para:
   ```
   C:\SubtitleEdit\Plugins\CamtasiaXmlExporter\
   ```
3. Reinicie o Subtitle Edit. O plugin aparece no menu **File**.

## Como usar

1. Abra o `.srt` da aula no Subtitle Edit (o nome do arquivo deve corresponder ao nome do vídeo Camtasia — ex.: `Aula_01.srt` para `Aula_01_config.xml`).
2. Vá em **File → Exportar para Camtasia SCORM XML**.
3. O plugin atualiza o `_config.xml` e o `_player.html` na mesma pasta.
4. Reempacote a pasta SCORM (`.zip`) e suba/atualize no Moodle (ou outro LMS compatível com SCORM 1.3).

> O plugin regenera **todos** os marcadores de legenda a cada execução — pode rodar de novo sobre um `_config.xml` já processado sem precisar limpar nada manualmente.

## Compilar a partir do código-fonte

Requer [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
cd CamtasiaXmlExporter
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

Isso gera um executável standalone (`CamtasiaXmlExporter.exe`) que **não depende** do .NET estar instalado na máquina de destino — por isso o `plugin.json` usa `"executables"` em vez de `"runtime": "dotnet"` (veja [Notas técnicas](#notas-técnicas) abaixo).

## Estrutura do `plugin.json`

```json
{
  "apiVersion": 1,
  "name": "Exportar para Camtasia SCORM XML",
  "description": "Atualiza as legendas diretamente no arquivo _config.xml do Camtasia Smart Player.",
  "version": "1.0.0",
  "author": "Natan",
  "menu": "File",
  "minSeVersion": "5.0.0",
  "executables": {
    "windows": "CamtasiaXmlExporter.exe"
  }
}
```

## Notas técnicas

Detalhes que custaram para descobrir e que vale registrar caso o plugin precise de manutenção futura:

- **`"executables"` vs `"runtime": "dotnet"`**: o Subtitle Edit 5 tem dois formatos de manifesto de plugin externo. Com `"runtime": "dotnet"`, o SE literalmente executa o comando `dotnet <entry>` — e falha com *"An error occurred trying to start process 'dotnet'..."* se o SDK/runtime do .NET não estiver instalado e no PATH da máquina. Para um `.exe` self-contained (publicado com `--self-contained true -p:PublishSingleFile=true`), use `"executables": { "windows": "NomeDoExe.exe" }` em vez disso.

- **Acentuação**: o texto das legendas deve ser gravado como **UTF-8 puro**, sem escapar caracteres acentuados no formato RTF (`\uN?`). O parser de RTF embutido no TechSmith Smart Player não interpreta esse control word — ele imprime o código literalmente na tela (`\u234?` em vez de "ê"). Apenas `&`, `<`, `>` (reservados em XML) e `\`, `{`, `}` (reservados em RTF) precisam de escape.

- **`tsc:fgColor` / `tsc:bgColor` obrigatórios**: todo bloco de track `Captioning` no `_config.xml` precisa terminar com:
  ```xml
  </xmpDM:markers>
  <tsc:fgColor xmpG:red="255" xmpG:green="255" xmpG:blue="255"/>
  <tsc:bgColor xmpG:red="0" xmpG:green="0" xmpG:blue="0"/>
  </rdf:Description>
  ```
  Sem essas duas tags, o Smart Player quebra ao carregar o vídeo com o erro genérico "Parece que há um problema no acesso a certos recursos deste vídeo", e no console do navegador aparece:
  ```
  Cannot read properties of undefined (reading 'getAttribute')
    at ... addCaptionTrackFromXmpElement
  ```
  O plugin insere essas tags automaticamente, tanto ao criar uma track nova quanto ao reparar um `_config.xml` já processado por uma versão anterior do plugin que não as incluía.

- **Sem BOM nos arquivos regravados**: tanto o `_config.xml` quanto o `_player.html` são gravados com `UTF8Encoding(false)` (UTF-8 sem *byte order mark*). `Encoding.UTF8` do .NET grava BOM por padrão, o que pode fazer o player tratar o arquivo como corrompido.

- O `_player.html` só é regravado se o valor de `setCaptionsEnabled` realmente mudar — evita reescritas desnecessárias do arquivo.

## Requisitos

- Subtitle Edit 5.0.0 ou superior.
- Windows (x64). O executável é publicado como self-contained e não requer o .NET instalado separadamente.

## Estrutura do repositório

```
CamtasiaXmlExporter/
├── CamtasiaExporter.cs        # Código-fonte
├── CamtasiaXmlExporter.csproj # Projeto .NET 8
├── plugin.json                # Manifesto do plugin (Subtitle Edit 5)
└── CamtasiaXmlExporter.exe    # Build compilada (self-contained)
```

## Licença

Este projeto está licenciado sob a Licença MIT - consulte o arquivo [LICENSE](LICENSE) para mais detalhes.
