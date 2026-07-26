namespace AtendimentoDeCampo.Domain;

/// <summary>Base / ponto de atendimento em campo (acampamento, escola, abrigo).</summary>
public class Base
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nome { get; set; } = string.Empty;

    /// <summary>Prefixo de 3 caracteres usado nos codigos de atendimento desta base.</summary>
    public string PrefixoCodigo { get; set; } = string.Empty;

    public bool Ativa { get; set; } = true;
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;

    public ICollection<Atendimento> Atendimentos { get; set; } = new List<Atendimento>();
    public ICollection<EstoqueBase> Estoque { get; set; } = new List<EstoqueBase>();
}

/// <summary>Profissional / voluntario que opera o sistema.</summary>
public class Profissional
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Identificador de login, curto e unico.
    ///
    /// Substitui a identidade anterior por nome + funcao, que impedia duas
    /// pessoas homonimas na mesma funcao de terem conta — a segunda simplesmente
    /// nao conseguia se registrar — e obrigava a digitar o nome completo a cada
    /// plantao, no celular, onde um caractere diferente criava outra conta.
    /// </summary>
    public string Usuario { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    /// <summary>Opcional; usado apenas para contato e recuperacao de senha.</summary>
    public string? Email { get; set; }

    public FuncaoProfissional Funcao { get; set; }
    public ConselhoTipo ConselhoTipo { get; set; } = ConselhoTipo.Nenhum;
    public string? Registro { get; set; }
    public string SenhaHash { get; set; } = string.Empty;
    public Idioma Idioma { get; set; } = Idioma.Pt;

    public StatusConta Status { get; set; } = StatusConta.Pendente;

    /// <summary>
    /// Pode aprovar contas. E um eixo proprio, e nao a funcao Coordenacao:
    /// coordenar a operacao em campo e administrar acessos do sistema sao
    /// responsabilidades diferentes, e nem sempre da mesma pessoa.
    /// </summary>
    public bool EhAdministrador { get; set; }

    public Guid? RevisadoPorId { get; set; }
    public Profissional? RevisadoPor { get; set; }
    public DateTime? RevisadoEm { get; set; }

    /// <summary>Preenchido quando a conta e recusada, para que a pessoa saiba o motivo.</summary>
    public string? MotivoRecusa { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<Etapa> Etapas { get; set; } = new List<Etapa>();
}

public class Paciente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Codigo legivel entregue ao paciente, ex.: "4K7Z-2YAP". E o unico jeito de
    /// reencontrar quem nao tem documento — a maioria em campo — nas visitas
    /// seguintes, inclusive em outra base.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;
    public TipoDocumento TipoDocumento { get; set; } = TipoDocumento.SemDocumento;
    public string? NumeroDocumento { get; set; }
    public DateOnly? DataNascimento { get; set; }

    /// <summary>Usada apenas quando a data de nascimento e desconhecida, comum em campo.</summary>
    public int? IdadeAproximada { get; set; }

    public Sexo Sexo { get; set; } = Sexo.NaoInformado;

    /// <summary>Alergia estruturada. Ver <see cref="StatusAlergia"/>.</summary>
    public StatusAlergia StatusAlergia { get; set; } = StatusAlergia.NaoPerguntado;

    /// <summary>Preenchido somente quando <see cref="StatusAlergia"/> = PossuiAlergia.</summary>
    public string? Alergias { get; set; }

    public List<CondicaoCronica> CondicoesCronicas { get; set; } = new();
    public string? OutraCondicaoCronica { get; set; }
    public List<Vulnerabilidade> Vulnerabilidades { get; set; } = new();

    public bool ConsentimentoRegistro { get; set; }
    public DateTime? ConsentimentoEm { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<Atendimento> Atendimentos { get; set; } = new List<Atendimento>();
}

public class Atendimento
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Codigo curto legivel, ex.: "PAN-4K7Z". Prefixo identifica a base.</summary>
    public string Codigo { get; set; } = string.Empty;

    public Guid BaseId { get; set; }
    public Base? Base { get; set; }

    public Guid PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    public StatusAtendimento Status { get; set; } = StatusAtendimento.Aberto;

    /// <summary>Definida na triagem; espelhada aqui para filtro no painel.</summary>
    public ClassificacaoRisco? ClassificacaoRisco { get; set; }

    public string? QueixaPrincipal { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? PrecisaoMetros { get; set; }

    public Guid CriadoPorId { get; set; }
    public Profissional? CriadoPor { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Guid? FinalizadoPorId { get; set; }
    public Profissional? FinalizadoPor { get; set; }
    public DateTime? FinalizadoEm { get; set; }

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<Etapa> Etapas { get; set; } = new List<Etapa>();
    public ICollection<PassagemFila> PassagensFila { get; set; } = new List<PassagemFila>();
    public ICollection<Auditoria> Auditorias { get; set; } = new List<Auditoria>();
}

/// <summary>
/// Uma etapa do atendimento (triagem, consulta, odontologia...). Cada etapa tem
/// autoria e carimbo de tempo proprios, como no sistema de referencia.
/// </summary>
public class Etapa
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    public Especialidade Especialidade { get; set; }
    public StatusEtapa Status { get; set; } = StatusEtapa.Aguardando;

    public Guid? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    public DateTime? IniciadaEm { get; set; }
    public DateTime? ConcluidaEm { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;

    public Triagem? Triagem { get; set; }
    public Consulta? Consulta { get; set; }
    public Odontologia? Odontologia { get; set; }
    public Enfermagem? Enfermagem { get; set; }
    public ICollection<Dispensacao> Dispensacoes { get; set; } = new List<Dispensacao>();
}

public class Triagem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EtapaId { get; set; }
    public Etapa? Etapa { get; set; }

    public int? PressaoSistolica { get; set; }
    public int? PressaoDiastolica { get; set; }
    public int? FrequenciaCardiaca { get; set; }
    public int? FrequenciaRespiratoria { get; set; }
    public int? SaturacaoO2 { get; set; }
    public double? TemperaturaCelsius { get; set; }
    public int? GlicemiaCapilar { get; set; }

    public List<Sintoma> Sintomas { get; set; } = new();
    public string? OutroSintoma { get; set; }

    /// <summary>
    /// Campo dedicado. CORRIGE: no sistema de referencia a resposta de sintoma
    /// caia no campo de medicamentos por falta de separacao clara entre eles.
    /// </summary>
    public string? MedicamentosEmUso { get; set; }

    public StatusAlergia StatusAlergia { get; set; } = StatusAlergia.NaoPerguntado;
    public string? Alergias { get; set; }

    public ClassificacaoRisco ClassificacaoRisco { get; set; }
    public Especialidade? Encaminhamento { get; set; }
    public string? Observacoes { get; set; }
}

public class Consulta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EtapaId { get; set; }
    public Etapa? Etapa { get; set; }

    public string? SintomasDescricao { get; set; }

    /// <summary>
    /// CID-10 estruturado. CORRIGE: no sistema de referencia o diagnostico era
    /// texto livre, entao "J00", "anemia" e "1. Mialgia 2. Artralgia" conviviam
    /// na mesma estatistica de "principais diagnosticos".
    /// </summary>
    public string? Cid10Codigo { get; set; }
    public Cid10? Cid10 { get; set; }

    public string? DiagnosticoObservacao { get; set; }
    public string? Conduta { get; set; }
    public DesfechoConsulta? Desfecho { get; set; }
    public Especialidade? EncaminhadoPara { get; set; }

    public List<SintomaSaudeMental> SintomasSaudeMental { get; set; } = new();
    public List<PerdaVivenciada> PerdasVivenciadas { get; set; } = new();

    public ConsultaOrtopedia? Ortopedia { get; set; }
}

/// <summary>Bloco extra preenchido quando a consulta e de ortopedia.</summary>
public class ConsultaOrtopedia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConsultaId { get; set; }
    public Consulta? Consulta { get; set; }

    public string? Localizacao { get; set; }
    public string? MecanismoTrauma { get; set; }
    public bool Imobilizacao { get; set; }
    public bool NecessitaRaioX { get; set; }
}

public class Odontologia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EtapaId { get; set; }
    public Etapa? Etapa { get; set; }

    public string? Queixa { get; set; }
    public string? Cid10Codigo { get; set; }
    public Cid10? Cid10 { get; set; }

    public List<ProcedimentoOdontologico> Procedimentos { get; set; } = new();
    public string? OutroProcedimento { get; set; }
    public DesfechoConsulta? Desfecho { get; set; }

    public ICollection<MarcacaoDente> Marcacoes { get; set; } = new List<MarcacaoDente>();
}

/// <summary>
/// Uma marcacao = um estado, em um dente, opcionalmente em faces especificas.
/// Varias marcacoes podem coexistir no mesmo dente.
/// </summary>
public class MarcacaoDente
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OdontologiaId { get; set; }
    public Odontologia? Odontologia { get; set; }

    /// <summary>Numeracao FDI: 11-18, 21-28, 31-38, 41-48 e deciduos 51-85.</summary>
    public int Dente { get; set; }

    public EstadoDente Estado { get; set; }
    public List<FaceDentaria> Faces { get; set; } = new();
}

public class Enfermagem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EtapaId { get; set; }
    public Etapa? Etapa { get; set; }

    public List<ProcedimentoEnfermagem> Procedimentos { get; set; } = new();
    public string? OutroProcedimento { get; set; }
    public string? Observacoes { get; set; }
    public DesfechoConsulta? Desfecho { get; set; }
}

/// <summary>
/// CORRIGE: no sistema de referencia o item dispensado era texto livre. O mesmo
/// farmaco aparecia como "Acetaminofen", "Acetaminofeno", "Acetominofen" e
/// "1. Paracetamol 1 gramo" em linhas separadas, e ate uma nota clinica inteira
/// ("Confirmo tamanho de nodulo...") foi registrada como item dispensado.
/// </summary>
public class ItemCatalogo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nome { get; set; } = string.Empty;
    public string? PrincipioAtivo { get; set; }
    public string? Concentracao { get; set; }
    public FormaFarmaceutica Forma { get; set; }
    public UnidadeDispensacao Unidade { get; set; }
    public CategoriaItem Categoria { get; set; } = CategoriaItem.Medicamento;

    /// <summary>
    /// Vias compativeis com a apresentacao. CORRIGE: no sistema de referencia
    /// via e forma eram escolhidas soltas, gerando registros como
    /// "Prednisolona 20 mg - Xarope - comprimido".
    /// </summary>
    public List<ViaAdministracao> ViasPermitidas { get; set; } = new();

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<Dispensacao> Dispensacoes { get; set; } = new List<Dispensacao>();
    public ICollection<EstoqueBase> Estoques { get; set; } = new List<EstoqueBase>();
}

public class EstoqueBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BaseId { get; set; }
    public Base? Base { get; set; }
    public Guid ItemId { get; set; }
    public ItemCatalogo? Item { get; set; }
    public int Quantidade { get; set; }
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}

public class Dispensacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EtapaId { get; set; }
    public Etapa? Etapa { get; set; }

    public Guid? ItemId { get; set; }
    public ItemCatalogo? Item { get; set; }

    /// <summary>
    /// Usado apenas quando o item nao existe no catalogo. Exige justificativa, e
    /// esses registros ficam marcados para revisao posterior da coordenacao.
    /// </summary>
    public string? DescricaoLivre { get; set; }
    public string? JustificativaItemLivre { get; set; }

    public int Quantidade { get; set; }
    public UnidadeDispensacao Unidade { get; set; }
    public ViaAdministracao? Via { get; set; }
    public string? Posologia { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
}

/// <summary>Catalogo CID-10 com descricao nos tres idiomas da interface.</summary>
public class Cid10
{
    public string Codigo { get; set; } = string.Empty;
    public string DescricaoPt { get; set; } = string.Empty;
    public string DescricaoEs { get; set; } = string.Empty;
    public string DescricaoEn { get; set; } = string.Empty;
    public string? Capitulo { get; set; }
}

/// <summary>
/// Entrada e saida do paciente em cada fila, base do relatorio de tempo de espera.
/// </summary>
public class PassagemFila
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }
    public Especialidade Especialidade { get; set; }
    public DateTime EntrouEm { get; set; } = DateTime.UtcNow;
    public DateTime? SaiuEm { get; set; }
}

/// <summary>
/// Trilha de auditoria com diff campo a campo, espelhando o "Historico de
/// alteracoes" do sistema de referencia.
/// </summary>
public class Auditoria
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }
    public Guid ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    public AcaoAuditoria Acao { get; set; }
    public Especialidade? Especialidade { get; set; }
    public string? Campo { get; set; }
    public string? ValorAnterior { get; set; }
    public string? ValorNovo { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
}
