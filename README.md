# Atendimento de Campo — API

API da plataforma de atendimento de campo hospitalar: cadastro do paciente,
triagem com protocolo START, consultas por especialidade, odontologia com
odontograma, enfermagem, dispensação de medicamentos e prontuário com trilha de
auditoria.

**Stack:** .NET 8 · ASP.NET Core · Entity Framework Core 8 · PostgreSQL

## Rodando

```bash
docker compose up -d          # Postgres em localhost:5432
dotnet run --project src/AtendimentoDeCampo.Api
```

A API aplica as migrations e o seed no boot (bases, CID-10 e catálogo de itens).
Swagger em `/swagger` no ambiente de desenvolvimento.

### Testes

```bash
dotnet test                   # só domínio, sem banco
```

Para incluir os testes de integração, aponte um Postgres de teste:

```bash
export ATENDIMENTO_TEST_DB="Host=localhost;Database=atendimento_campo_test;Username=postgres;Password=postgres"
dotnet test
```

Sem essa variável os testes de integração são pulados, não falham.

## Deploy no Railway

O repositório traz `Dockerfile` e `railway.json`. O Nixpacks não lida bem com
solução multi-projeto, então o build usa Docker explicitamente.

### 1. Banco

Adicione um **PostgreSQL** ao projeto no Railway. Ele expõe `DATABASE_URL` no
formato URI (`postgresql://usuário:senha@host:porta/banco`), que o Npgsql não
entende nativamente — a aplicação converte no boot.

Referencie a variável no serviço da API:

```
DATABASE_URL = ${{Postgres.DATABASE_URL}}
```

Prefira a URL **interna** (`*.railway.internal`) quando API e banco estão no
mesmo projeto: não sai para a internet e não consome tráfego de egresso.

### 2. Variáveis

| Variável | Valor |
|---|---|
| `DATABASE_URL` | `${{Postgres.DATABASE_URL}}` |
| `Jwt__Chave` | segredo com **32+ caracteres** |
| `Jwt__Emissor` | `atendimento-de-campo` |
| `Jwt__Audiencia` | `atendimento-de-campo-app` |
| `Auth__SenhaEquipe` | senha de primeiro acesso da equipe |
| `Cors__OrigensTexto` | URL do front, ex.: `https://seu-app.up.railway.app` |

`PORT` é injetada pela plataforma; não defina à mão.

O separador é `__` (dois sublinhados) — é assim que o ASP.NET Core mapeia
variável de ambiente para configuração aninhada.

`DATABASE_URL` tem prioridade sobre `ConnectionStrings__Postgres`. A ordem
importa: o `appsettings.json` traz uma conexão de desenvolvimento que nunca é
nula, e se ela vencesse o deploy tentaria `localhost:5432` e morreria no boot.

`Cors__OrigensTexto` aceita várias origens separadas por vírgula. Existe porque
o painel do Railway só oferece campo de texto simples, e a forma nativa de
declarar lista (`Cors__Origens__0`) é fácil de errar. Barra final é removida
automaticamente: o navegador envia a origem sem ela, e com a barra o CORS
falharia sem explicar o motivo.

### 3. Health check

`railway.json` aponta para `/health`. A aplicação roda migrations e seed no
boot, com espera e novas tentativas enquanto o banco não responde — no primeiro
deploy o Postgres costuma demorar alguns segundos a mais que a API.

### Como isso foi verificado

A aplicação publicada foi executada como a plataforma faz, apenas com `PORT` e
`DATABASE_URL` em formato URI, confirmando que ela escuta em `0.0.0.0` na porta
recebida, conecta ao banco, aplica migrations, semeia os dados, responde ao
health check, libera somente a origem configurada no CORS e mantém o Swagger
fora do ar fora de Development.

## Estrutura

| Projeto | Papel |
|---|---|
| `AtendimentoDeCampo.Domain` | Entidades, enums e regras puras (START, alergia, odontograma, dispensação, tempo de fila) |
| `AtendimentoDeCampo.Infrastructure` | `DbContext`, migrations, seed, auditoria |
| `AtendimentoDeCampo.Api` | Controllers, DTOs, autenticação JWT |
| `AtendimentoDeCampo.Tests` | Testes de domínio e de integração ponta a ponta |

## Autenticação

Login com **nome + função + registro do conselho + senha**. No primeiro acesso a
senha informada é a senha da equipe (`Auth:SenhaEquipe`) e a conta é criada
naquele momento. Como o login acontece antes da escolha da base, a senha da
equipe é global.

Em produção, troque `Jwt:Chave` e `Auth:SenhaEquipe` por variáveis de ambiente.

## Decisões de modelagem

Este backend foi desenhado a partir da análise de um sistema de campo em
produção. Sete problemas estruturais foram identificados nos registros reais e
tratados no modelo. Estão marcados com `CORRIGE:` no código.

### 1. Alerta de alergia com falso positivo

No sistema analisado a alergia era texto livre e o prontuário exibia alerta
vermelho para **qualquer** valor preenchido — inclusive `"Nega alergia
medicamentosa"`. Pacientes sem alergia apareciam com alerta de alergia.

Isso não é cosmético: alerta que aparece em todo mundo deixa de ser lido, e o
paciente realmente alérgico perde a proteção que o alerta existe para dar.

Agora `StatusAlergia` é um enum (`NaoPerguntado` / `SemAlergiaConhecida` /
`PossuiAlergia`) e o alerta só dispara no terceiro caso. A API rejeita descrição
de alergia combinada com estado que nega alergia.

### 2. Item dispensado como texto livre

O relatório de consumo real tinha `Acetaminofen`, `Acetaminofeno`,
`Acetominofen` e `1. Paracetamol 1 gramo` como quatro itens distintos, e
`Ceterizina` / `Cetericina` / `Cetirizina 10 mg` como outros três. A contagem de
consumo era inutilizável.

A dispensação agora aponta para `ItemCatalogo`. Item fora do catálogo continua
possível — é comum receber doação em campo — mas exige justificativa e fica
marcado para revisão.

### 3. Nota clínica no campo de medicamento

Uma conduta inteira (`"Confirmo tamanho de nódulo, oriento paciente quanto
seguimento e investigação com punção."`) estava registrada como item dispensado
e contava como insumo na estatística.

`ValidadorDispensacao` barra textos longos e textos com verbo de conduta em
primeira pessoa, apontando o profissional para o campo de conduta da consulta.

### 4. Via de administração incompatível com a apresentação

Existiam registros como `Prednisolona 20 mg · Xarope · comprimido` e
`Diclofenaco · Oral · 1 ampola`, porque via e forma eram escolhidas soltas.

`ItemCatalogo.ViasPermitidas` amarra as duas coisas, e a unidade da dispensação
vem do catálogo em vez de ser digitada.

### 5. Diagnóstico sem codificação

`J00`, `anemia` e `1. Mialgia 2. Artralgia` conviviam na mesma estatística de
"principais diagnósticos".

Agora há tabela `Cid10` com descrição nos três idiomas. O CID é obrigatório para
concluir consulta com alta ou encaminhamento; observação livre continua
disponível em campo separado.

### 6. Odontograma perdia estados sobrepostos

Um dente com cárie **e** extração indicada era pintado de uma cor só: o amarelo
cobria o rosa e a cárie sumia do desenho, sobrevivendo apenas no resumo textual.

`MarcacaoDente` é uma linha por estado, então estados coexistem por dente e por
face. Cárie + extração indicada é uma combinação clinicamente comum e não é
tratada como contraditória; ausente + qualquer outro estado, sim.

### 7. Edição após finalização sem rastro

Um atendimento finalizado às 15:49 aparecia editado às 15:56 e às 16:11, sem
trava, sem justificativa e sem indicação de que a edição veio depois do fecho.

Corrigir registro em campo é legítimo e continua permitido, mas agora exige
reabertura explícita com justificativa (`POST /reabrir`), e as edições seguintes
são gravadas como `EditouAposFinalizacao`.

### Ainda: separação de eixos

O sistema analisado misturava especialidade (Clínica Geral, Odontologia) com
cargo (Médico, Psicólogo, Enfermagem) num único campo "área", o que impedia
qualquer leitura confiável de produção. Aqui são dois eixos independentes:
`Especialidade` (a fila) e `FuncaoProfissional` (quem atendeu).

## Protocolo START

`ProtocoloStart.Avaliar` implementa o algoritmo (deambulação → respiração →
perfusão → consciência) e devolve uma **sugestão** com justificativa.

A classificação gravada é sempre a escolhida pelo profissional. Divergência
entre sugestão e escolha não bloqueia nada: é registrada na auditoria. Software
clínico não decide no lugar de quem está com o paciente na frente.

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/auth/login` | Login (cria conta no primeiro acesso) |
| `GET` | `/api/bases` | Bases ativas |
| `GET` | `/api/atendimentos` | Lista por base, fila, risco e busca |
| `GET` | `/api/atendimentos/{id}` | Prontuário completo |
| `GET` | `/api/atendimentos/codigo/{codigo}` | Busca pelo código curto |
| `POST` | `/api/atendimentos` | Cadastro do paciente + abertura |
| `PUT` | `/api/atendimentos/{id}/triagem` | Triagem e classificação START |
| `PUT` | `/api/atendimentos/{id}/consulta` | Consulta médica / pediatria / ortopedia / saúde mental |
| `PUT` | `/api/atendimentos/{id}/odontologia` | Odontologia e odontograma |
| `PUT` | `/api/atendimentos/{id}/enfermagem` | Procedimentos de enfermagem |
| `POST` | `/api/atendimentos/{id}/finalizar` | Finaliza |
| `POST` | `/api/atendimentos/{id}/reabrir` | Reabre com justificativa |
| `GET` | `/api/catalogo/itens` | Catálogo de medicamentos e insumos |
| `GET` | `/api/catalogo/cid10` | Catálogo CID-10 |

## Código do atendimento

Formato `PRE-XXXX`, ex.: `ACA-4K7Z`. O prefixo identifica a base e usa A–Z
inteiro para continuar reconhecível. O sufixo é sorteado e usa um alfabeto sem
`I`, `O`, `S`, `0`, `1` e `5`, porque é lido em voz alta e anotado à mão na fila.
