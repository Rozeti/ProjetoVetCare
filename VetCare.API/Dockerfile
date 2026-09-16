# Build em estágios: o SDK só existe na compilação, e a imagem final leva
# apenas o runtime e os binários publicados.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar primeiro o csproj aproveita o cache do restore enquanto o código muda.
COPY VetCare.API/VetCare.API.csproj VetCare.API/
RUN dotnet restore VetCare.API/VetCare.API.csproj

COPY VetCare.API/ VetCare.API/
RUN dotnet publish VetCare.API/VetCare.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl é instalado para que o HEALTHCHECK consulte o endpoint da própria
# aplicação; a imagem base do runtime não traz cliente HTTP.
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# As imagens .NET já trazem o usuário sem privilégios "app" (uid 1654). A
# aplicação roda com ele para que um comprometimento não alcance o root.
# Mídias e documentos ficam em volume e precisam pertencer a esse usuário.
RUN mkdir -p /app/wwwroot/uploads /app/wwwroot/documentos \
    && chown -R app:app /app/wwwroot

USER app

ENV ASPNETCORE_URLS=http://+:5265
EXPOSE 5265

# /health/pronto também verifica o banco, que é o que define se a API pode atender.
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl --fail --silent http://localhost:5265/health/pronto || exit 1

ENTRYPOINT ["dotnet", "VetCare.API.dll"]
