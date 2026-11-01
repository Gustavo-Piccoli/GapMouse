# GapMouse — Documento de Design

- **Data:** 2026-10-31
- **Autor:** Gustavo Auad Piccoli (com Claude)
- **Status:** rascunho para aprovação
- **Origem:** `MOUSE.ps1` / `MOUSE.bat` (script PowerShell atual)

Legenda: itens marcados com **[decisão do Gustavo]** foram decididos explicitamente por ele. Itens marcados com **[decisão do Claude]** são escolhas minhas, que o Gustavo pode revisar. O que não tem marca e está em "Deixado para o construtor" é livre.

---

## 1. Intenção e porquê

O Gustavo tem um script PowerShell que mantém o PC ativo enquanto ele está longe, e funciona exatamente como ele quer: impede bloqueio/suspensão e mantém o status "Disponível" no Microsoft Teams. Ele quer transformar esse script em uma **aplicação Windows caprichada, digna de portfólio**, que qualquer pessoa possa baixar do GitHub dele e usar.

Dois públicos, em ordem:

1. **Recrutadores/devs que avaliam o portfólio:** o repositório precisa demonstrar engenharia de alto nível (arquitetura limpa, testes, CI/CD, documentação, releases automatizadas).
2. **Usuários finais:** gente que baixa, instala (sem admin) e usa. Tem que "simplesmente funcionar", ser leve e discreta.

Nome: **GapMouse** **[decisão do Gustavo]**. "GAP" = iniciais de Gustavo Auad Piccoli; em inglês *gap* = lacuna, o que o app preenche quando você está ausente. Verificado em 2026-10-31: livre no winget e no GitHub.

## 2. Objetivos, não-objetivos e anti-objetivos

### Objetivos
- Preservar **exatamente** o comportamento do motor atual (detecção de inatividade, movimento em curva Bézier com velocidade variável, detecção de retorno do usuário).
- Interface gráfica com abas (Início, Logs, Configurações, Sobre), tema escuro por padrão.
- Rodar em segundo plano com ícone na bandeja do Windows e menu de contexto.
- Pausar/Retomar sem fechar o app, com pausa temporizada.
- Iniciar com o Windows (opcional).
- Distribuição: instalador, portátil e winget.
- Leve: um único `.exe` autocontido de ~20–30 MB, sem exigir .NET instalado.
- Repositório público exemplar: Clean Architecture, SOLID, DDD na medida certa, testes, CI/CD, releases automatizadas.

### Não-objetivos (v1)
- Suporte a macOS/Linux (mesmo que o Avalonia permita, o motor é Windows-only).
- Simular input real via `SendInput` (ver débito técnico, §9).
- Atualização automática silenciosa (o app só **avisa** que há versão nova).
- Agendamento por horário (ex.: "só ativo das 9h às 18h").

### Anti-objetivos (seria fracasso mesmo funcionando)
- **Bloatware:** dependências pesadas, executável inchado, consumo de RAM/CPU perceptível.
- **Telemetria ou qualquer envio de dados.** Única chamada de rede: checagem de atualização no GitHub, desligável.
- **Exigir administrador** para instalar ou rodar.
- **Over-engineering:** DDD/Clean Architecture aplicados de forma cerimonial, com camadas vazias ou abstrações sem uso. Um avaliador experiente deve ver *julgamento*, não *checklist*.
- Regredir o comportamento que já funciona hoje (Teams "Disponível", PC sem suspender).

## 3. Restrições

- **Plataforma:** Windows 10/11, x64 e ARM64.
- **Stack:** .NET 10 (SDK 10.0.300 instalado na máquina do Gustavo), C#.
- **UI:** Avalonia + **Native AOT** **[decisão do Gustavo]**. Tudo precisa ser AOT-compatível: bindings compilados, `System.Text.Json` com source generator, P/Invoke via `LibraryImport`, nada de reflection dinâmica em runtime.
- **Idioma:** código, commits, README, nomes de branches e issues em **inglês** **[decisão do Gustavo]**. Interface em **pt-BR e en**, idioma inicial segue o do Windows (fallback: en) **[decisão do Claude]**.
- **Repositório:** GitHub pessoal `Gustavo-Piccoli/GapMouse`, **público** **[decisão do Claude, aceita]**. O `gh` já está autenticado com escopos `repo` e `workflow`.
- **Licença:** MIT **[decisão do Claude, aceita]**.
- **Execução do desenvolvimento:** paralelizar com agentes; **todo agente deve ser criado com o modelo Sonnet**, nunca Opus **[decisão do Gustavo]**.

## 4. Abordagens consideradas (tecnologia de UI)

| | Prós | Contras | Tamanho (estimado) |
|---|---|---|---|
| **A) WPF + MVVM** | Stack desktop mais pedida em vagas; tema Fluent nativo (.NET 9+) | Bandeja não é nativa; não aceita trimming → executável grande | lite ~1–2 MB / completo ~65–75 MB |
| **B) WinForms** | Bandeja nativa; o mais simples | Visual datado, modo escuro limitado; impressiona menos | lite ~1 MB / completo ~45–55 MB |
| **C) Avalonia + Native AOT** ✅ | Um `.exe` nativo pequeno, abre instantâneo; Fluent claro/escuro; bandeja nativa; MVVM testável; AOT+CI é diferencial | Controles não nativos; menos comum em vagas que WPF; AOT exige disciplina | **único `.exe` ~20–30 MB** |

**Escolhida: C** **[decisão do Gustavo]**. Motivo: único caminho que entrega app moderno, autocontido e não "pesado" (o Gustavo considerou ~70 MB pesado). Consequência: como o `.exe` completo já é pequeno, **não haverá versão "lite"** dependente do .NET instalado; as variantes são instalador + portátil (+ winget).

Também descartadas: WinUI 3 (empacotamento complexo, executável grande) e Win32 puro com AOT (menor de todos, mas custo de construir UI com abas é desproporcional).

## 5. Comportamento (o que foi decidido)

### 5.1 Motor — estados

```
            idle >= limite                 uso real detectado
 [Waiting] ───────────────────► [Moving] ───────────────────► [Waiting]
     ▲  │                                                        
     │  └── Pausar ──► [Paused] ◄── Pausar ── (de qualquer estado)
     └──── Retomar / tempo da pausa esgotado ────┘
```

- **Waiting (Aguardando):** só observa. Lê o tempo ocioso do sistema (`GetLastInputInfo`) a cada *intervalo de checagem*. Quando ocioso ≥ *tempo de inatividade*, vai para Moving. Mantém o "keep-awake" ativo.
- **Moving (Movendo):** executa um movimento natural, depois espera um intervalo aleatório entre *mín* e *máx*, checando input a cada *intervalo de checagem*. Volta para Waiting ao detectar uso real. Mantém o "keep-awake" ativo.
- **Paused (Pausado)** **[decisão do Gustavo]:** "como se o programa estivesse desligado". Não observa, não move, e **libera o controle de energia** (`SetThreadExecutionState(ES_CONTINUOUS)`): o Windows volta a poder bloquear/suspender e o Teams fica "Ausente" no tempo dele. O app continua aberto na bandeja. Ao retomar, vai para **Waiting**.
- **Encerrar:** libera o controle de energia (equivalente ao `finally` do script) e fecha.

### 5.2 Motor — algoritmo (portar fielmente do `MOUSE.ps1`)

- **Movimento:** curva Bézier quadrática da posição atual até um destino com deslocamento aleatório em X e Y de ±*amplitude* (padrão 120 px); ponto de controle no meio ± metade da amplitude (hoje ±60 px); 20–40 passos; atraso por passo `8 + rand(0..4) − sin(t·π)·6` ms; pausa final de 100–500 ms. Cursor movido com `SetCursorPos` (equivalente a `Cursor.Position`).
- **Limites:** destino limitado ao **monitor onde o cursor está** **[decisão do Claude, aceita]** (hoje: só o monitor principal).
- **Detecção de uso real durante o movimento:** guarda o instante de início; a cada passo, se `idleAtual < segundosDesdeInicio − 0.5`, aborta e volta para Waiting.
- **Detecção durante a espera:** se `idle < 1 s`, volta para Waiting.
- **Fato que sustenta a lógica** (verificado em 2026-10-31 na máquina do Gustavo): `SetCursorPos` **não** zera o contador de inatividade do Windows (idle foi de 28,2 s para 28,5 s após mover o cursor). Por isso o próprio movimento não se confunde com uso real. Mesmo assim, o Teams do Gustavo permanece "Disponível" com o script atual — comportamento a preservar.
- **Keep-awake:** `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED [| ES_DISPLAY_REQUIRED])`, reaplicado periodicamente nos loops como hoje.

### 5.3 Pausa

Opções **[decisão do Gustavo]:** **10 min, 30 min, 1 h, Personalizado, Nunca** ("Nunca" = pausa por tempo indeterminado, até retomar manualmente). Ao fim do tempo, retoma sozinho e registra no log "Retomado (tempo esgotado)". A aba Início mostra o tempo restante da pausa. **[decisão do Claude]** "Personalizado" abre um campo de minutos (1 a 1440).

### 5.4 Manter tela ligada

- **Ligado (padrão, = hoje):** `ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED` — não suspende nem apaga a tela.
- **Desligado:** só `ES_SYSTEM_REQUIRED` — não suspende, mas a tela apaga conforme a configuração de energia do Windows.
- **Ponto a validar:** com a tela apagada, o Teams pode marcar "Ausente". Documentar no README/tooltip após testar.

### 5.5 Ícone na bandeja

- Ícone **muda de cor por estado** (Aguardando / Movendo / Pausado) **[decisão do Claude, aceita]**; tooltip com o estado atual.
- **Menu (clique direito):** `Pausar ▸ (10 min, 30 min, 1 h, Personalizado…, Nunca)` ou `Retomar` quando pausado · `Abrir` · `Sair`. **[decisão do Claude, aceita]** — "Ativar/Desativar" foi unificado com Pausar/Retomar, pois têm o mesmo significado.
- **Duplo clique** abre a janela.
- Fechar a janela (✕) **minimiza para a bandeja**, não encerra **[decisão do Claude]**. Encerrar só por "Sair"/"Encerrar aplicação".

### 5.6 Interface — abas (esboço aprovado pelo Gustavo, todas as sugestões incluídas)

**Início**
- Estado atual com cor + contador de inatividade ("Inativo há 45 s de 120 s") com barra de progresso; em pausa, tempo restante.
- Botão Pausar ▾ (com as opções de 5.3) / Retomar.
- Horários: programa iniciado; última ativação do movimento; última parada (você voltou); última pausa / retomada.
- **Hoje:** tempo mantido ativo pelo app (soma do tempo em Moving) e número de ativações do movimento. **[decisão do Claude]** Zera à meia-noite; persiste entre reinícios do app no mesmo dia (salvo junto ao estado do app).
- Botão Encerrar aplicação.

**Logs**
- Linhas no formato `HH:mm:ss | TIPO | mensagem`, **cor diferente por tipo** **[decisão do Gustavo]**: START, WAITING, MOVING, RETURN (uso real detectado), PAUSED, RESUMED, CONFIG (configuração alterada), STOP, além de WARN/ERROR.
- Filtros por categoria (Todos / Movimento / Pausas / Sistema).
- Botões: Copiar, Abrir pasta, Limpar (limpa a visualização, não apaga arquivos).
- **[decisão do Claude]** A tela mantém as últimas 1.000 linhas em memória.

**Configurações**
- *Movimento:* tempo de inatividade até mover (padrão **120 s**); intervalo entre movimentos mín/máx (padrão **10 s / 25 s**, os valores do `.bat`); amplitude (padrão **120 px**); intervalo de checagem (padrão **1 s**).
- *Energia:* manter tela ligada (padrão **ligado**).
- *Sistema:* iniciar com o Windows; iniciar minimizado na bandeja; notificações ao mudar de estado (padrão **desligado**); atalho global de pausar/retomar (padrão `Ctrl+Alt+P`, configurável); verificar atualizações automaticamente (padrão **ligado**).
- *Aparência:* tema **Escuro (padrão)** / Claro **[decisão do Gustavo]**; idioma Português / English.
- *Logs:* salvar em arquivo (padrão **ligado**); manter por N dias (padrão **7**).
- Botões Restaurar padrões / Salvar. Validação inline (ex.: mín ≤ máx; valores positivos). Mudanças no motor valem a partir do próximo ciclo, sem reiniciar o app.

**Sobre**
- Versão, link para o GitHub, licença, botão "Verificar atualizações", caminhos dos arquivos de configuração e log.

### 5.7 Recursos transversais (todos aceitos pelo Gustavo)

1. **Instância única:** abrir o app de novo traz a janela da instância existente.
2. **Atalho global** para pausar/retomar. Se o registro falhar (conflito), logar WARN e indicar na aba Configurações.
3. **Build ARM64** além de x64.
4. **Sem telemetria.** Única rede: checagem de atualização (GitHub Releases API), desligável.
5. **Aviso no README:** uso em PC corporativo é responsabilidade do usuário (políticas da empresa).
6. **`MOUSE.ps1` e `MOUSE.bat`** vão para `legacy/` no repositório, como a origem do projeto.
7. **Checagem de atualização:** ao iniciar e a cada 24 h; se houver versão nova, avisa (notificação + aba Sobre) com link para a Release. Nunca baixa/instala sozinho.

## 6. Distribuição

| Artefato | Descrição |
|---|---|
| `GapMouse-Setup-<versão>-<arch>.exe` | Instalador **por usuário, sem admin** (Inno Setup, `PrivilegesRequired=lowest`), instala em `%LocalAppData%\Programs\GapMouse`, atalho no Menu Iniciar, aparece em "Aplicativos instalados". Checkbox "Iniciar com o Windows" no instalador. |
| `GapMouse-<versão>-<arch>-portable.zip` | Um único `.exe` AOT. Configurações e logs ficam **ao lado do `.exe`** (roda até de pendrive). |
| `winget install GustavoPiccoli.GapMouse` | Aponta para o instalador. |
| `SHA256SUMS.txt` | Checksums de todos os artefatos. |

- **Detecção de modo portátil** **[decisão do Claude]:** se existir um arquivo marcador `portable` (ou `GapMouse.settings.json`) ao lado do `.exe`, usa a pasta do `.exe`; senão, `%AppData%\GapMouse` (config) e `%LocalAppData%\GapMouse\logs` (logs). O `.zip` portátil já vem com o marcador.
- **Iniciar com o Windows:** chave `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` apontando para o `.exe` com `--minimized`. No modo portátil, se o `.exe` mudar de lugar, o app corrige o caminho ao iniciar (se a opção estiver ligada).
- **Desinstalação:** remove arquivos do app e a chave Run; **[decisão do Claude]** mantém configurações em `%AppData%` (prática comum).
- **Assinatura digital:** solicitar SignPath Foundation (gratuito para open source) **após** a v1.0 pública. Até lá, README explica o aviso do SmartScreen.
- **Winget:** primeira submissão manual (PR em `microsoft/winget-pkgs`) após a v1.0; atualizações seguintes automatizadas no CD (`wingetcreate`, segredo com PAT).
- **Downloads:** GitHub Releases; README aponta para `releases/latest`. Nenhum binário é commitado no repositório.

## 7. Arquitetura

Clean Architecture com dependências apontando para dentro; DDD **na medida** de um app pequeno (sem agregados/repositórios artificiais).

```
GapMouse.Domain          (sem dependências)
GapMouse.Application     → Domain
GapMouse.Infrastructure  → Application, Domain   (Windows: P/Invoke, registro, arquivos, HTTP)
GapMouse.App             → todos (Avalonia, MVVM, composição/DI)
tests/
  GapMouse.Domain.Tests
  GapMouse.Application.Tests
  GapMouse.Infrastructure.Tests   (o que for testável sem efeitos colaterais perigosos)
  GapMouse.Architecture.Tests     (regras de dependência entre camadas)
```

- **Domain:** máquina de estados do motor; política de pausa (duração, expiração); configurações como *value objects* com invariantes (mín ≤ máx, faixas válidas); eventos de domínio (`MovementStarted`, `UserReturned`, `Paused`, `Resumed`, `SettingsChanged`…); geração da trajetória Bézier (pura, recebe aleatoriedade injetada).
- **Application:** casos de uso (Pause, Resume, UpdateSettings, ToggleStartup…), orquestração do loop do motor, e **portas**: `IIdleTimeProvider`, `ICursorController`, `IPowerRequestService`, `IMonitorBoundsProvider`, `IStartupRegistrar`, `ISettingsStore`, `IActivityLog`, `IUpdateChecker`, `IGlobalHotkey`, `IRandom`; tempo via `TimeProvider` do .NET.
- **Infrastructure:** implementações Win32 (`GetLastInputInfo`, `SetCursorPos`, `GetCursorPos`, `SetThreadExecutionState`, `MonitorFromPoint`/`GetMonitorInfo`, `RegisterHotKey`), JSON com source generator, registro HKCU, logs em arquivo com retenção, cliente da API do GitHub.
- **App:** Avalonia com bindings compilados, MVVM (CommunityToolkit.Mvvm, source generators), tray, temas, i18n, instância única, composição via `Microsoft.Extensions.DependencyInjection`.
- **Testes:** motor e regras testados com `FakeTimeProvider` e aleatoriedade determinística (sem esperas reais); cenários-chave: entra em Moving no limite exato; aborta movimento quando idle cai; pausa libera energia; pausa temporizada expira; configurações inválidas rejeitadas.

## 8. Processo de engenharia e CI/CD

- **Branches:** `main` protegida; trabalho em branches + PR (mesmo sendo projeto solo, para exibir o fluxo).
- **Commits:** Conventional Commits. **Versionamento:** SemVer, automatizado (ex.: release-please gera versão, `CHANGELOG.md` e tag).
- **CI (a cada PR/push):** build, `dotnet format --verify-no-changes`, testes, cobertura (publicada no PR), CodeQL, build de publicação AOT para garantir que nada quebrou o AOT.
- **CD (na tag `v*`):** publicação AOT x64 e ARM64 → instalador Inno Setup + `.zip` portátil + `SHA256SUMS.txt` → GitHub Release com notas.
- **Higiene:** Dependabot, `.editorconfig`, analyzers com warnings como erro, `Directory.Build.props`/`Directory.Packages.props` (versões centralizadas).
- **Documentação:** README com GIF do app, badges (CI, release, licença), instruções de instalação e de build; ADRs em `docs/adr/` (ex.: escolha do Avalonia+AOT, manutenção de `SetCursorPos`, ausência de versão lite); `CONTRIBUTING.md`.

## 9. Débito técnico registrado

**`SendInput` + hooks de baixo nível.** O app depende de `SetCursorPos` não zerar o idle do Windows (é assim que separa movimento sintético de uso real) e, empiricamente, isso já mantém o Teams "Disponível" na máquina do Gustavo. Se algum dia o Teams (ou outro app de presença) deixar de reconhecer, a alternativa é simular input com `SendInput` (que zera o idle) e detectar o usuário via `WH_MOUSE_LL`/`WH_KEYBOARD_LL`, ignorando eventos com a flag `LLMHF_INJECTED`/`LLKHF_INJECTED`. A porta `ICursorController` + detector de retorno devem ser desenhados de forma que essa troca seja uma nova implementação, não uma reescrita. **[decisão do Gustavo]**

## 10. Entregas sugeridas (ordem)

1. **Fundação:** repositório, solução, camadas, CI básico, `legacy/`.
2. **Motor:** domínio + aplicação + infraestrutura Win32, com testes; rodando headless/console para validar paridade com o script.
3. **Bandeja + instância única + pausa** (app utilizável sem janela).
4. **Janela com abas** (Início, Logs, Configurações, Sobre), temas, i18n.
5. **Persistência, iniciar com Windows, atalho global, notificações, checagem de atualização.**
6. **Distribuição:** CD, instalador, portátil, ARM64, Release v1.0.0, README final.
7. **Pós-v1:** winget, SignPath.

## 11. Decisões complementares (após revisão do construtor)

Todas **[decisão do Claude]**:

1. **Bandeja e atalho sem janela:** com "iniciar minimizado", a janela pode nunca ser criada. Bandeja, atalho global (`RegisterHotKey`) e instância única devem funcionar **sem a janela principal**. Use uma janela oculta *message-only* (HWND próprio) na Infrastructure para receber `WM_HOTKEY` e as mensagens de ativação da segunda instância.
   - Nota: na implementação o shell usa uma janela de topo oculta, e não *message-only* (ver ADR 0006).
2. **Fechar no ✕ pela primeira vez:** mostra um balão avisando "O GapMouse continua rodando na bandeja". O aviso aparece só uma vez (fica salvo nas configurações).
3. **Ao retomar** (manual ou por fim do tempo), o motor vai para Waiting usando o **idle real do sistema**. Se o usuário ficou longe o tempo todo, o motor passa direto para Moving no próximo ciclo. Esse comportamento é o desejado.
4. **"Hoje"** conta **apenas o tempo em Moving** e o número de entradas em Moving. O tempo pausado não entra em nenhuma métrica.
5. **Limites do monitor:** o monitor é determinado pela posição do cursor **no início de cada movimento**. O destino é limitado à área desse monitor, e cada ponto da curva também, porque o ponto de controle pode passar da borda. O cursor nunca sai do monitor no meio da curva.
6. **Faixas aleatórias:** o que o usuário configura vale com **limites inclusivos**: "10 a 25 s" pode sortear 25. No script, `Get-Random -Maximum` exclui o máximo; essa diferença é intencional, e a paridade exigida é a de comportamento, não a de bits. Faixas internas: passos 20–40 e ruído de atraso 0–4 ms, ambos inclusivos.
7. **Notificações** (mudança de estado e atualização disponível): **balão da bandeja** via `Shell_NotifyIcon` (leve e compatível com AOT), não toasts do WinRT.
8. **Troca de idioma ao vivo**, sem reiniciar: um localizador observável, compatível com bindings compilados, em vez de `.resx` com reflection.
9. **Proteção da `main` (repositório solo):** exige PR e CI verde, **0 aprovações obrigatórias**, e o dono pode fazer o merge (inclusive dos PRs de release automatizados).
10. **ARM64:** build no runner hospedado `windows-11-arm` do GitHub (gratuito para repositório público). Faz parte da release, ou seja, não é "melhor esforço".
11. **Passos manuais do Gustavo, pós-v1:**
    - criar o PAT do GitHub para automatizar o winget e cadastrá-lo como segredo do repositório;
    - fazer a primeira submissão ao `microsoft/winget-pkgs` (o ID `GustavoPiccoli.GapMouse` é reservado por essa submissão);
    - solicitar a assinatura ao SignPath.

## 12. Deixado para o construtor

- Bibliotecas específicas de teste/asserção (evitar FluentAssertions ≥ 8 por mudança de licença), de logging (preferir algo leve e AOT-compatível) e a ferramenta exata de release automation.
- Estrutura interna de pastas dentro de cada projeto, nomes de classes.
- Formato exato do arquivo de log em disco e do JSON de configurações.
- Desenho visual final das telas (seguindo o esboço aprovado: tema escuro, Fluent).
- Arte do ícone (três variações de cor por estado).
- Como dividir o trabalho entre agentes paralelos (respeitando: modelo Sonnet).
