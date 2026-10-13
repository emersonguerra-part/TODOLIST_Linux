# To-Do List — Versão Nativa para Linux (Zorin OS)

Esta versão foi desenvolvida com **Avalonia UI (.NET 8)** para ser 100% nativa no Linux, mantendo o visual, performance, animações de confetes e efeitos sonoros exatamente iguais à versão do Windows.

---

## Estrutura do Projeto
- `MainWindow.axaml` & `MainWindow.axaml.cs`: Interface, barra de título personalizada (`#3B3B3B`), temas claro/escuro (`#222222`), perfis e modais.
- `Controls/ConfettiOverlay.cs`: Motor de partículas em Skia que reproduz a chuva de confetes no topo e a explosão radial 360° no checkbox das subtarefas.
- `Services/`:
  - `FirebaseService.cs`: Sincronização em tempo real com Firebase Firestore REST.
  - `SyncCoordinator.cs`: Coordenação de merge seguro de dados locais e remotos.
  - `TodoStorageService.cs`: Armazenamento local compatível com XDG (`~/.local/share/TodoListApp`).
  - `ThemeService.cs`: Alternância de cores e temas.
  - `SoundEffectHelper.cs`: Efeitos sonoros (`pw-play`, `paplay`, `aplay`).
- `publish_linux/`: Binário autossuficiente pré-compilado para arquitetura `linux-x64` (não requer .NET SDK instalado no Zorin OS).
- `flatpak/`: Manifesto e metadados oficiais para empacotamento Flatpak.

---

## Como instalar no Zorin OS

Você tem duas formas muito simples de rodar no Zorin OS:

### Opção 1: Pacote Flatpak (Recomendada para Zorin OS)
1. Copie a pasta `TODOLIST_Linux` para o seu PC com Zorin OS (ex: via pendrive ou rede).
2. Abra o terminal dentro da pasta `flatpak` e execute o script:
   ```bash
   cd flatpak
   chmod +x build-flatpak.sh
   ./build-flatpak.sh
   ```
3. O script gerará o arquivo `com.emerson.todolist.flatpak`.
4. Para instalar, basta dar **duplo clique** no arquivo `.flatpak` (abrindo na Central de Programas do Zorin OS) ou rodar:
   ```bash
   flatpak install --user com.emerson.todolist.flatpak
   ```
5. O aplicativo aparecerá no menu iniciar e pode ser aberto também via:
   ```bash
   flatpak run com.emerson.todolist
   ```

---

### Opção 2: Instalação Direta (Sem precisar de Flatpak)
Se quiser instalar em 2 segundos sem compilar nada:
1. No terminal do Zorin OS, dentro da pasta `TODOLIST_Linux`, execute:
   ```bash
   chmod +x install-direct.sh
   ./install-direct.sh
   ```
2. O aplicativo será copiado para `~/.local/share/TodoListApp`, o ícone registrado e adicionado ao menu de aplicativos do Zorin OS imediatamente.
