#!/usr/bin/env bash
set -e

echo "=== Compilando pacote Flatpak do To-Do List para Zorin OS ==="

# Verifica se o flatpak-builder está instalado
if ! command -v flatpak-builder &> /dev/null; then
    echo "Instalando flatpak-builder..."
    sudo apt update && sudo apt install -y flatpak-builder
fi

# Instala o runtime e o sdk do Freedesktop se necessário
flatpak install -y --noninteractive flathub org.freedesktop.Platform//23.08 org.freedesktop.Sdk//23.08 || true

# Compila o Flatpak no diretório build-dir
echo "Executando flatpak-builder..."
flatpak-builder --force-clean --user --install-deps-from=flathub build-dir com.emerson.todolist.yml

# Cria o repositório e gera o bundle instalável
echo "Exportando pacote instalável com.emerson.todolist.flatpak..."
flatpak-builder --export-only --repo=repo build-dir com.emerson.todolist.yml
flatpak build-bundle repo com.emerson.todolist.flatpak com.emerson.todolist

echo ""
echo "=== Sucesso! ==="
echo "O pacote 'com.emerson.todolist.flatpak' foi gerado!"
echo "Para instalar no Zorin OS, dê um duplo clique no arquivo .flatpak ou execute:"
echo "  flatpak install --user com.emerson.todolist.flatpak"
echo "Para executar o app:"
echo "  flatpak run com.emerson.todolist"
