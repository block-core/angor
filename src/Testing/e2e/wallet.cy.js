import "../support/commands/commands";
import "../support/commands/wallet_commands";
import {
  Navbar,
  WALLET_DATA_CY,
  QR_CODE_CY,
  ERROR_MESSAGES,
  TEST_DATA,
} from "../support/enums";

describe("walletSpec", { retries: 3 }, () => {
  beforeEach(() => {
    cy.visitLocalhost();
  });

  it("createWallet", () => {
    cy.clickOnNavBar(Navbar.WALLET);
    cy.clickElementWithDataCy(WALLET_DATA_CY.CREATE_WALLET);
    cy.clickElementWithDataCy(WALLET_DATA_CY.GENERATE_WALLET_WORDS);
    cy.get("textarea.form-control[readonly]")
      .invoke("val")
      .as("walletWords")
      .then((walletWords) => {
        // cy.clickSubmitButton(ERROR_MESSAGES.NULL_PASSWORD_MESSAGE); //for some reason doesnt work in github, add after works //try to create wallet without password and checkbox
        cy.typeTextInElement("password", "abc123");
        cy.clickSubmitButton();
        cy.get(`[data-cy=${WALLET_DATA_CY.CHECKBOX_ERROR}]`).should(
          "contain",
          ERROR_MESSAGES.NO_CHECKBOX_TICK
        );
        cy.clickOnCheckBoxByDataCy(WALLET_DATA_CY.WALLET_CHECKBOX);
        cy.clickSubmitButton();
        cy.waitForLoader();
        cy.get(`[data-cy=${WALLET_DATA_CY.BALANCE}]`).should(
          "have.text",
          "Balance: "
        );
        cy.verifyBalance("0", WALLET_DATA_CY.BALANCE_AMOUNT);
        //verify words
        cy.clickElementWithDataCy(WALLET_DATA_CY.WALLET_WORDS);
        cy.get(".input-group").type("abc123");
        cy.clickElementWithDataCy(WALLET_DATA_CY.WALLET_WORDS_SHOW);
        cy.get(`[data-cy=${WALLET_DATA_CY.WALLET_WORDS_ALERT}]`).should(
          "contain.text",
          walletWords
        );
        cy.clickElementWithDataCy(WALLET_DATA_CY.CLOSE_WALLET_WORDS);
      });
    cy.clickElementWithDataCy(WALLET_DATA_CY.RECEIVE_FUNDS);
    cy.get(`[data-cy=${WALLET_DATA_CY.WALLET_ADDRESS}]`)
      .invoke("text")
      .then((walletAdress) => {
        cy.clickElementWithDataCy(QR_CODE_CY.WALLET_QR);
        cy.ElementWithDataCyShouldBeVisible(QR_CODE_CY.QR_IMAGE);
        cy.get(`[data-cy=${QR_CODE_CY.WALLET_ADDRESS}]`)
          .invoke("text")
          .then((walletAdressfromPopUp) => {
            expect(walletAdress).to.contain(walletAdressfromPopUp);
          });
      });
  });

  it("failRecoverWallet", () => {
    cy.clickOnNavBar(Navbar.WALLET);
    cy.recoverWallet("these are not valid wallet words", "");
    cy.verifyElementPopUp(
      ".modal-body",
      "New wallet password is null or empty"
    );
    cy.dismissModal();
    cy.recoverWallet(
      "they are not in the dictionary",
      TEST_DATA.WALLET_PASSWORD
    );
    cy.verifyElementPopUp(
      ".modal-body",
      "Word count should be equals to 12,15,18,21 or 24"
    );
    cy.dismissModal();
    cy.recoverWallet(" twelve", "");
    cy.verifyElementPopUp(
      ".modal-body",
      "Word these is not in the wordlist for this language, cannot continue to rebuild entropy from wordlist"
    );
    cy.dismissModal();
  });

  it("recoverWalletAndsendFunds", () => {
    cy.clickOnNavBar(Navbar.WALLET);
    cy.recoverWallet(TEST_DATA.TEST_WALLET, TEST_DATA.WALLET_PASSWORD);
    //get funds
    cy.get(`[data-cy=${WALLET_DATA_CY.BALANCE_AMOUNT}]`)
      .extractBTCValue()
      .then((btcAmount) => {
        cy.log(btcAmount);
        //send 0 funds
        cy.get("#sendToAddress").type(TEST_DATA.TEST_ADRESS);
        cy.clickElementWithDataCy(WALLET_DATA_CY.SEND_FUNDS);
        //expect error:
        cy.get(".modal-content").should("be.visible"); // Verify that the modal content is visible
        cy.get(".modal-header").should("have.class", "bg-danger"); // Verify that the modal header has a danger background
        cy.verifyElementPopUp(".modal-body", "Specify an amount");

        cy.dismissModal();
        cy.get("#sendAmount").type(0.001);
        cy.clickElementWithDataCy(WALLET_DATA_CY.SEND_FUNDS);
        cy.clickAndTypeElementWithDataCy(
          WALLET_DATA_CY.PASSWORD_FOR_SEND,
          TEST_DATA.WALLET_PASSWORD
        );
        cy.confirmSendFunds();

        cy.get(".modal-content").should("be.visible");

        cy.get(".modal-header").should("be.visible"); // Verify that the modal header is visible
        cy.contains(".modal-title", "Confirmation").should("be.visible"); // Verify that the modal title contains the text "Confirmation"
        cy.get("#feeRange").should("exist");

        //add change Feerate for 1 blocks is 0.0001 sats
        cy.contains("button.btn.btn-primary", "Confirm").click();
        cy.popUpOnScreenVerify(ERROR_MESSAGES.SENT_COMPLETE);
        cy.clickElementWithDataCy(WALLET_DATA_CY.HISTORY_REFRESH);
        cy.get(`[data-cy=${WALLET_DATA_CY.BALANCE_AMOUNT}]`)
          .extractBTCValue()
          .then((btcAmountAfter) => {
            const btcAmountAsNumber = parseFloat(btcAmount);
            const btcAmountAfterAsNumber = parseFloat(btcAmountAfter);
            expect(btcAmountAfterAsNumber).not.equal(btcAmountAsNumber); //maybe a flaky line
            //verify Addresses and Amounts
            cy.get(`[data-cy=${WALLET_DATA_CY.ADRESS_ROW}]`).eq(0).click();
            cy.get(`[data-cy=${WALLET_DATA_CY.ADRESS_EXPEND}]`).should(
              "contain.text",
              "0.001"
            );
          });
      });
  });

  it("wrongAdressAndPassword", () => {
    cy.clickOnNavBar(Navbar.WALLET);
    cy.recoverWallet(TEST_DATA.TEST_WALLET, TEST_DATA.WALLET_PASSWORD);
    cy.get("#sendToAddress").type("address not valid");
    cy.get("#sendAmount").type(0.0001);
    cy.clickElementWithDataCy(WALLET_DATA_CY.SEND_FUNDS);
    cy.clickAndTypeElementWithDataCy(
      WALLET_DATA_CY.PASSWORD_FOR_SEND,
      "wrong password"
    );
    cy.confirmSendFunds("Invalid password");
    cy.clickAndTypeElementWithDataCy(
      WALLET_DATA_CY.PASSWORD_FOR_SEND,
      TEST_DATA.WALLET_PASSWORD,
      true
    );
    cy.confirmSendFunds();
    cy.verifyElementPopUp(".modal-body", "Invalid string");
    cy.dismissModal();
  });
});
