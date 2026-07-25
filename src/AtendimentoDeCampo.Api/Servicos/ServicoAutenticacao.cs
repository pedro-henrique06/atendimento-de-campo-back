using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AtendimentoDeCampo.Api.Servicos;

public sealed record ResultadoLogin(bool Sucesso, LoginResponse? Resposta, string? Erro);

/// <summary>
/// Autenticacao de campo.
///
/// O fluxo espelha o do sistema de referencia: o profissional entra com nome,
/// funcao, registro do conselho e senha. No primeiro acesso ele usa a senha da
/// equipe, e a conta e criada naquele momento com essa mesma senha. Como o
/// login acontece antes da escolha da base, a senha da equipe e global, vinda
/// da configuracao.
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

    public async Task<ResultadoLogin> AutenticarAsync(LoginRequest req, CancellationToken ct = default)
    {
        var nome = req.Nome.Trim();

        if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(req.Senha))
        {
            return new ResultadoLogin(false, null, "nome ou senha invalidos");
        }

        var profissional = await _db.Profissionais
            .FirstOrDefaultAsync(p => p.Nome == nome && p.Funcao == req.Funcao, ct);

        if (profissional is null)
        {
            return await PrimeiroAcessoAsync(req, nome, ct);
        }

        if (!profissional.Ativo)
        {
            // Mensagem generica de proposito: nao revela se a conta existe.
            return new ResultadoLogin(false, null, "nome ou senha invalidos");
        }

        if (!BCrypt.Net.BCrypt.Verify(req.Senha, profissional.SenhaHash))
        {
            return new ResultadoLogin(false, null, "nome ou senha invalidos");
        }

        // Registro e idioma podem mudar entre plantoes; o login e um bom momento
        // para atualizar sem exigir uma tela de perfil.
        if (!string.IsNullOrWhiteSpace(req.Registro))
        {
            profissional.Registro = req.Registro.Trim();
            profissional.ConselhoTipo = ConselhoPara(req.Funcao);
        }

        profissional.Idioma = req.Idioma;
        await _db.SaveChangesAsync(ct);

        return new ResultadoLogin(true, GerarResposta(profissional, contaCriadaAgora: false), null);
    }

    private async Task<ResultadoLogin> PrimeiroAcessoAsync(
        LoginRequest req,
        string nome,
        CancellationToken ct)
    {
        var senhaEquipe = _config["Auth:SenhaEquipe"];

        if (string.IsNullOrWhiteSpace(senhaEquipe) || req.Senha != senhaEquipe)
        {
            return new ResultadoLogin(false, null, "nome ou senha invalidos");
        }

        var conselho = ConselhoPara(req.Funcao);

        if (conselho != ConselhoTipo.Nenhum && string.IsNullOrWhiteSpace(req.Registro))
        {
            return new ResultadoLogin(false, null, $"registro ({conselho}) e obrigatorio para esta funcao");
        }

        var profissional = new Profissional
        {
            Nome = nome,
            Funcao = req.Funcao,
            ConselhoTipo = conselho,
            Registro = req.Registro?.Trim(),
            // A senha da equipe vira a senha pessoal inicial, como no sistema
            // original ("Se ja tem conta, use a sua senha").
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.Senha),
            Idioma = req.Idioma
        };

        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync(ct);

        return new ResultadoLogin(true, GerarResposta(profissional, contaCriadaAgora: true), null);
    }

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

    private LoginResponse GerarResposta(Profissional profissional, bool contaCriadaAgora)
    {
        var (token, expira) = GerarToken(profissional);

        var dto = new ProfissionalDto(
            profissional.Id,
            profissional.Nome,
            profissional.Funcao,
            profissional.ConselhoTipo,
            profissional.Registro,
            profissional.Idioma);

        return new LoginResponse(token, expira, dto, contaCriadaAgora);
    }

    private (string Token, DateTime Expira) GerarToken(Profissional profissional)
    {
        var chave = _config["Jwt:Chave"]
            ?? throw new InvalidOperationException("Jwt:Chave nao configurada.");

        var horas = int.TryParse(_config["Jwt:HorasValidade"], out var h) ? h : 12;
        var expira = DateTime.UtcNow.AddHours(horas);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, profissional.Id.ToString()),
            new Claim(ClaimTypes.Name, profissional.Nome),
            new Claim("funcao", profissional.Funcao.ToString()),
            new Claim("idioma", profissional.Idioma.ToString())
        };

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
