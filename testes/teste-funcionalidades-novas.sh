#!/usr/bin/env bash
# Verifica as funcionalidades acrescentadas ao núcleo do sistema: carteira de
# vacinação, receituário, alertas clínicos, bloqueios de agenda, auditoria,
# paginação, notificações por e-mail e push, recuperação de senha e limitação de requisições.
set -u

source "$(dirname "${BASH_SOURCE[0]}")/comum.sh"
ok=0; falhou=0
CORPO=$(mktemp)

envia() { printf '%s' "$4" > "$CORPO"; curl -s -X "$1" "$API$2" -H "Content-Type: application/json; charset=utf-8" -H "Authorization: Bearer $3" --data-binary @"$CORPO"; }
get() { curl -s "$API$1" -H "Authorization: Bearer $2"; }
val() { extrair "$1" "$2"; }
dias() { date -u -d "$1 days" +%Y-%m-%d; }

checa() {
  if grep -q "$2" <<<"$3"; then echo "  OK   $1"; ok=$((ok+1));
  else echo "  FALHA $1"; echo "        esperava: $2"; echo "        recebeu: $(head -c 240 <<<"$3")"; falhou=$((falhou+1)); fi
}

aguardar_limitador

ADMIN=$(entrar admin@vetcare.com vetcare123)
TUTOR=$(entrar tutor@vetcare.com vetcare123)
VETTK=$(entrar veterinario@vetcare.com vetcare123)

if [ -z "$ADMIN" ]; then
  echo "Falha ao autenticar. A API está no ar e os dados de demonstração foram criados?"
  exit 1
fi

# A documentação interativa só é publicada fora de produção, o que serve de sonda para
# saber em qual ambiente a API está rodando.
if [ "$(curl -s -o /dev/null -w '%{http_code}' "$API/openapi/v1.json")" = "200" ]; then
  AMBIENTE=Development
else
  AMBIENTE=Production
fi
echo "Ambiente da API: $AMBIENTE"
echo

PET=$(val "$(get '/api/pets?busca=Thor' "$ADMIN")" id)
# Com um veterinário responsável por paciente, as sessões de Thor só entram na agenda dele.
VET=$(val "$(get '/api/pets?busca=Thor' "$ADMIN")" veterinarioResponsavelId)
[ -z "$VET" ] && VET=$(val "$(get /api/veterinarios "$ADMIN")" id)

echo "== Paginação (RNF-004) =="
r=$(get '/api/pets?pagina=1&tamanho=1' "$ADMIN")
checa "listagem devolve envelope paginado" '"totalDePaginas"' "$r"
checa "tamanho da página é respeitado" '"tamanho":1' "$r"

r=$(get '/api/pets?pagina=1&tamanho=5000' "$ADMIN")
checa "tamanho acima do limite é reduzido a 100" '"tamanho":100' "$r"

echo "== Alertas clínicos =="
r=$(get "/api/alergias/paciente/$PET" "$ADMIN")
checa "alertas do paciente listados" "dipirona" "$r"

r=$(get "/api/prontuarios/paciente/$PET" "$TUTOR")
checa "tutor enxerga os alertas clínicos" "dipirona" "$r"
checa "tutor não recebe observação interna" '"exibeObservacoesInternas":false' "$r"

if grep -q "intervenção cirúrgica caso não haja" <<<"$r"; then
  echo "  FALHA RN-003 observação interna vazou"; falhou=$((falhou+1))
else
  echo "  OK   RN-003 nenhuma observação interna no payload do tutor"; ok=$((ok+1))
fi

echo "== Carteira de vacinação =="
r=$(get "/api/vacinas/paciente/$PET" "$ADMIN")
checa "carteira lista as aplicações" "Antirrábica" "$r"
checa "situação da dose é calculada" '"situacaoDose"' "$r"
checa "dose vencida é sinalizada" '"Vencida"' "$r"
checa "dose próxima é sinalizada" '"A vencer"' "$r"

r=$(get '/api/vacinas/vencendo?dias=30' "$ADMIN")
checa "painel de prevenção lista doses a vencer" "Antirrábica" "$r"

r=$(envia POST /api/vacinas "$ADMIN" "{\"pacienteId\":\"$PET\",\"tipo\":\"Vacina\",\"nome\":\"Teste\",\"dataAplicacao\":\"$(dias -10)\",\"proximaDose\":\"$(dias -20)\"}")
checa "próxima dose anterior à aplicação é recusada" "posterior" "$r"

r=$(envia POST /api/vacinas "$ADMIN" "{\"pacienteId\":\"$PET\",\"tipo\":\"Suplemento\",\"nome\":\"Teste\",\"dataAplicacao\":\"$(dias -10)\"}")
checa "tipo inválido é recusado" "Tipo inválido" "$r"

r=$(get "/api/vacinas/paciente/$PET" "$TUTOR")
checa "tutor consulta a carteira do próprio pet" "Antirrábica" "$r"

echo "== Receituário =="
r=$(get "/api/prescricoes/paciente/$PET" "$ADMIN")
checa "receitas do paciente listadas" "Meloxicam" "$r"
PRESC=$(val "$r" id)

r=$(get "/api/prescricoes/$PRESC" "$TUTOR")
checa "tutor acessa a própria receita" "Condroitina" "$r"
checa "receita traz CRMV para impressão" '"crmv"' "$r"

r=$(envia POST /api/prescricoes "$ADMIN" "{\"pacienteId\":\"$PET\",\"itens\":[]}")
checa "receita sem medicamento é recusada" "medicamento" "$r"

echo "== Bloqueios de agenda =="
r=$(envia POST /api/bloqueios-agenda "$ADMIN" "{\"veterinarioId\":\"$VET\",\"inicio\":\"$(date -u -d '+45 days 08:00' +%Y-%m-%dT%H:%M:%S)\",\"fim\":\"$(date -u -d '+50 days 18:00' +%Y-%m-%dT%H:%M:%S)\",\"motivo\":\"Congresso de fisioterapia\"}")
BLOQ=$(val "$r" id)
checa "bloqueio criado" "Congresso" "$r"

TRAT=$(val "$(get "/api/tratamentos/paciente/$PET" "$ADMIN")" id)
r=$(envia POST /api/sessoes "$ADMIN" "{\"tratamentoId\":\"$TRAT\",\"veterinarioId\":\"$VET\",\"dataHora\":\"$(date -u -d '+47 days 14:00' +%Y-%m-%dT%H:%M:%S)\"}")
checa "agendamento em período bloqueado é recusado" "bloqueada" "$r"

r=$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$API/api/bloqueios-agenda/$BLOQ" -H "Authorization: Bearer $ADMIN")
checa "bloqueio removido" "200" "$r"

echo "== Auditoria =="
r=$(get '/api/auditoria?tamanho=5' "$ADMIN")
checa "trilha de auditoria acessível ao administrador" '"itens"' "$r"

r=$(get '/api/auditoria?entidade=Prontuario&tamanho=5' "$ADMIN")
checa "consulta ao prontuário foi registrada" "Prontuario" "$r"

r=$(curl -s -o /dev/null -w "%{http_code}" "$API/api/auditoria" -H "Authorization: Bearer $VETTK")
checa "auditoria fechada ao veterinário" "403" "$r"

echo "== Notificações por e-mail e push =="
# HU-015: além do mural do sistema, o usuário escolhe se quer os avisos por e-mail e no celular.
r=$(envia PUT /api/usuarios/me/preferencias-de-notificacao "$TUTOR" '{"notificarPorEmail":false,"notificarPorPush":true}')
checa "preferência de e-mail desligada" '"notificarPorEmail":false' "$r"
checa "preferência de push mantida" '"notificarPorPush":true' "$r"

r=$(envia PUT /api/usuarios/me/preferencias-de-notificacao "$TUTOR" '{"notificarPorEmail":true,"notificarPorPush":true}')
checa "preferência de e-mail religada" '"notificarPorEmail":true' "$r"

# O aplicativo registra o token de push do aparelho; a API o usa para entregar os avisos.
r=$(envia POST /api/dispositivos "$TUTOR" '{"tokenPush":"ExponentPushToken[roteiro-e2e]","plataforma":"android","nomeDoAparelho":"Celular do roteiro"}')
checa "aparelho registrado para push (o token não volta na resposta)" '"nomeDoAparelho":"Celular do roteiro"' "$r"

r=$(get /api/dispositivos "$TUTOR")
checa "aparelho aparece na lista do usuário" '"plataforma":"android"' "$r"

r=$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$API/api/dispositivos?token=ExponentPushToken%5Broteiro-e2e%5D" -H "Authorization: Bearer $TUTOR")
checa "aparelho removido ao sair da conta" "20[04]" "$r"

# Conta criada sem senha: o próprio usuário a define pelo e-mail de boas-vindas (primeiro acesso).
NOVO_EMAIL="primeiro-acesso-$RANDOM@vetcare.com"
r=$(envia POST /api/usuarios "$ADMIN" "{\"nome\":\"Primeiro Acesso\",\"email\":\"$NOVO_EMAIL\",\"perfil\":\"Apoio\",\"setor\":\"Recepção\"}")
checa "usuário criado sem senha recebe convite por e-mail" "$NOVO_EMAIL" "$r"

echo "== Recuperação de senha =="
# Estes endpoints dividem a política de limite com o login: as autenticações
# feitas acima podem ter consumido a janela.
aguardar_limitador

r=$(repetir_se_limitado envia POST /api/usuarios/recuperar-senha "" '{"email":"nao-existe@vetcare.com"}')
checa "e-mail inexistente recebe resposta neutra" "Se houver uma conta" "$r"

if [ "$AMBIENTE" = "Development" ]; then
  # Fora de produção, e sem servidor de e-mail, o link e o código vêm na própria resposta,
  # o que permite percorrer os dois caminhos (portal e aplicativo) sem caixa de entrada.
  r=$(repetir_se_limitado envia POST /api/usuarios/recuperar-senha "" "{\"email\":\"$NOVO_EMAIL\"}")
  CODIGO=$(val "$r" codigoDesenvolvimento)
  checa "solicitação gera código de 6 dígitos (aplicativo)" '^[0-9]\{6\}$' "$CODIGO"
  checa "solicitação gera link (portal)" '"tokenDesenvolvimento":"' "$r"

  r=$(repetir_se_limitado envia POST /api/usuarios/redefinir-senha "" "{\"email\":\"$NOVO_EMAIL\",\"codigo\":\"000000\",\"novaSenha\":\"NovaSenha2026\"}")
  checa "código errado é recusado e conta a tentativa" "Código incorreto" "$r"

  r=$(repetir_se_limitado envia POST /api/usuarios/redefinir-senha "" "{\"email\":\"$NOVO_EMAIL\",\"codigo\":\"$CODIGO\",\"novaSenha\":\"NovaSenha2026\"}")
  checa "senha definida com o código recebido" "sucesso" "$r"

  NOVO=$(entrar "$NOVO_EMAIL" NovaSenha2026)
  checa "usuário sem senha inicial consegue entrar" "." "$NOVO"

  r=$(repetir_se_limitado envia POST /api/usuarios/recuperar-senha "" '{"email":"apoio@vetcare.com"}')
  TOKEN=$(val "$r" tokenDesenvolvimento)

  r=$(repetir_se_limitado envia POST /api/usuarios/redefinir-senha "" "{\"token\":\"$TOKEN\",\"novaSenha\":\"NovaSenha2026\"}")
  checa "senha redefinida pelo link do e-mail" "sucesso" "$r"

  NOVO=$(entrar apoio@vetcare.com NovaSenha2026)
  checa "login com a nova senha funciona" "." "$NOVO"

  r=$(repetir_se_limitado envia POST /api/usuarios/redefinir-senha "" "{\"token\":\"$TOKEN\",\"novaSenha\":\"OutraSenha2026\"}")
  checa "link de uso único não é reaproveitado" "inválido ou expirado" "$r"

  r=$(envia PUT /api/usuarios/me/senha "$NOVO" '{"senhaAtual":"NovaSenha2026","novaSenha":"vetcare123"}')
  checa "senha restaurada para o padrão de demonstração" "sucesso" "$r"
else
  # Em produção o link e o código só saem por e-mail: a resposta não pode entregá-los a quem pediu.
  r=$(repetir_se_limitado envia POST /api/usuarios/recuperar-senha "" '{"email":"apoio@vetcare.com"}')
  checa "solicitação aceita sem expor as credenciais" "Se houver uma conta" "$r"

  # Procura um valor de verdade, e não só o nome do campo.
  if grep -q '"tokenDesenvolvimento":"' <<<"$r"; then
    echo "  FALHA link de recuperação vazou em produção"; falhou=$((falhou+1))
  else
    echo "  OK   link de recuperação não é exposto em produção"; ok=$((ok+1))
  fi

  if grep -q '"codigoDesenvolvimento":"' <<<"$r"; then
    echo "  FALHA código de recuperação vazou em produção"; falhou=$((falhou+1))
  else
    echo "  OK   código de recuperação não é exposto em produção"; ok=$((ok+1))
  fi

  r=$(repetir_se_limitado envia POST /api/usuarios/redefinir-senha "" '{"token":"token-invalido","novaSenha":"NovaSenha2026"}')
  checa "link inválido é recusado" "inválido ou expirado" "$r"
fi

echo "== Saúde e documentação =="
r=$(curl -s "$API/health/pronto")
checa "health check verifica o banco" '"banco-de-dados"' "$r"
checa "aplicação saudável" "Healthy" "$r"

r=$(curl -s -o /dev/null -w "%{http_code}" "$API/openapi/v1.json")

if [ "$AMBIENTE" = "Development" ]; then
  checa "especificação OpenAPI publicada" "200" "$r"
else
  # Publicar a superfície da API em produção entrega o mapa do sistema a quem quiser atacá-lo.
  checa "documentação desligada em produção" "404" "$r"
fi

echo "== Limitação de requisições =="
excedeu=0
for i in $(seq 1 15); do
  codigo=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API/api/usuarios/login" \
    -H "Content-Type: application/json" -d '{"email":"forca-bruta@vetcare.com","senha":"tentativa"}')
  [ "$codigo" = "429" ] && excedeu=1 && break
done
checa "excesso de tentativas é barrado com 429" "1" "$excedeu"

echo
echo "================================"
echo " OK: $ok   FALHAS: $falhou"
echo "================================"
[ "$falhou" -eq 0 ]
