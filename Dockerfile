FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props ./
COPY src/ ./src/
RUN dotnet restore src/OficinaMecanica.Ordens.Api/OficinaMecanica.Ordens.Api.csproj
RUN dotnet publish src/OficinaMecanica.Ordens.Api/OficinaMecanica.Ordens.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates wget \
    && case "$(dpkg --print-architecture)" in \
         arm64) checksum=15aed4f6d96e61bbda40fc40f673ef9104512b6729ddb0236521f0fd2f055230 ;; \
         amd64) checksum=61783ee01b1e0fdbb36c73f82522592a4c0ac8a03c41a1caab16cc11f7782a05 ;; \
         *) exit 1 ;; \
       esac \
    && wget -q "https://download.newrelic.com/dot_net_agent/previous_releases/10.57.0/newrelic-dotnet-agent_10.57.0_$(dpkg --print-architecture).deb" -O /tmp/newrelic.deb \
    && printf '%s  /tmp/newrelic.deb\n' "$checksum" | sha256sum --check --strict - \
    && apt-get install -y --no-install-recommends /tmp/newrelic.deb \
    && rm -rf /var/lib/apt/lists/* /tmp/newrelic.deb
WORKDIR /app
COPY --from=build /app/publish .
COPY observability/newrelic-instrumentation.xml /usr/local/newrelic-dotnet-agent/extensions/oficina.xml
ENV CORECLR_ENABLE_PROFILING=0 \
    CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
    CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
    CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
    NEW_RELIC_LOG_DIRECTORY=/tmp/newrelic \
    NEW_RELIC_DISTRIBUTED_TRACING_ENABLED=true \
    NEW_RELIC_TRANSACTION_TRACER_RECORD_SQL=off \
    NEW_RELIC_ATTRIBUTES_EXCLUDE=request.headers.*,request.parameters.*
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "OficinaMecanica.Ordens.Api.dll"]
