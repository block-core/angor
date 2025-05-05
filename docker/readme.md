# Deploying Angor Services with docker-compose

This guide will show you how to deploy Angor services, to allow users to self-host and gain more privacy.

## Table of Contents
- [Overview](#overview)
- [Deploy a Proxy Server](#deploy-a-proxy-server)
- [Deploy The Angor Application](#deploy-the-angor-application)
- [Deploy The Angor Hub](#deploy-the-angor-hub)
- [Creating Your Own Custom Signet](#creating-your-own-custom-signet)

## Overview

Angor services can be deployed using Docker Compose, enabling users to run the services on their own infrastructure for enhanced privacy and control.

### What Can You Self-Host?
- **The Angor App**: Build and run the Angor app locally or on a VPS instead of pulling the package from GitHub.
- **The Angor Hub**: Host the Angor Hub to manage and coordinate multiple Angor projects, allowing a user to filter what projects they think are a good investment.
- **Explorers and Blockchain Indexers**: Host specialized indexers required by Angor (future updates may remove this dependency for the web app).
- **Nostr Relays**: Ideal for project founders who want to host relays themselves to serve their community and investors.

## Deploy a Proxy Server

To deploy a proxy server, follow the instructions in the [Proxy Server README](/proxy/readme.md).

## Deploy The Angor Application

To deploy the Angor application, navigate to the `angor-app` directory and follow the instructions in the [Angor App README](/angor-app/readme.md).

## Deploy The Angor Hub

To deploy the Angor Hub, navigate to the `angor-hub` directory and follow the instructions in the [Angor Hub README](/angor-hub/readme.md).

## Creating Your Own Custom Signet

For creating your own custom signet, refer to the [Bitcoin Custom Signet documentation](https://github.com/block-core/bitcoin-custom-signet).
