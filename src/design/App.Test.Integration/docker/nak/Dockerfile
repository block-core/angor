# Build nip44-tool — a minimal Go CLI wrapping go-nostr/nip44 for
# cross-language NIP-44 encrypt/decrypt interop testing.
FROM golang:1.24-alpine AS builder

WORKDIR /build/nip44-tool
COPY nip44-tool/go.mod .
COPY nip44-tool/main.go .
RUN go mod tidy && go build -o /usr/local/bin/nip44-tool .

# Runtime — lightweight Alpine with just the nip44-tool binary
FROM alpine:3.20
COPY --from=builder /usr/local/bin/nip44-tool /usr/local/bin/nip44-tool
ENTRYPOINT ["sleep", "infinity"]
