namespace AtendimentoDeCampo.Domain;

public enum Idioma
{
    Pt = 0,
    Es = 1,
    En = 2
}

/// <summary>
/// Cargo do profissional. Eixo separado de <see cref="Especialidade"/>.
/// CORRIGE: no sistema de referencia a "area" misturava especialidade
/// (Clinica Geral, Odontologia) com cargo (Medico, Psicologo, Enfermagem),
/// o que impedia qualquer leitura confiavel de producao por area.
/// </summary>
public enum FuncaoProfissional
{
    Medico = 0,
    Enfermeiro = 1,
    TecnicoEnfermagem = 2,
    Dentista = 3,
    Psicologo = 4,
    Fisioterapeuta = 5,
    Farmaceutico = 6,
    Recepcao = 7,
    Coordenacao = 8,
    Outro = 9
}

public enum ConselhoTipo
{
    Nenhum = 0,
    Crm = 1,
    Coren = 2,
    Cro = 3,
    Crp = 4,
    Crefito = 5,
    Crf = 6
}

/// <summary>Fila / especialidade de atendimento.</summary>
public enum Especialidade
{
    Triagem = 0,
    ClinicaGeral = 1,
    Pediatria = 2,
    Ortopedia = 3,
    Odontologia = 4,
    Enfermagem = 5,
    SaudeMental = 6
}

public enum Sexo
{
    NaoInformado = 0,
    Feminino = 1,
    Masculino = 2,
    Outro = 3
}

public enum TipoDocumento
{
    SemDocumento = 0,
    CedulaIdentidade = 1,
    Passaporte = 2,
    Cpf = 3,
    Rg = 4,
    CarteiraEstrangeiro = 5,
    CertidaoNascimento = 6,
    Outro = 7
}

/// <summary>Classificacao de risco pelo protocolo START.</summary>
public enum ClassificacaoRisco
{
    Vermelho = 0,
    Amarelo = 1,
    Verde = 2,
    Preto = 3
}

public enum StatusAtendimento
{
    Aberto = 0,
    EmAndamento = 1,
    Finalizado = 2,
    Evadido = 3,
    Cancelado = 4
}

public enum StatusEtapa
{
    Aguardando = 0,
    EmAndamento = 1,
    Concluida = 2,
    Cancelada = 3
}

public enum DesfechoConsulta
{
    Alta = 0,
    Encaminhado = 1,
    Retorno = 2,
    Evasao = 3
}

public enum Sintoma
{
    Dor = 0,
    Tosse = 1,
    Febre = 2,
    Diarreia = 3,
    Vomito = 4,
    ErupcaoCutanea = 5,
    FaltaDeAr = 6,
    Cefaleia = 7,
    Outro = 8
}

public enum CondicaoCronica
{
    Hipertensao = 0,
    Diabetes = 1,
    Asma = 2,
    Obesidade = 3,
    Cardiopatia = 4,
    Epilepsia = 5,
    Outro = 6
}

public enum Vulnerabilidade
{
    Idoso65Mais = 0,
    Gestante = 1,
    Lactante = 2,
    CriancaMenor5 = 3,
    AuxilioMobilidade = 4,
    Deficiencia = 5,
    Desacompanhado = 6,
    Outro = 7
}

public enum SintomaSaudeMental
{
    Tristeza = 0,
    Ansiedade = 1,
    Insonia = 2,
    Luto = 3,
    IdeacaoSuicida = 4,
    Agitacao = 5,
    Outro = 6
}

public enum PerdaVivenciada
{
    Casa = 0,
    Familiar = 1,
    AnimalEstimacao = 2,
    Trabalho = 3,
    Documentos = 4,
    Outro = 5
}

/// <summary>
/// CORRIGE: no sistema de referencia a alergia era texto livre e a interface
/// exibia alerta vermelho para QUALQUER valor preenchido, inclusive
/// "Nega alergia medicamentosa". O alerta aparecia em pacientes sem alergia,
/// dessensibilizando a equipe justamente para o caso real. Agora o estado e
/// explicito e o alerta so dispara em <see cref="PossuiAlergia"/>.
/// </summary>
public enum StatusAlergia
{
    NaoPerguntado = 0,
    SemAlergiaConhecida = 1,
    PossuiAlergia = 2
}

public enum ViaAdministracao
{
    Oral = 0,
    Intramuscular = 1,
    Intravenosa = 2,
    Subcutanea = 3,
    Topica = 4,
    Inalatoria = 5,
    Oftalmica = 6,
    Otologica = 7,
    Retal = 8,
    Nasal = 9
}

public enum FormaFarmaceutica
{
    Comprimido = 0,
    Capsula = 1,
    Xarope = 2,
    Suspensao = 3,
    SolucaoOral = 4,
    Ampola = 5,
    Frasco = 6,
    Sache = 7,
    Creme = 8,
    Pomada = 9,
    Colirio = 10,
    Inalador = 11,
    Supositorio = 12,
    Insumo = 13
}

public enum UnidadeDispensacao
{
    Comprimido = 0,
    Capsula = 1,
    Frasco = 2,
    Ampola = 3,
    Sache = 4,
    Tubo = 5,
    Dose = 6,
    Unidade = 7,
    Ml = 8
}

public enum CategoriaItem
{
    Medicamento = 0,
    Insumo = 1,
    Material = 2,
    Ortese = 3
}

public enum FaceDentaria
{
    Mesial = 0,
    Distal = 1,
    Oclusal = 2,
    Vestibular = 3,
    Lingual = 4,
    Incisal = 5,
    Cervical = 6
}

/// <summary>
/// CORRIGE: no odontograma de referencia um dente so podia carregar um estado
/// visivel. Um dente com carie E extracao indicada era pintado de uma cor so e
/// a carie sumia do desenho, sobrevivendo apenas no resumo em texto. No modelo
/// novo cada marcacao e uma linha propria, entao estados coexistem por dente.
/// </summary>
public enum EstadoDente
{
    Higido = 0,
    Carie = 1,
    Restaurado = 2,
    Ausente = 3,
    ExtracaoIndicada = 4,
    Fratura = 5,
    Selante = 6,
    Protese = 7,
    Implante = 8,
    RestoRadicular = 9
}

public enum ProcedimentoOdontologico
{
    ProfilaxiaLimpeza = 0,
    OrientacaoHigieneBucal = 1,
    Restauracao = 2,
    Exodontia = 3,
    DrenagemAbscesso = 4,
    AplicacaoFluor = 5,
    Raspagem = 6,
    Outro = 7
}

public enum ProcedimentoEnfermagem
{
    Curativo = 0,
    AdministracaoMedicamento = 1,
    AfericaoSinaisVitais = 2,
    GlicemiaCapilar = 3,
    Nebulizacao = 4,
    RetiradaPontos = 5,
    Imobilizacao = 6,
    Orientacao = 7,
    Outro = 8
}

public enum AcaoAuditoria
{
    CriouAtendimento = 0,
    IniciouEtapa = 1,
    Editou = 2,
    ConcluiuEtapa = 3,
    FinalizouAtendimento = 4,
    ReabriuAtendimento = 5,
    EditouAposFinalizacao = 6,
    Cancelou = 7
}
