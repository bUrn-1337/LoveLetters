FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LoveLetters.csproj ./
RUN dotnet restore

COPY src/ ./
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

RUN chmod 1777 /tmp

RUN adduser --disabled-password --gecos "" ctfuser

RUN chown -R root:root /app && \
    chmod -R 444 /app/*.dll && \
    chmod 555 /app && \
    chown -R ctfuser:ctfuser /tmp

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV CTFD_URL=https://noobctf.infoseciitr.in
ENV CHALLENGE_ID=666

USER ctfuser

EXPOSE 8080

ENTRYPOINT ["dotnet", "LoveLetters.dll"]

