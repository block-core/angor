# Deploying Angor Services with Docker Compose

This guide will show you how to deploy Angor services, to allow users to self-host and gain more privacy.

## Overview

Angor services can be deployed using Docker Compose, enabling users to run the services on their own infrastructure for enhanced privacy and control.

## Prerequisites

- Install [Docker](https://www.docker.com/get-started).
- Install [Docker Compose](https://docs.docker.com/compose/install/).
- Ensure you have sufficient system resources to run the services.

## Step 1: Deploy a Proxy

[Deploy a Proxy Server](/proxy/readme.md).

## Step 2: Deploy Angor Services

### 1. Clone the Repository
```bash
git clone https://github.com/your-repo/angor.git
cd angor/docker
```

### 2. Configure Environment Variables
- Create a `.env` file in the `docker` directory.
- Add necessary environment variables, such as database credentials, API keys, and other configurations.

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

## Creating your own custom signet
TBD

https://github.com/block-core/bitcoin-custom-signet

## Troubleshooting

- Ensure Docker and Docker Compose are installed and running.
- Check for port conflicts or missing environment variables.
- Use `docker-compose logs` to debug issues.

## Additional Resources

- [Docker Documentation](https://docs.docker.com/)
- [Angor GitHub Repository](https://github.com/your-repo/angor)
