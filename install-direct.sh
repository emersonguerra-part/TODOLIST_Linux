#!/usr/bin/env bash
set -e

APP_DIR="$HOME/.local/share/TodoListApp"
BIN_DIR="$HOME/.local/bin"
DESKTOP_DIR="$HOME/.local/share/applications"
ICON_DIR="$HOME/.local/share/icons/hicolor/256x256/apps"

echo "=== Instalando To-Do List nativo no Zorin OS ==="

mkdir -p "$APP_DIR"
mkdir -p "$BIN_DIR"
mkdir -p "$DESKTOP_DIR"
mkdir -p "$ICON_DIR"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$SCRIPT_DIR/publish_linux"

if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Erro: pasta 'publish_linux' não encontrada em $SCRIPT_DIR."
    exit 1
fi

echo "Copiando arquivos da aplicação..."
cp -r "$PUBLISH_DIR"/* "$APP_DIR/"
chmod +x "$APP_DIR/TodoListApp.Linux"

echo "Criando atalho no menu do sistema..."
ln -sf "$APP_DIR/TodoListApp.Linux" "$BIN_DIR/todolist"

# Ícone
cp "$APP_DIR/app_icon.png" "$ICON_DIR/com.emerson.todolist.png"

# Desktop entry
cat <<EOF > "$DESKTOP_DIR/com.emerson.todolist.desktop"
[Desktop Entry]
Version=1.0
Type=Application
Name=To-Do List
GenericName=Gerenciador de Tarefas
Comment=Aplicativo de Gerenciamento de Tarefas com Sincronização em Nuvem
Exec=$APP_DIR/TodoListApp.Linux
Icon=com.emerson.todolist
Terminal=false
Categories=Utility;Office;ProjectManagement;
StartupWMClass=TodoListApp.Linux
EOF

chmod +x "$DESKTOP_DIR/com.emerson.todolist.desktop"
update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true

echo ""
echo "=== Instalação concluída com sucesso! ==="
echo "O aplicativo 'To-Do List' agora está no seu Menu Iniciar do Zorin OS!"
echo "Você também pode iniciá-lo pelo terminal digitando: todolist"
