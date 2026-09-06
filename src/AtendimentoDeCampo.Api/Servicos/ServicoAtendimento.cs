using System.Globalization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;
using AtendimentoDeCampo.Infrastructure;
using AtendimentoDeCampo.Infrastructure.Servicos;
using Microsoft.EntityFrameworkCore;

namespace AtendimentoDeCampo.Api.Servicos;

/// <summary>Erro de regra de negocio, traduzido para 400 pelo controller.</summary>
public sealed class RegraDeNegocioException : Exception
{
    public IReadOnlyList<string> Erros { get; }

    public RegraDeNegocioException(string erro) : this(new[] { erro })
    {
    }

    public RegraDeNegocioException(IReadOnlyList<string> erros)
        : base(string.Join(" ", erros))
        => Erros = erros;
}

public sealed class ServicoAtendimento
{
    private readonly AtendimentoDbContext _db;
    private readonly RegistradorAuditoria _auditoria;

    public ServicoAtendimento(AtendimentoDbContext db, RegistradorAuditoria auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // -----------------------------------------------------------------------
    // Criacao
    // -----------------------------------------------------------------------

    public async Task<ProntuarioDto> CriarAsync(
        CriarAtendimentoRequest req,
        Guid profissionalId,
        CancellationToken ct = default)
    {
        var basePonto = await _db.Bases.FirstOrDefaultAsync(b => b.Id == req.BaseId && b.Ativa, ct)
            ?? throw new RegraDeNegocioException("Base nao encontrada ou inativa.");

        var erros = AlertaAlergia.Validar(req.Paciente.StatusAlergia, req.Paciente.Alergias).ToList();

        if (!req.Paciente.ConsentimentoRegistro)
        {
            erros.Add("E necessario registrar o consentimento do paciente ou responsavel.");
        }

        if (req.Paciente.DataNascimento is null && req.Paciente.IdadeAproximada is null)
        {
            erros.Add("Informe a data de nascimento ou a idade aproximada.");
        }

        /*
            Menor de idade exige nome da mae e endereco. Em campo a crianca
            costuma chegar acompanhada de quem nao e o responsavel legal, e sao
            esses dois campos que permitem reencontrar a familia depois.

            A regra usa a idade calculada, e nao um campo a parte, para valer
            igual para quem informou a data de nascimento e para quem so soube
            dizer a idade aproximada.
        */
        var idade = CalculadoraIdade.Calcular(
            req.Paciente.DataNascimento, req.Paciente.IdadeAproximada);

        erros.AddRange(RegrasDoMenor.Validar(idade, req.Paciente.NomeDaMae, req.Paciente.Endereco));

        if (req.Paciente.ComunidadeId is Guid comunidadeId &&
            !await _db.Comunidades.AnyAsync(c => c.Id == comunidadeId && c.Ativa, ct))
        {
            erros.Add("Comunidade nao encontrada ou inativa.");
        }

        if (erros.Count > 0)
        {
            throw new RegraDeNegocioException(erros);
        }

        var paciente = await ResolverPacienteAsync(req.Paciente, ct);
        var codigo = await GerarCodigoUnicoAsync(basePonto.PrefixoCodigo, ct);

        var atendimento = new Atendimento
        {
            Codigo = codigo,
            BaseId = basePonto.Id,
            PacienteId = paciente.Id,
            Status = StatusAtendimento.Aberto,
            QueixaPrincipal = req.QueixaPrincipal,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            PrecisaoMetros = req.PrecisaoMetros,
            CriadoPorId = profissionalId
        };

        _db.Atendimentos.Add(atendimento);

        // Todo atendimento nasce na fila de triagem.
        _db.Etapas.Add(new Etapa
        {
            AtendimentoId = atendimento.Id,
            Especialidade = Especialidade.Triagem,
            Status = StatusEtapa.Aguardando
        });

        _db.PassagensFila.Add(new PassagemFila
        {
            AtendimentoId = atendimento.Id,
            Especialidade = Especialidade.Triagem
        });

        await _auditoria.RegistrarAsync(
            atendimento.Id, profissionalId, AcaoAuditoria.CriouAtendimento, ct: ct);

        await _db.SaveChangesAsync(ct);

        return await ObterProntuarioAsync(atendimento.Id, ct);
    }

    private async Task<Paciente> ResolverPacienteAsync(DadosPacienteRequest dados, CancellationToken ct)
    {
        var codigo = GeradorCodigoPaciente.Normalizar(dados.Codigo)
            ?? throw new RegraDeNegocioException("Codigo do paciente invalido.");

        // O codigo vem primeiro: e o unico identificador que existe para quem
        // nao tem documento, e e o que a pessoa carrega anotado.
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Codigo == codigo, ct);

        // Documento tambem reencontra o cadastro: quem perdeu o papel com o
        // codigo e voltou com a cedula na mao nao pode virar um cadastro novo.
        if (paciente is null && !string.IsNullOrWhiteSpace(dados.NumeroDocumento))
        {
            paciente = await _db.Pacientes.FirstOrDefaultAsync(
                p => p.TipoDocumento == dados.TipoDocumento &&
                     p.NumeroDocumento == dados.NumeroDocumento,
                ct);
        }

        if (paciente is null)
        {
            // Chega aqui com o codigo que a tela sorteou e ninguem usou ainda.
            // Quem garante a unicidade de verdade e o indice unico: a consulta
            // acima nao protege contra outro cadastro entrando no mesmo
            // instante.
            paciente = new Paciente { Codigo = codigo };
            _db.Pacientes.Add(paciente);
        }
        else if (paciente.Codigo != codigo)
        {
            // Encontrado pelo documento, com outro codigo ja no cadastro. O
            // codigo antigo prevalece: e o que esta anotado no papel de quem
            // voltou, e trocar invalidaria o papel.
            codigo = paciente.Codigo;
        }

        paciente.Nome = dados.Nome.Trim();
        paciente.TipoDocumento = dados.TipoDocumento;
        paciente.NumeroDocumento = string.IsNullOrWhiteSpace(dados.NumeroDocumento)
            ? null
            : dados.NumeroDocumento.Trim();
        paciente.CartaoSus = Limpar(dados.CartaoSus);
        paciente.ComunidadeId = dados.ComunidadeId;
        paciente.NomeDaMae = Limpar(dados.NomeDaMae);
        paciente.Endereco = Limpar(dados.Endereco);
        paciente.DataNascimento = dados.DataNascimento;
        paciente.IdadeAproximada = dados.IdadeAproximada;
        paciente.Sexo = dados.Sexo;
        paciente.StatusAlergia = dados.StatusAlergia;
        paciente.Alergias = dados.StatusAlergia == StatusAlergia.PossuiAlergia ? dados.Alergias : null;
        paciente.CondicoesCronicas = dados.CondicoesCronicas;
        paciente.Vulnerabilidades = dados.Vulnerabilidades;
        paciente.ConsentimentoRegistro = dados.ConsentimentoRegistro;
        paciente.ConsentimentoEm = dados.ConsentimentoRegistro ? DateTime.UtcNow : null;
        paciente.AtualizadoEm = DateTime.UtcNow;

        return paciente;
    }

    /// <summary>Texto vazio vira nulo: "" e "   " nao sao um dado preenchido.</summary>
    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>
    /// Sorteia um codigo de paciente ainda livre. Nao grava nada: o cadastro so
    /// nasce quando a tela e salva, ja com o consentimento marcado. Codigo
    /// sorteado e abandonado nao deixa rastro nenhum.
    /// </summary>
    public async Task<string> GerarCodigoPacienteUnicoAsync(CancellationToken ct = default)
    {
        for (var tentativa = 0; tentativa < 10; tentativa++)
        {
            var codigo = GeradorCodigoPaciente.Gerar();

            if (!await _db.Pacientes.AnyAsync(p => p.Codigo == codigo, ct))
            {
                return codigo;
            }
        }

        throw new RegraDeNegocioException(
            "Nao foi possivel gerar um codigo unico para o paciente. Tente novamente.");
    }

    /// <summary>
    /// Procura um paciente ja cadastrado. Aceita o codigo do paciente e tambem o
    /// codigo de um atendimento dele: os dois circulam na mesma fila, e quem
    /// digita o de atendimento por engano quer chegar na mesma pessoa —
    /// responder "nao encontrado" ali seria implicancia.
    /// </summary>
    public async Task<PacienteConhecidoDto?> ObterPacientePorCodigoAsync(
        string codigo,
        CancellationToken ct = default)
    {
        var normalizado = GeradorCodigoPaciente.Normalizar(codigo);

        var paciente = normalizado is null
            ? null
            : await _db.Pacientes
                .AsNoTracking()
                .Include(p => p.Comunidade)
                .FirstOrDefaultAsync(p => p.Codigo == normalizado, ct);

        if (paciente is null)
        {
            var doAtendimento = (codigo ?? string.Empty).Trim().ToUpperInvariant();

            paciente = await _db.Atendimentos
                .AsNoTracking()
                .Where(a => a.Codigo == doAtendimento)
                .Include(a => a.Paciente).ThenInclude(p => p!.Comunidade)
                .Select(a => a.Paciente!)
                .FirstOrDefaultAsync(ct);
        }

        if (paciente is null)
        {
            return null;
        }

        var visitas = await _db.Atendimentos
            .AsNoTracking()
            .Where(a => a.PacienteId == paciente.Id)
            .OrderByDescending(a => a.CriadoEm)
            .Select(a => new { a.CriadoEm, Base = a.Base!.Nome })
            .ToListAsync(ct);

        return new PacienteConhecidoDto(
            Mapeadores.ParaDto(paciente),
            visitas.Count,
            visitas.FirstOrDefault()?.CriadoEm,
            visitas.FirstOrDefault()?.Base);
    }

    private async Task<string> GerarCodigoUnicoAsync(string prefixo, CancellationToken ct)
    {
        for (var tentativa = 0; tentativa < 10; tentativa++)
        {
            var codigo = GeradorCodigoAtendimento.Gerar(prefixo);

            if (!await _db.Atendimentos.AnyAsync(a => a.Codigo == codigo, ct))
            {
                return codigo;
            }
        }

        throw new RegraDeNegocioException(
            "Nao foi possivel gerar um codigo unico para o atendimento. Tente novamente.");
    }

    // -----------------------------------------------------------------------
    // Triagem
    // -----------------------------------------------------------------------

    public async Task<SugestaoStartDto?> RegistrarTriagemAsync(
        Guid atendimentoId,
        RegistrarTriagemRequest req,
        Guid profissionalId,
        CancellationToken ct = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, ct);

        var erros = AlertaAlergia.Validar(req.StatusAlergia, req.Alergias).ToList();
        if (erros.Count > 0)
        {
            throw new RegraDeNegocioException(erros);
        }

        var etapa = await ObterOuCriarEtapaAsync(atendimento, Especialidade.Triagem, profissionalId, ct);
        var triagem = await _db.Triagens.FirstOrDefaultAsync(t => t.EtapaId == etapa.Id, ct);
        var novo = triagem is null;

        if (triagem is null)
        {
            triagem = new Triagem { EtapaId = etapa.Id };
            _db.Triagens.Add(triagem);
        }

        var antes = novo ? new Dictionary<string, string?>() : SnapshotTriagem(triagem);

        triagem.PressaoSistolica = req.PressaoSistolica;
        triagem.PressaoDiastolica = req.PressaoDiastolica;
        triagem.FrequenciaCardiaca = req.FrequenciaCardiaca;
        triagem.FrequenciaRespiratoria = req.FrequenciaRespiratoria;
        triagem.SaturacaoO2 = req.SaturacaoO2;
        triagem.TemperaturaCelsius = req.TemperaturaCelsius;
        triagem.GlicemiaCapilar = req.GlicemiaCapilar;
        triagem.PesoKg = req.PesoKg;
        triagem.AlturaCm = req.AlturaCm;
        triagem.EscalaDor = req.EscalaDor;
        triagem.Sintomas = req.Sintomas;
        triagem.OutroSintoma = req.OutroSintoma;
        triagem.MedicamentosEmUso = req.MedicamentosEmUso;
        triagem.StatusAlergia = req.StatusAlergia;
        triagem.Alergias = req.StatusAlergia == StatusAlergia.PossuiAlergia ? req.Alergias : null;
        triagem.ClassificacaoRisco = req.ClassificacaoRisco;
        triagem.Encaminhamento = req.Encaminhamento;
        triagem.Observacoes = req.Observacoes;

        atendimento.ClassificacaoRisco = req.ClassificacaoRisco;
        atendimento.Status = StatusAtendimento.EmAndamento;
        atendimento.AtualizadoEm = DateTime.UtcNow;

        // A alergia levantada na triagem atualiza o cadastro do paciente, que e
        // a fonte do alerta exibido no topo do prontuario.
        if (atendimento.Paciente is not null && req.StatusAlergia != StatusAlergia.NaoPerguntado)
        {
            atendimento.Paciente.StatusAlergia = req.StatusAlergia;
            atendimento.Paciente.Alergias = triagem.Alergias;
        }

        _auditoria.RegistrarDiffs(
            atendimentoId, profissionalId,
            RegistradorAuditoria.Comparar(antes, SnapshotTriagem(triagem)),
            Especialidade.Triagem,
            aposFinalizacao: atendimento.FinalizadoEm is not null);

        ConcluirEtapa(etapa, profissionalId);
        FecharPassagem(atendimento, Especialidade.Triagem, profissionalId);

        await _auditoria.RegistrarAsync(
            atendimentoId, profissionalId, AcaoAuditoria.ConcluiuEtapa, Especialidade.Triagem, ct);

        // O encaminhamento abre a proxima fila.
        if (req.Encaminhamento is Especialidade destino && destino != Especialidade.Triagem)
        {
            await AbrirFilaAsync(
                atendimento, destino, ct, profissionalId, Especialidade.Triagem);
        }

        await _db.SaveChangesAsync(ct);

        if (req.AchadosStart is null)
        {
            return null;
        }

        var achados = new AchadosStart
        {
            Deambula = req.AchadosStart.Deambula,
            RespiraEspontaneamente = req.AchadosStart.RespiraEspontaneamente,
            RespiraAposAberturaViaAerea = req.AchadosStart.RespiraAposAberturaViaAerea,
            FrequenciaRespiratoria = req.AchadosStart.FrequenciaRespiratoria ?? req.FrequenciaRespiratoria,
            PulsoRadialPresente = req.AchadosStart.PulsoRadialPresente,
            TempoEnchimentoCapilarSegundos = req.AchadosStart.TempoEnchimentoCapilarSegundos,
            ObedeceComandos = req.AchadosStart.ObedeceComandos
        };

        var sugestao = ProtocoloStart.Avaliar(achados);
        var divergente = sugestao.Classificacao != req.ClassificacaoRisco;

        // Divergencia nao bloqueia: quem esta com o paciente decide. Fica
        // registrada para leitura posterior da coordenacao.
        if (divergente)
        {
            _auditoria.RegistrarDiffs(
                atendimentoId, profissionalId,
                new[]
                {
                    // Canonico: a interface monta a frase "o protocolo sugeriu X,
                    // o profissional classificou como Y" no idioma de quem le.
                    new DiffCampo(
                        "triagem.divergenciaStart",
                        sugestao.Classificacao.ToString(),
                        req.ClassificacaoRisco.ToString())
                },
                Especialidade.Triagem,
                aposFinalizacao: false);

            await _db.SaveChangesAsync(ct);
        }

        return new SugestaoStartDto(sugestao.Classificacao, sugestao.Motivo, divergente);
    }

    // -----------------------------------------------------------------------
    // Consulta
    // -----------------------------------------------------------------------

    public async Task RegistrarConsultaAsync(
        Guid atendimentoId,
        RegistrarConsultaRequest req,
        Guid profissionalId,
        CancellationToken ct = default)
    {
        if (req.Especialidade is Especialidade.Triagem or Especialidade.Odontologia or Especialidade.Enfermagem)
        {
            throw new RegraDeNegocioException(
                $"{req.Especialidade} tem endpoint proprio e nao e registrada como consulta.");
        }

        var atendimento = await CarregarAsync(atendimentoId, ct);
        var etapa = await ObterOuCriarEtapaAsync(atendimento, req.Especialidade, profissionalId, ct);

        var consulta = await _db.Consultas
            .Include(c => c.Ortopedia)
            .FirstOrDefaultAsync(c => c.EtapaId == etapa.Id, ct);

        var novo = consulta is null;

        if (consulta is null)
        {
            consulta = new Consulta { EtapaId = etapa.Id };
            _db.Consultas.Add(consulta);
        }

        await ValidarCidAsync(req.Cid10Codigo, req.Desfecho, ct);

        var antes = novo ? new Dictionary<string, string?>() : SnapshotConsulta(consulta);

        consulta.SintomasDescricao = req.SintomasDescricao;
        consulta.Cid10Codigo = req.Cid10Codigo;
        consulta.DiagnosticoObservacao = req.DiagnosticoObservacao;
        consulta.Conduta = req.Conduta;
        consulta.Desfecho = req.Desfecho;
        consulta.EncaminhadoPara = req.EncaminhadoPara;
        consulta.SintomasSaudeMental = req.SintomasSaudeMental;
        consulta.PerdasVivenciadas = req.PerdasVivenciadas;

        if (req.Especialidade == Especialidade.Ortopedia && req.Ortopedia is not null)
        {
            consulta.Ortopedia ??= new ConsultaOrtopedia { ConsultaId = consulta.Id };
            consulta.Ortopedia.Localizacao = req.Ortopedia.Localizacao;
            consulta.Ortopedia.MecanismoTrauma = req.Ortopedia.MecanismoTrauma;
            consulta.Ortopedia.Imobilizacao = req.Ortopedia.Imobilizacao;
            consulta.Ortopedia.NecessitaRaioX = req.Ortopedia.NecessitaRaioX;

            if (novo)
            {
                _db.ConsultasOrtopedia.Add(consulta.Ortopedia);
            }
        }

        await SubstituirDispensacoesAsync(etapa.Id, req.Dispensacoes, ct);

        _auditoria.RegistrarDiffs(
            atendimentoId, profissionalId,
            RegistradorAuditoria.Comparar(antes, SnapshotConsulta(consulta)),
            req.Especialidade,
            aposFinalizacao: atendimento.FinalizadoEm is not null);

        ConcluirEtapa(etapa, profissionalId);
        FecharPassagem(atendimento, req.Especialidade, profissionalId);

        await _auditoria.RegistrarAsync(
            atendimentoId, profissionalId, AcaoAuditoria.ConcluiuEtapa, req.Especialidade, ct);

        if (req.Desfecho == DesfechoConsulta.Encaminhado && req.EncaminhadoPara is Especialidade destino)
        {
            await AbrirFilaAsync(
                atendimento, destino, ct, profissionalId, req.Especialidade);
        }

        atendimento.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // -----------------------------------------------------------------------
    // Odontologia
    // -----------------------------------------------------------------------

    public async Task RegistrarOdontologiaAsync(
        Guid atendimentoId,
        RegistrarOdontologiaRequest req,
        Guid profissionalId,
        CancellationToken ct = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, ct);
        var etapa = await ObterOuCriarEtapaAsync(atendimento, Especialidade.Odontologia, profissionalId, ct);

        var odonto = await _db.Odontologias
            .Include(o => o.Marcacoes)
            .FirstOrDefaultAsync(o => o.EtapaId == etapa.Id, ct);

        var novo = odonto is null;

        if (odonto is null)
        {
            odonto = new Odontologia { EtapaId = etapa.Id };
            _db.Odontologias.Add(odonto);
        }

        await ValidarCidAsync(req.Cid10Codigo, req.Desfecho, ct);
        ValidarOdontograma(req.Odontograma);

        var antes = novo
            ? new Dictionary<string, string?>()
            : SnapshotOdontologia(odonto);

        odonto.Queixa = req.Queixa;
        odonto.Cid10Codigo = req.Cid10Codigo;
        odonto.Procedimentos = req.Procedimentos;
        odonto.OutroProcedimento = req.OutroProcedimento;
        odonto.Desfecho = req.Desfecho;

        _db.MarcacoesDente.RemoveRange(odonto.Marcacoes);
        odonto.Marcacoes = req.Odontograma
            .Select(m => new MarcacaoDente
            {
                OdontologiaId = odonto.Id,
                Dente = m.Dente,
                Estado = m.Estado,
                Faces = m.Faces.Distinct().ToList()
            })
            .ToList();

        await SubstituirDispensacoesAsync(etapa.Id, req.Dispensacoes, ct);

        _auditoria.RegistrarDiffs(
            atendimentoId, profissionalId,
            RegistradorAuditoria.Comparar(antes, SnapshotOdontologia(odonto)),
            Especialidade.Odontologia,
            aposFinalizacao: atendimento.FinalizadoEm is not null);

        ConcluirEtapa(etapa, profissionalId);
        FecharPassagem(atendimento, Especialidade.Odontologia, profissionalId);

        await _auditoria.RegistrarAsync(
            atendimentoId, profissionalId, AcaoAuditoria.ConcluiuEtapa, Especialidade.Odontologia, ct);

        atendimento.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static void ValidarOdontograma(List<MarcacaoDenteRequest> marcacoes)
    {
        var erros = new List<string>();

        foreach (var m in marcacoes)
        {
            erros.AddRange(Odontograma.ValidarMarcacao(m.Dente, m.Estado, m.Faces));
        }

        foreach (var grupo in marcacoes.GroupBy(m => m.Dente))
        {
            erros.AddRange(Odontograma.ValidarConjunto(grupo.Key, grupo.Select(m => m.Estado)));

            var duplicados = grupo
                .GroupBy(m => m.Estado)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var estado in duplicados)
            {
                erros.Add($"Dente {grupo.Key} tem o estado {estado} repetido.");
            }
        }

        if (erros.Count > 0)
        {
            throw new RegraDeNegocioException(erros.Distinct().ToList());
        }
    }

    // -----------------------------------------------------------------------
    // Enfermagem
    // -----------------------------------------------------------------------

    public async Task RegistrarEnfermagemAsync(
        Guid atendimentoId,
        RegistrarEnfermagemRequest req,
        Guid profissionalId,
        CancellationToken ct = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, ct);
        var etapa = await ObterOuCriarEtapaAsync(atendimento, Especialidade.Enfermagem, profissionalId, ct);

        var enf = await _db.Enfermagens.FirstOrDefaultAsync(e => e.EtapaId == etapa.Id, ct);
        var novo = enf is null;

        if (enf is null)
        {
            enf = new Enfermagem { EtapaId = etapa.Id };
            _db.Enfermagens.Add(enf);
        }

        var antes = novo ? new Dictionary<string, string?>() : SnapshotEnfermagem(enf);

        enf.Procedimentos = req.Procedimentos;
        enf.OutroProcedimento = req.OutroProcedimento;
        enf.Observacoes = req.Observacoes;
        enf.Desfecho = req.Desfecho;

        await SubstituirDispensacoesAsync(etapa.Id, req.Dispensacoes, ct);

        _auditoria.RegistrarDiffs(
            atendimentoId, profissionalId,
            RegistradorAuditoria.Comparar(antes, SnapshotEnfermagem(enf)),
            Especialidade.Enfermagem,
            aposFinalizacao: atendimento.FinalizadoEm is not null);

        ConcluirEtapa(etapa, profissionalId);
        FecharPassagem(atendimento, Especialidade.Enfermagem, profissionalId);

        await _auditoria.RegistrarAsync(
            atendimentoId, profissionalId, AcaoAuditoria.ConcluiuEtapa, Especialidade.Enfermagem, ct);

        atendimento.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // -----------------------------------------------------------------------
    // Finalizacao
    // -----------------------------------------------------------------------

    public async Task FinalizarAsync(
        Guid atendimentoId,
        FinalizarAtendimentoRequest req,
        Guid profissionalId,
        CancellationToken ct = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, ct);

        if (atendimento.Status == StatusAtendimento.Finalizado)
        {
            throw new RegraDeNegocioException("Atendimento ja esta finalizado.");
        }

        var pendentes = atendimento.Etapas
            .Where(e => e.Status is StatusEtapa.Aguardando or StatusEtapa.EmAndamento)
            .Select(e => e.Especialidade.ToString())
            .ToList();

        if (pendentes.Count > 0)
        {
            throw new RegraDeNegocioException(
                $"Ha etapas pendentes: {string.Join(", ", pendentes)}. " +
                "Conclua ou cancele antes de finalizar.");
        }

        atendimento.Status = StatusAtendimento.Finalizado;
        atendimento.FinalizadoPorId = profissionalId;
        atendimento.FinalizadoEm = DateTime.UtcNow;
        atendimento.AtualizadoEm = DateTime.UtcNow;

        foreach (var passagem in atendimento.PassagensFila.Where(p => p.SaiuEm is null))
        {
            passagem.SaiuEm = DateTime.UtcNow;
        }

        await _auditoria.RegistrarAsync(
            atendimentoId, profissionalId, AcaoAuditoria.FinalizouAtendimento, ct: ct);

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Reabre um atendimento finalizado para correcao.
    ///
    /// CORRIGE: no sistema de referencia um atendimento finalizado as 15:49
    /// aparecia editado as 15:56 e as 16:11 sem trava, sem justificativa e sem
    /// nenhuma marca de que a edicao veio depois do fecho. Corrigir registro em
    /// campo e legitimo e continua permitido, mas agora exige um ato explicito,
    /// com motivo, e as edicoes seguintes ficam marcadas na auditoria.
    /// </summary>
    public async Task ReabrirAsync(
        Guid atendimentoId,
        FinalizarAtendimentoRequest req,
        Guid profissionalId,
        CancellationToken ct = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, ct);

        if (atendimento.Status != StatusAtendimento.Finalizado)
        {
            throw new RegraDeNegocioException("Atendimento nao esta finalizado.");
        }

        if (string.IsNullOrWhiteSpace(req.Justificativa))
        {
            throw new RegraDeNegocioException("Justificativa e obrigatoria para reabrir um atendimento.");
        }

        atendimento.Status = StatusAtendimento.EmAndamento;
        atendimento.AtualizadoEm = DateTime.UtcNow;

        _db.Auditorias.Add(new Auditoria
        {
            AtendimentoId = atendimentoId,
            ProfissionalId = profissionalId,
            Acao = AcaoAuditoria.ReabriuAtendimento,
            Campo = "atendimento.justificativaReabertura",
            ValorNovo = req.Justificativa.Trim()
        });

        await _db.SaveChangesAsync(ct);
    }

    // -----------------------------------------------------------------------
    // Consultas de leitura
    // -----------------------------------------------------------------------

    public async Task<List<AtendimentoResumoDto>> ListarAsync(
        Guid baseId,
        Especialidade? fila,
        ClassificacaoRisco? risco,
        string? busca,
        Guid? assumidosPor = null,
        bool ocultarAssumidosPorOutros = false,
        Guid? euId = null,
        CancellationToken ct = default)
    {
        var query = _db.Atendimentos
            .AsNoTracking()
            .Include(a => a.Paciente)
            .Include(a => a.Etapas).ThenInclude(e => e.Profissional)
            // A passagem aberta e o que a lista precisa para o cronometro e para
            // dizer quem encaminhou. Sem este Include ela viria vazia sob
            // AsNoTracking, e o cartao perderia os dois em silencio.
            .Include(a => a.PassagensFila).ThenInclude(p => p.EncaminhadaPor)
            .Where(a => a.BaseId == baseId && a.Status != StatusAtendimento.Cancelado);

        if (risco is not null)
        {
            query = query.Where(a => a.ClassificacaoRisco == risco);
        }

        if (fila is not null)
        {
            query = query.Where(a => a.Etapas.Any(
                e => e.Especialidade == fila && e.Status != StatusEtapa.Concluida));
        }

        // "Meus atendimentos": o que esta pessoa assumiu e ainda nao concluiu.
        if (assumidosPor is not null)
        {
            query = query.Where(a => a.Etapas.Any(
                e => e.ProfissionalId == assumidosPor && e.Status != StatusEtapa.Concluida));
        }

        /*
            A fila de quem esta livre nao mostra o que ja esta na mao de outra
            pessoa — e disso que serve assumir.

            `euId` e quem esta perguntando, e vem sempre, independente de
            `assumidosPor`. Usar `assumidosPor` aqui escondia o atendimento de
            quem acabou de assumi-lo: fora de "Meus" ele e nulo, e a comparacao
            virava "esconda tudo que esta assumido".
        */
        if (ocultarAssumidosPorOutros && fila is not null)
        {
            query = query.Where(a => !a.Etapas.Any(
                e => e.Especialidade == fila &&
                     e.Status != StatusEtapa.Concluida &&
                     e.ProfissionalId != null &&
                     e.ProfissionalId != euId));
        }

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            query = query.Where(a =>
                EF.Functions.ILike(a.Codigo, $"%{termo}%") ||
                EF.Functions.ILike(a.Paciente!.Nome, $"%{termo}%") ||
                (a.QueixaPrincipal != null && EF.Functions.ILike(a.QueixaPrincipal, $"%{termo}%")));
        }

        var atendimentos = await query
            .OrderByDescending(a => a.CriadoEm)
            .Take(200)
            .ToListAsync(ct);

        return atendimentos.Select(Mapeadores.ParaResumo).ToList();
    }

    // -----------------------------------------------------------------------
    // Assumir e liberar
    // -----------------------------------------------------------------------

    /// <summary>
    /// Assume a etapa: ela sai da fila de quem esta livre e passa a aparecer
    /// como "em atendimento com <fulano>".
    ///
    /// Sem isto, dois profissionais abrem o mesmo paciente ao mesmo tempo e
    /// nenhum dos dois sabe — com a fila cheia isso acontece, e o segundo so
    /// descobre quando vai salvar por cima do primeiro.
    /// </summary>
    public async Task<AtendimentoResumoDto> AssumirEtapaAsync(
        Guid atendimentoId,
        Especialidade especialidade,
        Guid profissionalId,
        CancellationToken ct = default)
    {
        var atendimento = await CarregarComEtapasAsync(atendimentoId, ct);
        var etapa = EtapaAberta(atendimento, especialidade);

        if (etapa.ProfissionalId == profissionalId)
        {
            // Ja e dele. Reassumir nao e erro: acontece quando a tela recarrega.
            return Mapeadores.ParaResumo(atendimento);
        }

        if (etapa.ProfissionalId is not null)
        {
            var dono = await _db.Profissionais
                .AsNoTracking()
                .Where(p => p.Id == etapa.ProfissionalId)
                .Select(p => p.Nome)
                .FirstOrDefaultAsync(ct);

            throw new RegraDeNegocioException(
                $"Este atendimento ja esta com {dono ?? "outro profissional"}.");
        }

        etapa.ProfissionalId = profissionalId;
        etapa.Status = StatusEtapa.EmAndamento;
        etapa.IniciadaEm ??= DateTime.UtcNow;

        /*
            O cronometro da tela conta a partir daqui, e nao da entrada na fila:
            entre uma coisa e outra esta a espera, e somar as duas faria todo
            atendimento parecer durar o plantao inteiro.
        */
        if (PassagemAberta(atendimento, especialidade) is PassagemFila passagem)
        {
            passagem.ProfissionalId = profissionalId;
            passagem.AssumidaEm ??= DateTime.UtcNow;
        }

        /*
            Atender fora da propria fila e permitido: em campo a equipe e curta e
            as funcoes se cobrem — o medico tria quando a fila estoura. Mas fica
            marcado, porque uma excecao sem rastro nao e excecao, e virar rotina
            silenciosa e exatamente o que a fila por profissao veio evitar.

            O rastro esta no ato, e nao em abrir a aba. Com a lista se atualizando
            sozinha a cada quinze segundos, registrar a consulta produziria uma
            linha de auditoria por quarto de minuto e enterraria o que importa.
        */
        var funcao = await _db.Profissionais
            .AsNoTracking()
            .Where(p => p.Id == profissionalId)
            .Select(p => (FuncaoProfissional?)p.Funcao)
            .FirstOrDefaultAsync(ct);

        var acao = funcao is not null && !FilasDaFuncao.EhDaFuncao(funcao.Value, especialidade)
            ? AcaoAuditoria.AssumiuForaDaSuaFila
            : AcaoAuditoria.AssumiuEtapa;

        await _auditoria.RegistrarAsync(atendimentoId, profissionalId, acao, especialidade, ct);

        await _db.SaveChangesAsync(ct);

        return Mapeadores.ParaResumo(atendimento);
    }

    /// <summary>
    /// Devolve a etapa para a fila.
    ///
    /// Quem assumiu pode liberar; a coordenacao pode liberar a de qualquer um,
    /// porque em campo alguem assume e sai para outra emergencia, e sem essa
    /// saida o paciente ficaria preso numa fila que ninguem mais ve.
    /// </summary>
    public async Task<AtendimentoResumoDto> LiberarEtapaAsync(
        Guid atendimentoId,
        Especialidade especialidade,
        Guid profissionalId,
        bool ehAdministrador,
        CancellationToken ct = default)
    {
        var atendimento = await CarregarComEtapasAsync(atendimentoId, ct);
        var etapa = EtapaAberta(atendimento, especialidade);

        if (etapa.ProfissionalId is null)
        {
            return Mapeadores.ParaResumo(atendimento);
        }

        if (etapa.ProfissionalId != profissionalId && !ehAdministrador)
        {
            throw new RegraDeNegocioException(
                "Somente quem assumiu, ou a coordenacao, pode devolver o atendimento para a fila.");
        }

        etapa.ProfissionalId = null;
        etapa.Status = StatusEtapa.Aguardando;

        // O cronometro para e a passagem volta a nao ter dono: quem devolveu
        // para a fila nao atendeu, e contar isso como producao dele seria contar
        // um atendimento que nao houve.
        if (PassagemAberta(atendimento, especialidade) is PassagemFila passagem)
        {
            passagem.ProfissionalId = null;
            passagem.AssumidaEm = null;
        }

        await _auditoria.RegistrarAsync(
            atendimentoId, profissionalId, AcaoAuditoria.LiberouEtapa, especialidade, ct);

        await _db.SaveChangesAsync(ct);

        return Mapeadores.ParaResumo(atendimento);
    }

    /// <summary>
    /// Manda o paciente para outra fila, sem fechar consulta nenhuma.
    ///
    /// Ate agora a unica forma de redirecionar era concluir a consulta com
    /// desfecho "Encaminhado", o que exige CID-10. Quando a triagem simplesmente
    /// errou a fila — problema dentario que caiu na clinica geral — isso
    /// obrigaria o medico a inventar um diagnostico para uma consulta que nao
    /// aconteceu. E tambem nao existia caminho nenhum saindo da odontologia e da
    /// enfermagem, que nao tem campo de encaminhamento.
    ///
    /// O motivo e obrigatorio: quem recebe o paciente na fila seguinte precisa
    /// saber por que ele chegou ali, e um encaminhamento mudo faz o paciente
    /// circular entre filas sem ninguem entender o caminho.
    /// </summary>
    public async Task<ProntuarioDto> EncaminharAsync(
        Guid atendimentoId,
        Especialidade origem,
        Especialidade destino,
        string? motivo,
        Guid profissionalId,
        AcaoAuditoria acao = AcaoAuditoria.EncaminhouParaOutraFila,
        CancellationToken ct = default)
    {
        if (origem == destino)
        {
            throw new RegraDeNegocioException("A fila de destino e a mesma de origem.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new RegraDeNegocioException("Informe o motivo do encaminhamento.");
        }

        var atendimento = await CarregarAsync(atendimentoId, ct);

        if (atendimento.FinalizadoEm is not null)
        {
            throw new RegraDeNegocioException(
                "Este atendimento ja foi finalizado. Reabra antes de encaminhar.");
        }

        var etapa = atendimento.Etapas.FirstOrDefault(e => e.Especialidade == origem)
            ?? throw new RegraDeNegocioException("Este atendimento nao passou por esta fila.");

        if (etapa.Status == StatusEtapa.Concluida)
        {
            throw new RegraDeNegocioException("Esta etapa ja foi concluida.");
        }

        /*
            Nada clinico registrado significa que o paciente nunca foi atendido
            aqui: a etapa e cancelada, nao concluida. Marcar como concluida
            inflaria a producao da especialidade com atendimentos que nao
            existiram — e producao por area e justamente o numero que este
            sistema precisa manter confiavel.
        */
        var houveAtendimento = etapa.Triagem is not null
            || etapa.Consulta is not null
            || etapa.Odontologia is not null
            || etapa.Enfermagem is not null;

        etapa.Status = houveAtendimento ? StatusEtapa.Concluida : StatusEtapa.Cancelada;
        etapa.ConcluidaEm = DateTime.UtcNow;
        etapa.ProfissionalId = profissionalId;

        FecharPassagem(atendimento, origem, houveAtendimento ? profissionalId : null);
        await AbrirFilaAsync(atendimento, destino, ct, profissionalId, origem);

        await _auditoria.RegistrarAsync(atendimentoId, profissionalId, acao, origem, ct);

        // Chave e valores canonicos: a traducao acontece na hora de exibir, para
        // o historico nao congelar no idioma de quem encaminhou.
        _auditoria.RegistrarDiffs(
            atendimentoId,
            profissionalId,
            new[]
            {
                new DiffCampo("atendimento.fila", origem.ToString(), destino.ToString()),
                new DiffCampo("atendimento.motivoEncaminhamento", null, motivo.Trim())
            },
            origem,
            aposFinalizacao: false);

        atendimento.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await ObterProntuarioAsync(atendimentoId, ct);
    }

    /// <summary>
    /// Devolve o paciente para a fila que o encaminhou.
    ///
    /// E o caminho de volta do encaminhamento: o clinico manda para a pediatria,
    /// a pediatria vê que nao e caso dela e devolve. Sem isto a pediatra teria de
    /// lembrar de onde o paciente veio e escolher a fila na mao — e escolheria
    /// errado nas vezes em que o paciente ja passou por tres.
    ///
    /// A etapa de destino ja existe e ja esta concluida; <see cref="AbrirFilaAsync"/>
    /// a reabre com a ficha que ela ja tinha, e nao como consulta nova.
    /// </summary>
    public async Task<ProntuarioDto> DevolverAsync(
        Guid atendimentoId,
        Especialidade origem,
        string? motivo,
        Guid profissionalId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new RegraDeNegocioException("Informe o motivo da devolucao.");
        }

        var atendimento = await CarregarAsync(atendimentoId, ct);

        if (atendimento.FinalizadoEm is not null)
        {
            throw new RegraDeNegocioException(
                "Este atendimento ja foi finalizado. Reabra antes de devolver.");
        }

        var passagem = PassagemAberta(atendimento, origem)
            ?? throw new RegraDeNegocioException("Este atendimento nao esta nesta fila.");

        var destino = passagem.EncaminhadaDe
            ?? throw new RegraDeNegocioException(
                "Este atendimento nao veio de outra fila: nao ha para onde devolver.");

        return await EncaminharAsync(atendimentoId, origem, destino, motivo, profissionalId,
            AcaoAuditoria.DevolveuParaOrigem, ct);
    }

    /// <summary>
    /// Da alta: encerra a etapa de quem esta atendendo e o atendimento junto.
    ///
    /// Ate agora a alta so fechava a etapa, e o atendimento ficava aberto ate
    /// alguem lembrar de finalizar no prontuario. Na pratica ninguem lembrava, e
    /// o paciente aparecia em aberto no dia seguinte — o que estraga tanto a fila
    /// quanto o tempo medido.
    ///
    /// Filas pendentes de outras especialidades nao impedem a alta, mas nao somem
    /// caladas: sem <paramref name="cancelarPendentes"/> a chamada e recusada com
    /// a lista, para a tela perguntar antes. Elas sao canceladas, e nao
    /// concluidas: ninguem atendeu, e concluir inflaria a producao da
    /// especialidade com atendimento que nao aconteceu.
    /// </summary>
    public async Task<ProntuarioDto> DarAltaAsync(
        Guid atendimentoId,
        Especialidade especialidade,
        bool cancelarPendentes,
        Guid profissionalId,
        CancellationToken ct = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, ct);

        if (atendimento.FinalizadoEm is not null)
        {
            throw new RegraDeNegocioException("Atendimento ja esta finalizado.");
        }

        var minha = atendimento.Etapas.FirstOrDefault(e => e.Especialidade == especialidade)
            ?? throw new RegraDeNegocioException("Este atendimento nao passou por esta fila.");

        var pendentes = atendimento.Etapas
            .Where(e => e.Id != minha.Id)
            .Where(e => e.Status is StatusEtapa.Aguardando or StatusEtapa.EmAndamento)
            .ToList();

        if (pendentes.Count > 0 && !cancelarPendentes)
        {
            throw new RegraDeNegocioException(
                "Ha filas pendentes: " +
                string.Join(", ", pendentes.Select(e => e.Especialidade.ToString())) +
                ". Confirme para dar alta cancelando estas filas.");
        }

        if (minha.Status is StatusEtapa.Aguardando or StatusEtapa.EmAndamento)
        {
            ConcluirEtapa(minha, profissionalId);
            FecharPassagem(atendimento, especialidade, profissionalId);
        }

        foreach (var pendente in pendentes)
        {
            pendente.Status = StatusEtapa.Cancelada;
            pendente.ConcluidaEm = DateTime.UtcNow;

            // Sem profissional: a fila foi cancelada, nao atendida.
            FecharPassagem(atendimento, pendente.Especialidade);

            await _auditoria.RegistrarAsync(
                atendimentoId, profissionalId, AcaoAuditoria.CancelouFilaPendente,
                pendente.Especialidade, ct);
        }

        atendimento.Status = StatusAtendimento.Finalizado;
        atendimento.FinalizadoPorId = profissionalId;
        atendimento.FinalizadoEm = DateTime.UtcNow;
        atendimento.AtualizadoEm = DateTime.UtcNow;

        foreach (var aberta in atendimento.PassagensFila.Where(p => p.SaiuEm is null))
        {
            aberta.SaiuEm = DateTime.UtcNow;
        }

        await _auditoria.RegistrarAsync(
            atendimentoId, profissionalId, AcaoAuditoria.DeuAlta, especialidade, ct);

        await _db.SaveChangesAsync(ct);

        return await ObterProntuarioAsync(atendimentoId, ct);
    }

    private async Task<Atendimento> CarregarComEtapasAsync(Guid id, CancellationToken ct)
        => await _db.Atendimentos
            .Include(a => a.Paciente)
            .Include(a => a.Etapas).ThenInclude(e => e.Profissional)
            // Assumir e liberar mexem na passagem: sem este Include a colecao
            // vem vazia e o cronometro nunca comeca a contar, calado.
            .Include(a => a.PassagensFila).ThenInclude(p => p.EncaminhadaPor)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new RegraDeNegocioException("Atendimento nao encontrado.");

    private static Etapa EtapaAberta(Atendimento atendimento, Especialidade especialidade)
    {
        var etapa = atendimento.Etapas.FirstOrDefault(e => e.Especialidade == especialidade)
            ?? throw new RegraDeNegocioException("Este atendimento nao passou por esta fila.");

        if (etapa.Status == StatusEtapa.Concluida)
        {
            throw new RegraDeNegocioException("Esta etapa ja foi concluida.");
        }

        return etapa;
    }

    public async Task<ProntuarioDto> ObterProntuarioAsync(Guid id, CancellationToken ct = default)
    {
        var atendimento = await _db.Atendimentos
            .AsNoTracking()
            .Include(a => a.Base)
            .Include(a => a.Paciente).ThenInclude(p => p!.Comunidade)
            .Include(a => a.CriadoPor)
            .Include(a => a.FinalizadoPor)
            .Include(a => a.PassagensFila).ThenInclude(p => p.EncaminhadaPor)
            .Include(a => a.Auditorias).ThenInclude(x => x.Profissional)
            .Include(a => a.Etapas).ThenInclude(e => e.Profissional)
            .Include(a => a.Etapas).ThenInclude(e => e.Triagem)
            .Include(a => a.Etapas).ThenInclude(e => e.Consulta).ThenInclude(c => c!.Ortopedia)
            .Include(a => a.Etapas).ThenInclude(e => e.Consulta).ThenInclude(c => c!.Cid10)
            .Include(a => a.Etapas).ThenInclude(e => e.Odontologia).ThenInclude(o => o!.Marcacoes)
            .Include(a => a.Etapas).ThenInclude(e => e.Odontologia).ThenInclude(o => o!.Cid10)
            .Include(a => a.Etapas).ThenInclude(e => e.Enfermagem)
            .Include(a => a.Etapas).ThenInclude(e => e.Dispensacoes).ThenInclude(d => d.Item)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new RegraDeNegocioException("Atendimento nao encontrado.");

        return Mapeadores.ParaProntuario(atendimento);
    }

    public async Task<Guid?> ResolverPorCodigoAsync(string codigo, CancellationToken ct = default)
        => await _db.Atendimentos
            .AsNoTracking()
            .Where(a => a.Codigo == codigo.ToUpperInvariant())
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);

    // -----------------------------------------------------------------------
    // Apoio
    // -----------------------------------------------------------------------

    private async Task<Atendimento> CarregarAsync(Guid id, CancellationToken ct)
        => await _db.Atendimentos
            .Include(a => a.Paciente)
            .Include(a => a.Etapas)
            .Include(a => a.PassagensFila)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new RegraDeNegocioException("Atendimento nao encontrado.");

    private async Task<Etapa> ObterOuCriarEtapaAsync(
        Atendimento atendimento,
        Especialidade especialidade,
        Guid profissionalId,
        CancellationToken ct)
    {
        var etapa = atendimento.Etapas.FirstOrDefault(e => e.Especialidade == especialidade);

        if (etapa is null)
        {
            etapa = new Etapa
            {
                AtendimentoId = atendimento.Id,
                Especialidade = especialidade,
                Status = StatusEtapa.EmAndamento,
                ProfissionalId = profissionalId,
                IniciadaEm = DateTime.UtcNow
            };

            _db.Etapas.Add(etapa);
            atendimento.Etapas.Add(etapa);

            if (!atendimento.PassagensFila.Any(p => p.Especialidade == especialidade))
            {
                var passagem = new PassagemFila
                {
                    AtendimentoId = atendimento.Id,
                    Especialidade = especialidade
                };

                _db.PassagensFila.Add(passagem);
                atendimento.PassagensFila.Add(passagem);
            }

            await _auditoria.RegistrarAsync(
                atendimento.Id, profissionalId, AcaoAuditoria.IniciouEtapa, especialidade, ct);

            return etapa;
        }

        if (etapa.IniciadaEm is null)
        {
            etapa.IniciadaEm = DateTime.UtcNow;
            etapa.Status = StatusEtapa.EmAndamento;
            etapa.ProfissionalId = profissionalId;

            await _auditoria.RegistrarAsync(
                atendimento.Id, profissionalId, AcaoAuditoria.IniciouEtapa, especialidade, ct);
        }

        return etapa;
    }

    private static void ConcluirEtapa(Etapa etapa, Guid profissionalId)
    {
        etapa.Status = StatusEtapa.Concluida;
        etapa.ConcluidaEm = DateTime.UtcNow;
        etapa.ProfissionalId = profissionalId;
    }

    /// <summary>A passagem em aberto pela fila, se houver.</summary>
    private static PassagemFila? PassagemAberta(Atendimento atendimento, Especialidade especialidade)
        => atendimento.PassagensFila
            .Where(p => p.Especialidade == especialidade && p.SaiuEm is null)
            .OrderByDescending(p => p.EntrouEm)
            .FirstOrDefault();

    /// <param name="atendidoPor">
    /// Quem atendeu, ou nulo quando o paciente saiu da fila sem ter sido
    /// atendido. Distinguir importa: passagem sem profissional nao entra na
    /// producao de ninguem, e e exatamente o caso de quem recebe o paciente na
    /// fila errada e reencaminha sem abrir a ficha.
    /// </param>
    private static void FecharPassagem(
        Atendimento atendimento,
        Especialidade especialidade,
        Guid? atendidoPor = null)
    {
        var passagem = PassagemAberta(atendimento, especialidade);

        if (passagem is null)
        {
            return;
        }

        passagem.SaiuEm = DateTime.UtcNow;

        if (atendidoPor is null)
        {
            return;
        }

        passagem.ProfissionalId = atendidoPor;
        passagem.AssumidaEm ??= passagem.EntrouEm;
        passagem.ConcluidaEm = passagem.SaiuEm;
    }

    /// <summary>
    /// Abre a fila de destino, ou reabre a que ja foi concluida.
    ///
    /// Reabrir e o caso da devolucao: a pediatria devolve ao clinico que
    /// encaminhou, e a etapa da clinica geral ja existe e ja esta concluida.
    /// Como o indice e unico por (atendimento, especialidade), nao ha uma
    /// segunda etapa a criar — a mesma volta para "aguardando", com a ficha
    /// clinica que ja tinha.
    ///
    /// Os horarios da etapa sao zerados junto. Se ficassem, a etapa mediria do
    /// primeiro atendimento ate o fim do segundo, engolindo o desvio pela outra
    /// fila. Cada passagem, com o seu profissional e o seu tempo, esta guardada
    /// em <see cref="PassagemFila"/>, que e de onde a producao e lida.
    /// </summary>
    private async Task AbrirFilaAsync(
        Atendimento atendimento,
        Especialidade destino,
        CancellationToken ct,
        Guid? encaminhadaPorId = null,
        Especialidade? encaminhadaDe = null)
    {
        var etapa = atendimento.Etapas.FirstOrDefault(e => e.Especialidade == destino);

        if (etapa is null)
        {
            etapa = new Etapa
            {
                AtendimentoId = atendimento.Id,
                Especialidade = destino,
                Status = StatusEtapa.Aguardando
            };

            _db.Etapas.Add(etapa);
            atendimento.Etapas.Add(etapa);
        }
        else if (etapa.Status is StatusEtapa.Concluida or StatusEtapa.Cancelada)
        {
            etapa.Status = StatusEtapa.Aguardando;
            etapa.ProfissionalId = null;
            etapa.IniciadaEm = null;
            etapa.ConcluidaEm = null;
        }

        if (PassagemAberta(atendimento, destino) is null)
        {
            var passagem = new PassagemFila
            {
                AtendimentoId = atendimento.Id,
                Especialidade = destino,
                EncaminhadaPorId = encaminhadaPorId,
                EncaminhadaDe = encaminhadaDe
            };

            _db.PassagensFila.Add(passagem);
            atendimento.PassagensFila.Add(passagem);
        }

        await Task.CompletedTask;
    }

    private async Task ValidarCidAsync(string? codigo, DesfechoConsulta? desfecho, CancellationToken ct)
    {
        // Diagnostico e exigido quando a consulta se encerra em alta ou
        // encaminhamento; evasao e retorno podem nao ter diagnostico fechado.
        var exigeCid = desfecho is DesfechoConsulta.Alta or DesfechoConsulta.Encaminhado;

        if (string.IsNullOrWhiteSpace(codigo))
        {
            if (exigeCid)
            {
                throw new RegraDeNegocioException(
                    "Informe o codigo CID-10 do diagnostico para concluir a consulta.");
            }

            return;
        }

        var existe = await _db.Cid10s.AnyAsync(c => c.Codigo == codigo, ct);

        if (!existe)
        {
            throw new RegraDeNegocioException($"CID-10 '{codigo}' nao existe no catalogo.");
        }
    }

    private async Task SubstituirDispensacoesAsync(
        Guid etapaId,
        List<DispensacaoRequest> pedidos,
        CancellationToken ct)
    {
        var existentes = await _db.Dispensacoes.Where(d => d.EtapaId == etapaId).ToListAsync(ct);
        _db.Dispensacoes.RemoveRange(existentes);

        if (pedidos.Count == 0)
        {
            return;
        }

        var ids = pedidos.Where(p => p.ItemId is not null).Select(p => p.ItemId!.Value).ToList();
        var itens = await _db.ItensCatalogo.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);

        var erros = new List<string>();

        foreach (var pedido in pedidos)
        {
            var item = pedido.ItemId is not null && itens.TryGetValue(pedido.ItemId.Value, out var i) ? i : null;

            var entrada = new EntradaDispensacao
            {
                ItemId = pedido.ItemId,
                DescricaoLivre = pedido.DescricaoLivre,
                JustificativaItemLivre = pedido.JustificativaItemLivre,
                Quantidade = pedido.Quantidade,
                Via = pedido.Via
            };

            var errosItem = ValidadorDispensacao.Validar(entrada, item);

            if (errosItem.Count > 0)
            {
                erros.AddRange(errosItem);
                continue;
            }

            _db.Dispensacoes.Add(new Dispensacao
            {
                EtapaId = etapaId,
                ItemId = pedido.ItemId,
                DescricaoLivre = pedido.DescricaoLivre?.Trim(),
                JustificativaItemLivre = pedido.JustificativaItemLivre?.Trim(),
                Quantidade = pedido.Quantidade,
                // A unidade vem do catalogo quando ha item; digitar unidade a mao
                // era outra fonte de divergencia no sistema antigo.
                Unidade = item?.Unidade ?? UnidadeDispensacao.Unidade,
                Via = pedido.Via,
                Posologia = pedido.Posologia
            });
        }

        if (erros.Count > 0)
        {
            throw new RegraDeNegocioException(erros.Distinct().ToList());
        }
    }

    // -----------------------------------------------------------------------
    // Snapshots para o diff da auditoria
    //
    // As chaves e os valores gravados aqui sao CANONICOS: chave estavel de
    // campo e nome de enum, nunca texto traduzido. Quem traduz e a interface,
    // no idioma de quem esta lendo.
    //
    // CORRIGE, tambem no proprio sistema: gravar o rotulo pronto congelaria o
    // historico no idioma de quem digitou — um registro feito em espanhol
    // apareceria em espanhol para quem le em portugues meses depois — e faria
    // o identificador do enum vazar para a tela, que e exatamente o defeito
    // observado no sistema de referencia (`consulta_medica` cru no relatorio).
    // -----------------------------------------------------------------------

    private static Dictionary<string, string?> SnapshotTriagem(Triagem t) => new()
    {
        ["triagem.pressaoArterial"] = t.PressaoSistolica is null ? null : $"{t.PressaoSistolica}x{t.PressaoDiastolica}",
        ["triagem.frequenciaCardiaca"] = t.FrequenciaCardiaca?.ToString(),
        ["triagem.frequenciaRespiratoria"] = t.FrequenciaRespiratoria?.ToString(),
        ["triagem.saturacaoO2"] = t.SaturacaoO2?.ToString(),
        ["triagem.temperatura"] = t.TemperaturaCelsius?.ToString(CultureInfo.InvariantCulture),
        ["triagem.glicemia"] = t.GlicemiaCapilar?.ToString(),
        ["triagem.peso"] = t.PesoKg?.ToString(CultureInfo.InvariantCulture),
        ["triagem.altura"] = t.AlturaCm?.ToString(),
        ["triagem.escalaDor"] = t.EscalaDor?.ToString(),
        ["triagem.sintomas"] = t.Sintomas.Count == 0 ? null : string.Join(",", t.Sintomas),
        ["triagem.outroSintoma"] = t.OutroSintoma,
        ["triagem.medicamentosEmUso"] = t.MedicamentosEmUso,
        ["triagem.statusAlergia"] = t.StatusAlergia.ToString(),
        ["triagem.alergias"] = t.Alergias,
        ["triagem.classificacaoRisco"] = t.ClassificacaoRisco.ToString(),
        ["triagem.encaminhamento"] = t.Encaminhamento?.ToString(),
        ["triagem.observacoes"] = t.Observacoes
    };

    private static Dictionary<string, string?> SnapshotConsulta(Consulta c) => new()
    {
        ["consulta.sintomas"] = c.SintomasDescricao,
        ["consulta.cid10"] = c.Cid10Codigo,
        ["consulta.diagnosticoObservacao"] = c.DiagnosticoObservacao,
        ["consulta.conduta"] = c.Conduta,
        ["consulta.desfecho"] = c.Desfecho?.ToString(),
        ["consulta.encaminhadoPara"] = c.EncaminhadoPara?.ToString(),
        ["consulta.sintomasSaudeMental"] = c.SintomasSaudeMental.Count == 0 ? null : string.Join(",", c.SintomasSaudeMental),
        ["consulta.perdasVivenciadas"] = c.PerdasVivenciadas.Count == 0 ? null : string.Join(",", c.PerdasVivenciadas),
        ["ortopedia.localizacao"] = c.Ortopedia?.Localizacao,
        ["ortopedia.mecanismoTrauma"] = c.Ortopedia?.MecanismoTrauma,
        ["ortopedia.imobilizacao"] = c.Ortopedia is null ? null : Booleano(c.Ortopedia.Imobilizacao),
        ["ortopedia.necessitaRaioX"] = c.Ortopedia is null ? null : Booleano(c.Ortopedia.NecessitaRaioX)
    };

    private static Dictionary<string, string?> SnapshotOdontologia(Odontologia o) => new()
    {
        ["odontologia.queixa"] = o.Queixa,
        ["odontologia.cid10"] = o.Cid10Codigo,
        ["odontologia.procedimentos"] = o.Procedimentos.Count == 0 ? null : string.Join(",", o.Procedimentos),
        ["odontologia.outroProcedimento"] = o.OutroProcedimento,
        ["odontologia.odontograma"] = o.Marcacoes.Count == 0 ? null : Odontograma.ResumirCanonico(o.Marcacoes),
        ["odontologia.desfecho"] = o.Desfecho?.ToString()
    };

    private static Dictionary<string, string?> SnapshotEnfermagem(Enfermagem e) => new()
    {
        ["enfermagem.procedimentos"] = e.Procedimentos.Count == 0 ? null : string.Join(",", e.Procedimentos),
        ["enfermagem.outroProcedimento"] = e.OutroProcedimento,
        ["enfermagem.observacoes"] = e.Observacoes,
        ["enfermagem.desfecho"] = e.Desfecho?.ToString()
    };

    /// <summary>Booleano canonico, traduzido na interface.</summary>
    private static string Booleano(bool valor) => valor ? "true" : "false";
}
