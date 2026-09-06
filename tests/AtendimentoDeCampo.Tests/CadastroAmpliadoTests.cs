using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;

namespace AtendimentoDeCampo.Tests;

public class RegrasDoMenorTests
{
    [Fact]
    public void Menor_sem_nome_da_mae_e_endereco_e_recusado()
    {
        // Em campo a crianca chega acompanhada de quem nao e o responsavel
        // legal, e sao esses dois campos que permitem reencontrar a familia.
        var erros = RegrasDoMenor.Validar(8, null, null);

        Assert.Equal(2, erros.Count);
    }

    [Fact]
    public void Menor_com_os_dois_campos_passa()
    {
        Assert.Empty(RegrasDoMenor.Validar(8, "Maria Silva", "Rua das Flores, 10"));
    }

    [Fact]
    public void Adulto_nao_precisa_de_nenhum_dos_dois()
    {
        Assert.Empty(RegrasDoMenor.Validar(30, null, null));
    }

    [Fact]
    public void Idade_desconhecida_nao_presume_menor()
    {
        /*
            Boa parte dos pacientes chega sem documento e sem saber a idade.
            Presumir menor bloquearia o cadastro de adulto com dois campos que
            ninguem sabe responder, e a recepcao aprenderia a inventar dado —
            que e pior que nao ter o dado.
        */
        Assert.False(RegrasDoMenor.EhMenor(null));
        Assert.Empty(RegrasDoMenor.Validar(null, null, null));
    }

    [Fact]
    public void A_maioridade_comeca_aos_dezoito()
    {
        Assert.True(RegrasDoMenor.EhMenor(17));
        Assert.False(RegrasDoMenor.EhMenor(18));
    }

    [Fact]
    public void Espaco_em_branco_nao_conta_como_preenchido()
    {
        Assert.NotEmpty(RegrasDoMenor.Validar(8, "   ", "  "));
    }
}

public class CalculadoraImcTests
{
    [Fact]
    public void Calcula_o_imc_a_partir_de_peso_e_altura()
    {
        // 70 kg e 1,75 m -> 22,9.
        Assert.Equal(22.9, CalculadoraImc.Calcular(70, 175));
    }

    [Fact]
    public void Sem_peso_ou_sem_altura_nao_ha_imc()
    {
        Assert.Null(CalculadoraImc.Calcular(70, null));
        Assert.Null(CalculadoraImc.Calcular(null, 175));

        // Zero e negativo entrariam numa divisao que devolveria infinito.
        Assert.Null(CalculadoraImc.Calcular(70, 0));
        Assert.Null(CalculadoraImc.Calcular(0, 175));
    }

    [Theory]
    [InlineData(17.0, FaixaImc.Baixo)]
    [InlineData(22.0, FaixaImc.Adequado)]
    [InlineData(27.0, FaixaImc.Sobrepeso)]
    [InlineData(33.0, FaixaImc.Obesidade)]
    public void Classifica_o_adulto_pelos_cortes_da_oms(double imc, FaixaImc esperada)
    {
        Assert.Equal(esperada, CalculadoraImc.Classificar(imc, 30));
    }

    [Fact]
    public void Nao_classifica_crianca_pelo_corte_de_adulto()
    {
        /*
            Em crianca o IMC se le em curva por idade e sexo. O corte de adulto
            diria "baixo peso" para uma crianca perfeitamente saudavel, e uma
            resposta errada e pior que nenhuma resposta.
        */
        Assert.Null(CalculadoraImc.Classificar(15.0, 8));
        Assert.Null(CalculadoraImc.Classificar(15.0, 19));
        Assert.NotNull(CalculadoraImc.Classificar(15.0, 20));
    }

    [Fact]
    public void Sem_idade_nao_classifica()
    {
        Assert.Null(CalculadoraImc.Classificar(22.0, null));
    }
}

/// <summary>
/// Campos que a equipe sente falta todo dia: cartao do SUS, comunidade, regra do
/// menor, peso, altura e escala de dor.
/// </summary>
[Collection(Colecoes.Api)]
public class CadastroAmpliadoTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public CadastroAmpliadoTests(ApiFixture fixture) => _fixture = fixture;

    private Task<HttpClient> EnfermeiroAsync(string usuario = "cadastro.enf")
        => _fixture.ClienteDeAsync(usuario, "Cadastro Enfermeiro", FuncaoProfissional.Enfermeiro, "60601");

    private static async Task<Guid> BaseAsync(HttpClient client)
        => (await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json))![0].Id;

    private async Task<Guid> ComunidadeAsync(string nome)
    {
        var admin = await _fixture.ClienteDoAdministradorAsync();

        var resposta = await admin.PostAsJsonAsync("/api/comunidades", new { nome }, Json);
        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<ComunidadeAdminDto>(Json))!.Id;
    }

    private static async Task<HttpResponseMessage> AbrirAsync(
        HttpClient client,
        Guid baseId,
        object paciente)
    {
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        var corpo = new Dictionary<string, object?>
        {
            ["baseId"] = baseId,
            ["paciente"] = Combinar(paciente, codigo)
        };

        return await client.PostAsJsonAsync("/api/atendimentos", corpo, Json);
    }

    /// <summary>Junta o codigo sorteado aos campos que o teste quer exercitar.</summary>
    private static Dictionary<string, object?> Combinar(object paciente, string codigo)
    {
        var campos = new Dictionary<string, object?>
        {
            ["codigo"] = codigo,
            ["tipoDocumento"] = "SemDocumento",
            ["sexo"] = "NaoInformado",
            ["statusAlergia"] = "NaoPerguntado",
            ["consentimentoRegistro"] = true
        };

        foreach (var prop in paciente.GetType().GetProperties())
        {
            campos[char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..]] = prop.GetValue(paciente);
        }

        return campos;
    }

    // -----------------------------------------------------------------------
    // Cartao do SUS
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Cartao_do_sus_convive_com_o_documento()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();
        var baseId = await BaseAsync(client);

        // Como tipo de documento, um excluiria o outro e o numero do cartao se
        // perderia. Sao campos separados justamente por isso.
        var resposta = await AbrirAsync(client, baseId, new
        {
            Nome = "Paciente Com Sus",
            IdadeAproximada = 40,
            TipoDocumento = "Rg",
            NumeroDocumento = "MG1234567",
            CartaoSus = "700123456789012"
        });

        resposta.EnsureSuccessStatusCode();

        var prontuario = await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json);

        Assert.Equal("MG1234567", prontuario!.Paciente.NumeroDocumento);
        Assert.Equal("700123456789012", prontuario.Paciente.CartaoSus);
    }

    // -----------------------------------------------------------------------
    // Comunidade
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Comunidade_vem_da_lista_e_aparece_no_prontuario()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();
        var baseId = await BaseAsync(client);
        var comunidadeId = await ComunidadeAsync("Vila Uniao");

        var resposta = await AbrirAsync(client, baseId, new
        {
            Nome = "Paciente Da Vila",
            IdadeAproximada = 35,
            ComunidadeId = comunidadeId
        });

        resposta.EnsureSuccessStatusCode();

        var prontuario = await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json);

        // O nome precisa chegar junto: so o id deixaria a tela sem o que mostrar.
        Assert.Equal(comunidadeId, prontuario!.Paciente.ComunidadeId);
        Assert.Equal("Vila Uniao", prontuario.Paciente.Comunidade);
    }

    [SkippableFact]
    public async Task Comunidade_inexistente_e_recusada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();
        var baseId = await BaseAsync(client);

        var resposta = await AbrirAsync(client, baseId, new
        {
            Nome = "Paciente De Lugar Nenhum",
            IdadeAproximada = 35,
            ComunidadeId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Comunidade_desativada_sai_da_lista_mas_nao_do_historico()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();
        var baseId = await BaseAsync(client);
        var comunidadeId = await ComunidadeAsync("Acampamento Velho");

        var abertura = await AbrirAsync(client, baseId, new
        {
            Nome = "Paciente Do Acampamento",
            IdadeAproximada = 50,
            ComunidadeId = comunidadeId
        });

        abertura.EnsureSuccessStatusCode();
        var prontuario = await abertura.Content.ReadFromJsonAsync<ProntuarioDto>(Json);

        var admin = await _fixture.ClienteDoAdministradorAsync();
        (await admin.PostAsJsonAsync($"/api/comunidades/{comunidadeId}/ativa", new { ativa = false }, Json))
            .EnsureSuccessStatusCode();

        // Sai do cadastro novo...
        var ativas = await client.GetFromJsonAsync<List<ComunidadeDto>>("/api/comunidades", Json);
        Assert.DoesNotContain(ativas!, c => c.Id == comunidadeId);

        // ...mas o paciente ja atendido continua sendo de la, senao a
        // estatistica do ano passado deixaria de bater com a de hoje.
        var depois = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{prontuario!.Id}", Json);

        Assert.Equal("Acampamento Velho", depois!.Paciente.Comunidade);
    }

    [SkippableFact]
    public async Task Comunidade_com_nome_repetido_e_recusada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        await ComunidadeAsync("Praia Grande");

        var admin = await _fixture.ClienteDoAdministradorAsync();

        // Duas com o mesmo nome devolveriam ao cadastro a ambiguidade que a
        // lista veio eliminar — inclusive com a caixa trocada.
        var resposta = await admin.PostAsJsonAsync("/api/comunidades", new { nome = "praia grande" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task So_a_coordenacao_mantem_a_lista_de_comunidades()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();

        // Ler as ativas e de quem cadastra paciente; manter a lista, nao.
        (await client.GetAsync("/api/comunidades")).EnsureSuccessStatusCode();

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/comunidades", new { nome = "Nao Deveria" }, Json)).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/comunidades/todas")).StatusCode);
    }

    // -----------------------------------------------------------------------
    // Menor de idade
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Menor_sem_nome_da_mae_nao_abre_atendimento()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();
        var baseId = await BaseAsync(client);

        var resposta = await AbrirAsync(client, baseId, new
        {
            Nome = "Crianca Sem Responsavel",
            IdadeAproximada = 7,
            Endereco = "Rua das Flores, 10"
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("mae", await resposta.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Menor_com_os_dois_campos_abre_normalmente()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();
        var baseId = await BaseAsync(client);

        var resposta = await AbrirAsync(client, baseId, new
        {
            Nome = "Crianca Acompanhada",
            IdadeAproximada = 7,
            NomeDaMae = "Maria Silva",
            Endereco = "Rua das Flores, 10"
        });

        resposta.EnsureSuccessStatusCode();

        var prontuario = await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json);

        Assert.True(prontuario!.Paciente.EhMenor);
        Assert.Equal("Maria Silva", prontuario.Paciente.NomeDaMae);
    }

    [SkippableFact]
    public async Task Adulto_nao_precisa_de_nome_da_mae()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();
        var baseId = await BaseAsync(client);

        var resposta = await AbrirAsync(client, baseId, new
        {
            Nome = "Adulto Sozinho",
            IdadeAproximada = 40
        });

        resposta.EnsureSuccessStatusCode();

        var prontuario = await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json);
        Assert.False(prontuario!.Paciente.EhMenor);
    }

    [SkippableFact]
    public async Task A_regra_do_menor_vale_pela_data_de_nascimento_tambem()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();
        var baseId = await BaseAsync(client);

        // Quem informou a data de nascimento nao pode escapar da regra so
        // porque nao digitou a idade.
        var nascimento = DateTime.UtcNow.AddYears(-10).ToString("yyyy-MM-dd");

        var resposta = await AbrirAsync(client, baseId, new
        {
            Nome = "Crianca Com Certidao",
            DataNascimento = nascimento
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Triagem: peso, altura e dor
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Peso_altura_e_dor_ficam_gravados_com_o_imc_calculado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();
        var baseId = await BaseAsync(client);

        var abertura = await AbrirAsync(client, baseId, new
        {
            Nome = "Paciente Pesado",
            IdadeAproximada = 30
        });

        abertura.EnsureSuccessStatusCode();
        var prontuario = await abertura.Content.ReadFromJsonAsync<ProntuarioDto>(Json);

        (await client.PutAsJsonAsync($"/api/atendimentos/{prontuario!.Id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            pesoKg = 70.0,
            alturaCm = 175,
            escalaDor = 7
        }, Json)).EnsureSuccessStatusCode();

        var depois = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{prontuario.Id}", Json);

        var triagem = depois!.Triagem!;

        Assert.Equal(70.0, triagem.PesoKg);
        Assert.Equal(175, triagem.AlturaCm);
        Assert.Equal(7, triagem.EscalaDor);

        // O IMC nao e gravado: e derivado, e guardar derivado so cria a chance
        // de ele discordar da origem.
        Assert.Equal(22.9, triagem.Imc);
        Assert.Equal(FaixaImc.Adequado, triagem.FaixaImc);
    }

    [SkippableFact]
    public async Task Dor_zero_e_diferente_de_nao_ter_perguntado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();
        var baseId = await BaseAsync(client);

        var abertura = await AbrirAsync(client, baseId, new
        {
            Nome = "Paciente Sem Dor",
            IdadeAproximada = 30
        });

        abertura.EnsureSuccessStatusCode();
        var prontuario = await abertura.Content.ReadFromJsonAsync<ProntuarioDto>(Json);

        (await client.PutAsJsonAsync($"/api/atendimentos/{prontuario!.Id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            escalaDor = 0
        }, Json)).EnsureSuccessStatusCode();

        var depois = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{prontuario.Id}", Json);

        // "Sem dor" e uma resposta; o nulo e que significa "nao perguntei".
        Assert.Equal(0, depois!.Triagem!.EscalaDor);
    }

    [SkippableFact]
    public async Task Dor_acima_de_dez_e_recusada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();
        var baseId = await BaseAsync(client);

        var abertura = await AbrirAsync(client, baseId, new
        {
            Nome = "Paciente Da Dor Onze",
            IdadeAproximada = 30
        });

        abertura.EnsureSuccessStatusCode();
        var prontuario = await abertura.Content.ReadFromJsonAsync<ProntuarioDto>(Json);

        var resposta = await client.PutAsJsonAsync($"/api/atendimentos/{prontuario!.Id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            escalaDor = 11
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task A_faixa_do_imc_nao_aparece_para_crianca()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();
        var baseId = await BaseAsync(client);

        var abertura = await AbrirAsync(client, baseId, new
        {
            Nome = "Crianca Pesada",
            IdadeAproximada = 8,
            NomeDaMae = "Maria Silva",
            Endereco = "Rua das Flores, 10"
        });

        abertura.EnsureSuccessStatusCode();
        var prontuario = await abertura.Content.ReadFromJsonAsync<ProntuarioDto>(Json);

        (await client.PutAsJsonAsync($"/api/atendimentos/{prontuario!.Id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            pesoKg = 25.0,
            alturaCm = 130
        }, Json)).EnsureSuccessStatusCode();

        var depois = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{prontuario.Id}", Json);

        // O IMC aparece; a faixa nao, porque o corte da OMS e de adulto e
        // diria "baixo peso" para uma crianca saudavel.
        Assert.NotNull(depois!.Triagem!.Imc);
        Assert.Null(depois.Triagem.FaixaImc);
    }

    [SkippableFact]
    public async Task A_escala_de_dor_nao_mexe_na_sugestao_do_start()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();
        var baseId = await BaseAsync(client);

        var abertura = await AbrirAsync(client, baseId, new
        {
            Nome = "Paciente Com Muita Dor",
            IdadeAproximada = 30
        });

        abertura.EnsureSuccessStatusCode();
        var prontuario = await abertura.Content.ReadFromJsonAsync<ProntuarioDto>(Json);

        /*
            O START classifica por deambulacao, respiracao, perfusao e
            consciencia. Dor nao faz parte do protocolo, e enfia-la ali
            produziria um START que nao e o START — deixaria de poder ser
            conferido contra o protocolo publicado.
        */
        var resposta = await client.PutAsJsonAsync($"/api/atendimentos/{prontuario!.Id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            escalaDor = 10,
            achadosStart = new { deambula = true }
        }, Json);

        resposta.EnsureSuccessStatusCode();

        var sugestao = await resposta.Content.ReadFromJsonAsync<SugestaoStartDto>(Json);

        Assert.Equal(ClassificacaoRisco.Verde, sugestao!.Sugerida);
        Assert.False(sugestao.Divergente);
    }
}
