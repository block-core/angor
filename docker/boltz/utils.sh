#!/usr/bin/env bash
BITCOIN_CLI_PARAMS="-rpcconnect=angornet-btc-node -rpcport=38332 -rpcuser=rpcuser -rpcpassword=rpcpassword"

function bitcoin-cli-sim() {
  bitcoin-cli $BITCOIN_CLI_PARAMS "$@"
}

function bitcoin-cli-sim-server() {
  bitcoin-cli $BITCOIN_CLI_PARAMS "$@"
}

function bitcoin-cli-sim-client() {
  bitcoin-cli $BITCOIN_CLI_PARAMS "$@"
}

# Elements/Liquid regtest
ELEMENTS_CLI_PARAMS="-rpcconnect=elementsd -rpcport=18884 -rpcuser=regtest -rpcpassword=regtest"

function elements-cli-sim() {
  elements-cli $ELEMENTS_CLI_PARAMS "$@"
}

function elements-cli-sim-server() {
  elements-cli $ELEMENTS_CLI_PARAMS "$@"
}

function elements-cli-sim-client() {
  elements-cli $ELEMENTS_CLI_PARAMS "$@"
}

elements-init() {
  echo "Waiting for elementsd to be reachable..."
  while true; do
    sleep 1
    if elements-cli-sim getblockchaininfo &> /dev/null; then
      echo "elementsd is reachable"
      break
    fi
  done

  # Create wallet if it doesn't exist
  if ! elements-cli-sim listwallets | jq -e 'index("regtest")' > /dev/null 2>&1; then
    elements-cli-sim createwallet regtest || true
  fi

  # Mine initial blocks to get coins
  echo "Mining initial Liquid blocks..."
  elements-cli-sim -generate 1 > /dev/null
  echo "Elements block height: $(elements-cli-sim getblockcount)"
  echo "L-BTC balance: $(elements-cli-sim getbalance)"
}

#backwards compat
boltzcli-sim() {
  boltzcli --datadir=/root/.boltz-client --host boltz-client "$@"
}

boltz-client-cli-sim() {
  boltzcli-sim "$@"
}

# args(i, cmd)
lightning-cli-sim() {
  i=$1
  shift # shift first argument so we can use $@
  lightning-cli --network signet --lightning-dir=/root/.lightning-"$i" "$@"
}

# args(i, cmd)
lncli-sim() {
  i=$1
  shift # shift first argument so we can use $@
  lncli --network signet --rpcserver=lnd-$i:10009 --lnddir=/root/.lnd-"$i" "$@"
}

# client/backend convenience wrappers
lightning-cli-sim-client() {
  lightning-cli-sim 1 "$@"
}

lightning-cli-sim-server() {
  lightning-cli-sim 2 "$@"
}

lncli-sim-client() {
  lncli-sim 1 "$@"
}

lncli-sim-server() {
  lncli-sim 2 "$@"
}

arkd-sim() {
  arkd --url http://arkd:7071 "$@"
}

# args(i)
fund_cln_node() {
  address=$(lightning-cli-sim $1 newaddr | jq -r .p2tr)
  echo "funding: $address on cln-node: $1"
  bitcoin-cli-sim-server -named sendtoaddress address=$address amount=30 fee_rate=1 > /dev/null
}

# args(i)
fund_lnd_node() {
  address=$(lncli-sim $1 newaddress p2wkh | jq -r .address)
  echo "funding: $address on lnd-node: $1"
  bitcoin-cli-sim-server -named sendtoaddress address=$address amount=30 fee_rate=1 > /dev/null
}

# args(i, j)
connect_cln_node() {
  pubkey=$(lightning-cli-sim $2 getinfo | jq -r '.id')
  lightning-cli-sim $1 connect $pubkey@cln-$2:9735 | jq -r '.id'
}

signet-init() {
  # Wait until signet node is reachable
  while true; do
      sleep 1
      if bitcoin-cli-sim getblockchaininfo &> /dev/null; then
          echo "signet node is reachable"
          break
      else
          echo "Waiting for signet node (angornet-btc-node) to be reachable..."
      fi
  done

  echo "Signet block height: $(bitcoin-cli-sim getblockcount)"
}

signet-start(){
  signet-init-lightning
}

waitForArkdToSync(){
  while ! arkd-sim wallet balance > /dev/null 2>&1; do
    sleep 1
  done

  echo "arkd synced"
}

arkd-init(){
  echo "creating arkd wallet"
  echo "waiting for arkd to be ready..."
  while ! curl -s http://arkd:7070 > /dev/null 2>&1; do
    sleep 1
  done
  echo "arkd is ready"

  arkd-sim wallet create --password ark
  arkd-sim wallet unlock --password ark
  waitForArkdToSync

  bitcoin-cli-sim-server -rpcwallet=regtest sendtoaddress $(arkd-sim wallet address) 25
  bitcoin-cli-sim-server -rpcwallet=regtest -generate 1
  echo "funded arkd"

  while ! curl -sf http://arkd:7070/v1/info > /dev/null 2>&1; do
    sleep 1
  done
}

fulmine-init(){
  echo "creating fulmine wallet"
  curl -s -X POST http://fulmine:7001/api/v1/wallet/create \
    -H "Content-Type: application/json" \
    -d '{"private_key": "693b0b993e69953c35838e96c8c41430e0ae881c2faa1bc95e203cdaec5f3fdf", "password": "ark", "server_url": "http://arkd:7070"}'

  curl -s -X POST http://fulmine:7001/api/v1/wallet/unlock \
    -H "Content-Type: application/json" \
    -d '{"password": "ark"}' > /dev/null

  echo "funding fulmine"
  
  while true; do
    fulmineAddress=$(curl -s -X GET http://fulmine:7001/api/v1/address | jq -r .address)
    if [ ! -z "$fulmineAddress" ] && [ "$fulmineAddress" != "null" ]; then
      echo "fulmine address: $fulmineAddress"
      fulmineAddress=${fulmineAddress#bitcoin:}
      echo "fulmine address: $fulmineAddress"
      fulmineAddress=${fulmineAddress%%\?*}
      echo "fulmine address: $fulmineAddress"
      break
    fi
    sleep 1
  done

  bitcoin-cli-sim-server -rpcwallet=regtest sendtoaddress $fulmineAddress 1
  bitcoin-cli-sim-server -rpcwallet=regtest -generate 1

  echo "waiting for fulmine transactions..."
  while true; do
    txCount=$(curl -s -X GET http://fulmine:7001/api/v1/transactions | jq -r '.transactions | length')
    if [ ! -z "$txCount" ] && [ "$txCount" -gt 0 ]; then
      echo "fulmine has $txCount transaction(s)"
      break
    fi
    sleep 1
  done

  echo "settling fulmine..."
  curl -s -X GET http://fulmine:7001/api/v1/settle
  echo "fulmine settled"
}

signet-init-lightning(){
  lightning-sync
  lightning-init
}

lightning-sync(){
  wait-for-cln-sync 1
  wait-for-cln-sync 2
  wait-for-lnd-sync 1
  wait-for-lnd-sync 2
}

lightning-init(){
  echo "Setting up lightning channels on signet..."
  echo "NOTE: Channel opens will confirm with natural signet block production"

  channel_size=24000000 # 0.024 btc
  balance_size=12000000 # 0.12 btc

  # Connect nodes to each other
  # lnd-1 -> lnd-2
  lncli-sim 1 connect $(lncli-sim 2 getinfo | jq -r '.identity_pubkey')@lnd-2 > /dev/null 2>&1 || true
  echo "connected lnd-1 to lnd-2"

  # lnd-1 -> cln-1
  lncli-sim 1 connect $(lightning-cli-sim 1 getinfo | jq -r '.id')@cln-1 > /dev/null 2>&1 || true
  echo "connected lnd-1 to cln-1"

  # lnd-2 -> cln-1
  lncli-sim 2 connect $(lightning-cli-sim 1 getinfo | jq -r '.id')@cln-1 > /dev/null 2>&1 || true
  echo "connected lnd-2 to cln-1"

  # lnd-1 -> cln-2
  lncli-sim 1 connect $(lightning-cli-sim 2 getinfo | jq -r '.id')@cln-2 > /dev/null 2>&1 || true
  echo "connected lnd-1 to cln-2"

  # lnd-2 -> cln-2
  lncli-sim 2 connect $(lightning-cli-sim 2 getinfo | jq -r '.id')@cln-2 > /dev/null 2>&1 || true
  echo "connected lnd-2 to cln-2"

  # cln-1 -> cln-2 P2P connection for offer fetching
  lightning-cli-sim 1 connect $(lightning-cli-sim 2 getinfo | jq -r '.id')@cln-2:9735 > /dev/null 2>&1 || true
  echo "connected cln-1 to cln-2"

  echo "All lightning nodes connected."
  echo "To open channels, fund the node wallets and use openchannel commands."
  echo "Channels will confirm with natural signet block production."
}

wait-for-lnd-channel(){
  while true; do
    pending=$(lncli-sim $1 pendingchannels | jq -r '.pending_open_channels | length')
    echo "lnd-$1 pendingchannels: $pending"
    if [[ "$pending" == "0" ]]; then
      break
    fi
    sleep 1
  done
}

wait-for-lnd-sync(){
  while true; do
    if [[ "$(lncli-sim $1 getinfo 2>&1 | jq -r '.synced_to_chain' 2> /dev/null)" == "true" ]]; then
      echo "lnd-$1 is synced!"
      break
    fi
    echo "waiting for lnd-$1 to sync..."
    sleep 1
  done
}

wait-for-cln-channel(){
  while true; do
    pending=$(lightning-cli-sim $1 getinfo | jq -r '.num_pending_channels | length')
    echo "cln-$1 pendingchannels: $pending"
    if [[ "$pending" == "0" ]]; then
      if [[ "$(lightning-cli-sim $1 getinfo 2>&1 | jq -r '.warning_bitcoind_sync' 2> /dev/null)" == "null" ]]; then
        if [[ "$(lightning-cli-sim $1 getinfo 2>&1 | jq -r '.warning_lightningd_sync' 2> /dev/null)" == "null" ]]; then
          break
        fi
      fi
    fi
    sleep 1
  done
}

wait-for-cln-sync(){
  while true; do
    if [[ "$(lightning-cli-sim $1 getinfo 2>&1 | jq -r '.warning_bitcoind_sync' 2> /dev/null)" == "null" ]]; then
      if [[ "$(lightning-cli-sim $1 getinfo 2>&1 | jq -r '.warning_lightningd_sync' 2> /dev/null)" == "null" ]]; then
        echo "cln-$1 is synced!"
        break
      fi
    fi
    echo "waiting for cln-$1 to sync..."
    sleep 1
  done
}
