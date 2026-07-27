namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>
/// Quais filas interessam a cada funcao.
///
/// Serve para a tela abrir na fila certa em vez de obrigar cada pessoa a achar
/// a sua no meio de sete abas. Nao e permissao: em campo a equipe e curta e as
/// funcoes se cobrem — medico faz triagem quando a fila estoura, e enfermeiro
/// acompanha uma consulta. Trancar aqui atrapalharia o atendimento sem proteger
/// nada, ja que quem entrou no sistema ja foi aprovado pela coordenacao.
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
        FuncaoProfissional.Medico =>
        [
            Especialidade.ClinicaGeral,
            Especialidade.Pediatria,
            Especialidade.Ortopedia,
            Especialidade.Triagem
        ],

        // Triagem primeiro: e onde a enfermagem comeca o plantao.
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

    /// <summary>Fila que abre por padrao para a funcao.</summary>
    public static Especialidade Padrao(FuncaoProfissional funcao) => De(funcao)[0];
}
