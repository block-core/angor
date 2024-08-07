Cypress.Commands.add("visitLocalhost", () => {
  // Set the viewport to a desktop resolution
  cy.viewport(1280, 720); // You can adjust the width and height as needed
  cy.visit("http://localhost:5062/");
  cy.get("#youtube-video", { timeout: 5000 }).should("be.visible");
  cy.get(
    '#theme-icon'
  )
    .should("be.visible")
    .click(); // Interact with the theme icon
});

Cypress.Commands.add("clickOnNavBar", (dir) => {
  cy.get(`[href="${dir}"]`).should("be.visible").click();
});

Cypress.Commands.add("clickOnButtonContain", (name) => {
  cy.log(name);
  cy.get(".btn.btn-primary")
    .contains(`${name}`)
    .filter(":visible") // Filter to only visible elements
    .click(); // Click on it
});

Cypress.Commands.add("clickElementWithDataCy", (datacy) => {
  cy.get(`[data-cy=${datacy}]`).click();
});

Cypress.Commands.add("clickAndTypeElementWithDataCy", (datacy, msg,clear) => {
  if(clear){
    cy.get(`[data-cy=${datacy}]`).clear()
  }
    cy.get(`[data-cy=${datacy}]`).type(msg);
});

Cypress.Commands.add("ElementWithDataCyShouldBeVisible", (datacy) => {
  cy.get(`[data-cy=${datacy}]`).should("be.visible");
});

Cypress.Commands.add("clickCardContains", (name, msg) => {
  cy.get('.card-body[role="button"]').contains(`${name}`).click();
  cy.log(msg);
  if (msg) {
    cy.get(".snackbar").should("be.visible"); // Assuming snackbar has a class 'snackbar'
    cy.get(".snackbar").contains(msg); // Assuming snackbar contains text
  }
});

Cypress.Commands.add("clickSubmitButton", (msg) => {
  cy.get(".btn.btn-success").click();
  if (msg) {
    cy.get(`[data-cy=alert]`).should("be.visible"); // Assuming snackbar has a class 'snackbar'
    cy.get(`[data-cy=alert]`).contains(msg); // Assuming snackbar contains text
  }
});

Cypress.Commands.add("popUpOnScreenVerify", (msg) => {
  cy.get(`[data-cy=alert]`).should("be.visible"); // Assuming snackbar has a class 'snackbar'
  cy.get(`[data-cy=alert]`).contains(msg); // Assuming snackbar contains text
});

Cypress.Commands.add("typeTextInElement", (type, msg) => {
  cy.get(`.input-group input[type=${type}]`).type(msg);
});

Cypress.Commands.add("clickOnCheckBox", () => {
  cy.get(`.form-check input[type="checkbox"]`).check();
});

Cypress.Commands.add("clickOnCheckBoxByDataCy", (datacy) => {
  cy.get(`[data-cy=${datacy}]`).check();
});

Cypress.Commands.add("verifyTextInDataCy", (datacy, text) => {
  cy.get(`[data-cy=${datacy}]`).contains(text).should("exist");
});

Cypress.Commands.add(
  "verifyTextInDataCyWithExistElement",
  (exist, datacy, text) => {
    cy.get(exist).get(`[data-cy=${datacy}]`).contains(text).should("exist");
  }
);

Cypress.Commands.add("waitForLoader", () => {
  cy.get(".loader", { timeout: 25000 }).should("not.exist");
});

Cypress.Commands.add("dismissModal", () => {
  // Verify that the modal is visible
  cy.get(".modal-content").should("be.visible");

  // Click the "Dismiss" button
  cy.get("button.btn.btn-light").contains("Dismiss").click();
});

Cypress.Commands.add("verifyElementPopUp", (element,msg) => {
    cy.contains(element, msg).should("be.visible");
  });
