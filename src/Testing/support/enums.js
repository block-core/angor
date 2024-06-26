const Navbar = {
  WALLET: "wallet",
  BROWSE: "browse",
  CREATE_WALLET: "Create Wallet",
  GENERATE_WALLET: "Generate New Wallet Words",
};

const WALLET_DATA_CY = {
  CREATE_WALLET: "create-wallet",
  GENERATE_WALLET_WORDS: "generate-wallet-words",
  BALANCE: "balance",
  BALANCE_AMOUNT: "balance-amount",
  WALLET_CHECKBOX: "wallet-checkbox",
  WALLET_WORDS: "wallet-words",
  WALLET_WORDS_SHOW: "wallet-words-in-popup",
  WALLET_WORDS_ALERT: "alert-wallet-words",
  WALLET_ADDRESS: "wallet-address",
  RECEIVE_FUNDS: "receive-button",
  WALLET_QR: "SHOW_QR_CODE_WALLET",
  CLOSE_WALLET_WORDS: "close-show-wallet-words",
  CHECKBOX_ERROR: "checkbox-error",
  RECOVER_WALLET: "recover-wallet",
  SEND_FUNDS: "send-button",
  PASSWORD_FOR_SEND: "password-enter-for-send",
  HISTORY_REFRESH: "history-refresh",
  CONFIRMED_BALANCE: "confirmed-balance",
  ADRESS_ROW: 'adress-row',
  ADRESS_EXPEND: 'expend-amount',
};

const TEST_DATA = {
  TEST_WALLET:
    "desk sadness wrong odor home cabbage shove panic unusual wool force fee",
  TEST_ADRESS: "tb1qyhyspf53js8xp889zrwj2fjnd4y9wxsckzafrm",
  WALLET_PASSWORD: 'aa',
};

const QR_CODE_CY = {
  QR_IMAGE: "QR_IMAGE_IN_POPUP",
  WALLET_ADDRESS: "WALLET_ADRESS_IN_QR_POPUP",
  WALLET_QR: "SHOW_QR_CODE",
};

const ERROR_MESSAGES = {
  NULL_PASSWORD_MESSAGE: "New wallet password is null or empty",
  NO_PROJECTS_FOUND: "No projects found.",
  NO_CHECKBOX_TICK:
    "Please confirm that you have backed up your wallet words and password.",
  SENT_COMPLETE: "Sent complete!",
  WALLET_FOR_INVEST: "You must create a wallet if you want to invest",
  FUNDS_INVESTED: "Signature request sent",
  MIN_FUNDS_ERROR: "Seeder minimum investment amount of 2 BTC was not reached",
  NOT_ENOUGH_FUNDS: "Not enough funds",
};

const BROWSE_DATA_CY = {
  FIND_BUTTON: "find-button",
  SEARCHED_PROJECT: "searchedProject",
  SEARCHED_TITLE: "searchedTitle",
  SEARCHED_SUB_TITLE: "searchedSubTitle",
  // PROJECT_INFO: "project-info", removed for now
  PROJECT_GRID: "projectsGrid",
  INVEST_BUTTON: "INVEST_BUTTON",
  NEXT_BUTTON: "NEXT_BUTTON",
};

export {
  Navbar,
  WALLET_DATA_CY,
  QR_CODE_CY,
  ERROR_MESSAGES,
  BROWSE_DATA_CY,
  TEST_DATA,
};
