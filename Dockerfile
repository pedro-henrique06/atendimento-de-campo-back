# Build em duas etapas: a imagem final carrega so o runtime, sem o SDK.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /origem

# Copiar so os csproj primeiro faz o restore virar camada de cache: mudanca de
# codigo nao invalida o download dos pacotes.
COPY AtendimentoDeCampo.sln .
COPY src/AtendimentoDeCampo.Domain/AtendimentoDeCampo.Domain.csproj src/AtendimentoDeCampo.Domain/
COPY src/AtendimentoDeCampo.Infrastructure/AtendimentoDeCampo.Infrastructure.csproj src/AtendimentoDeCampo.Infrastructure/
COPY src/AtendimentoDeCampo.Api/AtendimentoDeCampo.Api.csproj src/AtendimentoDeCampo.Api/
COPY tests/AtendimentoDeCampo.Tests/AtendimentoDeCampo.Tests.csproj tests/AtendimentoDeCampo.Tests/
RUN dotnet restore src/AtendimentoDeCampo.Api/AtendimentoDeCampo.Api.csproj

COPY . .
RUN dotnet publish src/AtendimentoDeCampo.Api/AtendimentoDeCampo.Api.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Usuario sem privilegio: a aplicacao nao precisa de root para nada.
RUN adduser --disabled-password --gecos "" --uid 5000 aplicacao \
    && chown -R aplicacao /app
USER aplicacao

COPY --from=build /app .

# A porta real vem de PORT em tempo de execucao; este EXPOSE e so documentacao.
EXPOSE 8080

ENTRYPOINT ["dotnet", "AtendimentoDeCampo.Api.dll"]
