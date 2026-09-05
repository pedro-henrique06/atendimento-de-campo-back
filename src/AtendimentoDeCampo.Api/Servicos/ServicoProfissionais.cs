using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;
using AtendimentoDeCampo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AtendimentoDeCampo.Api.Servicos;

/// <summary>
/// Gestao de contas pela coordenacao: criar, reclassificar a profissao,
/// redefinir senha, desativar e conceder permissao de administracao.
///
/// Criar aqui e o unico caminho de entrada no sistema — nao existe auto-registro.
/// Aprovar e recusar continuam para as contas que se registraram antes dessa
/// mudanca e ficaram pendentes.
/// </summary>
public sealed class ServicoProfissionais
{
    private readonly AtendimentoDbContext _db;

    public ServicoProfissionais(AtendimentoDbContext db) => _db = db;

    /// <summary>
    /// Cria a conta e devolve a senha do primeiro acesso.
    ///
    /// A conta ja nasce <see cref="StatusConta.Ativa"/>: quem cria e quem
    /// aprovaria, e deixar pendente exigiria que a coordenacao aprovasse o
    /// proprio cadastro.
    /// </summary>
    public async Task<ContaCriadaDto> CriarAsync(
        CriarContaRequest req,
        Guid administradorId,
        CancellationToken ct = default)
    {
        var usuario = NomeDeUsuario.Normalizar(req.Usuario);
        var erros = new List<string>(NomeDeUsuario.Validar(req.Usuario));

        if (string.IsNullOrWhiteSpace(req.Nome) || req.Nome.Trim().Length < 3)
        {
            erros.Add("Informe o nome completo.");
        }

        erros.AddRange(ValidarProfissao(req.Funcao, req.Registro));

        if (erros.Count > 0)
        {
            throw new RegraDeNegocioException(erros);
        }

        if (await _db.Profissionais.AnyAsync(p => p.Usuario == usuario, ct))
        {
            throw new RegraDeNegocioException("Este usuario ja esta em uso. Escolha outro.");
        }

        var senha = SenhaProvisoria.Gerar();

        var profissional = new Profissional
        {
            Usuario = usuario,
            Nome = req.Nome.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Funcao = req.Funcao,
            ConselhoTipo = ServicoAutenticacao.ConselhoPara(req.Funcao),
            Registro = req.Registro?.Trim(),
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(senha),
            Idioma = req.Idioma,
            Status = StatusConta.Ativa,
            PrecisaTrocarSenha = true,
            CriadaPorId = administradorId
        };

        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync(ct);

        // A senha em claro sai daqui e nao volta: so o hash e gravado.
        return new ContaCriadaDto(ServicoAutenticacao.ParaDto(profissional), senha);
    }

    /// <summary>
    /// Muda a profissao — e, com ela, a fila que a pessoa passa a ver.
    ///
    /// Existe principalmente para reclassificar quem foi cadastrado como
    /// "medico" antes de clinico geral, pediatra e ortopedista serem profissoes
    /// separadas.
    /// </summary>
    public async Task<ProfissionalDto> AlterarProfissaoAsync(
        Guid id,
        FuncaoProfissional funcao,
        string? registro,
        CancellationToken ct = default)
    {
        var profissional = await CarregarAsync(id, ct);
        var conselho = ServicoAutenticacao.ConselhoPara(funcao);

        // O registro so precisa vir de novo quando muda de conselho: trocar
        // clinico geral por pediatra mantem o mesmo CRM.
        var registroFinal = string.IsNullOrWhiteSpace(registro)
            ? (conselho == profissional.ConselhoTipo ? profissional.Registro : null)
            : registro.Trim();

        var erros = ValidarProfissao(funcao, registroFinal);

        if (erros.Count > 0)
        {
            throw new RegraDeNegocioException(erros);
        }

        profissional.Funcao = funcao;
        profissional.ConselhoTipo = conselho;
        profissional.Registro = registroFinal;

        await _db.SaveChangesAsync(ct);

        return ServicoAutenticacao.ParaDto(profissional);
    }

    /// <summary>
    /// Sorteia uma senha provisoria nova, para quem perdeu a de acesso.
    ///
    /// Sem isto uma senha esquecida deixaria a conta inutil para sempre: em
    /// campo nao ha e-mail de recuperacao, e boa parte das contas nem tem
    /// e-mail cadastrado.
    /// </summary>
    public async Task<ContaCriadaDto> RedefinirSenhaAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var profissional = await CarregarAsync(id, ct);
        var senha = SenhaProvisoria.Gerar();

        profissional.SenhaHash = BCrypt.Net.BCrypt.HashPassword(senha);
        profissional.PrecisaTrocarSenha = true;

        await _db.SaveChangesAsync(ct);

        return new ContaCriadaDto(ServicoAutenticacao.ParaDto(profissional), senha);
    }

    /// <summary>Verifica disponibilidade do usuario enquanto a coordenacao digita.</summary>
    public async Task<bool> UsuarioDisponivelAsync(string usuario, CancellationToken ct = default)
    {
        var normalizado = NomeDeUsuario.Normalizar(usuario);

        if (NomeDeUsuario.Validar(usuario).Count > 0)
        {
            return false;
        }

        return !await _db.Profissionais.AnyAsync(p => p.Usuario == normalizado, ct);
    }

    private static List<string> ValidarProfissao(FuncaoProfissional funcao, string? registro)
    {
        var erros = new List<string>();
        var conselho = ServicoAutenticacao.ConselhoPara(funcao);

        if (conselho != ConselhoTipo.Nenhum && string.IsNullOrWhiteSpace(registro))
        {
            erros.Add($"Registro no {conselho} e obrigatorio para esta profissao.");
        }

        // "Medico" sem especialidade existe so para as contas anteriores as
        // especialidades. Aceitar em cadastro novo recriaria justamente o
        // problema que a separacao resolve: alguem que cai em tres filas.
        if (funcao == FuncaoProfissional.Medico)
        {
            erros.Add(
                "Escolha a especialidade: clinico geral, pediatra ou ortopedista. " +
                "\"Medico\" existe apenas para contas antigas.");
        }

        return erros;
    }

    public async Task<List<ProfissionalDto>> ListarAsync(
        StatusConta? status,
        string? busca,
        CancellationToken ct = default)
    {
        var query = _db.Profissionais.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            query = query.Where(p =>
                EF.Functions.ILike(p.Nome, $"%{termo}%") ||
                EF.Functions.ILike(p.Usuario, $"%{termo}%") ||
                (p.Registro != null && EF.Functions.ILike(p.Registro, $"%{termo}%")));
        }

        var lista = await query
            // Pendentes primeiro: e a fila de trabalho do administrador.
            .OrderBy(p => p.Status == StatusConta.Pendente ? 0 : 1)
            .ThenByDescending(p => p.CriadoEm)
            .Take(200)
            .ToListAsync(ct);

        return lista.Select(ServicoAutenticacao.ParaDto).ToList();
    }

    public async Task<int> ContarPendentesAsync(CancellationToken ct = default)
        => await _db.Profissionais.CountAsync(p => p.Status == StatusConta.Pendente, ct);

    public async Task<ProfissionalDto> AprovarAsync(
        Guid id,
        Guid administradorId,
        CancellationToken ct = default)
    {
        var profissional = await CarregarAsync(id, ct);

        if (profissional.Status == StatusConta.Ativa)
        {
            throw new RegraDeNegocioException("Esta conta ja esta ativa.");
        }

        profissional.Status = StatusConta.Ativa;
        profissional.MotivoRecusa = null;
        profissional.RevisadoPorId = administradorId;
        profissional.RevisadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ServicoAutenticacao.ParaDto(profissional);
    }

    public async Task<ProfissionalDto> RecusarAsync(
        Guid id,
        string? motivo,
        Guid administradorId,
        CancellationToken ct = default)
    {
        var profissional = await CarregarAsync(id, ct);

        if (string.IsNullOrWhiteSpace(motivo))
        {
            // Sem motivo a pessoa fica sem saber se errou algum dado ou se foi
            // recusada de proposito, e volta a tentar criar conta.
            throw new RegraDeNegocioException("Informe o motivo da recusa.");
        }

        ImpedirAutoAlteracao(id, administradorId, "recusar a propria conta");

        profissional.Status = StatusConta.Recusada;
        profissional.MotivoRecusa = motivo.Trim();
        profissional.RevisadoPorId = administradorId;
        profissional.RevisadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ServicoAutenticacao.ParaDto(profissional);
    }

    public async Task<ProfissionalDto> DesativarAsync(
        Guid id,
        Guid administradorId,
        CancellationToken ct = default)
    {
        var profissional = await CarregarAsync(id, ct);

        ImpedirAutoAlteracao(id, administradorId, "desativar a propria conta");
        await ImpedirRemoverUltimoAdministradorAsync(profissional, ct);

        profissional.Status = StatusConta.Desativada;
        profissional.RevisadoPorId = administradorId;
        profissional.RevisadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ServicoAutenticacao.ParaDto(profissional);
    }

    public async Task<ProfissionalDto> DefinirAdministradorAsync(
        Guid id,
        bool ehAdministrador,
        Guid administradorId,
        CancellationToken ct = default)
    {
        var profissional = await CarregarAsync(id, ct);

        if (!ehAdministrador)
        {
            ImpedirAutoAlteracao(id, administradorId, "remover a propria permissao de administrador");
            await ImpedirRemoverUltimoAdministradorAsync(profissional, ct);
        }

        if (ehAdministrador && profissional.Status != StatusConta.Ativa)
        {
            throw new RegraDeNegocioException("Aprove a conta antes de torna-la administradora.");
        }

        profissional.EhAdministrador = ehAdministrador;
        await _db.SaveChangesAsync(ct);

        return ServicoAutenticacao.ParaDto(profissional);
    }

    private async Task<Profissional> CarregarAsync(Guid id, CancellationToken ct)
        => await _db.Profissionais.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new RegraDeNegocioException("Profissional nao encontrado.");

    private static void ImpedirAutoAlteracao(Guid id, Guid administradorId, string acao)
    {
        if (id == administradorId)
        {
            throw new RegraDeNegocioException($"Voce nao pode {acao}.");
        }
    }

    /// <summary>
    /// Sem administrador ativo ninguem aprova mais nada, e o sistema trava sem
    /// caminho de volta pela interface.
    /// </summary>
    private async Task ImpedirRemoverUltimoAdministradorAsync(Profissional alvo, CancellationToken ct)
    {
        if (!alvo.EhAdministrador)
        {
            return;
        }

        var outros = await _db.Profissionais.CountAsync(
            p => p.EhAdministrador && p.Status == StatusConta.Ativa && p.Id != alvo.Id,
            ct);

        if (outros == 0)
        {
            throw new RegraDeNegocioException(
                "Este e o unico administrador ativo. Promova outro antes de remove-lo.");
        }
    }
}
