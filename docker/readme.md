# Deploying Angor Services with docker-compose

This guide will show you how to deploy Angor services, to allow users to self-host and gain more privacy.

## Table of Contents
- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Deploy a Proxy Server](#deploy-a-proxy-server)
- [Deploy The Angor Application](#deploy-the-angor-application)
- [Customization](#customization)
- [Creating Your Own Custom Signet](#creating-your-own-custom-signet)
- [Troubleshooting](#troubleshooting)
- [Additional Resources](#additional-resources)

## Overview

Angor services can be deployed using Docker Compose, enabling users to run the services on their own infrastructure for enhanced privacy and control.

### What Can You Self-Host?
- **The Angor App**: Build and run the Angor app locally or on a VPS instead of pulling the package from GitHub.
- **Explorers and Blockchain Indexers**: Host specialized indexers required by Angor (future updates may remove this dependency for the web app).
- **Nostr Relays**: Ideal for project founders who want to host relays themselves to serve their community and investors.

## Prerequisites

- Install [Docker](https://www.docker.com/get-started).
- Install [Docker Compose](https://docs.docker.com/compose/install/).
- Ensure you have sufficient system resources to run the services.

## Deploy a Proxy Server

First, you must deploy a reverse proxy to route incoming HTTPS requests.

[How to deploy a proxy server](/proxy/readme.md).

## Deploy The Angor Application

You can self-host Angor on a VPS or even locally on your computer.

### 1. Clone the Repository
```bash
git clone https://github.com/your-repo/angor.git
cd angor/docker
```

### 2. Configure Environment Variables
- Create a `.env` file in the `docker` directory.
- Add necessary environment variables, such as database credentials, API keys, and other configurations.

### Example `.env` File
```env
DATABASE_URL=postgres://user:password@localhost:5432/angor
API_KEY=your_api_key
```

### 3. Build Docker Images
```bash
docker-compose build
```

### 4. Start the Services
```bash
docker-compose up -d
```

### 5. Verify Deployment
- Check the logs to ensure all services are running:
  ```bash
  docker-compose logs -f
  ```
- Access the services via their respective endpoints.

### 6. Stop the Services
```bash
docker-compose down
```

## Customization

- Modify the `docker-compose.yml` file to adjust service configurations.
- Use `docker-compose.override.yml` for local development overrides.

## Creating Your Own Custom Signet

TBD

For more information, visit [Bitcoin Custom Signet](https://github.com/block-core/bitcoin-custom-signet).

## Troubleshooting

- Ensure Docker and Docker Compose are installed and running.
- Check for port conflicts or missing environment variables.
- Use `docker-compose logs` to debug issues.

## Additional Resources

- [Docker Documentation](https://docs.docker.com/)
- [Angor GitHub Repository](https://github.com/your-repo/angor)
