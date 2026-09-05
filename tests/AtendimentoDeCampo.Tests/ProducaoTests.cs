using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Producao por profissional.
///
/// O dado ja estava gravado ha tempos — cada etapa guarda quem atendeu, quando
/// comecou e quando terminou. Estes testes cobrem a leitura, que e onde as
/// decisoes moram: o que conta como atendimento e o que fica de fora.
/// </summary>
[Collection(Colecoes.Api)]
public class ProducaoTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ProducaoTests(ApiFixture fixture) => _fixture = fixture;

    private static async Task<Guid> BaseAsync(HttpClient client)
    {
        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        return bases![0].Id;
    }

    private static async Task<ProntuarioDto> AbrirAsync(HttpClient client, Guid baseId, string nome)
    {
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        var resposta = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId,
            paciente = new
            {
                codigo,
                nome,
                tipoDocumento = "SemDocumento",
                idadeAproximada = 30,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;
    }

    /// <summary>
    /// Fecha a triagem com uma duracao conhecida.
    ///
    /// Os instantes sao ajustados direto no banco: pelo HTTP a etapa duraria os
    /// milissegundos da chamada, e ai nao daria para testar nada sobre tempo.
    /// </summary>
    private async Task ConcluirTriagemAsync(Guid atendimentoId, Guid profissionalId, int minutos)
    {
        using var escopo = _fixture.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AtendimentoDbContext>();

        var etapa = await db.Etapas.FirstAsync(e =>
            e.AtendimentoId == atendimentoId && e.Especialidade == Especialidade.Triagem);

        etapa.ProfissionalId = profissionalId;
        etapa.Status = StatusEtapa.Concluida;
        etapa.ConcluidaEm = DateTime.UtcNow;
        etapa.IniciadaEm = etapa.ConcluidaEm.Value.AddMinutes(-minutos);

        await db.SaveChangesAsync();
    }

    private async Task<(HttpClient Cliente, Guid Id)> EnfermeiroAsync(string usuario, string nome)
    {
        var (cliente, eu) = await _fixture.ClienteEPerfilDeAsync(
            usuario, nome, FuncaoProfissional.Enfermeiro, usuario.GetHashCode().ToString("X"));

        return (cliente, eu.Id);
    }

    private static async Task<List<ProducaoProfissionalDto>> ProducaoAsync(HttpClient client, Guid baseId)
        => (await client.GetFromJsonAsync<List<ProducaoProfissionalDto>>(
            $"/api/relatorios/producao?baseId={baseId}", Json))!;

    [SkippableFact]
    public async Task Soma_os_atendimentos_concluidos_por_pessoa()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (cliente, id) = await EnfermeiroAsync("producao.conta", "Producao Conta");
        var baseId = await BaseAsync(cliente);

        var um = await AbrirAsync(cliente, baseId, "Paciente Contado Um");
        var dois = await AbrirAsync(cliente, baseId, "Paciente Contado Dois");

        await ConcluirTriagemAsync(um.Id, id, 10);
        await ConcluirTriagemAsync(dois.Id, id, 20);

        var minha = (await ProducaoAsync(cliente, baseId)).Single(p => p.ProfissionalId == id);

        Assert.Equal(2, minha.Atendimentos);
        Assert.Equal(30, minha.MinutosTotais);
    }

    [SkippableFact]
    public async Task Etapa_ainda_aberta_nao_entra()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (cliente, id) = await EnfermeiroAsync("producao.aberta", "Producao Aberta");
        var baseId = await BaseAsync(cliente);

        var atendimento = await AbrirAsync(cliente, baseId, "Paciente Ainda Aberto");

        (await cliente.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir", new { }, Json))
            .EnsureSuccessStatusCode();

        // Uma etapa aberta teria a duracao do plantao inteiro de quem esqueceu
        // de fechar, e um esquecimento assim deformaria a tabela toda.
        var producao = await ProducaoAsync(cliente, baseId);

        Assert.DoesNotContain(producao, p => p.ProfissionalId == id);
    }

    [SkippableFact]
    public async Task Etapa_cancelada_no_encaminhamento_nao_conta_como_producao()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (cliente, id) = await EnfermeiroAsync("producao.cancelada", "Producao Cancelada");
        var baseId = await BaseAsync(cliente);

        var atendimento = await AbrirAsync(cliente, baseId, "Paciente Encaminhado Sem Atender");

        (await cliente.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            encaminhamento = "ClinicaGeral"
        }, Json)).EnsureSuccessStatusCode();

        (await cliente.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/encaminhar",
            new { destino = "Odontologia", motivo = "Nao e caso clinico." },
            Json)).EnsureSuccessStatusCode();

        var minha = (await ProducaoAsync(cliente, baseId)).SingleOrDefault(p => p.ProfissionalId == id);

        // A clinica geral recebeu e passou adiante sem atender. Contar isso
        // inflaria a especialidade com atendimento que nao aconteceu.
        Assert.DoesNotContain(
            minha?.PorFila ?? [],
            f => f.Especialidade == Especialidade.ClinicaGeral);
    }

    [SkippableFact]
    public async Task Separa_a_producao_por_fila()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (cliente, id) = await EnfermeiroAsync("producao.porfila", "Producao Por Fila");
        var baseId = await BaseAsync(cliente);

        var atendimento = await AbrirAsync(cliente, baseId, "Paciente Da Fila");
        await ConcluirTriagemAsync(atendimento.Id, id, 15);

        var minha = (await ProducaoAsync(cliente, baseId)).Single(p => p.ProfissionalId == id);
        var triagem = minha.PorFila.Single(f => f.Especialidade == Especialidade.Triagem);

        // Sem separar por fila nao da para saber que fila consome o plantao, que
        // e o que decide escala.
        Assert.Equal(1, triagem.Atendimentos);
        Assert.Equal(15, triagem.MinutosTotais);
    }

    [SkippableFact]
    public async Task A_mediana_ignora_a_ficha_esquecida_aberta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (cliente, id) = await EnfermeiroAsync("producao.mediana", "Producao Mediana");
        var baseId = await BaseAsync(cliente);

        foreach (var minutos in new[] { 10, 12, 600 })
        {
            var atendimento = await AbrirAsync(cliente, baseId, $"Paciente De {minutos}");
            await ConcluirTriagemAsync(atendimento.Id, id, minutos);
        }

        var minha = (await ProducaoAsync(cliente, baseId)).Single(p => p.ProfissionalId == id);

        // A media daria 207 minutos e faria a tabela mentir sobre o dia inteiro
        // por causa de um caso so.
        Assert.Equal(12, minha.MinutosMedianos);
        Assert.Equal(622, minha.MinutosTotais);
    }

    [SkippableFact]
    public async Task Atendimento_de_segundos_conta_como_um_minuto()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (cliente, id) = await EnfermeiroAsync("producao.curta", "Producao Curta");
        var baseId = await BaseAsync(cliente);

        var atendimento = await AbrirAsync(cliente, baseId, "Paciente Rapido");
        await ConcluirTriagemAsync(atendimento.Id, id, 0);

        var minha = (await ProducaoAsync(cliente, baseId)).Single(p => p.ProfissionalId == id);

        // Zerar sumiria com o atendimento do total, como se nao tivesse ocorrido.
        Assert.Equal(1, minha.MinutosTotais);
    }

    [SkippableFact]
    public async Task Fora_do_periodo_nao_entra()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (cliente, id) = await EnfermeiroAsync("producao.periodo", "Producao Periodo");
        var baseId = await BaseAsync(cliente);

        var atendimento = await AbrirAsync(cliente, baseId, "Paciente De Ontem");
        await ConcluirTriagemAsync(atendimento.Id, id, 10);

        var ontem = DateTime.UtcNow.AddDays(-1).ToString("O");
        var anteontem = DateTime.UtcNow.AddDays(-2).ToString("O");

        var producao = await cliente.GetFromJsonAsync<List<ProducaoProfissionalDto>>(
            $"/api/relatorios/producao?baseId={baseId}&de={anteontem}&ate={ontem}", Json);

        Assert.DoesNotContain(producao!, p => p.ProfissionalId == id);
    }

    [SkippableFact]
    public async Task Periodo_invertido_e_recusado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (cliente, _) = await EnfermeiroAsync("producao.invertida", "Producao Invertida");
        var baseId = await BaseAsync(cliente);

        var hoje = DateTime.UtcNow.ToString("O");
        var semanaPassada = DateTime.UtcNow.AddDays(-7).ToString("O");

        // Silenciar devolveria uma tabela vazia, e a coordenacao concluiria que
        // ninguem atendeu nada.
        var resposta = await cliente.GetAsync(
            $"/api/relatorios/producao?baseId={baseId}&de={hoje}&ate={semanaPassada}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Quem_nao_e_coordenacao_ve_so_a_propria_producao()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (uma, idUma) = await EnfermeiroAsync("producao.privada.um", "Producao Privada Um");
        var (outra, idOutra) = await EnfermeiroAsync("producao.privada.dois", "Producao Privada Dois");

        var baseId = await BaseAsync(uma);

        var doUm = await AbrirAsync(uma, baseId, "Paciente Do Um");
        var doDois = await AbrirAsync(outra, baseId, "Paciente Do Dois");

        await ConcluirTriagemAsync(doUm.Id, idUma, 10);
        await ConcluirTriagemAsync(doDois.Id, idOutra, 10);

        var vistoPorUma = await ProducaoAsync(uma, baseId);

        // Producao alheia e avaliacao de desempenho, e quem responde por isso e
        // a coordenacao.
        Assert.Contains(vistoPorUma, p => p.ProfissionalId == idUma);
        Assert.DoesNotContain(vistoPorUma, p => p.ProfissionalId == idOutra);
    }

    [SkippableFact]
    public async Task A_coordenacao_ve_a_equipe_inteira()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (cliente, id) = await EnfermeiroAsync("producao.visivel", "Producao Visivel");
        var baseId = await BaseAsync(cliente);

        var atendimento = await AbrirAsync(cliente, baseId, "Paciente Da Equipe");
        await ConcluirTriagemAsync(atendimento.Id, id, 10);

        var admin = await _fixture.ClienteDoAdministradorAsync();
        var producao = await ProducaoAsync(admin, baseId);

        Assert.Contains(producao, p => p.ProfissionalId == id);
    }

    [SkippableFact]
    public async Task A_tabela_traz_o_registro_do_conselho()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (cliente, id) = await EnfermeiroAsync("producao.coren", "Producao Coren");
        var baseId = await BaseAsync(cliente);

        var atendimento = await AbrirAsync(cliente, baseId, "Paciente Com Coren");
        await ConcluirTriagemAsync(atendimento.Id, id, 10);

        var minha = (await ProducaoAsync(cliente, baseId)).Single(p => p.ProfissionalId == id);

        // Producao vira relatorio para financiador: o nome sozinho nao
        // identifica ninguem fora do sistema.
        Assert.Equal(ConselhoTipo.Coren, minha.Conselho);
        Assert.False(string.IsNullOrWhiteSpace(minha.Registro));
    }
}
