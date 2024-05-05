import { Navbar, WALLET_DATA_CY } from '../enums';
import './commands'

Cypress.Commands.add('verifyBalance', (num,datacy) => {
    cy.get(`[data-cy=${datacy}]`)
        .should('be.visible')
        .contains(`${num}`);
})

Cypress.Commands.add('createWallet', () => {
    cy.clickElementWithDataCy(WALLET_DATA_CY.CREATE_WALLET);
    cy.clickElementWithDataCy(WALLET_DATA_CY.GENERATE_WALLET_WORDS);
    cy.typeTextInElement('password','abc123');
    cy.clickOnCheckBoxByDataCy(WALLET_DATA_CY.WALLET_CHECKBOX);
    cy.clickSubmitButton();
    cy.waitForLoader();
});

Cypress.Commands.add('recoverWallet', (walletWords,password) => {
    cy.clickElementWithDataCy(WALLET_DATA_CY.RECOVER_WALLET);
    cy.get('.modal-body > :nth-child(1) > .form-control')
        .type(walletWords);
    cy.typeTextInElement('password',password);
    cy.clickOnCheckBoxByDataCy(WALLET_DATA_CY.WALLET_CHECKBOX);
    cy.clickSubmitButton();
    cy.waitForLoader();
});

Cypress.Commands.add('extractBTCValue', { prevSubject: true }, ($element) => {
    return cy.wrap($element).invoke('text').then((text) => {
      // Split the text by space and get the first part (before "TBTC")
      const btcAmount = text.trim().split(' ')[0];
      // Return the extracted numerical value
      return btcAmount;
    });
  });
  