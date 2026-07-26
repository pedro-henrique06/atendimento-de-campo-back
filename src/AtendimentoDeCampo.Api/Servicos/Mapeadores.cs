using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;

namespace AtendimentoDeCampo.Api.Servicos;

/// <summary>Conversao das entidades para os contratos da API.</summary>
public static class Mapeadores
{
    public static PacienteDto ParaDto(Paciente p)
    {
        var idade = CalculadoraIdade.Calcular(p.DataNascimento, p.IdadeAproximada);
        var alerta = AlertaAlergia.Avaliar(p.StatusAlergia, p.Alergias);

        return new PacienteDto(
            p.Id,
            p.Codigo,
            p.Nome,
            p.TipoDocumento,
            p.NumeroDocumento,
            p.DataNascimento,
            idade,
            p.Sexo,
            p.StatusAlergia,
            p.Alergias,
            new AlertaAlergiaDto(alerta.DeveExibirAlerta, alerta.Texto),
            p.CondicoesCronicas,
            p.Vulnerabilidades,
            p.ConsentimentoRegistro);
    }

    public static AtendimentoResumoDto ParaResumo(Atendimento a)
    {
        var etapas = a.Etapas
            .OrderBy(e => e.CriadaEm)
            .Select(e => new EtapaResumoDto(
                e.Id,
                e.Especialidade,
                e.Status,
                e.Profissional?.Nome,
                e.IniciadaEm,
                e.ConcluidaEm))
            .ToList();

        return new AtendimentoResumoDto(
            a.Id,
            a.Codigo,
            a.Paciente?.Nome ?? "-",
            a.Status,
            a.ClassificacaoRisco,
            a.QueixaPrincipal,
            etapas,
            a.CriadoEm,
            a.FinalizadoEm);
    }

    public static ProntuarioDto ParaProntuario(Atendimento a)
    {
        var etapas = a.Etapas.OrderBy(e => e.CriadaEm).ToList();

        var triagemEtapa = etapas.FirstOrDefault(e => e.Especialidade == Especialidade.Triagem);
        var odontoEtapa = etapas.FirstOrDefault(e => e.Especialidade == Especialidade.Odontologia);
        var enfEtapa = etapas.FirstOrDefault(e => e.Especialidade == Especialidade.Enfermagem);

        var consultas = etapas
            .Where(e => e.Consulta is not null)
            .Select(ParaConsultaDto)
            .ToList();

        var tempos = TempoDeFila.Calcular(a.PassagensFila)
            .Select(e => new EsperaFilaDto(
                e.Especialidade,
                e.EntrouEm,
                e.SaiuEm,
                e.Espera is null ? null : (int)Math.Round(e.Espera.Value.TotalMinutes)))
            .ToList();

        var historico = a.Auditorias
            .OrderByDescending(x => x.CriadaEm)
            .Select(x => new AuditoriaDto(
                x.Profissional?.Nome ?? "-",
                x.Acao,
                x.Especialidade,
                x.Campo,
                x.ValorAnterior,
                x.ValorNovo,
                x.CriadaEm))
            .ToList();

        LocalizacaoDto? localizacao = a.Latitude is double lat && a.Longitude is double lon
            ? new LocalizacaoDto(lat, lon, a.PrecisaoMetros)
            : null;

        return new ProntuarioDto(
            a.Id,
            a.Codigo,
            new BaseDto(a.Base!.Id, a.Base.Nome, a.Base.PrefixoCodigo, a.Base.Ativa),
            ParaDto(a.Paciente!),
            a.Status,
            a.ClassificacaoRisco,
            a.QueixaPrincipal,
            localizacao,
            a.CriadoPor?.Nome ?? "-",
            a.CriadoEm,
            a.FinalizadoPor?.Nome,
            a.FinalizadoEm,
            triagemEtapa?.Triagem is null ? null : ParaTriagemDto(triagemEtapa),
            consultas,
            odontoEtapa?.Odontologia is null ? null : ParaOdontologiaDto(odontoEtapa),
            enfEtapa?.Enfermagem is null ? null : ParaEnfermagemDto(enfEtapa),
            tempos,
            historico,
            etapas.Select(e => new EtapaResumoDto(
                e.Id,
                e.Especialidade,
                e.Status,
                e.Profissional?.Nome,
                e.IniciadaEm,
                e.ConcluidaEm)).ToList());
    }

    private static TriagemDto ParaTriagemDto(Etapa etapa)
    {
        var t = etapa.Triagem!;

        return new TriagemDto(
            etapa.Id,
            etapa.Profissional?.Nome,
            t.PressaoSistolica,
            t.PressaoDiastolica,
            t.FrequenciaCardiaca,
            t.FrequenciaRespiratoria,
            t.SaturacaoO2,
            t.TemperaturaCelsius,
            t.GlicemiaCapilar,
            t.Sintomas,
            t.OutroSintoma,
            t.MedicamentosEmUso,
            t.StatusAlergia,
            t.Alergias,
            t.ClassificacaoRisco,
            t.Encaminhamento,
            t.Observacoes,
            etapa.ConcluidaEm);
    }

    private static ConsultaDto ParaConsultaDto(Etapa etapa)
    {
        var c = etapa.Consulta!;

        OrtopediaRequest? ortopedia = c.Ortopedia is null
            ? null
            : new OrtopediaRequest
            {
                Localizacao = c.Ortopedia.Localizacao,
                MecanismoTrauma = c.Ortopedia.MecanismoTrauma,
                Imobilizacao = c.Ortopedia.Imobilizacao,
                NecessitaRaioX = c.Ortopedia.NecessitaRaioX
            };

        return new ConsultaDto(
            etapa.Id,
            etapa.Especialidade,
            etapa.Profissional?.Nome,
            c.SintomasDescricao,
            c.Cid10Codigo,
            c.Cid10?.DescricaoPt,
            c.DiagnosticoObservacao,
            c.Conduta,
            c.Desfecho,
            c.EncaminhadoPara,
            ortopedia,
            etapa.Dispensacoes.Select(ParaDispensacaoDto).ToList(),
            etapa.ConcluidaEm);
    }

    private static OdontologiaDto ParaOdontologiaDto(Etapa etapa)
    {
        var o = etapa.Odontologia!;

        return new OdontologiaDto(
            etapa.Id,
            etapa.Profissional?.Nome,
            o.Queixa,
            o.Cid10Codigo,
            o.Cid10?.DescricaoPt,
            o.Procedimentos,
            o.OutroProcedimento,
            o.Desfecho,
            o.Marcacoes
                .OrderBy(m => m.Dente)
                .Select(m => new MarcacaoDenteDto(m.Dente, m.Estado, m.Faces))
                .ToList(),
            Odontograma.Resumir(o.Marcacoes),
            etapa.Dispensacoes.Select(ParaDispensacaoDto).ToList(),
            etapa.ConcluidaEm);
    }

    private static EnfermagemDto ParaEnfermagemDto(Etapa etapa)
    {
        var e = etapa.Enfermagem!;

        return new EnfermagemDto(
            etapa.Id,
            etapa.Profissional?.Nome,
            e.Procedimentos,
            e.OutroProcedimento,
            e.Observacoes,
            e.Desfecho,
            etapa.Dispensacoes.Select(ParaDispensacaoDto).ToList(),
            etapa.ConcluidaEm);
    }

    private static DispensacaoDto ParaDispensacaoDto(Dispensacao d)
    {
        var nome = d.Item is not null
            ? MontarNomeItem(d.Item)
            : d.DescricaoLivre ?? "-";

        return new DispensacaoDto(
            d.Id,
            nome,
            d.Quantidade,
            d.Unidade,
            d.Via,
            d.Posologia,
            d.Item is null);
    }

    public static string MontarNomeItem(ItemCatalogo item)
        => string.IsNullOrWhiteSpace(item.Concentracao)
            ? item.Nome
            : $"{item.Nome} {item.Concentracao}";

    public static ItemCatalogoDto ParaDto(ItemCatalogo i)
        => new(i.Id, i.Nome, i.PrincipioAtivo, i.Concentracao, i.Forma, i.Unidade, i.Categoria, i.ViasPermitidas);

    public static Cid10Dto ParaDto(Cid10 c, Idioma idioma)
    {
        var descricao = idioma switch
        {
            Idioma.Es => c.DescricaoEs,
            Idioma.En => c.DescricaoEn,
            _ => c.DescricaoPt
        };

        return new Cid10Dto(c.Codigo, descricao, c.Capitulo);
    }
}
