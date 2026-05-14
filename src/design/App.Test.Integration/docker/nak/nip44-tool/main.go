// nip44-tool: a minimal CLI for raw NIP-44 encrypt/decrypt interop testing.
// Uses the same go-nostr nip44 package that nak uses internally.
//
// Usage:
//   nip44-tool encrypt <sender-privkey-hex> <recipient-pubkey-hex> <plaintext>
//   nip44-tool decrypt <recipient-privkey-hex> <sender-pubkey-hex> <base64-payload>
package main

import (
	"encoding/hex"
	"fmt"
	"os"
	"strings"

	"github.com/nbd-wtf/go-nostr"
	"github.com/nbd-wtf/go-nostr/nip44"
)

func main() {
	if len(os.Args) < 2 {
		fmt.Fprintln(os.Stderr, "usage: nip44-tool <encrypt|decrypt> <privkey-hex> <pubkey-hex> <text-or-payload>")
		os.Exit(1)
	}

	cmd := os.Args[1]

	switch cmd {
	case "encrypt":
		if len(os.Args) < 5 {
			fmt.Fprintln(os.Stderr, "usage: nip44-tool encrypt <privkey-hex> <pubkey-hex> <plaintext>")
			os.Exit(1)
		}
		privHex := os.Args[2]
		pubHex := os.Args[3]
		plaintext := strings.Join(os.Args[4:], " ")

		conversationKey, err := nip44.GenerateConversationKey(pubHex, privHex)
		if err != nil {
			fmt.Fprintf(os.Stderr, "conversation key error: %v\n", err)
			os.Exit(1)
		}

		encrypted, err := nip44.Encrypt(plaintext, conversationKey)
		if err != nil {
			fmt.Fprintf(os.Stderr, "encrypt error: %v\n", err)
			os.Exit(1)
		}

		fmt.Print(encrypted)

	case "decrypt":
		if len(os.Args) < 5 {
			fmt.Fprintln(os.Stderr, "usage: nip44-tool decrypt <privkey-hex> <pubkey-hex> <base64-payload>")
			os.Exit(1)
		}
		privHex := os.Args[2]
		pubHex := os.Args[3]
		payload := os.Args[4]

		conversationKey, err := nip44.GenerateConversationKey(pubHex, privHex)
		if err != nil {
			fmt.Fprintf(os.Stderr, "conversation key error: %v\n", err)
			os.Exit(1)
		}

		decrypted, err := nip44.Decrypt(payload, conversationKey)
		if err != nil {
			fmt.Fprintf(os.Stderr, "decrypt error: %v\n", err)
			os.Exit(1)
		}

		fmt.Print(decrypted)

	case "convo-key":
		// Debug: print the conversation key (hex) for verification
		if len(os.Args) < 4 {
			fmt.Fprintln(os.Stderr, "usage: nip44-tool convo-key <privkey-hex> <pubkey-hex>")
			os.Exit(1)
		}
		privHex := os.Args[2]
		pubHex := os.Args[3]

		conversationKey, err := nip44.GenerateConversationKey(pubHex, privHex)
		if err != nil {
			fmt.Fprintf(os.Stderr, "conversation key error: %v\n", err)
			os.Exit(1)
		}

		fmt.Print(hex.EncodeToString(conversationKey[:]))

	case "pubkey":
		// Derive public key from private key
		if len(os.Args) < 3 {
			fmt.Fprintln(os.Stderr, "usage: nip44-tool pubkey <privkey-hex>")
			os.Exit(1)
		}
		privHex := os.Args[2]
		pub, err := nostr.GetPublicKey(privHex)
		if err != nil {
			fmt.Fprintf(os.Stderr, "pubkey error: %v\n", err)
			os.Exit(1)
		}
		fmt.Print(pub)

	default:
		fmt.Fprintf(os.Stderr, "unknown command: %s\n", cmd)
		fmt.Fprintln(os.Stderr, "commands: encrypt, decrypt, convo-key, pubkey")
		os.Exit(1)
	}
}
