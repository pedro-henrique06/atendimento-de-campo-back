using System.Text;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Servicos;
using AtendimentoDeCampo.Infrastructure;
using AtendimentoDeCampo.Infrastructure.Servicos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AtendimentoDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<RegistradorAuditoria>();
builder.Services.AddScoped<ServicoAtendimento>();
builder.Services.AddScoped<ServicoAutenticacao>();

builder.Services
    .AddControllers()
    .AddJsonOptions(opt =>
    {
        // Enums viajam como texto: o front trabalha com "Vermelho", nao com 0,
        // e um valor novo no meio do enum nao muda o significado do payload.
        opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// A chave e lida de IConfiguration resolvida do container, e nao de
// builder.Configuration antes do Build(). Ler cedo congela o valor do
// appsettings e ignora qualquer fonte adicionada depois (host de teste,
// user-secrets, cofre de segredos). Como quem assina o token le a configuracao
// final, as duas pontas passariam a usar chaves diferentes e todo request
// autenticado voltaria 401 sem explicacao.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((opt, config) =>
    {
        var chave = config["Jwt:Chave"]
            ?? throw new InvalidOperationException("Configure Jwt:Chave.");

        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["Jwt:Emissor"],
            ValidAudience = config["Jwt:Audiencia"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave))
        };
    });

builder.Services.AddAuthorization();

const string PoliticaCors = "front";
builder.Services.AddCors(opt => opt.AddPolicy(PoliticaCors, p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origens").Get<string[]>() ?? Array.Empty<string>())
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Migrations e seed no boot. Em campo o deploy costuma ser feito por quem esta
// operando, sem passo manual de banco.
if (builder.Configuration.GetValue("Banco:MigrarNoBoot", true))
{
    using var escopo = app.Services.CreateScope();
    var db = escopo.ServiceProvider.GetRequiredService<AtendimentoDbContext>();
    await db.Database.MigrateAsync();
    await Seed.ExecutarAsync(db);
}

// Regra de negocio vira 400 com a lista de erros, que e o formato que o
// formulario do front sabe exibir campo a campo.
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (RegraDeNegocioException ex)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsJsonAsync(new { erros = ex.Erros });
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(PoliticaCors);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

/// <summary>Exposta para os testes de integracao com WebApplicationFactory.</summary>
public partial class Program;
