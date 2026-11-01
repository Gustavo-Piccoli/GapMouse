# GapMouse — atualização visual (v1.1)

Status: aprovado por delegação. Gustavo pediu execução totalmente automática ("faça todo o processo
automaticamente sem minha aprovação") usando as skills de design instaladas (impeccable). Todas as escolhas abaixo
são decisões minhas, registradas como tal.

Contexto de produto: `PRODUCT.md` (raiz). Contrato de direção: `.impeccable/surfaces/src-gapmouse-app-views-homeview-axaml.md`.

## 1. Intenção

O app do Plano 3 usa o Fluent padrão do Avalonia, com cores fixas no XAML (`#3B82F6`, `#EF4444`...). Não há
tokens, tema claro de verdade (as cores fixas não acompanham o tema) nem uma linguagem visual própria. O objetivo é
dar ao GapMouse uma identidade visual coerente, de nível portfólio, sem perder a cara de utilitário nativo do
Windows, e publicar o resultado como nova versão (1.1.0, via `feat:` e release-please).

Modo da superfície: **Operate** (o usuário abre a janela por segundos para conferir, pausar, ler o log ou ajustar
algo). A familiaridade é um recurso: navegação e controles continuam os padrões do Windows/Fluent.

## 2. Objetivos, não objetivos e anti-objetivos

Objetivos:
- Estado legível de longe: o estado (aguardando, movendo, pausado) é a primeira coisa que se vê, por cor **e** texto.
- Um sistema de tokens (cores, raios, tipografia, espaçamento) com variantes claro/escuro reais, aplicado a todas as
  telas e ao diálogo de pausa personalizada.
- Navegação lateral (rail) no lugar das abas superiores, padrão do app Configurações do Windows 11.
- Ferramenta de capturas de tela headless (sem abrir janela real) para verificar o visual e gerar imagens do README.

Não objetivos:
- Mudar comportamento, view models, textos existentes ou o ícone da bandeja.
- Pausar quando a tela estiver bloqueada (trabalho separado, já combinado, fica para depois).
- Assinatura de código e atalhos do instalador (pendentes, outra rodada).

Anti-objetivos (fracasso mesmo que funcione):
- Parecer fantasia de outra coisa (painel de avião, terminal, papel) em vez de um app do Windows.
- Cor carregando sozinha o significado do estado; texto pequeno sem contraste AA.
- Animações decorativas, ou qualquer animação longa (> 250 ms).
- Quebrar AOT (bindings compilados, nada de reflexão) ou os testes de arquitetura (view models sem Avalonia).

## 3. Restrições

- Avalonia 12.1.4 + `Avalonia.Themes.Fluent`, Native AOT, `AvaloniaUseCompiledBindingsByDefault=true`.
- View models não referenciam Avalonia; conversores e estilos ficam em `Views/` ou `Styles/` do projeto App.
- Todo texto novo entra no `Localizer` como `T("English", "Português")`; tags de log continuam em inglês.
- Janela 760×600 por padrão, mínimo 640×520; os textos em português são mais longos e precisam caber.
- Avisos como erro e `dotnet format` limpo.

## 4. Abordagens consideradas

1. **Retocar o Fluent padrão** (trocar cores fixas por recursos, ajustar espaçamentos). Barato, mas mantém a cara
   genérica; não atende "atualização visual" de portfólio.
2. **Biblioteca de temas de terceiros** (FluentAvalonia, Semi.Avalonia, Material). Visual pronto, mas adiciona
   dependência grande, riscos de AOT e trim, e entrega a identidade de outra pessoa.
3. **Sistema próprio sobre o Fluent** (escolhida): paleta do Fluent recolorida via `FluentTheme.Palettes`, tokens
   próprios em dicionários claro/escuro, estilos por classe e templates só onde preciso (rail). Mantém controles
   nativos, zero dependências novas no app, e dá identidade. Custo: mais XAML para manter.

## 5. Direção visual: "a plaquinha de porta"

Sorteio da skill impeccable (`seed:d8eec48099a74886`), candidata 4 de 7: a placa de "Não perturbe" de hotel. Uma
face, uma palavra, legível do outro lado do corredor, virada quando o estado muda. O mundo contribui só com
paleta, tipografia, densidade e um gesto assinatura; layout, navegação e controles são os padrões.

Gesto assinatura: no Início, a **face da placa** é um cartão inteiro na cor do estado, com a palavra do estado em
caixa alta. O **furo da maçaneta** no topo do cartão é o anel de progresso de ociosidade (enche até o próximo
movimento). Quando o estado muda, a face "vira" (animação curta de escala horizontal).

Reforços emprestados dos desafiantes do sorteio:
- tabela de horários: hierarquia por peso e caixa dentro de uma escala tipográfica curta;
- painel de partidas: valores vivos em células fixas (numerais tabulares), nada "pula" quando muda;
- bilhete aéreo: tags do log como campos carimbados de largura fixa;
- catálogo de evento: o log mantém densidade;
- diagrama de metrô: uma só geometria (dois raios, uma espessura de linha) em todas as telas.

## 6. Sistema de design (valores fixados)

### 6.1 Cores

Tokens (`SolidColorBrush`, chave `GmXxxBrush`), definidos em `Styles/Tokens.axaml` com
`ResourceDictionary.ThemeDictionaries` (`Dark` e `Light`) e usados sempre com `DynamicResource`.

| Token | Escuro | Claro | Uso |
|---|---|---|---|
| `GmGroundBrush` | `#14161B` | `#F4F5F7` | fundo da área de conteúdo |
| `GmRailBrush` | `#1C2130` | `#E6E9F0` | trilho de navegação (segunda camada neutra) |
| `GmSurfaceBrush` | `#1B1E25` | `#FFFFFF` | cartões de grupo, linhas, lista do log |
| `GmSurfaceHoverBrush` | `#232731` | `#EEF0F4` | hover de linhas e itens do rail |
| `GmHairlineBrush` | `#2C313C` | `#D8DCE3` | divisórias e bordas (1 px) |
| `GmTextBrush` | `#ECEEF2` | `#16181D` | texto principal |
| `GmTextMutedBrush` | `#A0A8B6` | `#576070` | rótulos e texto secundário |
| `GmAccentBrush` | `#2F6FED` | `#2F5FD0` | ação primária, seleção do rail |
| `GmAccentTextBrush` | `#8FB0FF` | `#2350B8` | texto/ícone de destaque sobre o fundo |
| `GmCriticalTextBrush` | `#F87171` | `#B42318` | mensagens de erro de validação |
| `GmWarningTextBrush` | `#FBBF24` | `#9A5B00` | avisos (atalho indisponível) |

Campos de estado (iguais nos dois temas, cada um com sua tinta):

| Estado | Campo | Tinta | Tinta secundária |
|---|---|---|---|
| Aguardando | `#2F5FD0` | `#FFFFFF` | `#D6E2FF` |
| Movendo | `#17804A` | `#FFFFFF` | `#D3F5E2` |
| Pausado | `#E3A21A` | `#1F1503` | `#4A3407` |

Paleta do Fluent: `FluentTheme.Palettes` com `ColorPaletteResources` para `Dark` e `Light`, `Accent` igual a
`GmAccentBrush` e `RegionColor` igual a `GmGroundBrush`, para que ToggleSwitch, ComboBox, foco, seleção de texto e
botões `accent` herdem o acento.

Tags do log (campos carimbados, sólidos, iguais nos dois temas; `LogKindBrushConverter` passa a devolver o fundo,
e um segundo conversor devolve a tinta):

| Tag | Fundo | Tinta |
|---|---|---|
| START | `#475569` | `#FFFFFF` |
| WAITING | `#2F5FD0` | `#FFFFFF` |
| MOVING | `#17804A` | `#FFFFFF` |
| RETURN | `#5B6474` | `#FFFFFF` |
| PAUSED, RESUMED | `#E3A21A` | `#1F1503` |
| CONFIG | `#6B5BD6` | `#FFFFFF` |
| STOP, ERROR | `#B42318` | `#FFFFFF` |
| WARN | `#C2410C` | `#FFFFFF` |

### 6.2 Tipografia

Família única: `Segoe UI Variable Text, Segoe UI` (texto) e `Segoe UI Variable Display, Segoe UI` (palavra de
estado). Dados de tempo do log: `Cascadia Mono, Consolas` (é medida, não fantasia). Numerais tabulares
(`FontFeatures="tnum"`) em todo valor vivo (tempos, contadores).

Escala (px): 12 legenda · 14 corpo · 16 título de grupo (SemiBold) · 20 título de tela (SemiBold) · 30 palavra de
estado (Bold, caixa alta, `LetterSpacing` ≈ 0,6). A hierarquia vem de peso e caixa; não há rótulo pequeno em caixa
alta acima de títulos.

### 6.3 Geometria e espaço

- Raios: 6 px em controles (`ControlCornerRadius`), 12 px em cartões e na face da placa (`OverlayCornerRadius` 8).
- Linha: 1 px em `GmHairlineBrush`; elevação só por borda, sem sombra.
- Espaço (px): 4, 8, 12, 16, 24, 32. Margem da área de conteúdo 24; mais espaço acima de um título que abaixo.

### 6.4 Movimento

Uma única animação de assinatura: a face da placa vira ao mudar de estado (escala horizontal de 0,3 a 1 em 220 ms,
`CubicEaseOut`). Hover e seleção usam as transições padrão do Fluent. Nada mais se move.

## 7. Telas

**Janela e navegação.** `TabControl` com `TabStripPlacement="Left"` e template próprio: rail de 184 px em
`GmRailBrush`, com o ícone e o nome "GapMouse" no topo; cada item tem ícone (fonte `Segoe Fluent Icons, Segoe MDL2
Assets`: Início `E80F`, Logs `E9F9`, Configurações `E713`, Sobre `E946`) e rótulo; o item selecionado ganha
fundo `GmSurfaceHoverBrush` e uma barra de 3 px × 16 px em `GmAccentBrush` à esquerda do ícone (indicador de
seleção do Windows 11, não borda de cartão). Conteúdo em `GmGroundBrush`. Os itens continuam `TabItem` (acessíveis
por teclado e UI Automation).

**Início.** Duas colunas. À esquerda, a face da placa (220 px de largura, altura total): anel do furo (56 px,
trilho na tinta a 25 %, arco na tinta, `SweepAngle = IdleProgress × 360`; no estado pausado só o trilho), a
palavra do estado em caixa alta, o texto de ociosidade e o texto de pausa na tinta secundária. À direita, título
"Hoje" com duas linhas (tempo ativo, ativações) e título "Recentes" (texto novo) com as cinco linhas de horário
(iniciado, último movimento, último retorno, última pausa, última retomada): rótulo à esquerda em
`GmTextMutedBrush`, valor à direita em SemiBold tabular, divisórias de 1 px, dentro de um cartão `GmSurfaceBrush`
por grupo. Barra de ações embaixo: Pausar (`DropDownButton`, classe `accent`, mesmas opções) ou Retomar (botão
`accent`), e Sair do aplicativo como botão discreto alinhado à direita. A `ProgressBar` sai (o anel a substitui).

**Logs.** Barra de ferramentas: filtro (ComboBox) à esquerda; Copiar, Abrir pasta e Limpar à direita, com ícones.
Lista densa num cartão: hora (mono, `GmTextMutedBrush`), tag carimbada (76 px, 11 px SemiBold, raio 6) e mensagem.
Estado vazio (texto novo) quando não houver linhas, explicando que cada início, movimento, retorno e pausa aparece
ali.

**Configurações.** Padrão do Windows 11: um cartão por grupo (Movimento, Energia, Sistema, Aparência, Logs) com
linhas separadas por divisórias; rótulo à esquerda (e dica/erro abaixo dele), controle à direita (TextBox de
96 px, `ToggleSwitch` no lugar de cada CheckBox, ComboBox de 180 px). Barra fixa no rodapé, fora da rolagem, com
Salvar (`accent`), Restaurar padrões e a mensagem de status. Bindings e o `x:Name="HotkeyBox"` intactos.

**Sobre.** Cabeçalho com o ícone do app (48 px), nome e versão; um cartão com linhas: Licença; Repositório (URL +
botão Abrir); Arquivo de configurações e Pasta de logs (caminho em `SelectableTextBlock` + botão Abrir pasta);
Atualizações (botão Verificar, status e Abrir release quando houver).

**Pausa personalizada.** Mesmos tokens: fundo `GmGroundBrush`, prompt, TextBox, erro em `GmCriticalTextBrush`,
OK (`accent`, padrão) e Cancelar à direita.

## 8. Ferramenta de capturas

Projeto `tools/GapMouse.Screenshots` (console, não distribuído, não AOT): sobe o Avalonia em modo headless com Skia,
monta `MainWindowViewModel` com dublês (motor falso com estado configurável, serviço de configurações em memória,
log com linhas de exemplo), abre a `MainWindow` em 760×600 e salva PNG de cada aba e estado relevante (Início
aguardando, movendo e pausado; Logs; Configurações; Sobre), em tema escuro e claro, inglês e português, em
`artifacts/screenshots/` (ignorada pelo git). Motivo: o app real tem instância única e não pode ser aberto ao lado
da instância do usuário; a captura headless é reproduzível e serve também para o README.

## 9. Verificação

- `dotnet build`, `dotnet test --filter-not-trait "Category=Win32"`, `dotnet format --verify-no-changes`.
- Publish AOT do app sem avisos (como o CI).
- Capturas headless revisadas em escuro/claro, EN/PT: nada cortado no mínimo de 640×520, contraste AA, estados
  distintos.
- Revisão de acabamento (impeccable) e revisão final de branch (opus).
- Ao final: `DESIGN.md` documentando o sistema construído; CLAUDE.md atualizado.

## 10. Deixado para quem constrói

Detalhes de template do `TabControl`, a forma exata de animar a virada (estilo com `Animation` disparada por
classe), o conversor do anel e os dados de exemplo das capturas. Se um glifo de ícone não existir na fonte, trocar
por outro equivalente da mesma fonte.
