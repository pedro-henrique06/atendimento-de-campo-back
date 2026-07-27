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
    public void Cada_funcao_abre_na_fila_onde_ela_trabalha()
    {
        Assert.Equal(Especialidade.ClinicaGeral, FilasDaFuncao.Padrao(FuncaoProfissional.Medico));
        Assert.Equal(Especialidade.Triagem, FilasDaFuncao.Padrao(FuncaoProfissional.Enfermeiro));
        Assert.Equal(Especialidade.Odontologia, FilasDaFuncao.Padrao(FuncaoProfissional.Dentista));
        Assert.Equal(Especialidade.SaudeMental, FilasDaFuncao.Padrao(FuncaoProfissional.Psicologo));
        Assert.Equal(Especialidade.Triagem, FilasDaFuncao.Padrao(FuncaoProfissional.Recepcao));
    }

    [Fact]
    public void O_medico_alcanca_a_triagem()
    {
        // Nao e permissao: em campo a equipe e curta e as funcoes se cobrem.
        // Quando a fila de triagem estoura, o medico tria.
        Assert.Contains(Especialidade.Triagem, FilasDaFuncao.De(FuncaoProfissional.Medico));
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
    public void Nenhuma_funcao_fica_sem_fila()
    {
        foreach (var funcao in Enum.GetValues<FuncaoProfissional>())
        {
            Assert.NotEmpty(FilasDaFuncao.De(funcao));
        }
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

    private async Task<HttpClient> AdministradorAsync()
    {
        var client = _fixture.CreateClient();

        var login = await (await client.PostAsJsonAsync("/api/auth/login", new
        {
            usuario = ApiFixture.AdminUsuario,
            senha = ApiFixture.AdminSenha,
            idioma = "Pt"
        }, Json)).Content.ReadFromJsonAsync<LoginResponse>(Json);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    private async Task<(HttpClient Cliente, Guid Id, List<Especialidade> Filas)> ProfissionalAsync(
        string usuario,
        string nome)
    {
        var client = _fixture.CreateClient();
        const string senha = "plantao-2026";

        var registro = await client.PostAsJsonAsync("/api/auth/registrar", new
        {
            usuario,
            nome,
            funcao = "Medico",
            registro = usuario.GetHashCode().ToString("X"),
            senha,
            confirmacaoSenha = senha,
            idioma = "Pt"
        }, Json);

        if (registro.IsSuccessStatusCode)
        {
            var criado = await registro.Content.ReadFromJsonAsync<ProfissionalDto>(Json);
            var admin = await AdministradorAsync();
            (await admin.PostAsJsonAsync($"/api/profissionais/{criado!.Id}/aprovar", new { }, Json))
                .EnsureSuccessStatusCode();
        }

        var entrada = await client.PostAsJsonAsync("/api/auth/login",
            new { usuario, senha, idioma = "Pt" }, Json);
        entrada.EnsureSuccessStatusCode();

        var sessao = await entrada.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao!.Token);

        return (client, sessao.Profissional.Id, sessao.Profissional.Filas);
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
    public async Task O_login_diz_em_que_filas_a_funcao_trabalha()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // As filas vem no login, que e o que a sessao guarda. Sem isso a tela
        // nao tem como abrir na fila certa.
        var (_, _, filas) = await ProfissionalAsync("fila.medico", "Fila Medico");

        Assert.Equal(Especialidade.ClinicaGeral, filas[0]);
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
