using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;
using AtendimentoDeCampo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AtendimentoDeCampo.Api.Servicos;

/// <summary>
/// Cria o primeiro administrador a partir da configuracao.
///
/// Sem isso o sistema nasce travado: toda conta nova fica pendente e nao ha
/// ninguem para aprovar — nem a primeira.
///
/// Deliberadamente NAO existe um administrador padrao embutido. Um usuario
/// "admin" com senha conhecida num sistema publico e uma porta aberta, e seria
/// pior que o problema que resolve. Se a configuracao nao estiver preenchida, o
/// boot apenas registra um aviso e segue.
/// </summary>
public static class AdministradorInicial
{
    public static async Task GarantirAsync(
        AtendimentoDbContext db,
        IConfiguration config,
        ILogger logger,
        CancellationToken ct = default)
    {
        var usuario = NomeDeUsuario.Normalizar(config["Admin:Usuario"]);
        var senha = config["Admin:Senha"];
        var nome = config["Admin:Nome"] ?? "Administrador";

        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(senha))
        {
            if (!await db.Profissionais.AnyAsync(p => p.EhAdministrador, ct))
            {
                logger.LogWarning(
                    "Nenhum administrador cadastrado e Admin:Usuario/Admin:Senha nao configurados. " +
                    "Contas novas ficarao pendentes sem ninguem para aprovar.");
            }

            return;
        }

        var existente = await db.Profissionais.FirstOrDefaultAsync(p => p.Usuario == usuario, ct);

        if (existente is not null)
        {
            // Recupera o acesso se a conta foi desativada ou perdeu a permissao,
            // mas nao mexe na senha: trocar a senha a cada boot atrapalharia
            // quem ja usa a conta no dia a dia.
            var mudou = false;

            if (!existente.EhAdministrador)
            {
                existente.EhAdministrador = true;
                mudou = true;
            }

            if (existente.Status != StatusConta.Ativa)
            {
                existente.Status = StatusConta.Ativa;
                mudou = true;
            }

            if (mudou)
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Acesso de administrador restaurado para '{Usuario}'.", usuario);
            }

            return;
        }

        db.Profissionais.Add(new Profissional
        {
            Usuario = usuario,
            Nome = nome,
            Funcao = FuncaoProfissional.Coordenacao,
            ConselhoTipo = ConselhoTipo.Nenhum,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(senha),
            Status = StatusConta.Ativa,
            EhAdministrador = true
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Administrador inicial '{Usuario}' criado.", usuario);
    }
}
