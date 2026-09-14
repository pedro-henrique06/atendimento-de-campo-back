using AtendimentoDeCampo.Api.Servicos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// As filas que faltavam: ginecologia, cirurgia, anestesia, cardiologia,
/// ultrassom e farmacia.
///
/// Ultrassom e farmacia sao filas derivadas — ninguem chega nelas sem alguem ter
/// mandado —, mas sao filas: tem profissional proprio, entram na producao por
/// area e o paciente volta delas pelo caminho de devolucao que ja existe.
/// </summary>
public class FilasNovasTests
{
    [Fact]
    public void O_farmaceutico_sai_da_fila_da_enfermagem()
    {
        // Ate aqui ele caia na enfermagem por nao haver fila da farmacia. Isso
        // misturava a producao das duas e punha o farmaceutico numa fila de
        // atendimento que nao e a dele.
        Assert.Equal([Especialidade.Farmacia], FilasDaFuncao.De(FuncaoProfissional.Farmaceutico));
        Assert.False(FilasDaFuncao.EhDaFuncao(FuncaoProfissional.Farmaceutico, Especialidade.Enfermagem));
    }

    [Fact]
    public void Cada_especialidade_nova_tem_a_sua_fila()
    {
        Assert.Equal([Especialidade.Ginecologia], FilasDaFuncao.De(FuncaoProfissional.Ginecologista));
        Assert.Equal([Especialidade.Cirurgia], FilasDaFuncao.De(FuncaoProfissional.Cirurgiao));
        Assert.Equal([Especialidade.Cardiologia], FilasDaFuncao.De(FuncaoProfissional.Cardiologista));
        Assert.Equal([Especialidade.Ultrassom], FilasDaFuncao.De(FuncaoProfissional.Ultrassonografista));
    }

    [Fact]
    public void O_anestesista_ve_a_anestesia_e_a_cirurgia()
    {
        var filas = FilasDaFuncao.De(FuncaoProfissional.Anestesista);

        // Avaliar antes e acompanhar durante sao o trabalho dele, nao uma
        // excecao a regra de uma fila por profissao.
        Assert.Equal([Especialidade.Anestesia, Especialidade.Cirurgia], filas);
        Assert.Equal(Especialidade.Anestesia, FilasDaFuncao.Padrao(FuncaoProfissional.Anestesista));
    }

    [Fact]
    public void As_profissoes_novas_sao_oferecidas_no_cadastro()
    {
        var cadastro = FilasDaFuncao.ParaCadastro;

        Assert.Contains(FuncaoProfissional.Ginecologista, cadastro);
        Assert.Contains(FuncaoProfissional.Cirurgiao, cadastro);
        Assert.Contains(FuncaoProfissional.Anestesista, cadastro);
        Assert.Contains(FuncaoProfissional.Cardiologista, cadastro);
        Assert.Contains(FuncaoProfissional.Ultrassonografista, cadastro);

        // O medico generico continua fora: existe so para as contas anteriores
        // as especialidades.
        Assert.DoesNotContain(FuncaoProfissional.Medico, cadastro);
    }

    [Fact]
    public void Quem_assina_laudo_ou_receita_tem_conselho()
    {
        // Sem conselho, o cadastro nao exige registro — e a ficha sai sem a
        // identificacao que vale fora do sistema.
        Assert.Equal(ConselhoTipo.Crm, ServicoAutenticacao.ConselhoPara(FuncaoProfissional.Ginecologista));
        Assert.Equal(ConselhoTipo.Crm, ServicoAutenticacao.ConselhoPara(FuncaoProfissional.Cirurgiao));
        Assert.Equal(ConselhoTipo.Crm, ServicoAutenticacao.ConselhoPara(FuncaoProfissional.Anestesista));
        Assert.Equal(ConselhoTipo.Crm, ServicoAutenticacao.ConselhoPara(FuncaoProfissional.Cardiologista));

        // O laudo de USG do formulario de papel e assinado por "Medico ___ CRM".
        Assert.Equal(ConselhoTipo.Crm, ServicoAutenticacao.ConselhoPara(FuncaoProfissional.Ultrassonografista));
    }

    [Fact]
    public void Coordenacao_enxerga_tambem_as_filas_novas()
    {
        var filas = FilasDaFuncao.De(FuncaoProfissional.Coordenacao);

        // Quem enxerga a operacao inteira precisa enxergar inteira mesmo: uma
        // fila de fora da lista sumiria da tela da coordenacao.
        foreach (var fila in Enum.GetValues<Especialidade>())
        {
            Assert.Contains(fila, filas);
        }
    }
}
