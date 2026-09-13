using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// As fichas das filas que a etapa anterior abriu: o laudo do ultrassom e a
/// passagem pela farmacia. Mais o bloco de ginecologia, que entra na consulta
/// em vez de virar ficha propria.
///
/// Ate aqui essas filas roteavam o paciente e nao tinham onde escrever nada —
/// quem assumia so podia encaminhar, devolver ou encerrar.
/// </summary>
[Collection(Colecoes.Api)]
public class FichasDasFilasNovasTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public FichasDasFilasNovasTests(ApiFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Coordenacao ve todas as filas: o que se testa aqui e o conteudo da
    /// ficha, nao a restricao por profissao — essa tem testes proprios.
    /// </summary>
    private Task<HttpClient> ProfissionalAsync(string usuario)
        => _fixture.ClienteDeAsync(usuario, $"Ficha {usuario}", FuncaoProfissional.Coordenacao);

    private static async Task<ProntuarioDto> AbrirAsync(HttpClient client, string nome)
    {
        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        var resposta = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId = bases![0].Id,
            paciente = new
            {
                codigo,
                nome,
                tipoDocumento = "SemDocumento",
                idadeAproximada = 32,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;
    }

    private static async Task TriarParaAsync(HttpClient client, Guid id, Especialidade destino)
    {
        (await client.PutAsJsonAsync($"/api/atendimentos/{id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            encaminhamento = destino.ToString()
        }, Json)).EnsureSuccessStatusCode();
    }

    private static async Task<ProntuarioDto> LerAsync(HttpClient client, Guid id)
        => (await client.GetFromJsonAsync<ProntuarioDto>($"/api/atendimentos/{id}", Json))!;

    // -----------------------------------------------------------------------
    // Ultrassom
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Laudo_do_ultrassom_guarda_analise_conclusao_e_quem_assinou()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("ficha.usg");
        var atendimento = await AbrirAsync(client, "Paciente Do Laudo");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/encaminhar",
            new { destino = "Ultrassom", motivo = "Avaliar massa abdominal." },
            Json)).EnsureSuccessStatusCode();

        var salvo = await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/ultrassom", new
        {
            exameSolicitado = "USG de abdome total",
            indicacao = "Massa palpavel em hipocondrio direito.",
            analise = "Figado de dimensoes normais. Vesicula com calculo movel de 9 mm.",
            conclusao = "Colelitiase.",
            desfecho = "Alta"
        }, Json);

        salvo.EnsureSuccessStatusCode();

        var prontuario = await LerAsync(client, atendimento.Id);
        var laudo = prontuario.Ultrassom;

        Assert.NotNull(laudo);
        Assert.Equal("USG de abdome total", laudo!.ExameSolicitado);
        Assert.Equal("Colelitiase.", laudo.Conclusao);
        Assert.Contains("calculo movel", laudo.Analise);

        // O laudo e assinado: no papel a linha final e "Medico ___ CRM ___", e
        // sem ela o documento nao vale fora do sistema.
        Assert.NotNull(laudo.Profissional);
        Assert.Equal("Ficha ficha.usg", laudo.Profissional!.Nome);

        // Concluir a ficha fecha a fila do ultrassom, como nas outras.
        var etapa = prontuario.Etapas.Single(e => e.Especialidade == Especialidade.Ultrassom);
        Assert.Equal(StatusEtapa.Concluida, etapa.Status);
    }

    [SkippableFact]
    public async Task Ultrassom_devolve_para_quem_pediu_pelo_desfecho_da_ficha()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("ficha.usg.volta");
        var atendimento = await AbrirAsync(client, "Paciente Que Volta");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/encaminhar",
            new { destino = "Ultrassom", motivo = "Avaliar rins." },
            Json)).EnsureSuccessStatusCode();

        (await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/ultrassom", new
        {
            exameSolicitado = "USG de rins e vias urinarias",
            analise = "Rins topicos, sem dilatacao.",
            conclusao = "Exame sem alteracoes.",
            desfecho = "Encaminhado",
            encaminhadoPara = "ClinicaGeral"
        }, Json)).EnsureSuccessStatusCode();

        var prontuario = await LerAsync(client, atendimento.Id);

        // Quem pediu o exame precisa do laudo de volta: sem isso o paciente sai
        // com um exame que ninguem leu.
        var clinica = prontuario.Etapas.Single(e => e.Especialidade == Especialidade.ClinicaGeral);
        Assert.Equal(StatusEtapa.Aguardando, clinica.Status);
        Assert.Equal(Especialidade.Ultrassom, clinica.EncaminhadaDe);
    }

    [SkippableFact]
    public async Task Ultrassom_nao_e_registrado_como_consulta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("ficha.usg.rota");
        var atendimento = await AbrirAsync(client, "Paciente Da Rota Errada");

        await TriarParaAsync(client, atendimento.Id, Especialidade.Ultrassom);

        // Duas portas para a mesma etapa gravariam duas fichas diferentes na
        // mesma fila, e a segunda apagaria a primeira da tela.
        var resposta = await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/consulta", new
        {
            especialidade = "Ultrassom",
            sintomasDescricao = "Nao deveria entrar por aqui."
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Farmacia
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Farmacia_registra_o_que_saiu_com_quem_entregou_e_quando()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("ficha.farmacia");
        var atendimento = await AbrirAsync(client, "Paciente Da Farmacia");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/encaminhar",
            new { destino = "Farmacia", motivo = "Dispensar o que foi prescrito." },
            Json)).EnsureSuccessStatusCode();

        (await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/farmacia", new
        {
            orientacoes = "Tomar apos as refeicoes.",
            observacoes = "Apresentacao trocada: so havia comprimido de 500 mg.",
            desfecho = "Alta",
            dispensacoes = new[]
            {
                new
                {
                    descricaoLivre = "Dipirona 500 mg",
                    justificativaItemLivre = "Item ainda nao cadastrado no catalogo desta base.",
                    quantidade = 10
                }
            }
        }, Json)).EnsureSuccessStatusCode();

        var prontuario = await LerAsync(client, atendimento.Id);
        var farmacia = prontuario.Farmacia;

        Assert.NotNull(farmacia);
        Assert.Equal("Tomar apos as refeicoes.", farmacia!.Orientacoes);
        Assert.Contains("comprimido de 500 mg", farmacia.Observacoes);

        // "Medicamento, profissional e hora" — o trio que a checagem de papel
        // pede. Os dois ultimos sao a propria etapa, e nao campos digitados:
        // copiados para dentro da ficha, poderiam divergir do resto.
        var item = Assert.Single(farmacia.Dispensacoes);
        Assert.Equal("Dipirona 500 mg", item.Item);
        Assert.Equal(10, item.Quantidade);
        Assert.Equal("Ficha ficha.farmacia", farmacia.Profissional!.Nome);
        Assert.NotNull(farmacia.ConcluidaEm);
    }

    [SkippableFact]
    public async Task Farmacia_nao_e_registrada_como_consulta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("ficha.farmacia.rota");
        var atendimento = await AbrirAsync(client, "Paciente Da Outra Rota");

        await TriarParaAsync(client, atendimento.Id, Especialidade.Farmacia);

        var resposta = await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/consulta", new
        {
            especialidade = "Farmacia",
            sintomasDescricao = "Nao deveria entrar por aqui."
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Ginecologia
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Ginecologia_guarda_a_historia_menstrual_junto_da_consulta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("ficha.gineco");
        var atendimento = await AbrirAsync(client, "Paciente Da Ginecologia");

        await TriarParaAsync(client, atendimento.Id, Especialidade.Ginecologia);

        (await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/consulta", new
        {
            especialidade = "Ginecologia",
            sintomasDescricao = "Dor pelvica ha duas semanas.",
            cid10Codigo = "M79.1",
            desfecho = "Alta",
            ginecologia = new
            {
                dataUltimaMenstruacao = "2026-08-20",
                gestacoes = 3,
                partos = 2,
                abortos = 1,
                gestante = false,
                metodoContraceptivo = "Injetavel trimestral",
                ultimoPreventivo = "Ha cerca de tres anos"
            }
        }, Json)).EnsureSuccessStatusCode();

        var prontuario = await LerAsync(client, atendimento.Id);
        var consulta = prontuario.Consultas.Single(c => c.Especialidade == Especialidade.Ginecologia);

        Assert.NotNull(consulta.Ginecologia);

        // Tres campos, e nao um texto "3/2/1": em texto, "G3 P2 A1", "3-2-1" e
        // "III/II/I" contam a mesma coisa de tres jeitos e nenhum deles soma.
        Assert.Equal(3, consulta.Ginecologia!.Gestacoes);
        Assert.Equal(2, consulta.Ginecologia.Partos);
        Assert.Equal(1, consulta.Ginecologia.Abortos);
        Assert.Equal(new DateOnly(2026, 8, 20), consulta.Ginecologia.DataUltimaMenstruacao);
        Assert.False(consulta.Ginecologia.Gestante);
    }

    [SkippableFact]
    public async Task O_bloco_de_ginecologia_so_e_gravado_na_fila_da_ginecologia()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("ficha.gineco.outra");
        var atendimento = await AbrirAsync(client, "Paciente Da Clinica");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        // A tela nao oferece o bloco fora da ginecologia; se um cliente antigo
        // mandar mesmo assim, ele nao pode entrar pela porta de tras.
        (await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            sintomasDescricao = "Tosse.",
            cid10Codigo = "M79.1",
            desfecho = "Alta",
            ginecologia = new { gestacoes = 9 }
        }, Json)).EnsureSuccessStatusCode();

        var prontuario = await LerAsync(client, atendimento.Id);
        var consulta = prontuario.Consultas.Single(c => c.Especialidade == Especialidade.ClinicaGeral);

        Assert.Null(consulta.Ginecologia);
    }
}
