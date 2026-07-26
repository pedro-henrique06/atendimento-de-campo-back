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
| `Admin__Usuario` | usuário do administrador inicial |
| `Admin__Senha` | senha do administrador inicial |
| `Admin__Nome` | nome exibido do administrador |
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

## Contas e autenticação

Registro e login são fluxos separados.

**Registro** (`POST /api/auth/registrar`) é aberto, mas a conta nasce
`Pendente` e **não acessa nada** até um administrador aprovar. Num prontuário
isso vale o atrito: cada ato clínico fica atribuído a uma pessoa, e a aprovação
é o momento em que alguém confirma que essa pessoa é quem diz ser.

**Login** (`POST /api/auth/login`) é por **usuário e senha**. O usuário é curto,
único e normalizado — `Claudia.Luz` e `claudia.luz` são a mesma conta.

O modelo anterior identificava o profissional por nome + função, com índice
único. Duas pessoas homônimas na mesma função não conseguiam ter conta: a
segunda simplesmente falhava ao se registrar.

A resposta do login distingue os motivos da recusa:

| Motivo | Significado |
|---|---|
| `CredenciaisInvalidas` | usuário não existe **ou** senha errada |
| `ContaPendente` | aguardando aprovação |
| `ContaRecusada` | recusada; vem com o motivo escrito pelo administrador |
| `ContaDesativada` | acesso revogado |

Usuário inexistente e senha errada compartilham a mesma resposta de propósito —
é o único caso em que a distinção revelaria quem tem conta. Nos demais a pessoa
já provou a senha, então explicar não vaza nada, e sem isso ela ficaria tentando
de novo achando que errou.

### Primeiro administrador

Sem administrador o sistema nasce travado: toda conta fica pendente e não há
ninguém para aprovar. Configure:

```
Admin__Usuario   = coordenacao
Admin__Nome      = Coordenação de Campo
Admin__Senha     = <senha forte>
```

**Não existe administrador padrão embutido.** Um usuário `admin` com senha
conhecida num sistema público seria pior que o problema que resolve. Sem essas
variáveis o boot apenas registra um aviso.

Se a conta indicada já existir, ela é reativada e volta a ser administradora —
mas a senha não é sobrescrita, para não atrapalhar quem já usa a conta.

Em produção, defina `Jwt__Chave` e as variáveis `Admin__*` por ambiente.

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
| `POST` | `/api/auth/registrar` | Cria conta, pendente de aprovação |
| `GET` | `/api/auth/usuario-disponivel` | Verifica se o usuário está livre |
| `POST` | `/api/auth/login` | Login por usuário e senha |
| `GET` | `/api/profissionais` | Lista contas (administrador) |
| `POST` | `/api/profissionais/{id}/aprovar` | Aprova conta (administrador) |
| `POST` | `/api/profissionais/{id}/recusar` | Recusa com motivo (administrador) |
| `POST` | `/api/profissionais/{id}/desativar` | Revoga acesso (administrador) |
| `POST` | `/api/profissionais/{id}/administrador` | Concede ou remove administração |
| `GET` | `/api/bases` | Bases ativas |
| `GET` | `/api/bases/todas` | Todas, inclusive inativas (administrador) |
| `GET` | `/api/bases/prefixo-sugerido` | Prefixo livre derivado do nome (administrador) |
| `POST` | `/api/bases` | Cria base (administrador) |
| `PUT` | `/api/bases/{id}` | Renomeia; prefixo só antes do primeiro atendimento |
| `POST` | `/api/bases/{id}/ativa` | Ativa ou desativa (administrador) |
| `GET` | `/api/pacientes/codigo-novo` | Sorteia um código livre, sem gravar nada |
| `GET` | `/api/pacientes/codigo/{codigo}` | Reencontra o paciente pelo código dele ou de um atendimento |
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

## Os dois códigos

**Código do atendimento** — formato `PRE-XXXX`, ex.: `ACA-4K7Z`. Identifica uma
visita. O prefixo identifica a base e usa A–Z inteiro para continuar
reconhecível.

**Código do paciente** — formato `XXXX-XXXX`, ex.: `4K7Z-2YAP`. Identifica a
pessoa, e é o que ela leva anotada. Sem documento, é o único jeito de reencontrar
alguém na visita seguinte — e em campo a maioria não tem documento.

Os dois sorteiam de um alfabeto sem `I`, `O`, `S`, `0`, `1` e `5`: são lidos em
voz alta e copiados à mão num papel que pode passar semanas no bolso.

Os formatos são deliberadamente diferentes porque os dois circulam na mesma fila
e alguém vai digitar um no lugar do outro. `/api/pacientes/codigo/{codigo}`
aceita os dois e chega na mesma pessoa, em vez de responder "não encontrado".

### Cadastro de bases

O prefixo entra no código de cada atendimento aberto na base (`ACA-4K7Z`). Depois
que o primeiro atendimento sai, ele está impresso em papéis que a equipe já
distribuiu — trocá-lo faria o papel dizer uma base e o sistema dizer outra. Por
isso **o prefixo só é editável enquanto a base não tem atendimento nenhum**. O
nome continua livre para sempre.

**Base não se apaga, se desativa.** O histórico dos atendimentos aponta para ela,
e apagar levaria junto o registro de onde cada pessoa foi atendida. Duas recusas
protegem a operação:

- desativar base com atendimento **em aberto** — a seleção só lista bases ativas,
  então isso sumiria com gente que ainda está na fila, sem caminho de volta;
- desativar a **única** base ativa — sem base ativa ninguém escolhe base, e sem
  base escolhida o app inteiro para, inclusive a tela de gestão.

### O código nasce antes do cadastro

`GET /api/pacientes/codigo-novo` sorteia um código livre e **não grava nada**. O
cadastro só nasce quando o formulário é salvo, já com o consentimento marcado —
um código sorteado e abandonado não deixa rastro. A unicidade de verdade é o
índice único em `pacientes."Codigo"`; a consulta do sorteio não protege contra
dois cadastros entrando no mesmo instante.

Na criação do atendimento o código vem primeiro na identificação do paciente, e
o documento fica como segunda via: quem perdeu o papel e voltou com a cédula na
mão não pode virar um cadastro novo. Quando o documento reencontra um cadastro
que já tem outro código, o código antigo prevalece — é o que está escrito no
papel de quem voltou.
