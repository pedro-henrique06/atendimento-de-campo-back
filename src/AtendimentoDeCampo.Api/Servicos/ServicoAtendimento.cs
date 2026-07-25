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
        Paciente? paciente = null;

        // Paciente com documento pode estar retornando; reaproveitar o cadastro
        // preserva o historico entre atendimentos.
        if (!string.IsNullOrWhiteSpace(dados.NumeroDocumento))
        {
            paciente = await _db.Pacientes.FirstOrDefaultAsync(
                p => p.TipoDocumento == dados.TipoDocumento &&
                     p.NumeroDocumento == dados.NumeroDocumento,
                ct);
        }

        if (paciente is null)
        {
            paciente = new Paciente();
            _db.Pacientes.Add(paciente);
        }

        paciente.Nome = dados.Nome.Trim();
        paciente.TipoDocumento = dados.TipoDocumento;
        paciente.NumeroDocumento = string.IsNullOrWhiteSpace(dados.NumeroDocumento)
            ? null
            : dados.NumeroDocumento.Trim();
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
        FecharPassagem(atendimento, Especialidade.Triagem);

        await _auditoria.RegistrarAsync(
            atendimentoId, profissionalId, AcaoAuditoria.ConcluiuEtapa, Especialidade.Triagem, ct);

        // O encaminhamento abre a proxima fila.
        if (req.Encaminhamento is Especialidade destino && destino != Especialidade.Triagem)
        {
            await AbrirFilaAsync(atendimento, destino, ct);
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
                    new DiffCampo(
                        "Classificacao de risco (START)",
                        $"sugestao do protocolo: {sugestao.Classificacao} ({sugestao.Motivo})",
                        $"escolhida pelo profissional: {req.ClassificacaoRisco}")
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
        FecharPassagem(atendimento, req.Especialidade);

        await _auditoria.RegistrarAsync(
            atendimentoId, profissionalId, AcaoAuditoria.ConcluiuEtapa, req.Especialidade, ct);

        if (req.Desfecho == DesfechoConsulta.Encaminhado && req.EncaminhadoPara is Especialidade destino)
        {
            await AbrirFilaAsync(atendimento, destino, ct);
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
        FecharPassagem(atendimento, Especialidade.Odontologia);

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
        FecharPassagem(atendimento, Especialidade.Enfermagem);

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
            Campo = "Justificativa",
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
        CancellationToken ct = default)
    {
        var query = _db.Atendimentos
            .AsNoTracking()
            .Include(a => a.Paciente)
            .Include(a => a.Etapas).ThenInclude(e => e.Profissional)
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

    public async Task<ProntuarioDto> ObterProntuarioAsync(Guid id, CancellationToken ct = default)
    {
        var atendimento = await _db.Atendimentos
            .AsNoTracking()
            .Include(a => a.Base)
            .Include(a => a.Paciente)
            .Include(a => a.CriadoPor)
            .Include(a => a.FinalizadoPor)
            .Include(a => a.PassagensFila)
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

    private static void FecharPassagem(Atendimento atendimento, Especialidade especialidade)
    {
        var passagem = atendimento.PassagensFila
            .Where(p => p.Especialidade == especialidade && p.SaiuEm is null)
            .OrderByDescending(p => p.EntrouEm)
            .FirstOrDefault();

        if (passagem is not null)
        {
            passagem.SaiuEm = DateTime.UtcNow;
        }
    }

    private async Task AbrirFilaAsync(Atendimento atendimento, Especialidade destino, CancellationToken ct)
    {
        if (!atendimento.Etapas.Any(e => e.Especialidade == destino))
        {
            var etapa = new Etapa
            {
                AtendimentoId = atendimento.Id,
                Especialidade = destino,
                Status = StatusEtapa.Aguardando
            };

            _db.Etapas.Add(etapa);
            atendimento.Etapas.Add(etapa);
        }

        if (!atendimento.PassagensFila.Any(p => p.Especialidade == destino && p.SaiuEm is null))
        {
            var passagem = new PassagemFila
            {
                AtendimentoId = atendimento.Id,
                Especialidade = destino
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
    // -----------------------------------------------------------------------

    private static Dictionary<string, string?> SnapshotTriagem(Triagem t) => new()
    {
        ["Pressao arterial (mmHg)"] = t.PressaoSistolica is null ? null : $"{t.PressaoSistolica}x{t.PressaoDiastolica}",
        ["Frequencia cardiaca (bpm)"] = t.FrequenciaCardiaca?.ToString(),
        ["Frequencia respiratoria (irpm)"] = t.FrequenciaRespiratoria?.ToString(),
        ["Saturacao O2 (%)"] = t.SaturacaoO2?.ToString(),
        ["Temperatura (C)"] = t.TemperaturaCelsius?.ToString(),
        ["Glicemia capilar"] = t.GlicemiaCapilar?.ToString(),
        ["Sintomas atuais"] = t.Sintomas.Count == 0 ? null : string.Join(", ", t.Sintomas),
        ["Outro sintoma"] = t.OutroSintoma,
        ["Medicamentos em uso"] = t.MedicamentosEmUso,
        ["Alergia"] = DescreverAlergia(t.StatusAlergia, t.Alergias),
        ["Classificacao de risco (START)"] = t.ClassificacaoRisco.ToString(),
        ["Encaminhamento"] = t.Encaminhamento?.ToString(),
        ["Observacoes"] = t.Observacoes
    };

    private static Dictionary<string, string?> SnapshotConsulta(Consulta c) => new()
    {
        ["Sintomas"] = c.SintomasDescricao,
        ["Diagnostico (CID-10)"] = c.Cid10Codigo,
        ["Observacao do diagnostico"] = c.DiagnosticoObservacao,
        ["Conduta"] = c.Conduta,
        ["Desfecho da consulta"] = c.Desfecho?.ToString(),
        ["Encaminhado para"] = c.EncaminhadoPara?.ToString(),
        ["Sintomas de saude mental"] = c.SintomasSaudeMental.Count == 0 ? null : string.Join(", ", c.SintomasSaudeMental),
        ["Perdas vivenciadas"] = c.PerdasVivenciadas.Count == 0 ? null : string.Join(", ", c.PerdasVivenciadas),
        ["Localizacao"] = c.Ortopedia?.Localizacao,
        ["Mecanismo do trauma"] = c.Ortopedia?.MecanismoTrauma,
        ["Imobilizacao"] = c.Ortopedia is null ? null : (c.Ortopedia.Imobilizacao ? "Sim" : "Nao"),
        ["Necessita raio-X"] = c.Ortopedia is null ? null : (c.Ortopedia.NecessitaRaioX ? "Sim" : "Nao")
    };

    private static Dictionary<string, string?> SnapshotOdontologia(Odontologia o) => new()
    {
        ["Queixa / observacoes"] = o.Queixa,
        ["Diagnostico (CID-10)"] = o.Cid10Codigo,
        ["Procedimentos realizados"] = o.Procedimentos.Count == 0 ? null : string.Join(", ", o.Procedimentos),
        ["Outro procedimento"] = o.OutroProcedimento,
        ["Odontograma"] = o.Marcacoes.Count == 0 ? null : Odontograma.Resumir(o.Marcacoes),
        ["Desfecho da consulta"] = o.Desfecho?.ToString()
    };

    private static Dictionary<string, string?> SnapshotEnfermagem(Enfermagem e) => new()
    {
        ["Procedimentos"] = e.Procedimentos.Count == 0 ? null : string.Join(", ", e.Procedimentos),
        ["Outro procedimento"] = e.OutroProcedimento,
        ["Observacoes"] = e.Observacoes,
        ["Desfecho da consulta"] = e.Desfecho?.ToString()
    };

    private static string DescreverAlergia(StatusAlergia status, string? alergias) => status switch
    {
        StatusAlergia.PossuiAlergia => $"Possui: {alergias}",
        StatusAlergia.SemAlergiaConhecida => "Sem alergia conhecida",
        _ => "Nao perguntado"
    };
}
