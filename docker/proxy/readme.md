# Deploying a Proxy Server

### How the Proxy Works

The proxy acts as a reverse proxy to route incoming HTTPS requests to the appropriate Angor services. It ensures that all traffic is encrypted using SSL/TLS, providing secure communication between clients and the hosted services. The proxy typically uses tools like Nginx or Traefik to manage SSL certificates and routing.

### Required Environment Variables

To configure the proxy, you need to set the following environment variables in the `.env` file:

- `VIRTUAL_HOST`: The domain name for your Angor services (e.g., `example.com`).
- `LETSENCRYPT_EMAIL`: The email address used for SSL certificate registration (e.g., for Let's Encrypt).
- `VIRTUAL_PORT`: The port on which the proxy will listen for incoming traffic (default: `443` for HTTPS).
- `VIRTUAL_NETWORK`: The name of the internal network.
- `LETSENCRYPT_HOST`: The domain name for your Angor services.
- `LETSENCRYPT_EMAIL`: The email address used for SSL certificate registration.

### Example docker environment section
```env
VIRTUAL_HOST: explorer.angor.io
VIRTUAL_PORT: 9910
VIRTUAL_PROTO: http
VIRTUAL_NETWORK: proxy
LETSENCRYPT_HOST: explorer.angor.io
LETSENCRYPT_EMAIL: admin@blockcore.net
ASPNETCORE_URLS: http://+:9910
```

### Deploy the Proxy
1. Ensure the `nginx.conf` file is present.  
2. Start the proxy service in the proxy folder:
   ```bash
   docker-compose up -d proxy
   ```
3. Verify that the proxy is running and accessible via the configured domain name.

