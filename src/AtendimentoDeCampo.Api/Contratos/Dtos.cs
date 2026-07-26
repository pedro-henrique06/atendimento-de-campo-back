using System.ComponentModel.DataAnnotations;
using AtendimentoDeCampo.Domain;

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
/// Registro de nova conta. A conta nasce pendente e nao acessa nada ate um
/// administrador aprovar.
/// </summary>
public sealed record RegistroRequest
{
    [Required, MaxLength(40)]
    public string Usuario { get; init; } = string.Empty;

    [Required, MaxLength(160)]
    public string Nome { get; init; } = string.Empty;

    [EmailAddress, MaxLength(200)]
    public string? Email { get; init; }

    [Required]
    public FuncaoProfissional Funcao { get; init; }

    [MaxLength(40)]
    public string? Registro { get; init; }

    [Required]
    public string Senha { get; init; } = string.Empty;

    [Required]
    public string ConfirmacaoSenha { get; init; } = string.Empty;

    public Idioma Idioma { get; init; } = Idioma.Pt;
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
    DateTime CriadoEm);

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
    DateOnly? DataNascimento,
    int? Idade,
    Sexo Sexo,
    StatusAlergia StatusAlergia,
    string? Alergias,
    AlertaAlergiaDto Alerta,
    List<CondicaoCronica> CondicoesCronicas,
    List<Vulnerabilidade> Vulnerabilidades,
    bool ConsentimentoRegistro);

public sealed record EtapaResumoDto(
    Guid Id,
    Especialidade Especialidade,
    StatusEtapa Status,
    string? Profissional,
    DateTime? IniciadaEm,
    DateTime? ConcluidaEm);

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
    List<AuditoriaDto> Historico);

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
    string? Profissional,
    int? PressaoSistolica,
    int? PressaoDiastolica,
    int? FrequenciaCardiaca,
    int? FrequenciaRespiratoria,
    int? SaturacaoO2,
    double? TemperaturaCelsius,
    int? GlicemiaCapilar,
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
    string? Profissional,
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
    string? Profissional,
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
    string? Profissional,
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
