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
