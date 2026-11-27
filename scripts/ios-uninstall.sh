#!/usr/bin/env bash
set -euo pipefail

# Controla visibilidade do cursor ao sair
cleanup() {
  tput cnorm 2>/dev/null || true
}
trap cleanup EXIT

# Menu interativo navegável com setas ↑/↓, Enter e Esc/q
select_menu() {
  local title="$1"
  local hint="$2"
  shift 2
  local items=("$@")
  local count=${#items[@]}
  local selected=0
  local key=""
  local seq=""

  tput civis 2>/dev/null || true

  while true; do
    clear
    echo "============================================================"
    echo " $title"
    echo " $hint"
    echo "============================================================"
    echo ""

    for ((i=0; i<count; i++)); do
      if [[ $i -eq $selected ]]; then
        printf "  \033[1;36m▶ %s\033[0m\n" "${items[$i]}"
      else
        printf "    %s\n" "${items[$i]}"
      fi
    done

    IFS= read -rsn1 key
    if [[ "$key" == $'\x1b' ]]; then
      read -rsn2 -t 0.05 seq || true
      if [[ "$seq" == "[A" ]]; then
        ((selected--))
        [[ $selected -lt 0 ]] && selected=$((count - 1))
      elif [[ "$seq" == "[B" ]]; then
        ((selected++))
        [[ $selected -ge $count ]] && selected=0
      elif [[ -z "$seq" ]]; then
        tput cnorm 2>/dev/null || true
        return 255
      fi
    elif [[ "$key" == "" ]]; then
      tput cnorm 2>/dev/null || true
      return "$selected"
    elif [[ "$key" == "q" || "$key" == "Q" ]]; then
      tput cnorm 2>/dev/null || true
      return 255
    fi
  done
}

# 1. Listar devices físicos conectados
echo "Buscando dispositivos iOS físicos conectados..."
TMP_DEVICES=$(mktemp /tmp/devicectl_devices.XXXXXX.json)
xcrun devicectl list devices --json-output "$TMP_DEVICES" >/dev/null 2>&1

DEVICE_LINES=()
while IFS= read -r line; do
  [[ -n "$line" ]] && DEVICE_LINES+=("$line")
done < <(python3 - "$TMP_DEVICES" <<'PY'
import json, sys

with open(sys.argv[1], 'r', encoding='utf-8') as f:
    data = json.load(f)

devices = data.get('result', {}).get('devices', [])
for d in devices:
    hw = d.get('hardwareProperties', {})
    conn = d.get('connectionProperties', {})
    dev_props = d.get('deviceProperties', {})
    if hw.get('platform') == 'iOS' and hw.get('reality') == 'physical':
        name = dev_props.get('name', 'iPhone')
        udid = hw.get('udid') or d.get('identifier', '')
        state = conn.get('tunnelState', 'unknown')
        print(f"{name} [{state}] | {udid}")
PY
)
rm -f "$TMP_DEVICES"

if [[ ${#DEVICE_LINES[@]} -eq 0 ]]; then
  echo "Nenhum iPhone/iPad físico conectado foi encontrado."
  echo "Verifique o cabo USB e se o aparelho está desbloqueado."
  exit 1
fi

set +e
select_menu "SELECIONE O DISPOSITIVO FÍSICO" "[↑/↓] Mover   [Enter] Selecionar   [Esc/q] Sair" "${DEVICE_LINES[@]}"
DEV_IDX=$?
set -e

if [[ $DEV_IDX -eq 255 ]]; then
  clear
  echo "Operação cancelada."
  exit 0
fi

SELECTED_DEVICE="${DEVICE_LINES[$DEV_IDX]}"
DEVICE_UDID=$(echo "$SELECTED_DEVICE" | awk -F' \\| ' '{print $2}')
DEVICE_NAME=$(echo "$SELECTED_DEVICE" | awk -F' \\| ' '{print $1}')

# 2. Loop de listagem e desinstalação de apps
while true; do
  clear
  echo "Carregando aplicativos instalados em: $DEVICE_NAME..."
  TMP_APPS=$(mktemp /tmp/devicectl_apps.XXXXXX.json)
  xcrun devicectl device info apps --device "$DEVICE_UDID" --json-output "$TMP_APPS" >/dev/null 2>&1

  APP_LINES=()
  while IFS= read -r line; do
    [[ -n "$line" ]] && APP_LINES+=("$line")
  done < <(python3 - "$TMP_APPS" <<'PY'
import json, sys

with open(sys.argv[1], 'r', encoding='utf-8') as f:
    data = json.load(f)

apps = data.get('result', {}).get('apps', [])
for app in apps:
    bundle = app.get('bundleIdentifier', '')
    name = app.get('name', bundle)
    # Filtra apps nativos da Apple para focar nos seus apps instalados
    if bundle and not bundle.startswith('com.apple.'):
        print(f"{name} ({bundle}) | {bundle}")

for app in apps:
    bundle = app.get('bundleIdentifier', '')
    name = app.get('name', bundle)
    if bundle and bundle.startswith('com.apple.'):
        print(f"[Sistema] {name} ({bundle}) | {bundle}")
PY
)
  rm -f "$TMP_APPS"

  if [[ ${#APP_LINES[@]} -eq 0 ]]; then
    echo "Nenhum aplicativo encontrado no dispositivo."
    exit 0
  fi

  set +e
  select_menu "APPS EM: $DEVICE_NAME" "[↑/↓] Navegar   [Enter] Desinstalar App   [Esc/q] Sair" "${APP_LINES[@]}"
  APP_IDX=$?
  set -e

  if [[ $APP_IDX -eq 255 ]]; then
    clear
    echo "Saindo..."
    exit 0
  fi

  SELECTED_APP="${APP_LINES[$APP_IDX]}"
  BUNDLE_ID=$(echo "$SELECTED_APP" | awk -F' \\| ' '{print $2}')
  APP_LABEL=$(echo "$SELECTED_APP" | awk -F' \\| ' '{print $1}')

  clear
  echo "Desinstalando: $APP_LABEL"
  echo "Bundle ID:     $BUNDLE_ID"
  echo "------------------------------------------------------------"
  if xcrun devicectl device uninstall app --device "$DEVICE_UDID" "$BUNDLE_ID"; then
    echo ""
    echo "✔ App removido com sucesso! Atualizando lista..."
    sleep 1
  else
    echo ""
    echo "✖ Falha ao remover $BUNDLE_ID. Pressione qualquer tecla para voltar à lista..."
    read -rsn1
  fi
done
