# Servidor de licenças. Construa a partir da raiz do repositório:
#   docker build -f src/LicencasServidor/Dockerfile -t licencas-pdv .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Licenciamento.Comum/ src/Licenciamento.Comum/
COPY src/LicencasServidor/ src/LicencasServidor/
RUN dotnet publish src/LicencasServidor -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# Banco, chaves de sessão e (opcionalmente) a chave privada ficam no volume /dados.
RUN mkdir /dados && chown app:app /dados
ENV ASPNETCORE_URLS=http://+:8080 \
    Dados__Pasta=/dados
VOLUME /dados
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "LicencasServidor.dll"]
