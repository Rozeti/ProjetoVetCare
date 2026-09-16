#!/usr/bin/env bash
# Funções compartilhadas pelos roteiros de verificação.
#
# Os endpoints de autenticação têm limitação de requisições (RNF-002) e os roteiros
# exercitam o bloqueio de propósito. Rodar dois em sequência esgotaria a janela, então o
# login espera a janela liberar em vez de acusar uma falha que não existe.

API="${API_URL:-http://localhost:5265}"

# Primeiro valor textual de uma chave JSON. Sem greediness: com envelope paginado, um
# casamento guloso devolveria o campo de um objeto aninhado em vez do primeiro item.
extrair() { grep -o "\"$2\":\"[^\"]*\"" <<<"$1" | head -1 | sed 's/.*":"//; s/"$//'; }

# entrar EMAIL SENHA -> imprime o token (vazio se as credenciais forem recusadas)
entrar() {
  local email="$1" senha="$2" tentativa resposta corpo codigo

  for tentativa in $(seq 1 10); do
    resposta=$(curl -s -w '\n%{http_code}' -X POST "$API/api/usuarios/login" \
      -H 'Content-Type: application/json' -d "{\"email\":\"$email\",\"senha\":\"$senha\"}")

    codigo=${resposta##*$'\n'}
    corpo=${resposta%$'\n'*}

    if [ "$codigo" != "429" ]; then
      extrair "$corpo" token
      return 0
    fi

    sleep 10
  done

  return 1
}

# Repete uma chamada enquanto o limitador estiver recusando. Os endpoints de autenticação
# dividem uma janela de 10 requisições por minuto, e um roteiro longo a esgota com facilidade.
# Uso: resposta=$(repetir_se_limitado envia POST /caminho "$TOKEN" "$CORPO")
repetir_se_limitado() {
  local tentativa resposta

  for tentativa in $(seq 1 8); do
    resposta=$("$@")

    grep -q "Muitas tentativas" <<<"$resposta" || { printf '%s' "$resposta"; return 0; }
    sleep 10
  done

  printf '%s' "$resposta"
  return 1
}

# Espera a janela do limitador liberar antes de o roteiro começar.
aguardar_limitador() {
  local tentativa codigo

  for tentativa in $(seq 1 10); do
    codigo=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/usuarios/login" \
      -H 'Content-Type: application/json' -d '{"email":"sonda@vetcare.com","senha":"sonda"}')

    [ "$codigo" != "429" ] && return 0
    sleep 10
  done

  return 1
}
