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
/// Autenticacao e registro.
///
/// Os dois fluxos sao separados e explicitos. Antes eram o mesmo: se o nome nao
/// existisse e a senha batesse com a da equipe, a conta era criada em silencio —
/// o que fazia um erro de digitacao no nome virar uma conta nova em vez de um
/// erro de login.
///
/// Agora quem se registra fica <see cref="StatusConta.Pendente"/> e nao acessa
/// nada ate um administrador aprovar. Num prontuario isso vale o atrito: cada
/// ato clinico fica atribuido a uma pessoa, e a aprovacao e o momento em que
/// alguem confirma que essa pessoa e quem diz ser.
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
    // Registro
    // -----------------------------------------------------------------------

    public async Task<ProfissionalDto> RegistrarAsync(RegistroRequest req, CancellationToken ct = default)
    {
        var usuario = NomeDeUsuario.Normalizar(req.Usuario);
        var erros = new List<string>(NomeDeUsuario.Validar(req.Usuario));

        erros.AddRange(PoliticaDeSenha.Validar(req.Senha, usuario, req.Nome));

        if (req.Senha != req.ConfirmacaoSenha)
        {
            erros.Add("As senhas nao conferem.");
        }

        if (string.IsNullOrWhiteSpace(req.Nome) || req.Nome.Trim().Length < 3)
        {
            erros.Add("Informe o nome completo.");
        }

        var conselho = ConselhoPara(req.Funcao);

        if (conselho != ConselhoTipo.Nenhum && string.IsNullOrWhiteSpace(req.Registro))
        {
            erros.Add($"Registro no {conselho} e obrigatorio para esta funcao.");
        }

        if (erros.Count > 0)
        {
            throw new RegraDeNegocioException(erros);
        }

        if (await _db.Profissionais.AnyAsync(p => p.Usuario == usuario, ct))
        {
            throw new RegraDeNegocioException("Este usuario ja esta em uso. Escolha outro.");
        }

        var profissional = new Profissional
        {
            Usuario = usuario,
            Nome = req.Nome.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Funcao = req.Funcao,
            ConselhoTipo = conselho,
            Registro = req.Registro?.Trim(),
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.Senha),
            Idioma = req.Idioma,
            Status = StatusConta.Pendente
        };

        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync(ct);

        return ParaDto(profissional);
    }

    /// <summary>Verifica disponibilidade do usuario enquanto a pessoa digita.</summary>
    public async Task<bool> UsuarioDisponivelAsync(string usuario, CancellationToken ct = default)
    {
        var normalizado = NomeDeUsuario.Normalizar(usuario);

        if (NomeDeUsuario.Validar(usuario).Count > 0)
        {
            return false;
        }

        return !await _db.Profissionais.AnyAsync(p => p.Usuario == normalizado, ct);
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

    /// <summary>Conselho profissional esperado para cada funcao.</summary>
    public static ConselhoTipo ConselhoPara(FuncaoProfissional funcao) => funcao switch
    {
        FuncaoProfissional.Medico => ConselhoTipo.Crm,
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
        FilasDaFuncao.De(p.Funcao).ToList());

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
