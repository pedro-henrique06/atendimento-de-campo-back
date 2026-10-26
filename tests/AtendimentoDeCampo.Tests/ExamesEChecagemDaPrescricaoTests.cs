using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// O que faltava da pagina "Prescricao Medica": os exames complementares que a
/// consulta pede, os procedimentos feitos ali mesmo, e a assinatura ao lado de
/// cada linha da prescricao.
/// </summary>
[Collection(Colecoes.Api)]
public class ExamesEChecagemDaPrescricaoTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ExamesEChecagemDaPrescricaoTests(ApiFixture fixture) => _fixture = fixture;

    private Task<HttpClient> ClinicoAsync(string usuario = "prescricao.clinico")
        => _fixture.ClienteDeAsync(usuario, "Clinico Da Prescricao", FuncaoProfissional.Coordenacao);

    private static async Task<Guid> NaClinicaAsync(HttpClient client, string nome)
    {
        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        var criado = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId = bases![0].Id,
            paciente = new
            {
                codigo,
                nome,
                tipoDocumento = "SemDocumento",
                idadeAproximada = 39,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        criado.EnsureSuccessStatusCode();

        var prontuario = (await criado.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;

        (await client.PutAsJsonAsync($"/api/atendimentos/{prontuario.Id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            encaminhamento = "ClinicaGeral"
        }, Json)).EnsureSuccessStatusCode();

        return prontuario.Id;
    }

    private static async Task<ConsultaDto> SalvarConsultaAsync(HttpClient client, Guid id, object consulta)
    {
        (await client.PutAsJsonAsync($"/api/atendimentos/{id}/consulta", consulta, Json))
            .EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>($"/api/atendimentos/{id}", Json);

        return prontuario!.Consultas.Single(c => c.Especialidade == Especialidade.ClinicaGeral);
    }

    private static async Task<Guid> ItemAsync(HttpClient client, string nome)
    {
        var itens = await client.GetFromJsonAsync<List<ItemCatalogoDto>>("/api/catalogo/itens", Json);

        return itens!.First(i => i.Nome.Contains(nome, StringComparison.OrdinalIgnoreCase)).Id;
    }

    [SkippableFact]
    public async Task Os_exames_pedidos_na_consulta_voltam_no_prontuario()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ClinicoAsync();
        var id = await NaClinicaAsync(client, "Paciente Com Exames");

        var consulta = await SalvarConsultaAsync(client, id, new
        {
            especialidade = "ClinicaGeral",
            sintomasDescricao = "Fraqueza e palidez ha um mes.",
            exameLaboratorialSolicitado = true,
            exameLaboratorialDescricao = "Hemograma, ferritina, parasitologico de fezes.",
            papanicolauSolicitado = true,
            papanicolauObservacao = "Ultimo ha mais de cinco anos.",
            outroExameSolicitado = "Teste rapido de sifilis."
        });

        Assert.True(consulta.ExameLaboratorialSolicitado);
        Assert.Equal("Hemograma, ferritina, parasitologico de fezes.", consulta.ExameLaboratorialDescricao);
        Assert.True(consulta.PapanicolauSolicitado);
        Assert.Equal("Teste rapido de sifilis.", consulta.OutroExameSolicitado);
    }

    [SkippableFact]
    public async Task O_detalhe_do_exame_nao_e_gravado_sem_o_pedido()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ClinicoAsync();
        var id = await NaClinicaAsync(client, "Paciente Sem Exames");

        // "Quais exames" ao lado de "nao pedi nenhum" e contradicao gravada, e
        // quem le depois acredita no detalhe, nao na caixa.
        var consulta = await SalvarConsultaAsync(client, id, new
        {
            especialidade = "ClinicaGeral",
            exameLaboratorialSolicitado = false,
            exameLaboratorialDescricao = "Hemograma.",
            papanicolauSolicitado = false,
            papanicolauObservacao = "Ultimo ha cinco anos.",
            procedimentosObservacao = "Curativo em perna direita."
        });

        Assert.Null(consulta.ExameLaboratorialDescricao);
        Assert.Null(consulta.PapanicolauObservacao);
        Assert.Null(consulta.ProcedimentosObservacao);
    }

    [SkippableFact]
    public async Task Os_procedimentos_feitos_na_consulta_sao_registraveis()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ClinicoAsync();
        var id = await NaClinicaAsync(client, "Paciente Com Procedimento");

        var consulta = await SalvarConsultaAsync(client, id, new
        {
            especialidade = "ClinicaGeral",
            procedimentos = new[] { "PassagemDiu", "Curativo" },
            procedimentosObservacao = "DIU de cobre. Curativo em perna direita."
        });

        Assert.Equal(
            [ProcedimentoNaConsulta.PassagemDiu, ProcedimentoNaConsulta.Curativo],
            consulta.Procedimentos);

        Assert.Contains("DIU de cobre", consulta.ProcedimentosObservacao);
    }

    [SkippableFact]
    public async Task Checar_assina_a_linha_com_quem_checou_e_quando()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ClinicoAsync();
        var id = await NaClinicaAsync(client, "Paciente Que Recebeu");
        var dipirona = await ItemAsync(client, "Dipirona");

        var consulta = await SalvarConsultaAsync(client, id, new
        {
            especialidade = "ClinicaGeral",
            cid10Codigo = "R51",
            conduta = "Analgesia.",
            desfecho = "Alta",
            dispensacoes = new[]
            {
                new { itemId = dipirona, quantidade = 10, via = "Oral", posologia = "6/6 h se dor." }
            }
        });

        var linha = consulta.Dispensacoes.Single();

        Assert.Null(linha.ChecadaEm);
        Assert.Null(linha.ChecadaPor);

        // Quem prescreve e uma pessoa, quem administra e outra: a assinatura ao
        // lado do item e o que liga as duas.
        var checada = await client.PostAsJsonAsync(
            $"/api/atendimentos/{id}/prescricao/{linha.Id}/checagem",
            new { checada = true },
            Json);

        checada.EnsureSuccessStatusCode();

        var depois = (await checada.Content.ReadFromJsonAsync<DispensacaoDto>(Json))!;

        Assert.NotNull(depois.ChecadaEm);
        Assert.Equal("Clinico Da Prescricao", depois.ChecadaPor!.Nome);
    }

    [SkippableFact]
    public async Task Rechecar_nao_reescreve_a_hora_e_desmarcar_limpa()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ClinicoAsync();
        var id = await NaClinicaAsync(client, "Paciente Da Hora Da Checagem");
        var paracetamol = await ItemAsync(client, "Paracetamol");

        var consulta = await SalvarConsultaAsync(client, id, new
        {
            especialidade = "ClinicaGeral",
            dispensacoes = new[]
            {
                new { itemId = paracetamol, quantidade = 12, via = "Oral", posologia = "8/8 h." }
            }
        });

        var linhaId = consulta.Dispensacoes.Single().Id;

        async Task<DispensacaoDto> ChecarAsync(bool checada)
        {
            var resposta = await client.PostAsJsonAsync(
                $"/api/atendimentos/{id}/prescricao/{linhaId}/checagem",
                new { checada },
                Json);

            resposta.EnsureSuccessStatusCode();

            return (await resposta.Content.ReadFromJsonAsync<DispensacaoDto>(Json))!;
        }

        var primeira = await ChecarAsync(true);

        await Task.Delay(1100);

        var segunda = await ChecarAsync(true);

        /*
            A hora da checagem e a da checagem. Reescrita a cada toque, ela
            viraria a hora do ultimo toque — pelo mesmo motivo das paradas da
            lista de verificacao cirurgica.

            Igualdade exata, e nao margem: a hora gravada e truncada para a
            precisao do Postgres na ida, entao a resposta da primeira checagem
            — que sai da entidade em memoria — e identica a que qualquer
            leitura seguinte traz.
        */
        Assert.Equal(primeira.ChecadaEm, segunda.ChecadaEm);

        var desmarcada = await ChecarAsync(false);

        Assert.Null(desmarcada.ChecadaEm);
        Assert.Null(desmarcada.ChecadaPor);

        // Desmarcar e a correcao de quem checou a linha errada, e numa
        // prescricao e exatamente o tipo de coisa que alguem vai querer
        // explicar depois.
        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>($"/api/atendimentos/{id}", Json);

        Assert.Contains(prontuario!.Historico, h => h.Campo == "prescricao.checagem");
    }

    [SkippableFact]
    public async Task O_que_ja_foi_dado_nao_some_quando_a_prescricao_e_reescrita()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ClinicoAsync();
        var id = await NaClinicaAsync(client, "Paciente Da Prescricao Corrigida");
        var dipirona = await ItemAsync(client, "Dipirona");
        var omeprazol = await ItemAsync(client, "Omeprazol");

        var consulta = await SalvarConsultaAsync(client, id, new
        {
            especialidade = "ClinicaGeral",
            dispensacoes = new[]
            {
                new { itemId = dipirona, quantidade = 10, via = "Oral", posologia = "6/6 h se dor." }
            }
        });

        var linha = consulta.Dispensacoes.Single();

        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{id}/prescricao/{linha.Id}/checagem",
            new { checada = true },
            Json)).EnsureSuccessStatusCode();

        /*
            O medico corrige a prescricao depois de a enfermagem ter dado o
            remedio: troca a dipirona por omeprazol.

            Salvar a ficha apaga e recria a lista inteira. Sem a regra, a
            assinatura de quem administrou sumiria junto, e o prontuario
            passaria a dizer que ninguem deu nada — editar a dose nao desfaz o
            que foi dado.
        */
        var corrigida = await SalvarConsultaAsync(client, id, new
        {
            especialidade = "ClinicaGeral",
            dispensacoes = new[]
            {
                new { itemId = omeprazol, quantidade = 7, via = "Oral", posologia = "1x ao dia." }
            }
        });

        Assert.Equal(2, corrigida.Dispensacoes.Count);

        var sobrevivente = corrigida.Dispensacoes.Single(d => d.Id == linha.Id);

        Assert.NotNull(sobrevivente.ChecadaEm);
        Assert.Contains("Dipirona", sobrevivente.Item);
        Assert.Contains(corrigida.Dispensacoes, d => d.Item.Contains("Omeprazol"));
    }

    [SkippableFact]
    public async Task Represcrever_o_mesmo_item_ja_checado_nao_cria_uma_segunda_linha()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ClinicoAsync();
        var id = await NaClinicaAsync(client, "Paciente Da Linha Repetida");
        var dipirona = await ItemAsync(client, "Dipirona");

        var consulta = await SalvarConsultaAsync(client, id, new
        {
            especialidade = "ClinicaGeral",
            dispensacoes = new[]
            {
                new { itemId = dipirona, quantidade = 10, via = "Oral", posologia = "6/6 h se dor." }
            }
        });

        var linha = consulta.Dispensacoes.Single();

        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{id}/prescricao/{linha.Id}/checagem",
            new { checada = true },
            Json)).EnsureSuccessStatusCode();

        // A ficha e salva de novo com a mesma prescricao — o caso comum, porque
        // qualquer edicao em outro campo reenvia a lista inteira.
        var denovo = await SalvarConsultaAsync(client, id, new
        {
            especialidade = "ClinicaGeral",
            conduta = "Mantida a analgesia.",
            dispensacoes = new[]
            {
                new { itemId = dipirona, quantidade = 10, via = "Oral", posologia = "6/6 h se dor." }
            }
        });

        // Dois registros do mesmo remedio — um assinado e outro nao — seriam
        // lidos como duas doses.
        var linhaFinal = Assert.Single(denovo.Dispensacoes);

        Assert.Equal(linha.Id, linhaFinal.Id);
        Assert.NotNull(linhaFinal.ChecadaEm);
    }
}
