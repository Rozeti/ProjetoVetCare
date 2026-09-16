#!/usr/bin/env bash
# Cria um cenário de demonstração coerente com a documentação do VetCare:
# equipe completa, pacientes, tratamento, histórico clínico, carteira de
# vacinação, receita e alertas clínicos.
#
# Use apenas em ambiente de avaliação: as contas criadas têm senha conhecida.
set -u

API="${API_URL:-http://localhost:5265}"
SENHA_DEMO="${SENHA_DEMO:-vetcare123}"
CORPO=$(mktemp)

# O Git Bash corrompe bytes UTF-8 passados como argumento para o curl.exe, então
# o corpo vai para um arquivo (escrito pelo printf, que é builtin) e o curl lê de lá.
envia() { # envia METODO CAMINHO TOKEN JSON
  printf '%s' "$4" > "$CORPO"
  curl -s -X "$1" "$API$2" -H "Content-Type: application/json; charset=utf-8" \
       -H "Authorization: Bearer $3" --data-binary @"$CORPO"
}

val() { grep -o "\"$2\":\"[^\"]*\"" <<<"$1" | head -1 | sed 's/.*":"//; s/"$//'; }
dias() { date -u -d "$1 days" +%Y-%m-%d; }
quando() { date -u -d "$1 days $2" +%Y-%m-%dT%H:%M:%S; }

ADMIN=$(val "$(curl -s -X POST "$API/api/usuarios/login" -H 'Content-Type: application/json' \
  -d "{\"email\":\"admin@vetcare.com\",\"senha\":\"$SENHA_DEMO\"}")" token)

if [ -z "$ADMIN" ]; then
  echo "Falha ao autenticar admin@vetcare.com. A API está no ar e a senha é '$SENHA_DEMO'?"
  exit 1
fi

echo "== Equipe =="

r=$(envia POST /api/usuarios "$ADMIN" "{\"nome\":\"Dra. Helena Duarte\",\"email\":\"veterinario@vetcare.com\",\"senha\":\"$SENHA_DEMO\",\"perfil\":\"Veterinario\",\"crmv\":\"CRMV-DF 90210\",\"especialidade\":\"Fisioterapia e reabilitação\"}")
VET=$(val "$r" veterinarioId)
[ -z "$VET" ] && VET=$(curl -s "$API/api/veterinarios" -H "Authorization: Bearer $ADMIN" | tr ',' '\n' | grep -m1 '"id"' | sed 's/.*"id":"\([^"]*\)".*/\1/')
echo "  veterinária: $VET"

envia POST /api/usuarios "$ADMIN" "{\"nome\":\"Recepção VetSPA\",\"email\":\"apoio@vetcare.com\",\"senha\":\"$SENHA_DEMO\",\"perfil\":\"Apoio\",\"setor\":\"Recepção\"}" > /dev/null
echo "  recepção criada"

r=$(envia POST /api/usuarios "$ADMIN" "{\"nome\":\"Marcos Tirulli\",\"email\":\"tutor@vetcare.com\",\"senha\":\"$SENHA_DEMO\",\"perfil\":\"Tutor\",\"telefone\":\"(61) 99999-1234\",\"endereco\":\"Águas Claras, Brasília - DF\"}")
TUTOR=$(val "$r" tutorId)
[ -z "$TUTOR" ] && TUTOR=$(curl -s "$API/api/tutores/selecao" -H "Authorization: Bearer $ADMIN" | tr ',' '\n' | grep -m1 '"id"' | sed 's/.*"id":"\([^"]*\)".*/\1/')
echo "  tutor: $TUTOR"

echo "== Pacientes =="

r=$(envia POST /api/pets "$ADMIN" "{\"nome\":\"Thor\",\"especie\":\"Cachorro\",\"raca\":\"Golden Retriever\",\"sexo\":\"Macho\",\"pelagem\":\"Dourada longa\",\"microchip\":\"982000123456789\",\"castrado\":true,\"dataNascimento\":\"2019-04-12\",\"pesoAtualKg\":32.5,\"tutorId\":\"$TUTOR\"}")
PET=$(val "$r" id)
echo "  Thor: $PET"

r=$(envia POST /api/pets "$ADMIN" "{\"nome\":\"Nina\",\"especie\":\"Gato\",\"raca\":\"Siamês\",\"sexo\":\"Fêmea\",\"castrado\":true,\"dataNascimento\":\"2022-09-05\",\"pesoAtualKg\":4.2,\"tutorId\":\"$TUTOR\"}")
PET2=$(val "$r" id)
echo "  Nina: $PET2"

echo "== Alertas clínicos =="
envia POST /api/alergias "$ADMIN" "{\"pacienteId\":\"$PET\",\"tipo\":\"Alergia\",\"descricao\":\"Reação cutânea a dipirona. Evitar em qualquer protocolo analgésico.\",\"gravidade\":\"Grave\"}" > /dev/null
envia POST /api/alergias "$ADMIN" "{\"pacienteId\":\"$PET\",\"tipo\":\"Cirurgia\",\"descricao\":\"Reconstrução de ligamento cruzado cranial direito em 2026.\",\"gravidade\":\"Moderada\"}" > /dev/null
echo "  2 alertas registrados para Thor"

echo "== Carteira de vacinação =="
envia POST /api/vacinas "$ADMIN" "{\"pacienteId\":\"$PET\",\"veterinarioId\":\"$VET\",\"tipo\":\"Vacina\",\"nome\":\"V10 (múltipla canina)\",\"fabricante\":\"Zoetis\",\"lote\":\"A2291\",\"dataAplicacao\":\"$(dias -330)\",\"proximaDose\":\"$(dias 20)\"}" > /dev/null
envia POST /api/vacinas "$ADMIN" "{\"pacienteId\":\"$PET\",\"veterinarioId\":\"$VET\",\"tipo\":\"Vacina\",\"nome\":\"Antirrábica\",\"fabricante\":\"MSD\",\"lote\":\"R8842\",\"dataAplicacao\":\"$(dias -400)\",\"proximaDose\":\"$(dias -35)\"}" > /dev/null
envia POST /api/vacinas "$ADMIN" "{\"pacienteId\":\"$PET\",\"veterinarioId\":\"$VET\",\"tipo\":\"Vermifugo\",\"nome\":\"Vermífugo de amplo espectro\",\"dataAplicacao\":\"$(dias -60)\",\"proximaDose\":\"$(dias 120)\"}" > /dev/null
echo "  3 registros (uma dose vencida, uma a vencer, uma em dia)"

echo "== Tratamento e histórico clínico =="

r=$(envia POST /api/tratamentos "$ADMIN" "{\"pacienteId\":\"$PET\",\"veterinarioId\":\"$VET\",\"dataInicio\":\"$(dias -30)\",\"objetivoTerapeutico\":\"Recuperar a amplitude de movimento do membro posterior direito após cirurgia de ruptura de ligamento cruzado.\",\"observacoesGerais\":\"Paciente colaborativo. Tutor orientado sobre exercícios domiciliares.\"}")
TRAT=$(val "$r" id)
echo "  tratamento: $TRAT"

envia POST /api/avaliacoes "$ADMIN" "{\"tratamentoId\":\"$TRAT\",\"veterinarioId\":\"$VET\",\"queixaPrincipal\":\"Claudicação do membro posterior direito há três semanas.\",\"anamnese\":\"Pós-operatório de ruptura de ligamento cruzado cranial, cirurgia há 45 dias.\",\"exameFisico\":\"Atrofia muscular em quadríceps direito, amplitude de movimento reduzida em 30%.\",\"hipoteseDiagnostica\":\"Atrofia muscular e rigidez articular pós-cirúrgica.\",\"planoTerapeutico\":\"Hidroterapia duas vezes por semana, laserterapia e exercícios proprioceptivos.\",\"observacaoInterna\":\"Avaliar nova intervenção cirúrgica caso não haja ganho funcional em 60 dias. Não comunicar ao tutor antes da reavaliação.\"}" > /dev/null
echo "  avaliação clínica registrada"

PESOS=(33.4 33.0 32.7)
DORES=(7 5 3)

for i in 0 1 2; do
  DIA=$(( 3 - i ))
  r=$(envia POST /api/sessoes "$ADMIN" "{\"tratamentoId\":\"$TRAT\",\"veterinarioId\":\"$VET\",\"dataHora\":\"$(quando "$DIA" 14:00)\",\"observacoes\":\"Sessão conforme o plano terapêutico.\"}")
  SESSAO=$(val "$r" id)

  if [ -n "$SESSAO" ]; then
    envia POST /api/atendimentos "$ADMIN" "{\"sessaoId\":\"$SESSAO\",\"veterinarioId\":\"$VET\",\"tecnicasAplicadas\":\"Hidroterapia, Laserterapia, Cinesioterapia\",\"escalaDor\":${DORES[$i]},\"evolucaoClinica\":\"Ganho progressivo de amplitude; o apoio do membro melhorou nesta sessão.\",\"proximosPassos\":\"Manter o protocolo e reavaliar a amplitude na próxima sessão.\",\"pesoKg\":${PESOS[$i]},\"temperaturaCelsius\":38.4,\"frequenciaCardiaca\":92,\"frequenciaRespiratoria\":24,\"observacaoInterna\":\"Paciente reage ao manuseio do membro; conduzir com cautela.\"}" > /dev/null
  fi
done
echo "  3 sessões concluídas com atendimento e evolução de peso/dor"

for d in 7 14; do
  envia POST /api/sessoes "$ADMIN" "{\"tratamentoId\":\"$TRAT\",\"veterinarioId\":\"$VET\",\"dataHora\":\"$(quando "$d" 13:00)\",\"observacoes\":\"Trazer o pet em jejum de duas horas.\"}" > /dev/null
done
echo "  2 sessões futuras aguardando confirmação do tutor"

echo "== Receituário =="
envia POST /api/prescricoes "$ADMIN" "{\"pacienteId\":\"$PET\",\"veterinarioId\":\"$VET\",\"orientacoes\":\"Suspender exercícios de impacto até a próxima reavaliação. Retornar imediatamente em caso de aumento da claudicação.\",\"itens\":[{\"medicamento\":\"Condroitina + Glucosamina 500mg\",\"dosagem\":\"1 comprimido\",\"frequencia\":\"a cada 24 horas\",\"duracao\":\"por 60 dias\",\"via\":\"Oral\",\"observacao\":\"Administrar junto com alimento.\"},{\"medicamento\":\"Meloxicam 2mg\",\"dosagem\":\"meio comprimido\",\"frequencia\":\"a cada 24 horas\",\"duracao\":\"por 5 dias\",\"via\":\"Oral\",\"observacao\":\"Não associar a outros anti-inflamatórios.\"}]}" > /dev/null
echo "  receita com 2 medicamentos emitida"

echo
echo "Dados de demonstração criados."
echo "Acessos (senha '$SENHA_DEMO'): admin@ / veterinario@ / apoio@ / tutor@vetcare.com"
