using System.ComponentModel.DataAnnotations;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;

namespace AtendimentoDeCampo.Api.Contratos;

// ---------------------------------------------------------------------------
// Autenticacao
// ---------------------------------------------------------------------------

/// <summary>Login: usuario e senha.</summary>
public sealed record LoginRequest
{
    [Required, MaxLength(40)]
    public string Usuario { get; init; } = string.Empty;

    [Required]
    public string Senha { get; init; } = string.Empty;

    public Idioma Idioma { get; init; } = Idioma.Pt;
}

/// <summary>
/// Cadastro de profissional, preenchido pela coordenacao.
///
/// Nao ha senha aqui de proposito: quem cadastra nao escolhe a senha de outra
/// pessoa. O sistema sorteia uma senha provisoria, devolve uma unica vez em
/// <see cref="ContaCriadaDto"/> e obriga a troca no primeiro acesso.
/// </summary>
public sealed record CriarContaRequest
{
    [Required, MaxLength(40)]
    public string Usuario { get; init; } = string.Empty;

    [Required, MaxLength(160)]
    public string Nome { get; init; } = string.Empty;

    [EmailAddress, MaxLength(200)]
    public string? Email { get; init; }

    /// <summary>Decide em que fila a pessoa cai. Ver <c>FilasDaFuncao</c>.</summary>
    [Required]
    public FuncaoProfissional Funcao { get; init; }

    [MaxLength(40)]
    public string? Registro { get; init; }

    public Idioma Idioma { get; init; } = Idioma.Pt;
}

/// <summary>
/// Conta recem-criada e a senha do primeiro acesso.
///
/// A senha aparece so nesta resposta: nao ha como consulta-la depois, porque so
/// o hash e gravado. Se ela se perder, a coordenacao sorteia outra.
/// </summary>
public sealed record ContaCriadaDto(ProfissionalDto Profissional, string SenhaProvisoria);

public sealed record AlterarProfissaoRequest
{
    [Required]
    public FuncaoProfissional Funcao { get; init; }

    [MaxLength(40)]
    public string? Registro { get; init; }
}

/// <summary>Troca da propria senha. Obrigatoria no primeiro acesso.</summary>
public sealed record TrocarSenhaRequest
{
    [Required]
    public string SenhaAtual { get; init; } = string.Empty;

    [Required]
    public string NovaSenha { get; init; } = string.Empty;

    [Required]
    public string ConfirmacaoSenha { get; init; } = string.Empty;
}

public sealed record LoginResponse(string Token, DateTime ExpiraEm, ProfissionalDto Profissional);

public sealed record ProfissionalDto(
    Guid Id,
    string Usuario,
    string Nome,
    string? Email,
    FuncaoProfissional Funcao,
    ConselhoTipo ConselhoTipo,
    string? Registro,
    Idioma Idioma,
    StatusConta Status,
    bool EhAdministrador,
    string? MotivoRecusa,
    DateTime CriadoEm,
    /// <summary>
    /// Filas da profissao, na ordem em que a tela deve oferece-las. A primeira e
    /// a que abre por padrao.
    ///
    /// Nao e tranca: ver e agir fora dela continua possivel, porque em campo as
    /// funcoes se cobrem. O que muda e o rastro — assumir um paciente fora daqui
    /// fica gravado no historico do atendimento.
    /// </summary>
    List<Especialidade> Filas,
    /// <summary>
    /// A senha ainda e a provisoria que a coordenacao entregou. Enquanto for
    /// verdadeiro, a unica coisa que a pessoa pode fazer e troca-la.
    /// </summary>
    bool PrecisaTrocarSenha);

public sealed record RecusarContaRequest
{
    [Required, MaxLength(300)]
    public string Motivo { get; init; } = string.Empty;
}

public sealed record DefinirAdministradorRequest
{
    public bool EhAdministrador { get; init; }
}

public sealed record UsuarioDisponivelResponse(string Usuario, bool Disponivel);

// ---------------------------------------------------------------------------
// Bases
// ---------------------------------------------------------------------------

public sealed record BaseDto(Guid Id, string Nome, string PrefixoCodigo, bool Ativa);

/// <summary>
/// A base como a coordenacao a ve: com o que ela precisa para decidir. O total
/// de atendimentos explica por que o prefixo travou, e os abertos explicam por
/// que a desativacao foi recusada.
/// </summary>
public sealed record BaseAdminDto(
    Guid Id,
    string Nome,
    string PrefixoCodigo,
    bool Ativa,
    DateTime CriadaEm,
    int TotalAtendimentos,
    int AtendimentosAbertos,
    bool PrefixoEditavel);

public sealed record SalvarBaseRequest
{
    [Required, MaxLength(160)]
    public string Nome { get; init; } = string.Empty;

    /// <summary>Vazio deriva do nome, que e o que a tela ja sugere.</summary>
    [MaxLength(3)]
    public string? PrefixoCodigo { get; init; }
}

public sealed record DefinirAtivaRequest
{
    public bool Ativa { get; init; }
}

public sealed record PrefixoSugeridoDto(string Prefixo);

public sealed record EncaminharRequest
{
    public Especialidade Destino { get; init; }

    /// <summary>
    /// Obrigatorio: quem recebe o paciente na fila seguinte precisa saber por
    /// que ele chegou ali. Encaminhamento mudo faz o paciente circular entre
    /// filas sem ninguem entender o caminho.
    /// </summary>
    [Required, MaxLength(300)]
    public string Motivo { get; init; } = string.Empty;
}

/// <summary>Devolucao para a fila que encaminhou o paciente.</summary>
public sealed record DevolverRequest
{
    /// <summary>
    /// Obrigatorio, pelo mesmo motivo do encaminhamento: quem recebe o paciente
    /// de volta precisa saber por que ele voltou.
    /// </summary>
    [Required, MaxLength(300)]
    public string Motivo { get; init; } = string.Empty;
}

/// <summary>Alta: encerra a etapa de quem atende e o atendimento junto.</summary>
public sealed record DarAltaRequest
{
    /// <summary>
    /// Confirma o cancelamento das filas que ficaram pendentes.
    ///
    /// Falso na primeira tentativa de proposito: a API recusa listando as filas,
    /// e a tela pergunta antes. Tirar o paciente da fila da odontologia em
    /// silencio e o tipo de coisa que so se descobre quando ele volta no dia
    /// seguinte perguntando pelo dentista.
    /// </summary>
    public bool CancelarPendentes { get; init; }
}

// ---------------------------------------------------------------------------
// Paciente e atendimento
// ---------------------------------------------------------------------------

public sealed record DadosPacienteRequest
{
    /// <summary>
    /// Codigo do paciente. Vem da tela: ou foi gerado agora para quem chega pela
    /// primeira vez, ou foi digitado por quem ja tinha um. E o que liga a visita
    /// de hoje ao cadastro de antes quando nao ha documento.
    /// </summary>
    [Required, MaxLength(9)]
    public string Codigo { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string Nome { get; init; } = string.Empty;

    public TipoDocumento TipoDocumento { get; init; } = TipoDocumento.SemDocumento;

    [MaxLength(60)]
    public string? NumeroDocumento { get; init; }

    /// <summary>
    /// Cartao do SUS. Campo proprio, e nao um tipo de documento: como tipo, um
    /// excluiria o outro e o numero se perderia.
    /// </summary>
    [MaxLength(20)]
    public string? CartaoSus { get; init; }

    /// <summary>Escolhida da lista mantida pela coordenacao; nao e texto livre.</summary>
    public Guid? ComunidadeId { get; init; }

    /// <summary>Obrigatorio quando o paciente e menor de idade.</summary>
    [MaxLength(200)]
    public string? NomeDaMae { get; init; }

    /// <summary>Obrigatorio quando o paciente e menor de idade.</summary>
    [MaxLength(300)]
    public string? Endereco { get; init; }

    public DateOnly? DataNascimento { get; init; }

    [Range(0, 130)]
    public int? IdadeAproximada { get; init; }

    public Sexo Sexo { get; init; } = Sexo.NaoInformado;

    public StatusAlergia StatusAlergia { get; init; } = StatusAlergia.NaoPerguntado;

    [MaxLength(500)]
    public string? Alergias { get; init; }

    public List<CondicaoCronica> CondicoesCronicas { get; init; } = new();
    public List<Vulnerabilidade> Vulnerabilidades { get; init; } = new();

    /// <summary>Consentimento explicito para registro dos dados, como no formulario original.</summary>
    public bool ConsentimentoRegistro { get; init; }
}

public sealed record CriarAtendimentoRequest
{
    [Required]
    public Guid BaseId { get; init; }

    [Required]
    public DadosPacienteRequest Paciente { get; init; } = new();

    [MaxLength(500)]
    public string? QueixaPrincipal { get; init; }

    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? PrecisaoMetros { get; init; }
}

public sealed record AlertaAlergiaDto(bool Exibir, string? Texto);

/// <summary>Codigo recem-sorteado, ainda nao gravado: so vira cadastro se a tela for salva.</summary>
public sealed record CodigoNovoDto(string Codigo);

/// <summary>
/// O que a tela mostra antes de reabrir um paciente conhecido: o suficiente para
/// a equipe confirmar que e a pessoa certa, sem despejar o prontuario inteiro.
/// </summary>
public sealed record PacienteConhecidoDto(
    PacienteDto Paciente,
    int TotalAtendimentos,
    DateTime? UltimoAtendimentoEm,
    string? UltimaBase);

public sealed record PacienteDto(
    Guid Id,
    string Codigo,
    string Nome,
    TipoDocumento TipoDocumento,
    string? NumeroDocumento,
    /// <summary>Campo proprio: a pessoa pode ter RG <em>e</em> cartao do SUS.</summary>
    string? CartaoSus,
    Guid? ComunidadeId,
    string? Comunidade,
    string? NomeDaMae,
    string? Endereco,
    DateOnly? DataNascimento,
    int? Idade,
    /// <summary>Menor de idade; nulo quando a idade e desconhecida.</summary>
    bool EhMenor,
    Sexo Sexo,
    StatusAlergia StatusAlergia,
    string? Alergias,
    AlertaAlergiaDto Alerta,
    List<CondicaoCronica> CondicoesCronicas,
    List<Vulnerabilidade> Vulnerabilidades,
    bool ConsentimentoRegistro);

/// <summary>
/// Quem assinou o ato clinico.
///
/// O nome sozinho nao basta numa ficha: o registro no conselho e o que
/// identifica a pessoa fora do sistema — e e o que a equipe, a auditoria e o
/// servico de referencia procuram quando precisam saber quem atendeu.
/// </summary>
public sealed record AutorDto(string Nome, ConselhoTipo Conselho, string? Registro);

/// <summary>
/// Etapa vista de fora, para a lista e para o prontuario saberem que fila esta
/// aberta e com quem.
/// </summary>
/// <remarks>
/// <c>Profissional</c> e so o nome, de proposito: aqui ele diz quem esta com o
/// paciente agora, e a tela compara com o nome de quem esta olhando. Assinatura
/// e outra coisa e mora nas fichas, em <see cref="AutorDto"/>.
/// </remarks>
public sealed record EtapaResumoDto(
    Guid Id,
    Especialidade Especialidade,
    StatusEtapa Status,
    string? Profissional,
    DateTime? IniciadaEm,
    DateTime? ConcluidaEm,
    /// <summary>
    /// Quando o profissional assumiu esta passagem. E daqui que o cronometro da
    /// tela conta — nao da entrada na fila, que incluiria a espera.
    /// </summary>
    DateTime? AssumidaEm = null,
    /// <summary>Quem encaminhou o paciente para esta fila, se veio de outra.</summary>
    string? EncaminhadaPor = null,
    /// <summary>De qual fila veio: o destino do botao de devolver.</summary>
    Especialidade? EncaminhadaDe = null);

public sealed record AtendimentoResumoDto(
    Guid Id,
    string Codigo,
    string PacienteNome,
    StatusAtendimento Status,
    ClassificacaoRisco? ClassificacaoRisco,
    string? Resumo,
    List<EtapaResumoDto> Etapas,
    DateTime CriadoEm,
    DateTime? FinalizadoEm);

public sealed record EsperaFilaDto(
    Especialidade Especialidade,
    DateTime EntrouEm,
    DateTime? SaiuEm,
    int? EsperaMinutos);

public sealed record AuditoriaDto(
    string Profissional,
    AcaoAuditoria Acao,
    Especialidade? Especialidade,
    string? Campo,
    string? ValorAnterior,
    string? ValorNovo,
    DateTime CriadaEm);

public sealed record LocalizacaoDto(double Latitude, double Longitude, double? PrecisaoMetros);

public sealed record ProntuarioDto(
    Guid Id,
    string Codigo,
    BaseDto Base,
    PacienteDto Paciente,
    StatusAtendimento Status,
    ClassificacaoRisco? ClassificacaoRisco,
    string? QueixaPrincipal,
    LocalizacaoDto? Localizacao,
    string CriadoPor,
    DateTime CriadoEm,
    string? FinalizadoPor,
    DateTime? FinalizadoEm,
    TriagemDto? Triagem,
    List<ConsultaDto> Consultas,
    OdontologiaDto? Odontologia,
    EnfermagemDto? Enfermagem,
    List<EsperaFilaDto> TempoNasFilas,
    List<AuditoriaDto> Historico,
    /// <summary>
    /// Etapas do atendimento. A tela precisa delas para saber qual fila esta
    /// aberta e oferecer o encaminhamento a partir dela.
    /// </summary>
    List<EtapaResumoDto> Etapas);

// ---------------------------------------------------------------------------
// Triagem
// ---------------------------------------------------------------------------

public sealed record AchadosStartRequest
{
    public bool Deambula { get; init; }
    public bool RespiraEspontaneamente { get; init; } = true;
    public bool RespiraAposAberturaViaAerea { get; init; }

    [Range(0, 80)]
    public int? FrequenciaRespiratoria { get; init; }

    public bool PulsoRadialPresente { get; init; } = true;

    [Range(0, 30)]
    public int? TempoEnchimentoCapilarSegundos { get; init; }

    public bool ObedeceComandos { get; init; } = true;
}

public sealed record RegistrarTriagemRequest
{
    [Range(40, 300)] public int? PressaoSistolica { get; init; }
    [Range(20, 200)] public int? PressaoDiastolica { get; init; }
    [Range(20, 260)] public int? FrequenciaCardiaca { get; init; }
    [Range(0, 80)] public int? FrequenciaRespiratoria { get; init; }
    [Range(50, 100)] public int? SaturacaoO2 { get; init; }
    [Range(30, 45)] public double? TemperaturaCelsius { get; init; }
    [Range(10, 900)] public int? GlicemiaCapilar { get; init; }

    /// <summary>Peso em quilos. E o que permite conferir dose pediatrica.</summary>
    [Range(0.5, 400)] public double? PesoKg { get; init; }

    [Range(20, 250)] public int? AlturaCm { get; init; }

    /// <summary>
    /// Dor autorreferida de 0 a 10. De 0, e nao de 1: "sem dor" e resposta, e
    /// nulo e "nao perguntei".
    /// </summary>
    [Range(0, 10)] public int? EscalaDor { get; init; }

    public List<Sintoma> Sintomas { get; init; } = new();

    [MaxLength(300)]
    public string? OutroSintoma { get; init; }

    [MaxLength(500)]
    public string? MedicamentosEmUso { get; init; }

    public StatusAlergia StatusAlergia { get; init; } = StatusAlergia.NaoPerguntado;

    [MaxLength(500)]
    public string? Alergias { get; init; }

    /// <summary>Classificacao escolhida pelo profissional. Sempre prevalece.</summary>
    [Required]
    public ClassificacaoRisco ClassificacaoRisco { get; init; }

    /// <summary>Achados objetivos; quando enviados, geram a sugestao do START.</summary>
    public AchadosStartRequest? AchadosStart { get; init; }

    public Especialidade? Encaminhamento { get; init; }

    [MaxLength(1000)]
    public string? Observacoes { get; init; }
}

public sealed record SugestaoStartDto(ClassificacaoRisco Sugerida, string Motivo, bool Divergente);

public sealed record TriagemDto(
    Guid EtapaId,
    AutorDto? Profissional,
    int? PressaoSistolica,
    int? PressaoDiastolica,
    int? FrequenciaCardiaca,
    int? FrequenciaRespiratoria,
    int? SaturacaoO2,
    double? TemperaturaCelsius,
    int? GlicemiaCapilar,
    double? PesoKg,
    int? AlturaCm,
    /// <summary>Calculado de peso e altura; nao e gravado.</summary>
    double? Imc,
    /// <summary>
    /// Faixa do IMC pelos cortes da OMS. Nula em menor de 20 anos: em crianca o
    /// IMC se le em curva por idade, e o corte de adulto diria "baixo peso" para
    /// uma crianca saudavel.
    /// </summary>
    FaixaImc? FaixaImc,
    int? EscalaDor,
    List<Sintoma> Sintomas,
    string? OutroSintoma,
    string? MedicamentosEmUso,
    StatusAlergia StatusAlergia,
    string? Alergias,
    ClassificacaoRisco ClassificacaoRisco,
    Especialidade? Encaminhamento,
    string? Observacoes,
    DateTime? ConcluidaEm);

// ---------------------------------------------------------------------------
// Consulta
// ---------------------------------------------------------------------------

public sealed record DispensacaoRequest
{
    public Guid? ItemId { get; init; }

    [MaxLength(200)]
    public string? DescricaoLivre { get; init; }

    [MaxLength(300)]
    public string? JustificativaItemLivre { get; init; }

    [Range(1, 10000)]
    public int Quantidade { get; init; } = 1;

    public ViaAdministracao? Via { get; init; }

    [MaxLength(300)]
    public string? Posologia { get; init; }
}

public sealed record OrtopediaRequest
{
    [MaxLength(200)] public string? Localizacao { get; init; }
    [MaxLength(1000)] public string? MecanismoTrauma { get; init; }
    public bool Imobilizacao { get; init; }
    public bool NecessitaRaioX { get; init; }
}

public sealed record RegistrarConsultaRequest
{
    [Required]
    public Especialidade Especialidade { get; init; }

    [MaxLength(2000)]
    public string? SintomasDescricao { get; init; }

    /// <summary>Codigo CID-10. Obrigatorio para concluir a consulta.</summary>
    [MaxLength(10)]
    public string? Cid10Codigo { get; init; }

    [MaxLength(1000)]
    public string? DiagnosticoObservacao { get; init; }

    [MaxLength(2000)]
    public string? Conduta { get; init; }

    public DesfechoConsulta? Desfecho { get; init; }
    public Especialidade? EncaminhadoPara { get; init; }

    public List<SintomaSaudeMental> SintomasSaudeMental { get; init; } = new();
    public List<PerdaVivenciada> PerdasVivenciadas { get; init; } = new();

    public OrtopediaRequest? Ortopedia { get; init; }
    public List<DispensacaoRequest> Dispensacoes { get; init; } = new();
}

public sealed record DispensacaoDto(
    Guid Id,
    string Item,
    int Quantidade,
    UnidadeDispensacao Unidade,
    ViaAdministracao? Via,
    string? Posologia,
    bool ForaDoCatalogo);

public sealed record ConsultaDto(
    Guid EtapaId,
    Especialidade Especialidade,
    AutorDto? Profissional,
    string? SintomasDescricao,
    string? Cid10Codigo,
    string? Cid10Descricao,
    string? DiagnosticoObservacao,
    string? Conduta,
    DesfechoConsulta? Desfecho,
    Especialidade? EncaminhadoPara,
    OrtopediaRequest? Ortopedia,
    List<DispensacaoDto> Dispensacoes,
    DateTime? ConcluidaEm);

// ---------------------------------------------------------------------------
// Odontologia
// ---------------------------------------------------------------------------

public sealed record MarcacaoDenteRequest
{
    [Range(11, 85)]
    public int Dente { get; init; }

    [Required]
    public EstadoDente Estado { get; init; }

    public List<FaceDentaria> Faces { get; init; } = new();
}

public sealed record RegistrarOdontologiaRequest
{
    [MaxLength(2000)]
    public string? Queixa { get; init; }

    [MaxLength(10)]
    public string? Cid10Codigo { get; init; }

    public List<ProcedimentoOdontologico> Procedimentos { get; init; } = new();

    [MaxLength(300)]
    public string? OutroProcedimento { get; init; }

    public DesfechoConsulta? Desfecho { get; init; }
    public List<MarcacaoDenteRequest> Odontograma { get; init; } = new();
    public List<DispensacaoRequest> Dispensacoes { get; init; } = new();
}

public sealed record MarcacaoDenteDto(int Dente, EstadoDente Estado, List<FaceDentaria> Faces);

public sealed record OdontologiaDto(
    Guid EtapaId,
    AutorDto? Profissional,
    string? Queixa,
    string? Cid10Codigo,
    string? Cid10Descricao,
    List<ProcedimentoOdontologico> Procedimentos,
    string? OutroProcedimento,
    DesfechoConsulta? Desfecho,
    List<MarcacaoDenteDto> Odontograma,
    string ResumoOdontograma,
    List<DispensacaoDto> Dispensacoes,
    DateTime? ConcluidaEm);

// ---------------------------------------------------------------------------
// Enfermagem
// ---------------------------------------------------------------------------

public sealed record RegistrarEnfermagemRequest
{
    public List<ProcedimentoEnfermagem> Procedimentos { get; init; } = new();

    [MaxLength(300)]
    public string? OutroProcedimento { get; init; }

    [MaxLength(2000)]
    public string? Observacoes { get; init; }

    public DesfechoConsulta? Desfecho { get; init; }
    public List<DispensacaoRequest> Dispensacoes { get; init; } = new();
}

public sealed record EnfermagemDto(
    Guid EtapaId,
    AutorDto? Profissional,
    List<ProcedimentoEnfermagem> Procedimentos,
    string? OutroProcedimento,
    string? Observacoes,
    DesfechoConsulta? Desfecho,
    List<DispensacaoDto> Dispensacoes,
    DateTime? ConcluidaEm);

// ---------------------------------------------------------------------------
// Catalogo
// ---------------------------------------------------------------------------

public sealed record ItemCatalogoDto(
    Guid Id,
    string Nome,
    string? PrincipioAtivo,
    string? Concentracao,
    FormaFarmaceutica Forma,
    UnidadeDispensacao Unidade,
    CategoriaItem Categoria,
    List<ViaAdministracao> ViasPermitidas);

public sealed record Cid10Dto(string Codigo, string Descricao, string? Capitulo);

// ---------------------------------------------------------------------------
// Finalizacao
// ---------------------------------------------------------------------------

public sealed record FinalizarAtendimentoRequest
{
    /// <summary>
    /// Obrigatoria quando o atendimento ja estava finalizado e esta sendo
    /// reaberto para correcao.
    /// </summary>
    [MaxLength(300)]
    public string? Justificativa { get; init; }
}

// ---------------------------------------------------------------------------
// Relatorios
// ---------------------------------------------------------------------------

public sealed record ProducaoPorFilaDto(
    Especialidade Especialidade,
    int Atendimentos,
    int MinutosTotais);

/// <summary>
/// Producao de um profissional no periodo.
///
/// Conta etapas concluidas, e nao pacientes: quem viu a mesma pessoa na triagem
/// e depois na enfermagem fez dois atendimentos, porque foram dois atos.
/// </summary>
public sealed record ProducaoProfissionalDto(
    Guid ProfissionalId,
    string Nome,
    FuncaoProfissional Funcao,
    ConselhoTipo Conselho,
    string? Registro,
    int Atendimentos,
    int MinutosTotais,
    /// <summary>Mediana, e nao media: uma ficha esquecida aberta deformaria a media.</summary>
    int? MinutosMedianos,
    List<ProducaoPorFilaDto> PorFila);


// ---------------------------------------------------------------------------
// Comunidades
// ---------------------------------------------------------------------------

/// <summary>Comunidade como o cadastro do paciente a oferece.</summary>
public sealed record ComunidadeDto(Guid Id, string Nome, bool Ativa);

/// <summary>
/// A comunidade como a coordenacao a ve. O total de pacientes e o que responde
/// se ainda faz sentido manter na lista.
/// </summary>
public sealed record ComunidadeAdminDto(
    Guid Id,
    string Nome,
    bool Ativa,
    DateTime CriadaEm,
    int TotalPacientes);

public sealed record SalvarComunidadeRequest
{
    [Required, MaxLength(160)]
    public string Nome { get; init; } = string.Empty;
}
