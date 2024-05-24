import "../support/commands/commands";
import "../support/commands/browse_commands";
import "../support/commands/wallet_commands";

import {
  Navbar,
  BROWSE_DATA_CY,
  ERROR_MESSAGES,
  WALLET_DATA_CY,
  TEST_DATA,
} from "../support/enums";

describe("browseProjectsSpec", { retries: 3 }, () => {
  beforeEach(() => {
    cy.visitLocalhost();
  });
  var testId = "angor1qr8rehc7pa0kh2rrndfl6e789nxzdkjerzd72j8";
  var testNostrId =
    "npub1upnev99qsx4haf0yq8jtkny8yr45c94yw42g7rkdgr4ewgv3sh2shmuxnk";
  it("browseBasic", () => {
    cy.clickOnNavBar(Navbar.BROWSE);
    cy.clickElementWithDataCy(BROWSE_DATA_CY.FIND_BUTTON);
    cy.searchProject({ msg: "This project does not exist" });
    // cy.get(`[data-cy=${BROWSE_DATA_CY.SEARCHED_PROJECT}]`).should("not.exist"); 'project-grid'
    cy.get(`[data-cy=project-grid]`).should("not.exist");
    cy.searchProject({ msg: testId, clear: true });

    cy.waitForLoader();
    const searchedProject = cy.get(`[data-cy=project-grid]`);
    // Verify project title
    cy.verifyTextInDataCyWithExistElement(
      searchedProject,
      BROWSE_DATA_CY.SEARCHED_TITLE,
      "Milad's Project For test"
    );
    // Verify project ID
    cy.verifyTextInDataCyWithExistElement(
      searchedProject,
      BROWSE_DATA_CY.SEARCHED_SUB_TITLE,
      "This project is dedicated to testing various functionalities and features."
    );
    // Verify Project MetaData
    cy.clickElementWithDataCy(BROWSE_DATA_CY.SEARCHED_TITLE);
    cy.clickElementWithDataCy(BROWSE_DATA_CY.INVEST_BUTTON);
    cy.popUpOnScreenVerify(ERROR_MESSAGES.WALLET_FOR_INVEST);
  });

  it("browseAndInvest", () => {
    // Step 1: Navigate and set up the wallet
    cy.clickOnNavBar(Navbar.WALLET);
    cy.recoverWallet(TEST_DATA.TEST_WALLET, TEST_DATA.WALLET_PASSWORD);

    // Step 2: Browse to the project
    cy.clickOnNavBar(Navbar.BROWSE);
    cy.searchProject({ msg: testId, clear: true });
    cy.waitForLoader();

    // Step 3: Select the project and attempt investments
    cy.clickElementWithDataCy(BROWSE_DATA_CY.SEARCHED_TITLE);
    cy.clickElementWithDataCy(BROWSE_DATA_CY.INVEST_BUTTON);

    // Case 1: Minimum not achieved
    attemptInvestment("0", ERROR_MESSAGES.MIN_FUNDS_ERROR, true);

    // Case 2: More than available in wallet
    attemptInvestment("30000", ERROR_MESSAGES.NOT_ENOUGH_FUNDS);

    // Case 3: Valid investment
    attemptInvestment("3", null, false, true);
  });
  function attemptInvestment(
    amount,
    errorMessage,
    confirmInvest = false,
    finalConfirm = false
  ) {
    cy.get("#investmentAmount").clear().type(amount);
    cy.clickElementWithDataCy(BROWSE_DATA_CY.NEXT_BUTTON);

    if (confirmInvest) {
      cy.clickAndTypeElementWithDataCy(
        WALLET_DATA_CY.PASSWORD_FOR_SEND,
        TEST_DATA.WALLET_PASSWORD
      );
      cy.confirmInvest();
    }

    if (errorMessage) {
      cy.verifyElementPopUp(".modal-body", errorMessage, { timeout: 5000 });
      cy.dismissModal();
    } else if (finalConfirm) {
      cy.contains("h5.modal-title", "Confirmation", { timeout: 10000 }).should(
        "be.visible"
      );
      cy.contains("button.btn.btn-primary", "Confirm").click();
      cy.contains("h4", "Waiting for the founder to approve", {
        matchCase: true,
      }).should("be.visible");
      cy.contains("button.btn.btn-danger", "Cancel")
        .should("be.visible")
        .and("not.be.disabled")
        .click();
      cy.popUpOnScreenVerify(ERROR_MESSAGES.FUNDS_INVESTED);
      cy.get("#investmentAmount").should("be.visible");
    }
  }
});
