# Stage 1: Copy source files
FROM angor-base:latest AS base
WORKDIR /source

COPY ./ .

FROM base AS publish
WORKDIR /source

# Publish Blazor WASM application
RUN dotnet publish Angor/Client/Angor.Client.csproj \
    -c Debug \
    -p:PublishTrimmed=false \
    -o /app \
    --verbosity minimal

FROM mcr.microsoft.com/dotnet/runtime:9.0

# Install Nginx and debugging tools
RUN apt-get update && apt-get install -y nginx unzip curl && apt-get clean

# Install Visual Studio remote debugger
RUN curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg

COPY --from=publish /app/wwwroot /var/www/html/

# Nginx configuration
COPY nginx.conf /etc/nginx/nginx.conf

RUN ln -sf /dev/stdout /var/log/nginx/access.log \
    && ln -sf /dev/stderr /var/log/nginx/error.log

# Expose debugging port
EXPOSE 4024

# Start Nginx
CMD ["nginx", "-g", "daemon off;"]
