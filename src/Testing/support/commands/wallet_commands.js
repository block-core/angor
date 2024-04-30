import { Navbar, WALLET_DATA_CY } from '../enums';
import './commands'

Cypress.Commands.add('verifyBalance', (num,datacy) => {
    cy.get(`[data-cy=${datacy}]`)
        .should('be.visible')
        .contains(`${num}`);
})

Cypress.Commands.add('createWallet', () => {
    cy.clickOnNavBar(Navbar.WALLET);
    cy.clickElementWithDataCy(WALLET_DATA_CY.CREATE_WALLET);
    cy.clickElementWithDataCy(WALLET_DATA_CY.GENERATE_WALLET_WORDS);
    cy.typeTextInElement('password','abc123');
    cy.clickOnCheckBoxByDataCy(WALLET_DATA_CY.WALLET_CHECKBOX);
    cy.clickSubmitButton();
    cy.waitForLoader();
});  