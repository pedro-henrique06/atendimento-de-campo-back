using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;
using AtendimentoDeCampo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AtendimentoDeCampo.Api.Servicos;

/// <summary>Motivo pelo qual um login foi recusado, para a tela poder explicar.</summary>
public enum MotivoRecusaLogin
{
    CredenciaisInvalidas = 0,
    ContaPendente = 1,
    ContaRecusada = 2,
    ContaDesativada = 3
}

public sealed record ResultadoLogin(
    bool Sucesso,
    LoginResponse? Resposta,
    MotivoRecusaLogin? Motivo = null,
    string? Detalhe = null);

/// <summary>
/// Autenticacao.
///
/// Nao existe auto-registro: a conta e criada pela coordenacao
/// (<see cref="ServicoProfissionais.CriarAsync"/>) e ja nasce ativa, porque quem
/// cria e quem aprovaria. Num prontuario isso e o que sustenta a atribuicao —
/// cada ato clinico fica no nome de uma pessoa, e o cadastro e o momento em que
/// alguem responde por essa pessoa ser quem diz ser.
///
/// A senha do primeiro acesso e sorteada e a coordenacao a conhece. Por isso
/// existe <see cref="Profissional.PrecisaTrocarSenha"/>: ate a troca a conta
/// entra, mas nao faz mais nada.
/// </summary>
public sealed class ServicoAutenticacao
{
    private readonly AtendimentoDbContext _db;
    private readonly IConfiguration _config;

    public ServicoAutenticacao(AtendimentoDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // -----------------------------------------------------------------------
    // Troca de senha
    // -----------------------------------------------------------------------

    /// <summary>
    /// Troca a propria senha. Exige a atual mesmo quando a pessoa ja esta
    /// autenticada: sem isso, um aparelho deixado destravado no meio do plantao
    /// vira uma conta tomada.
    /// </summary>
    /// <remarks>
    /// Devolve um token novo porque o antigo carrega
    /// <c>precisa_trocar_senha</c>, e e essa claim que o gate do
    /// <c>Program.cs</c> usa para barrar o resto da API. Sem trocar o token, a
    /// pessoa trocaria a senha e continuaria presa na mesma tela.
    /// </remarks>
    public async Task<LoginResponse> TrocarSenhaAsync(
        Guid profissionalId,
        TrocarSenhaRequest req,
        CancellationToken ct = default)
    {
        var profissional = await _db.Profissionais.FirstOrDefaultAsync(p => p.Id == profissionalId, ct)
            ?? throw new RegraDeNegocioException("Profissional nao encontrado.");

        if (!BCrypt.Net.BCrypt.Verify(req.SenhaAtual, profissional.SenhaHash))
        {
            throw new RegraDeNegocioException("A senha atual esta incorreta.");
        }

        var erros = new List<string>(
            PoliticaDeSenha.Validar(req.NovaSenha, profissional.Usuario, profissional.Nome));

        if (req.NovaSenha != req.ConfirmacaoSenha)
        {
            erros.Add("As senhas nao conferem.");
        }

        // Repetir a provisoria deixaria a conta exatamente onde estava: com a
        // coordenacao sabendo a senha.
        if (BCrypt.Net.BCrypt.Verify(req.NovaSenha, profissional.SenhaHash))
        {
            erros.Add("A nova senha precisa ser diferente da atual.");
        }

        if (erros.Count > 0)
        {
            throw new RegraDeNegocioException(erros);
        }

        profissional.SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.NovaSenha);
        profissional.PrecisaTrocarSenha = false;

        await _db.SaveChangesAsync(ct);

        var (token, expira) = GerarToken(profissional);

        return new LoginResponse(token, expira, ParaDto(profissional));
    }

    // -----------------------------------------------------------------------
    // Login
    // -----------------------------------------------------------------------

    public async Task<ResultadoLogin> AutenticarAsync(LoginRequest req, CancellationToken ct = default)
    {
        var usuario = NomeDeUsuario.Normalizar(req.Usuario);

        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(req.Senha))
        {
            return new ResultadoLogin(false, null, MotivoRecusaLogin.CredenciaisInvalidas);
        }

        var profissional = await _db.Profissionais.FirstOrDefaultAsync(p => p.Usuario == usuario, ct);

        // Mesma resposta para usuario inexistente e senha errada, de proposito:
        // nao adianta esconder o resto se o login revela quem tem conta.
        if (profissional is null || !BCrypt.Net.BCrypt.Verify(req.Senha, profissional.SenhaHash))
        {
            return new ResultadoLogin(false, null, MotivoRecusaLogin.CredenciaisInvalidas);
        }

        // A partir daqui a pessoa provou quem e, entao explicar a situacao da
        // conta nao vaza nada — e sem isso ela ficaria tentando de novo achando
        // que errou a senha.
        switch (profissional.Status)
        {
            case StatusConta.Pendente:
                return new ResultadoLogin(false, null, MotivoRecusaLogin.ContaPendente);

            case StatusConta.Recusada:
                return new ResultadoLogin(
                    false, null, MotivoRecusaLogin.ContaRecusada, profissional.MotivoRecusa);

            case StatusConta.Desativada:
                return new ResultadoLogin(false, null, MotivoRecusaLogin.ContaDesativada);
        }

        if (profissional.Idioma != req.Idioma)
        {
            profissional.Idioma = req.Idioma;
            await _db.SaveChangesAsync(ct);
        }

        var (token, expira) = GerarToken(profissional);

        return new ResultadoLogin(
            true,
            new LoginResponse(token, expira, ParaDto(profissional)));
    }

    // -----------------------------------------------------------------------
    // Apoio
    // -----------------------------------------------------------------------

    /// <summary>Conselho profissional esperado para cada profissao.</summary>
    public static ConselhoTipo ConselhoPara(FuncaoProfissional funcao) => funcao switch
    {
        FuncaoProfissional.Medico => ConselhoTipo.Crm,
        FuncaoProfissional.ClinicoGeral => ConselhoTipo.Crm,
        FuncaoProfissional.Pediatra => ConselhoTipo.Crm,
        FuncaoProfissional.Ortopedista => ConselhoTipo.Crm,
        FuncaoProfissional.Enfermeiro => ConselhoTipo.Coren,
        FuncaoProfissional.TecnicoEnfermagem => ConselhoTipo.Coren,
        FuncaoProfissional.Dentista => ConselhoTipo.Cro,
        FuncaoProfissional.Psicologo => ConselhoTipo.Crp,
        FuncaoProfissional.Fisioterapeuta => ConselhoTipo.Crefito,
        FuncaoProfissional.Farmaceutico => ConselhoTipo.Crf,
        _ => ConselhoTipo.Nenhum
    };

    public static ProfissionalDto ParaDto(Profissional p) => new(
        p.Id,
        p.Usuario,
        p.Nome,
        p.Email,
        p.Funcao,
        p.ConselhoTipo,
        p.Registro,
        p.Idioma,
        p.Status,
        p.EhAdministrador,
        p.MotivoRecusa,
        p.CriadoEm,
        FilasDaFuncao.De(p.Funcao).ToList(),
        p.PrecisaTrocarSenha);

    private (string Token, DateTime Expira) GerarToken(Profissional profissional)
    {
        var chave = _config["Jwt:Chave"]
            ?? throw new InvalidOperationException("Jwt:Chave nao configurada.");

        var horas = int.TryParse(_config["Jwt:HorasValidade"], out var h) ? h : 12;
        var expira = DateTime.UtcNow.AddHours(horas);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, profissional.Id.ToString()),
            new(ClaimTypes.Name, profissional.Nome),
            new("usuario", profissional.Usuario),
            new("funcao", profissional.Funcao.ToString()),
            new("idioma", profissional.Idioma.ToString())
        };

        // A permissao de administrador viaja como role para que os controllers
        // possam exigi-la com [Authorize(Roles = ...)].
        if (profissional.EhAdministrador)
        {
            claims.Add(new Claim(ClaimTypes.Role, Papeis.Administrador));
        }

        // Enquanto a senha for a provisoria que a coordenacao entregou, o token
        // carrega a marca e o gate no Program.cs recusa tudo menos a troca.
        if (profissional.PrecisaTrocarSenha)
        {
            claims.Add(new Claim(Claims.PrecisaTrocarSenha, "true"));
        }

        var credenciais = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Emissor"],
            audience: _config["Jwt:Audiencia"],
            claims: claims,
            expires: expira,
            signingCredentials: credenciais);

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}

/// <summary>Papeis usados na autorizacao.</summary>
public static class Papeis
{
    public const string Administrador = "Administrador";
}

/// <summary>Claims proprias do token, alem das padrao do JWT.</summary>
public static class Claims
{
    /// <summary>A senha ainda e a provisoria. Ver o gate no <c>Program.cs</c>.</summary>
    public const string PrecisaTrocarSenha = "precisa_trocar_senha";
}
