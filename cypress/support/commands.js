
Cypress.Commands.add('visitLocalhost', () => {
    // Set the viewport to a desktop resolution
    cy.viewport(1280, 720); // You can adjust the width and height as needed
    cy.visit('http://localhost:5062/');
    //  cy.get('.loader').should('not.exist'); // Wait for loader to disappear
    cy.get('span[role="button"].material-icons.opacity-10.btn-angor.fs-3#theme-icon').should('be.visible').click(); // Interact with the theme icon
}); 

Cypress.Commands.add('clickOnNavBar', (dir) => {
    cy.get(`[href="${dir}"]`).should('be.visible').click();
});

Cypress.Commands.add('clickOnButtonContain', (name) => {
    cy.log(name)
    cy.get('.btn.btn-primary')
        .contains(`${name}`)
        .filter(':visible') // Filter to only visible elements
        .click(); // Click on it
})



Cypress.Commands.add('clickSubmitButton', () => {
    cy.get('.btn.btn-success').click();
})

Cypress.Commands.add('waitForLoader', () => {
    cy.get('.loader').should('not.exist');
})

Cypress.Commands.add('verifyBalance', (num) => {
    cy.contains('span.fs-3 strong', `${num} TBTC`)
        .should('be.visible')
        .contains(`${num}`);
})


