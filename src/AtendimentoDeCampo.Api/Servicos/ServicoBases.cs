using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;
using AtendimentoDeCampo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AtendimentoDeCampo.Api.Servicos;

/// <summary>
/// Cadastro das bases de atendimento, pela coordenacao.
///
/// Duas regras mandam aqui, e as duas vem do codigo de atendimento:
///
///  - O prefixo entra no codigo de cada atendimento aberto na base ("ACA-4K7Z").
///    Depois que o primeiro atendimento sai, o prefixo esta impresso em papeis
///    que a equipe ja distribuiu; troca-lo faria o papel dizer uma base e o
///    sistema dizer outra. Por isso ele so e editavel enquanto a base nao tem
///    atendimento nenhum.
///
///  - Base nao se apaga, se desativa. O historico dos atendimentos aponta para
///    ela, e apagar levaria junto o registro de onde cada pessoa foi atendida.
/// </summary>
public sealed class ServicoBases
{
    private readonly AtendimentoDbContext _db;

    public ServicoBases(AtendimentoDbContext db) => _db = db;

    public async Task<List<BaseAdminDto>> ListarAsync(CancellationToken ct = default)
        => await _db.Bases
            .AsNoTracking()
            .OrderByDescending(b => b.Ativa)
            .ThenBy(b => b.Nome)
            .Select(b => new BaseAdminDto(
                b.Id,
                b.Nome,
                b.PrefixoCodigo,
                b.Ativa,
                b.CriadaEm,
                b.Atendimentos.Count,
                b.Atendimentos.Count(a => a.Status == StatusAtendimento.Aberto),
                // Sem atendimento nenhum, nada foi impresso ainda: o prefixo
                // pode ser corrigido.
                !b.Atendimentos.Any()))
            .ToListAsync(ct);

    public async Task<BaseAdminDto> CriarAsync(SalvarBaseRequest req, CancellationToken ct = default)
    {
        var nome = NomeValidado(req.Nome);
        var prefixo = PrefixoValidado(req.PrefixoCodigo, nome);

        await ImpedirNomeRepetidoAsync(nome, null, ct);
        await ImpedirPrefixoRepetidoAsync(prefixo, null, ct);

        var criada = new Base { Nome = nome, PrefixoCodigo = prefixo, Ativa = true };

        _db.Bases.Add(criada);
        await _db.SaveChangesAsync(ct);

        return new BaseAdminDto(criada.Id, criada.Nome, criada.PrefixoCodigo, criada.Ativa,
            criada.CriadaEm, 0, 0, true);
    }

    public async Task<BaseAdminDto> AtualizarAsync(
        Guid id,
        SalvarBaseRequest req,
        CancellationToken ct = default)
    {
        var alvo = await CarregarAsync(id, ct);
        var nome = NomeValidado(req.Nome);
        var prefixo = PrefixoValidado(req.PrefixoCodigo, nome);

        await ImpedirNomeRepetidoAsync(nome, id, ct);

        var temAtendimento = await _db.Atendimentos.AnyAsync(a => a.BaseId == id, ct);

        if (prefixo != alvo.PrefixoCodigo)
        {
            if (temAtendimento)
            {
                throw new RegraDeNegocioException(
                    "O prefixo nao pode mudar: ja existem atendimentos com codigo desta base. " +
                    "Os codigos ja entregues deixariam de bater com ela.");
            }

            await ImpedirPrefixoRepetidoAsync(prefixo, id, ct);
            alvo.PrefixoCodigo = prefixo;
        }

        alvo.Nome = nome;
        await _db.SaveChangesAsync(ct);

        return new BaseAdminDto(alvo.Id, alvo.Nome, alvo.PrefixoCodigo, alvo.Ativa, alvo.CriadaEm,
            await _db.Atendimentos.CountAsync(a => a.BaseId == id, ct),
            await _db.Atendimentos.CountAsync(a => a.BaseId == id && a.Status == StatusAtendimento.Aberto, ct),
            !temAtendimento);
    }

    public async Task<BaseAdminDto> DefinirAtivaAsync(
        Guid id,
        bool ativa,
        CancellationToken ct = default)
    {
        var alvo = await CarregarAsync(id, ct);

        if (!ativa && alvo.Ativa)
        {
            var abertos = await _db.Atendimentos
                .CountAsync(a => a.BaseId == id && a.Status == StatusAtendimento.Aberto, ct);

            if (abertos > 0)
            {
                // A selecao de base so lista bases ativas: desativar agora
                // tiraria da tela atendimentos que ainda estao na fila, sem
                // caminho de volta ate alguem reativar a base.
                throw new RegraDeNegocioException(
                    abertos == 1
                        ? "Esta base tem 1 atendimento em aberto. Finalize-o antes de desativar."
                        : $"Esta base tem {abertos} atendimentos em aberto. Finalize-os antes de desativar.");
            }

            var outrasAtivas = await _db.Bases.CountAsync(b => b.Ativa && b.Id != id, ct);

            if (outrasAtivas == 0)
            {
                // Sem base ativa ninguem escolhe base, e sem base escolhida o
                // app inteiro para — inclusive esta tela.
                throw new RegraDeNegocioException(
                    "Esta e a unica base ativa. Ative outra antes de desativar esta.");
            }
        }

        alvo.Ativa = ativa;
        await _db.SaveChangesAsync(ct);

        return new BaseAdminDto(alvo.Id, alvo.Nome, alvo.PrefixoCodigo, alvo.Ativa, alvo.CriadaEm,
            await _db.Atendimentos.CountAsync(a => a.BaseId == id, ct),
            await _db.Atendimentos.CountAsync(a => a.BaseId == id && a.Status == StatusAtendimento.Aberto, ct),
            !await _db.Atendimentos.AnyAsync(a => a.BaseId == id, ct));
    }

    /// <summary>Prefixo sugerido a partir do nome, como a tela oferece enquanto se digita.</summary>
    public async Task<string> SugerirPrefixoAsync(string nome, CancellationToken ct = default)
    {
        var baseSugerida = GeradorCodigoAtendimento.DerivarPrefixo(nome ?? string.Empty);

        if (!await _db.Bases.AnyAsync(b => b.PrefixoCodigo == baseSugerida, ct))
        {
            return baseSugerida;
        }

        // Sugestao tomada: troca a ultima letra ate achar uma livre, para a tela
        // oferecer algo utilizavel em vez de um prefixo que ja vai dar erro.
        foreach (var letra in GeradorCodigoAtendimento.AlfabetoPrefixo)
        {
            var alternativa = baseSugerida[..2] + letra;

            if (!await _db.Bases.AnyAsync(b => b.PrefixoCodigo == alternativa, ct))
            {
                return alternativa;
            }
        }

        return baseSugerida;
    }

    private async Task<Base> CarregarAsync(Guid id, CancellationToken ct)
        => await _db.Bases.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new RegraDeNegocioException("Base nao encontrada.");

    private static string NomeValidado(string? nome)
    {
        var limpo = (nome ?? string.Empty).Trim();

        if (limpo.Length < 2)
        {
            throw new RegraDeNegocioException("Informe o nome da base.");
        }

        return limpo.Length > 160 ? limpo[..160] : limpo;
    }

    /// <summary>
    /// Normaliza o prefixo para tres letras A-Z. Vazio cai na derivacao do nome,
    /// que e o que a tela ja sugere: quem nao mexeu no campo nao deve ser
    /// obrigado a inventar um.
    /// </summary>
    private static string PrefixoValidado(string? prefixo, string nome)
    {
        if (string.IsNullOrWhiteSpace(prefixo))
        {
            return GeradorCodigoAtendimento.DerivarPrefixo(nome);
        }

        var limpo = new string(prefixo.ToUpperInvariant()
            .Where(GeradorCodigoAtendimento.AlfabetoPrefixo.Contains)
            .ToArray());

        if (limpo.Length != 3)
        {
            throw new RegraDeNegocioException("O prefixo deve ter exatamente 3 letras de A a Z.");
        }

        return limpo;
    }

    private async Task ImpedirPrefixoRepetidoAsync(string prefixo, Guid? exceto, CancellationToken ct)
    {
        var emUso = await _db.Bases
            .AnyAsync(b => b.PrefixoCodigo == prefixo && (exceto == null || b.Id != exceto), ct);

        if (emUso)
        {
            throw new RegraDeNegocioException(
                $"O prefixo {prefixo} ja pertence a outra base. Dois codigos iguais apontariam para lugares diferentes.");
        }
    }

    /// <summary>
    /// Nomes repetidos nao quebram nada no banco, mas a selecao de base vira uma
    /// lista com duas entradas identicas e a equipe escolhe a errada.
    /// </summary>
    private async Task ImpedirNomeRepetidoAsync(string nome, Guid? exceto, CancellationToken ct)
    {
        var emUso = await _db.Bases.AnyAsync(
            b => b.Nome.ToLower() == nome.ToLower() && (exceto == null || b.Id != exceto), ct);

        if (emUso)
        {
            throw new RegraDeNegocioException("Ja existe uma base com esse nome.");
        }
    }
}
