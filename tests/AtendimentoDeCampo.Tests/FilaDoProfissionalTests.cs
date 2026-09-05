using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;

namespace AtendimentoDeCampo.Tests;

public class FilasDaFuncaoTests
{
    [Fact]
    public void Cada_profissao_abre_na_fila_onde_ela_trabalha()
    {
        Assert.Equal(Especialidade.ClinicaGeral, FilasDaFuncao.Padrao(FuncaoProfissional.ClinicoGeral));
        Assert.Equal(Especialidade.Pediatria, FilasDaFuncao.Padrao(FuncaoProfissional.Pediatra));
        Assert.Equal(Especialidade.Ortopedia, FilasDaFuncao.Padrao(FuncaoProfissional.Ortopedista));
        Assert.Equal(Especialidade.Triagem, FilasDaFuncao.Padrao(FuncaoProfissional.Enfermeiro));
        Assert.Equal(Especialidade.Odontologia, FilasDaFuncao.Padrao(FuncaoProfissional.Dentista));
        Assert.Equal(Especialidade.SaudeMental, FilasDaFuncao.Padrao(FuncaoProfissional.Psicologo));
        Assert.Equal(Especialidade.Triagem, FilasDaFuncao.Padrao(FuncaoProfissional.Recepcao));
    }

    [Fact]
    public void Cada_especialidade_medica_cai_numa_fila_so()
    {
        // E o ponto da separacao. Um "medico" generico cairia em clinica geral,
        // pediatria e ortopedia ao mesmo tempo, e "a fila dele" nao existiria.
        Assert.Equal([Especialidade.ClinicaGeral], FilasDaFuncao.De(FuncaoProfissional.ClinicoGeral));
        Assert.Equal([Especialidade.Pediatria], FilasDaFuncao.De(FuncaoProfissional.Pediatra));
        Assert.Equal([Especialidade.Ortopedia], FilasDaFuncao.De(FuncaoProfissional.Ortopedista));
    }

    [Fact]
    public void O_medico_sem_especialidade_ainda_alcanca_as_tres_filas_medicas()
    {
        // Conta criada antes das especialidades existirem. Estreitar aqui
        // trancaria essa pessoa para fora do proprio trabalho no dia do deploy.
        var filas = FilasDaFuncao.De(FuncaoProfissional.Medico);

        Assert.Contains(Especialidade.ClinicaGeral, filas);
        Assert.Contains(Especialidade.Pediatria, filas);
        Assert.Contains(Especialidade.Ortopedia, filas);
    }

    [Fact]
    public void A_enfermagem_tem_duas_filas_porque_comeca_triando()
    {
        var filas = FilasDaFuncao.De(FuncaoProfissional.Enfermeiro);

        Assert.Equal(Especialidade.Triagem, filas[0]);
        Assert.Contains(Especialidade.Enfermagem, filas);
    }

    [Fact]
    public void Coordenacao_e_Outro_enxergam_tudo()
    {
        var todas = Enum.GetValues<Especialidade>();

        Assert.Equal(todas.Length, FilasDaFuncao.De(FuncaoProfissional.Coordenacao).Count);
        // "Outro" e justamente o caso em que o sistema nao sabe o que a pessoa
        // faz: esconder filas ali trancaria alguem para fora sem motivo.
        Assert.Equal(todas.Length, FilasDaFuncao.De(FuncaoProfissional.Outro).Count);
    }

    [Fact]
    public void Nenhuma_profissao_fica_sem_fila()
    {
        foreach (var funcao in Enum.GetValues<FuncaoProfissional>())
        {
            Assert.NotEmpty(FilasDaFuncao.De(funcao));
        }
    }

    [Fact]
    public void Toda_fila_tem_alguma_profissao_que_a_atende()
    {
        // Uma fila sem profissao dona so aparece quando alguem cobre por acaso.
        // Como o cadastro nao oferece "Coordenacao" e "Outro" para atender, sao
        // as profissoes de cadastro que precisam cobrir o mapa.
        foreach (var fila in Enum.GetValues<Especialidade>())
        {
            Assert.Contains(
                FilasDaFuncao.ParaCadastro,
                funcao => FilasDaFuncao.EhDaFuncao(funcao, fila));
        }
    }

    [Fact]
    public void Medico_sem_especialidade_nao_e_oferecido_em_cadastro_novo()
    {
        // Existe so para as contas anteriores. Oferecer recriaria o problema.
        Assert.DoesNotContain(FuncaoProfissional.Medico, FilasDaFuncao.ParaCadastro);
    }

    [Fact]
    public void Fila_de_fora_e_reconhecida_como_de_fora()
    {
        Assert.True(FilasDaFuncao.EhDaFuncao(FuncaoProfissional.Dentista, Especialidade.Odontologia));

        // Nao impede nada — decide se o atendimento entra no historico como
        // feito fora da propria fila.
        Assert.False(FilasDaFuncao.EhDaFuncao(FuncaoProfissional.Dentista, Especialidade.ClinicaGeral));
    }
}

/// <summary>
/// Assumir e liberar. A regra existe porque, com a fila cheia, dois
/// profissionais abrem o mesmo paciente e o segundo so descobre quando salva
/// por cima do primeiro.
/// </summary>
[Collection(Colecoes.Api)]
public class AssumirEtapaTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public AssumirEtapaTests(ApiFixture fixture) => _fixture = fixture;

    private Task<HttpClient> AdministradorAsync()
        => _fixture.ClienteDoAdministradorAsync();

    /// <summary>
    /// Enfermeiro por padrao: a triagem, onde estes testes disputam o paciente,
    /// e fila dele. Assim o que se mede aqui e assumir e liberar, e nao o rastro
    /// de quem atende fora da propria fila.
    /// </summary>
    private async Task<(HttpClient Cliente, Guid Id, List<Especialidade> Filas)> ProfissionalAsync(
        string usuario,
        string nome,
        FuncaoProfissional funcao = FuncaoProfissional.Enfermeiro)
    {
        var (client, eu) = await _fixture.ClienteEPerfilDeAsync(
            usuario, nome, funcao, usuario.GetHashCode().ToString("X"));

        return (client, eu.Id, eu.Filas);
    }

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
                idadeAproximada = 40,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;
    }

    [SkippableFact]
    public async Task O_login_diz_em_que_fila_a_profissao_trabalha()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // As filas vem no login, que e o que a sessao guarda. Sem isso a tela
        // nao tem como abrir na fila certa.
        var (_, _, filas) = await ProfissionalAsync(
            "fila.pediatra", "Fila Pediatra", FuncaoProfissional.Pediatra);

        Assert.Equal([Especialidade.Pediatria], filas);
    }

    [SkippableFact]
    public async Task Assumido_sai_da_fila_de_quem_esta_livre()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (um, _, _) = await ProfissionalAsync("assume.um", "Assume Um");
        var (outro, _, _) = await ProfissionalAsync("assume.dois", "Assume Dois");

        var atendimento = await AbrirAsync(um, "Paciente Disputado");
        var baseId = (await um.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json))![0].Id;

        (await um.PostAsJsonAsync($"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir",
            new { }, Json)).EnsureSuccessStatusCode();

        var filaDoOutro = await outro.GetFromJsonAsync<List<AtendimentoResumoDto>>(
            $"/api/atendimentos?baseId={baseId}&fila=Triagem&ocultarAssumidos=true", Json);

        Assert.DoesNotContain(filaDoOutro!, a => a.Id == atendimento.Id);

        // Mas continua com quem assumiu: sumir de quem pegou seria absurdo.
        var meus = await um.GetFromJsonAsync<List<AtendimentoResumoDto>>(
            $"/api/atendimentos?baseId={baseId}&meus=true", Json);

        Assert.Contains(meus!, a => a.Id == atendimento.Id);
    }

    [SkippableFact]
    public async Task Quem_assumiu_continua_vendo_na_propria_fila()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (um, _, _) = await ProfissionalAsync("naosome.um", "Nao Some Um");
        var atendimento = await AbrirAsync(um, "Paciente Que Nao Some");
        var baseId = (await um.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json))![0].Id;

        (await um.PostAsJsonAsync($"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir",
            new { }, Json)).EnsureSuccessStatusCode();

        // Na fila normal, nao em "Meus": era exatamente aqui que o atendimento
        // sumia da vista de quem acabara de assumi-lo.
        var minhaFila = await um.GetFromJsonAsync<List<AtendimentoResumoDto>>(
            $"/api/atendimentos?baseId={baseId}&fila=Triagem&ocultarAssumidos=true", Json);

        Assert.Contains(minhaFila!, a => a.Id == atendimento.Id);
    }

    [SkippableFact]
    public async Task Dois_profissionais_nao_assumem_o_mesmo_paciente()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (um, _, _) = await ProfissionalAsync("colisao.um", "Colisao Um");
        var (outro, _, _) = await ProfissionalAsync("colisao.dois", "Colisao Dois");

        var atendimento = await AbrirAsync(um, "Paciente Unico");

        (await um.PostAsJsonAsync($"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir",
            new { }, Json)).EnsureSuccessStatusCode();

        var segunda = await outro.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir", new { }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);

        // A recusa diz com quem esta, senao ninguem sabe a quem perguntar.
        var corpo = await segunda.Content.ReadAsStringAsync();
        Assert.Contains("Colisao Um", corpo);
    }

    [SkippableFact]
    public async Task Reassumir_o_que_ja_e_meu_nao_e_erro()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (um, _, _) = await ProfissionalAsync("reassume.um", "Reassume Um");
        var atendimento = await AbrirAsync(um, "Paciente Recarregado");

        (await um.PostAsJsonAsync($"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir",
            new { }, Json)).EnsureSuccessStatusCode();

        // Acontece quando a tela recarrega. Falhar aqui assustaria sem motivo.
        (await um.PostAsJsonAsync($"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir",
            new { }, Json)).EnsureSuccessStatusCode();
    }

    [SkippableFact]
    public async Task Liberar_devolve_o_paciente_para_a_fila()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (um, _, _) = await ProfissionalAsync("libera.um", "Libera Um");
        var (outro, _, _) = await ProfissionalAsync("libera.dois", "Libera Dois");

        var atendimento = await AbrirAsync(um, "Paciente Devolvido");
        var baseId = (await um.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json))![0].Id;

        (await um.PostAsJsonAsync($"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir",
            new { }, Json)).EnsureSuccessStatusCode();
        (await um.PostAsJsonAsync($"/api/atendimentos/{atendimento.Id}/etapas/Triagem/liberar",
            new { }, Json)).EnsureSuccessStatusCode();

        var fila = await outro.GetFromJsonAsync<List<AtendimentoResumoDto>>(
            $"/api/atendimentos?baseId={baseId}&fila=Triagem&ocultarAssumidos=true", Json);

        Assert.Contains(fila!, a => a.Id == atendimento.Id);
    }

    [SkippableFact]
    public async Task Ninguem_libera_o_atendimento_de_outra_pessoa()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (um, _, _) = await ProfissionalAsync("alheio.um", "Alheio Um");
        var (outro, _, _) = await ProfissionalAsync("alheio.dois", "Alheio Dois");

        var atendimento = await AbrirAsync(um, "Paciente Alheio");

        (await um.PostAsJsonAsync($"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir",
            new { }, Json)).EnsureSuccessStatusCode();

        var tentativa = await outro.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/liberar", new { }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, tentativa.StatusCode);
    }

    [SkippableFact]
    public async Task A_coordenacao_destrava_o_que_ficou_preso()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (um, _, _) = await ProfissionalAsync("preso.um", "Preso Um");
        var atendimento = await AbrirAsync(um, "Paciente Preso");

        (await um.PostAsJsonAsync($"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir",
            new { }, Json)).EnsureSuccessStatusCode();

        // Alguem assume e sai para outra emergencia. Sem esta saida o paciente
        // ficaria preso numa fila que mais ninguem enxerga.
        var admin = await AdministradorAsync();

        (await admin.PostAsJsonAsync($"/api/atendimentos/{atendimento.Id}/etapas/Triagem/liberar",
            new { }, Json)).EnsureSuccessStatusCode();
    }

    [SkippableFact]
    public async Task Atender_fora_da_propria_fila_continua_permitido()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // Em campo a equipe e curta e as funcoes se cobrem. Trancar pararia o
        // plantao sem proteger nada: quem entrou ja foi cadastrado pela
        // coordenacao.
        var (dentista, _, _) = await ProfissionalAsync(
            "cobre.dentista", "Cobre Dentista", FuncaoProfissional.Dentista);

        var atendimento = await AbrirAsync(dentista, "Paciente Da Triagem Cheia");

        (await dentista.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir", new { }, Json))
            .EnsureSuccessStatusCode();
    }

    [SkippableFact]
    public async Task Atender_fora_da_propria_fila_fica_no_historico()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (dentista, _, _) = await ProfissionalAsync(
            "rastro.dentista", "Rastro Dentista", FuncaoProfissional.Dentista);

        var atendimento = await AbrirAsync(dentista, "Paciente Fora Da Fila");

        (await dentista.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir", new { }, Json))
            .EnsureSuccessStatusCode();

        var prontuario = await dentista.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{atendimento.Id}", Json);

        // Uma excecao sem rastro nao e excecao: viraria rotina silenciosa, que
        // e exatamente o que a fila por profissao veio evitar.
        Assert.Contains(
            prontuario!.Historico,
            a => a.Acao == AcaoAuditoria.AssumiuForaDaSuaFila &&
                 a.Especialidade == Especialidade.Triagem);
    }

    [SkippableFact]
    public async Task Atender_na_propria_fila_nao_vira_excecao_no_historico()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // O enfermeiro comeca o plantao na triagem. Marcar isso como fora da
        // fila encheria o historico de excecoes e esconderia as de verdade.
        var (enfermeiro, _, _) = await ProfissionalAsync(
            "rastro.enfermeiro", "Rastro Enfermeiro", FuncaoProfissional.Enfermeiro);

        var atendimento = await AbrirAsync(enfermeiro, "Paciente Da Propria Fila");

        (await enfermeiro.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir", new { }, Json))
            .EnsureSuccessStatusCode();

        var prontuario = await enfermeiro.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{atendimento.Id}", Json);

        Assert.Contains(prontuario!.Historico, a => a.Acao == AcaoAuditoria.AssumiuEtapa);
        Assert.DoesNotContain(
            prontuario.Historico, a => a.Acao == AcaoAuditoria.AssumiuForaDaSuaFila);
    }

    [SkippableFact]
    public async Task Nao_assume_etapa_que_o_atendimento_nao_tem()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (um, _, _) = await ProfissionalAsync("semfila.um", "Sem Fila Um");
        var atendimento = await AbrirAsync(um, "Paciente Sem Odonto");

        // So a triagem foi aberta; odontologia depende de encaminhamento.
        var resposta = await um.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Odontologia/assumir", new { }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
