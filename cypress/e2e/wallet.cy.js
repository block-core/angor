import '../support/commands'
import {Navbar,WALLET_DATA_CY} from '../support/enums'

describe('walletSpec', { retries: 3 }, () => {
  beforeEach(() => {
    cy.visitLocalhost();
  });

  it('createWallet', () => {
    cy.clickOnNavBar(Navbar.WALLET)
    cy.clickElementWithDataCy(WALLET_DATA_CY.CREATE_WALLET)
    cy.clickElementWithDataCy(WALLET_DATA_CY.GENERATE_WALLET_WORDS)
    //cy.clickSubmitButton('New wallet password is null or empty');
    cy.typeTextInElement('password','abc123')
    cy.clickOnCheckBoxByDataCy(WALLET_DATA_CY.WALLET_CHECKBOX);
    cy.clickSubmitButton();
    cy.waitForLoader()
    cy.get(`[data-cy=${WALLET_DATA_CY.BALANCE}]`).should('have.text', 'Balance: ');
    cy.verifyBalance('0',WALLET_DATA_CY.BALANCE_AMOUNT)
  });
});
