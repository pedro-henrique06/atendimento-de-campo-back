using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// As faixas de referencia, sem banco.
///
/// Elas marcam o que merece um segundo olhar na tabela horaria — e so isso.
/// Quem classifica risco e o protocolo START, na triagem.
/// </summary>
public class FaixasDeSinaisVitaisTests
{
    [Fact]
    public void Adulto_estavel_nao_acende_nada()
    {
        var fora = FaixasDeSinaisVitais.Avaliar(
            idade: 40,
            pressaoSistolica: 120,
            frequenciaCardiaca: 78,
            frequenciaRespiratoria: 16,
            saturacaoO2: 98,
            temperaturaCelsius: 36.5,
            glicemiaCapilar: 95);

        Assert.Equal(SinalForaDaFaixa.Nenhum, fora);
    }

    [Fact]
    public void Marca_cada_medida_que_saiu_da_faixa()
    {
        var fora = FaixasDeSinaisVitais.Avaliar(
            idade: 40,
            pressaoSistolica: 82,
            frequenciaCardiaca: 138,
            frequenciaRespiratoria: 30,
            saturacaoO2: 88,
            temperaturaCelsius: 39.2,
            glicemiaCapilar: 48);

        Assert.True(fora.HasFlag(SinalForaDaFaixa.PressaoSistolica));
        Assert.True(fora.HasFlag(SinalForaDaFaixa.FrequenciaCardiaca));
        Assert.True(fora.HasFlag(SinalForaDaFaixa.FrequenciaRespiratoria));
        Assert.True(fora.HasFlag(SinalForaDaFaixa.SaturacaoO2));
        Assert.True(fora.HasFlag(SinalForaDaFaixa.Temperatura));
        Assert.True(fora.HasFlag(SinalForaDaFaixa.Glicemia));
    }

    [Fact]
    public void Crianca_nao_e_medida_pelo_corte_de_adulto()
    {
        // FC 130 e FR 30 sao normais num bebe. Pelo corte de adulto a linha
        // acenderia inteira, e uma tabela que acende para todo mundo treina a
        // equipe a ignorar o destaque.
        var fora = FaixasDeSinaisVitais.Avaliar(
            idade: 1,
            pressaoSistolica: 85,
            frequenciaCardiaca: 130,
            frequenciaRespiratoria: 30,
            saturacaoO2: 97,
            temperaturaCelsius: 36.8,
            glicemiaCapilar: 90);

        Assert.Equal(SinalForaDaFaixa.Nenhum, fora);
    }

    [Fact]
    public void Sem_idade_nao_ha_marcacao()
    {
        // Paciente sem data de nascimento nem idade aproximada e comum em campo.
        // Chutar "adulto" acenderia a linha de uma crianca.
        var fora = FaixasDeSinaisVitais.Avaliar(null, 82, 138, 30, 88, 39.2, 48);

        Assert.Equal(SinalForaDaFaixa.Nenhum, fora);
    }

    [Fact]
    public void Medida_que_faltou_nao_acende()
    {
        // Nulo e "nao medi", nao "zero" — e zero acenderia tudo.
        var fora = FaixasDeSinaisVitais.Avaliar(40, null, null, null, null, null, null);

        Assert.Equal(SinalForaDaFaixa.Nenhum, fora);
    }
}

/// <summary>
/// A folha de observacao: a tabela horaria de sinais vitais do atendimento.
///
/// Fica no atendimento, e nao numa etapa, porque a pergunta que ela responde —
/// "a pressao esta caindo?" — so tem resposta com as medidas na mesma lista e em
/// ordem. Presa a uma fila, cada fila teria a sua e a tendencia sumiria.
/// </summary>
[Collection(Colecoes.Api)]
public class SinaisVitaisTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public SinaisVitaisTests(ApiFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Enfermeiro, que e quem preenche a folha de observacao — e com registro,
    /// porque o Coren e obrigatorio para a profissao.
    /// </summary>
    private Task<HttpClient> ProfissionalAsync(string usuario)
        => _fixture.ClienteDeAsync(usuario, $"Vitais {usuario}", FuncaoProfissional.Enfermeiro, "60602");

    private static async Task<ProntuarioDto> AbrirAsync(HttpClient client, string nome, int idade)
    {
        var menor = idade < 18;

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
                idadeAproximada = idade,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true,

                // Obrigatorios para menor: e o nome da mae que permite
                // reencontrar a familia depois.
                nomeDaMae = menor ? "Maria Da Silva" : null,
                endereco = menor ? "Comunidade Vila Uniao, casa 12" : null,
            }
        }, Json);

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;
    }

    private static Task<HttpResponseMessage> MedirAsync(HttpClient client, Guid id, object medida)
        => client.PostAsJsonAsync($"/api/atendimentos/{id}/sinais-vitais", medida, Json);

    private static async Task<ProntuarioDto> LerAsync(HttpClient client, Guid id)
        => (await client.GetFromJsonAsync<ProntuarioDto>($"/api/atendimentos/{id}", Json))!;

    [SkippableFact]
    public async Task A_tabela_sai_em_ordem_de_hora_e_nao_de_digitacao()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("vitais.ordem");
        var atendimento = await AbrirAsync(client, "Paciente Em Observacao", 40);

        var agora = DateTime.UtcNow;

        // Em campo se mede agora e se anota quando da: a das 14h pode ser
        // digitada depois da das 15h.
        (await MedirAsync(client, atendimento.Id, new
        {
            medidaEm = agora.AddHours(-1),
            pressaoSistolica = 110,
            pressaoDiastolica = 70,
            frequenciaCardiaca = 80,
        })).EnsureSuccessStatusCode();

        (await MedirAsync(client, atendimento.Id, new
        {
            medidaEm = agora.AddHours(-3),
            pressaoSistolica = 130,
            pressaoDiastolica = 85,
            frequenciaCardiaca = 72,
        })).EnsureSuccessStatusCode();

        var prontuario = await LerAsync(client, atendimento.Id);

        Assert.Equal(2, prontuario.SinaisVitais.Count);

        // Ordenada pela hora da medida: a tabela existe para mostrar evolucao, e
        // fora de ordem ela mostra o contrario do que aconteceu.
        Assert.Equal(130, prontuario.SinaisVitais[0].PressaoSistolica);
        Assert.Equal(110, prontuario.SinaisVitais[1].PressaoSistolica);
        Assert.True(prontuario.SinaisVitais[0].MedidaEm < prontuario.SinaisVitais[1].MedidaEm);
    }

    [SkippableFact]
    public async Task Guarda_quem_mediu()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("vitais.autor");
        var atendimento = await AbrirAsync(client, "Paciente Com Autor", 40);

        (await MedirAsync(client, atendimento.Id, new { saturacaoO2 = 97 })).EnsureSuccessStatusCode();

        var prontuario = await LerAsync(client, atendimento.Id);
        var medida = Assert.Single(prontuario.SinaisVitais);

        Assert.Equal("Vitais vitais.autor", medida.RegistradaPor);
    }

    [SkippableFact]
    public async Task Linha_em_branco_e_recusada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("vitais.branco");
        var atendimento = await AbrirAsync(client, "Paciente Sem Medida", 40);

        // Entraria na tabela, empurraria as outras para baixo e nao responderia
        // nada.
        var resposta = await MedirAsync(client, atendimento.Id, new { observacao = "Paciente dormindo." });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Hora_no_futuro_e_recusada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("vitais.futuro");
        var atendimento = await AbrirAsync(client, "Paciente Do Futuro", 40);

        var resposta = await MedirAsync(client, atendimento.Id, new
        {
            medidaEm = DateTime.UtcNow.AddHours(2),
            frequenciaCardiaca = 80,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Marca_a_medida_fora_da_faixa_do_adulto()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("vitais.faixa");
        var atendimento = await AbrirAsync(client, "Paciente Instavel", 40);

        (await MedirAsync(client, atendimento.Id, new
        {
            pressaoSistolica = 82,
            pressaoDiastolica = 50,
            saturacaoO2 = 88,
        })).EnsureSuccessStatusCode();

        var prontuario = await LerAsync(client, atendimento.Id);
        var medida = Assert.Single(prontuario.SinaisVitais);

        Assert.Contains("PressaoSistolica", medida.ForaDaFaixa);
        Assert.Contains("SaturacaoO2", medida.ForaDaFaixa);
    }

    [SkippableFact]
    public async Task Crianca_nao_recebe_a_marcacao_de_adulto()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("vitais.crianca");
        var atendimento = await AbrirAsync(client, "Bebe Saudavel", 1);

        (await MedirAsync(client, atendimento.Id, new
        {
            frequenciaCardiaca = 130,
            frequenciaRespiratoria = 30,
        })).EnsureSuccessStatusCode();

        var prontuario = await LerAsync(client, atendimento.Id);
        var medida = Assert.Single(prontuario.SinaisVitais);

        // Sao valores normais para a idade. Marcados, a equipe aprenderia a
        // ignorar a marcacao.
        Assert.Empty(medida.ForaDaFaixa);
    }

    [SkippableFact]
    public async Task Remover_uma_linha_deixa_rastro_de_quem_removeu()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("vitais.remove");
        var atendimento = await AbrirAsync(client, "Paciente Com Erro De Digitacao", 40);

        // Um numero possivel, digitado errado: 180 entra, 800 nem chega a
        // entrar — a faixa do proprio pedido barra antes.
        var criada = await MedirAsync(client, atendimento.Id, new { frequenciaCardiaca = 180 });
        criada.EnsureSuccessStatusCode();

        var medida = (await criada.Content.ReadFromJsonAsync<MedicaoSinaisVitaisDto>(Json))!;

        (await client.DeleteAsync($"/api/atendimentos/{atendimento.Id}/sinais-vitais/{medida.Id}"))
            .EnsureSuccessStatusCode();

        var prontuario = await LerAsync(client, atendimento.Id);

        Assert.Empty(prontuario.SinaisVitais);

        // Apagada a medida, o historico ainda diz quem apagou — como a linha
        // riscada no papel, que continua la.
        Assert.Contains(
            prontuario.Historico,
            h => h.Acao == AcaoAuditoria.RemoveuSinaisVitais && h.Profissional == "Vitais vitais.remove");
    }

    [SkippableFact]
    public async Task Medida_de_outro_atendimento_nao_e_removida_por_aqui()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("vitais.cruzado");
        var umPaciente = await AbrirAsync(client, "Paciente Um", 40);
        var outroPaciente = await AbrirAsync(client, "Paciente Dois", 40);

        var criada = await MedirAsync(client, umPaciente.Id, new { frequenciaCardiaca = 80 });
        criada.EnsureSuccessStatusCode();

        var medida = (await criada.Content.ReadFromJsonAsync<MedicaoSinaisVitaisDto>(Json))!;

        var resposta = await client.DeleteAsync(
            $"/api/atendimentos/{outroPaciente.Id}/sinais-vitais/{medida.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        var prontuario = await LerAsync(client, umPaciente.Id);
        Assert.Single(prontuario.SinaisVitais);
    }
}
