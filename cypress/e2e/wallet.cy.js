import '../support/commands'
import {Navbar} from '../support/enums'

describe('template spec', () => {
  beforeEach(() => {
    cy.visitLocalhost();
  });

  it('createWallet', () => {
    cy.clickOnNavBar(Navbar.WALLET)
    cy.clickCardContains('Create Wallet') // should use enum, for some reason passes null
    cy.clickOnButtonContain('Generate New Wallet Words')
    cy.clickSubmitButton('New wallet password is null or empty');
    cy.typeTextInElement('password','abc123')
    cy.clickOnCheckBox()
    cy.clickSubmitButton();
    cy.waitForLoader()
    cy.get('span.fs-4').should('have.text', 'Balance: ');
    cy.verifyBalance('0')
  });
});
