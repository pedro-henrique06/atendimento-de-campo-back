namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>
/// Em que fila cada profissao atende.
///
/// A tabela decide o que a pessoa ve ao entrar: o dentista abre na odontologia,
/// o pediatra na pediatria. Antes disso a lista abria sempre na triagem e cada
/// um tinha que achar a sua no meio de sete abas.
///
/// **Nao e uma tranca.** Ver e agir fora da propria fila continua possivel, e
/// isso e deliberado: em campo a equipe e curta e as funcoes se cobrem — o
/// medico tria quando a fila estoura, e trancar aqui pararia o plantao sem
/// proteger nada, ja que quem entrou no sistema foi cadastrado pela
/// coordenacao. O que existe e rastro: assumir um paciente fora da propria fila
/// fica gravado como <see cref="AcaoAuditoria.AssumiuForaDaSuaFila"/> no
/// historico do atendimento.
///
/// A ordem importa: a primeira da lista e a que abre por padrao.
/// </summary>
public static class FilasDaFuncao
{
    private static readonly Especialidade[] Todas =
    [
        Especialidade.Triagem,
        Especialidade.ClinicaGeral,
        Especialidade.Pediatria,
        Especialidade.Ortopedia,
        Especialidade.Odontologia,
        Especialidade.Enfermagem,
        Especialidade.SaudeMental
    ];

    public static IReadOnlyList<Especialidade> De(FuncaoProfissional funcao) => funcao switch
    {
        // Uma especialidade medica, uma fila. E o que faz "cada um ve a sua
        // fila" ser verdade: um "medico" generico cairia nas tres.
        FuncaoProfissional.ClinicoGeral => [Especialidade.ClinicaGeral],
        FuncaoProfissional.Pediatra => [Especialidade.Pediatria],
        FuncaoProfissional.Ortopedista => [Especialidade.Ortopedia],

        // Conta antiga, criada antes das especialidades existirem. Ve as tres
        // ate a coordenacao reclassificar.
        FuncaoProfissional.Medico =>
        [
            Especialidade.ClinicaGeral,
            Especialidade.Pediatria,
            Especialidade.Ortopedia
        ],

        // Duas filas, e nao uma excecao a regra: a enfermagem comeca o plantao
        // triando e depois atende na propria fila.
        FuncaoProfissional.Enfermeiro or FuncaoProfissional.TecnicoEnfermagem =>
        [
            Especialidade.Triagem,
            Especialidade.Enfermagem
        ],

        FuncaoProfissional.Dentista => [Especialidade.Odontologia],

        FuncaoProfissional.Psicologo => [Especialidade.SaudeMental],

        FuncaoProfissional.Fisioterapeuta => [Especialidade.Ortopedia],

        // O farmaceutico atua na dispensacao, que acontece dentro da enfermagem.
        FuncaoProfissional.Farmaceutico => [Especialidade.Enfermagem],

        // A recepcao cadastra e acompanha quem esta esperando para ser triado.
        FuncaoProfissional.Recepcao => [Especialidade.Triagem],

        // Coordenacao e "Outro" veem tudo: uma enxerga a operacao inteira, a
        // outra e justamente o caso em que o sistema nao sabe o que a pessoa faz.
        _ => Todas
    };

    /// <summary>Fila que abre por padrao para a profissao.</summary>
    public static Especialidade Padrao(FuncaoProfissional funcao) => De(funcao)[0];

    /// <summary>
    /// Se esta fila e do dia a dia da profissao. Falso nao impede nada — decide
    /// se o atendimento entra no historico como feito fora da propria fila.
    /// </summary>
    public static bool EhDaFuncao(FuncaoProfissional funcao, Especialidade fila)
        => De(funcao).Contains(fila);

    /// <summary>
    /// Profissoes oferecidas num cadastro novo, na ordem em que a tela deve
    /// lista-las. <see cref="FuncaoProfissional.Medico"/> fica de fora: existe
    /// so para as contas anteriores as especialidades.
    /// </summary>
    public static IReadOnlyList<FuncaoProfissional> ParaCadastro =>
    [
        FuncaoProfissional.ClinicoGeral,
        FuncaoProfissional.Pediatra,
        FuncaoProfissional.Ortopedista,
        FuncaoProfissional.Dentista,
        FuncaoProfissional.Enfermeiro,
        FuncaoProfissional.TecnicoEnfermagem,
        FuncaoProfissional.Psicologo,
        FuncaoProfissional.Fisioterapeuta,
        FuncaoProfissional.Farmaceutico,
        FuncaoProfissional.Recepcao,
        FuncaoProfissional.Coordenacao,
        FuncaoProfissional.Outro
    ];
}
