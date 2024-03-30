import '../support/commands'
import {Navbar} from '../support/enums'

describe('template spec', () => {
  beforeEach(() => {
    cy.visitLocalhost();
  });

  it('createWallet', () => {
    cy.clickOnNavBar(Navbar.WALLET)
    cy.clickOnButtonContain('Create Wallet') // should use enum, for some reason passes null
    cy.clickOnButtonContain('Generate New Wallet Words')
    cy.clickSubmitButton();
    cy.waitForLoader()
    cy.contains('span.fs-4', 'Confirmed balance:').should('be.visible');
    cy.verifyBalance('0')
  });
});
