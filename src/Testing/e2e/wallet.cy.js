import '../support/commands/commands'
import '../support/commands/wallet_commands'
import {Navbar,WALLET_DATA_CY,QR_CODE_CY,ERROR_MESSAGES} from '../support/enums'

describe('walletSpec', { retries: 3 }, () => {
  beforeEach(() => {
    cy.visitLocalhost();
  });

  it('createWallet', () => {
    cy.clickOnNavBar(Navbar.WALLET)
    cy.clickElementWithDataCy(WALLET_DATA_CY.CREATE_WALLET)
    cy.clickElementWithDataCy(WALLET_DATA_CY.GENERATE_WALLET_WORDS)
    cy.get('textarea.form-control[readonly]').invoke('val').as('walletWords').then(walletWords => {
      // cy.clickSubmitButton(ERROR_MESSAGES.NULL_PASSWORD_MESSAGE); //for some reason doesnt work in github, add after works //try to create wallet without password and checkbox
      cy.typeTextInElement('password','abc123')
      cy.clickSubmitButton()
      cy.get(`[data-cy=${WALLET_DATA_CY.CHECKBOX_ERROR}]`).should('contain', ERROR_MESSAGES.NO_CHECKBOX_TICK);
      cy.clickOnCheckBoxByDataCy(WALLET_DATA_CY.WALLET_CHECKBOX);
      cy.clickSubmitButton();
      cy.waitForLoader()
      cy.get(`[data-cy=${WALLET_DATA_CY.BALANCE}]`).should('have.text', 'Balance: ');
      cy.verifyBalance('0',WALLET_DATA_CY.BALANCE_AMOUNT)
      //verify words
      cy.clickElementWithDataCy(WALLET_DATA_CY.WALLET_WORDS)
      cy.get('.input-group').type('abc123')
      cy.clickElementWithDataCy(WALLET_DATA_CY.WALLET_WORDS_SHOW)
      cy.get(`[data-cy=${WALLET_DATA_CY.WALLET_WORDS_ALERT}]`).should('contain.text', walletWords);
      cy.clickElementWithDataCy(WALLET_DATA_CY.CLOSE_WALLET_WORDS)
    })
    cy.clickElementWithDataCy(WALLET_DATA_CY.RECEIVE_FUNDS)
    cy.get(`[data-cy=${WALLET_DATA_CY.WALLET_ADDRESS}]`).invoke('text').then(walletAdress => {
      cy.clickElementWithDataCy(QR_CODE_CY.WALLET_QR);
      cy.ElementWithDataCyShouldBeVisible(QR_CODE_CY.QR_IMAGE);
        cy.get(`[data-cy=${QR_CODE_CY.WALLET_ADDRESS}]`).invoke('text').then(walletAdressfromPopUp => {
          expect(walletAdress).to.contain(walletAdressfromPopUp);
        });
    });
    
  });

  it('sendFunds', () => {
    cy.createWallet();
  });
});

