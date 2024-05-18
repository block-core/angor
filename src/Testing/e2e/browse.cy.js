import "../support/commands/commands";
import "../support/commands/browse_commands";

import { Navbar, BROWSE_DATA_CY, ERROR_MESSAGES,WALLET_DATA_CY,TEST_DATA } from "../support/enums";

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
    const searchedProject = cy.get(
      `[data-cy=project-grid]`
    );
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
    cy.clickElementWithDataCy(BROWSE_DATA_CY.SEARCHED_TITLE)
    cy.clickElementWithDataCy(BROWSE_DATA_CY.INVEST_BUTTON)
    cy.popUpOnScreenVerify(ERROR_MESSAGES.WALLET_FOR_INVEST);
    //add in another test.
    // cy.get('#investmentAmount').type('3')
    // cy.clickElementWithDataCy(BROWSE_DATA_CY.NEXT_BUTTON)
    // cy.clickAndTypeElementWithDataCy(
    //   WALLET_DATA_CY.PASSWORD_FOR_SEND,
    //   TEST_DATA.WALLET_PASSWORD
    // );
  });
});
