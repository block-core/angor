import { BROWSE_DATA_CY } from "../enums";
import "./commands";

Cypress.Commands.add("searchProject", ({ msg, clear }) => {
  const searchField = cy.get("#searchQuery");
  if (clear) {
    searchField.clear();
  }
  searchField.type(msg);
  cy.clickElementWithDataCy(BROWSE_DATA_CY.FIND_BUTTON);
});

Cypress.Commands.add("confirmInvest", (err) => {
  cy.contains("button.btn.btn-primary", "Submit").click();
  if (err) {
    cy.contains("div.text-danger-emphasis", err).should(
      "be.visible"
    );
  }
});
